// Assets/Scripts/Multiplayer/LockstepManager.cs
// Core lockstep simulation manager for deterministic multiplayer RTS
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Unity.Entities;
using Unity.Mathematics;

namespace TheWaningBorder.Multiplayer
{
    /// <summary>
    /// Lockstep Manager coordinates deterministic simulation between players.
    /// 
    /// How it works:
    /// 1. Game runs in discrete "ticks" (e.g., 10 ticks per second)
    /// 2. Player commands are collected locally but NOT executed immediately
    /// 3. Commands are sent to all players with a target tick number
    /// 4. Simulation only advances when ALL players have confirmed their commands for that tick
    /// 5. All players execute the same commands on the same tick = deterministic
    /// 
    /// Network Protocol:
    /// - TICK|tickNumber|commandCount|cmd1|cmd2|...  (Player sends their commands for a tick)
    /// - SYNC|tickNumber|checksum                     (Periodic sync check)
    /// - PING|timestamp                               (Latency measurement)
    /// - PONG|timestamp                               (Latency response)
    /// </summary>
    public class LockstepManager : MonoBehaviour
    {
        public static LockstepManager Instance { get; private set; }

        // Configuration
        public const int TICKS_PER_SECOND = 10;
        public const float TICK_DURATION = 1f / TICKS_PER_SECOND;
        public const int INPUT_DELAY_TICKS = 2; // Commands execute 2 ticks in the future
        public const int MAX_TICK_BUFFER = 60;  // Store up to 6 seconds of commands

        // Network
        private UdpClient _udpClient;
        private int _localPort;
        private bool _isHost;
        private List<RemotePlayer> _remotePlayers = new List<RemotePlayer>();
        
        // Simulation state
        private int _currentTick = 0;
        private float _tickAccumulator = 0f;
        private bool _simulationStarted = false;
        private bool _waitingForPlayers = false;

        // Command buffers - commands indexed by tick number
        private Dictionary<int, List<LockstepCommand>> _localCommands = new Dictionary<int, List<LockstepCommand>>();
        private Dictionary<int, Dictionary<int, List<LockstepCommand>>> _remoteCommands = new Dictionary<int, Dictionary<int, List<LockstepCommand>>>();
        // _remoteCommands[tick][playerIndex] = list of commands

        // Confirmed ticks per player
        private Dictionary<int, int> _confirmedTicks = new Dictionary<int, int>();

        // Local player info
        private int _localPlayerIndex = 0;
        private Faction _localFaction = Faction.Blue;

        // Sync verification
        private Dictionary<int, uint> _checksums = new Dictionary<int, uint>();

        // Events
        public event Action<int> OnTickAdvanced;
        public event Action<string> OnDesyncDetected;

        // Debug
        [Header("Debug")]
        public bool LogCommands = false;
        public bool LogTicks = false;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void OnDestroy()
        {
            Cleanup();
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Initialize as host (player 0)
        /// </summary>
        public void InitializeAsHost(int port, List<RemotePlayerInfo> players)
        {
            _isHost = true;
            _localPlayerIndex = 0;
            _localFaction = Faction.Blue;

            // Create UDP socket
            _udpClient = new UdpClient(port);
            _udpClient.Client.ReceiveTimeout = 1;
            _localPort = port;

            // Add remote players
            _remotePlayers.Clear();
            for (int i = 0; i < players.Count; i++)
            {
                var player = new RemotePlayer
                {
                    PlayerIndex = i + 1, // Host is 0, remotes start at 1
                    Faction = players[i].Faction,
                    EndPoint = new IPEndPoint(IPAddress.Parse(players[i].IP), players[i].Port),
                    LastConfirmedTick = -1
                };
                _remotePlayers.Add(player);
                _confirmedTicks[player.PlayerIndex] = -1;
            }

            _confirmedTicks[_localPlayerIndex] = -1;

            Debug.Log($"[Lockstep] Initialized as HOST on port {port} with {players.Count} remote players");
        }

        /// <summary>
        /// Initialize as client (player 1+)
        /// </summary>
        public void InitializeAsClient(int localPort, string hostIP, int hostPort, int playerIndex, Faction faction)
        {
            _isHost = false;
            _localPlayerIndex = playerIndex;
            _localFaction = faction;

            // Create UDP socket
            _udpClient = new UdpClient(localPort);
            _udpClient.Client.ReceiveTimeout = 1;
            _localPort = localPort;

            // Add host as remote player
            _remotePlayers.Clear();
            _remotePlayers.Add(new RemotePlayer
            {
                PlayerIndex = 0,
                Faction = Faction.Blue,
                EndPoint = new IPEndPoint(IPAddress.Parse(hostIP), hostPort),
                LastConfirmedTick = -1
            });
            _confirmedTicks[0] = -1;
            _confirmedTicks[_localPlayerIndex] = -1;

            Debug.Log($"[Lockstep] Initialized as CLIENT (player {playerIndex}) connecting to {hostIP}:{hostPort}");
        }

        /// <summary>
        /// Start the lockstep simulation
        /// </summary>
        public void StartSimulation()
        {
            _currentTick = 0;
            _tickAccumulator = 0f;
            _simulationStarted = true;
            _waitingForPlayers = false;

            // Initialize with empty command lists for first few ticks
            for (int t = 0; t < INPUT_DELAY_TICKS + 1; t++)
            {
                _localCommands[t] = new List<LockstepCommand>();
                ConfirmTick(t); // Self-confirm empty ticks
            }

            Debug.Log("[Lockstep] Simulation started");
        }

        void Update()
        {
            if (!_simulationStarted) return;

            // Receive network messages
            ReceiveMessages();

            // Accumulate time
            _tickAccumulator += Time.deltaTime;

            // Try to advance simulation
            while (_tickAccumulator >= TICK_DURATION)
            {
                if (CanAdvanceTick())
                {
                    AdvanceTick();
                    _tickAccumulator -= TICK_DURATION;
                }
                else
                {
                    // Waiting for other players
                    if (!_waitingForPlayers)
                    {
                        _waitingForPlayers = true;
                        if (LogTicks) Debug.Log($"[Lockstep] Waiting for players at tick {_currentTick}");
                    }
                    break;
                }
            }
        }

        /// <summary>
        /// Queue a command to be executed in the future
        /// </summary>
        public void QueueCommand(LockstepCommand cmd)
        {
            int targetTick = _currentTick + INPUT_DELAY_TICKS;
            cmd.Tick = targetTick;
            cmd.PlayerIndex = _localPlayerIndex;

            if (!_localCommands.ContainsKey(targetTick))
                _localCommands[targetTick] = new List<LockstepCommand>();

            _localCommands[targetTick].Add(cmd);

            if (LogCommands)
                Debug.Log($"[Lockstep] Queued {cmd.Type} command for tick {targetTick}");
        }

        /// <summary>
        /// Confirm that we have no more commands for current input tick and broadcast
        /// </summary>
        public void ConfirmTick(int tick)
        {
            _confirmedTicks[_localPlayerIndex] = tick;

            // Broadcast our commands for this tick
            BroadcastTickCommands(tick);
        }

        /// <summary>
        /// Check if all players have confirmed the current tick
        /// </summary>
        private bool CanAdvanceTick()
        {
            // Check if we have confirmed commands from all players for the current tick
            if (!_confirmedTicks.ContainsKey(_localPlayerIndex) || _confirmedTicks[_localPlayerIndex] < _currentTick)
                return false;

            foreach (var player in _remotePlayers)
            {
                if (!_confirmedTicks.ContainsKey(player.PlayerIndex) || _confirmedTicks[player.PlayerIndex] < _currentTick)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Advance simulation by one tick
        /// </summary>
        private void AdvanceTick()
        {
            _waitingForPlayers = false;

            // Gather all commands for this tick
            var allCommands = new List<LockstepCommand>();

            // Local commands
            if (_localCommands.TryGetValue(_currentTick, out var localCmds))
                allCommands.AddRange(localCmds);

            // Remote commands
            if (_remoteCommands.TryGetValue(_currentTick, out var remoteCmdsForTick))
            {
                foreach (var playerCmds in remoteCmdsForTick.Values)
                    allCommands.AddRange(playerCmds);
            }

            // Sort commands deterministically (by player index, then by command order)
            allCommands.Sort((a, b) =>
            {
                int cmp = a.PlayerIndex.CompareTo(b.PlayerIndex);
                if (cmp != 0) return cmp;
                return a.CommandIndex.CompareTo(b.CommandIndex);
            });

            // Execute all commands
            foreach (var cmd in allCommands)
            {
                ExecuteCommand(cmd);
            }

            if (LogTicks)
                Debug.Log($"[Lockstep] Tick {_currentTick} executed with {allCommands.Count} commands");

            // Compute checksum for sync verification (every 10 ticks)
            if (_currentTick % 10 == 0)
            {
                uint checksum = ComputeGameStateChecksum();
                _checksums[_currentTick] = checksum;
                BroadcastSync(_currentTick, checksum);
            }

            // Cleanup old data
            CleanupOldTickData(_currentTick - MAX_TICK_BUFFER);

            // Advance tick counter
            _currentTick++;

            // Pre-confirm the next input tick if we have no pending commands
            int nextInputTick = _currentTick + INPUT_DELAY_TICKS;
            if (!_localCommands.ContainsKey(nextInputTick))
                _localCommands[nextInputTick] = new List<LockstepCommand>();

            // Fire event
            OnTickAdvanced?.Invoke(_currentTick);
        }

        /// <summary>
        /// Execute a single command
        /// </summary>
        private void ExecuteCommand(LockstepCommand cmd)
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return;

            var em = world.EntityManager;

            // Find entity by network ID
            Entity entity = FindEntityByNetworkId(cmd.EntityNetworkId);
            if (entity == Entity.Null)
            {
                Debug.LogWarning($"[Lockstep] Entity not found for network ID {cmd.EntityNetworkId}");
                return;
            }

            switch (cmd.Type)
            {
                case LockstepCommandType.Move:
                    TheWaningBorder.Core.CommandGateway.IssueMove(em, entity, cmd.TargetPosition);
                    break;

                case LockstepCommandType.Attack:
                    Entity target = FindEntityByNetworkId(cmd.TargetEntityId);
                    if (target != Entity.Null)
                        TheWaningBorder.Core.CommandGateway.IssueAttack(em, entity, target);
                    break;

                case LockstepCommandType.Stop:
                    TheWaningBorder.Core.CommandGateway.IssueStop(em, entity);
                    break;

                case LockstepCommandType.Build:
                    // Building placement handled separately
                    break;

                case LockstepCommandType.Train:
                    // Unit training handled separately
                    break;

                case LockstepCommandType.Gather:
                    Entity resource = FindEntityByNetworkId(cmd.TargetEntityId);
                    Entity deposit = FindEntityByNetworkId(cmd.SecondaryTargetId);
                    if (resource != Entity.Null)
                        TheWaningBorder.Core.CommandGateway.IssueGather(em, entity, resource, deposit);
                    break;
            }

            if (LogCommands)
                Debug.Log($"[Lockstep] Executed {cmd.Type} from player {cmd.PlayerIndex}");
        }

        // ==================== NETWORKING ====================

        private void ReceiveMessages()
        {
            if (_udpClient == null) return;

            try
            {
                while (_udpClient.Available > 0)
                {
                    IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
                    byte[] data = _udpClient.Receive(ref remoteEP);
                    string message = Encoding.UTF8.GetString(data);
                    ProcessMessage(message, remoteEP);
                }
            }
            catch (SocketException) { }
        }

        private void ProcessMessage(string message, IPEndPoint sender)
        {
            string[] parts = message.Split('|');
            if (parts.Length < 2) return;

            string msgType = parts[0];

            switch (msgType)
            {
                case "TICK":
                    ProcessTickMessage(parts, sender);
                    break;

                case "SYNC":
                    ProcessSyncMessage(parts, sender);
                    break;

                case "PING":
                    ProcessPing(parts, sender);
                    break;

                case "PONG":
                    ProcessPong(parts, sender);
                    break;
            }
        }

        private void ProcessTickMessage(string[] parts, IPEndPoint sender)
        {
            // Format: TICK|playerIndex|tickNumber|commandCount|cmd1|cmd2|...
            if (parts.Length < 4) return;

            if (!int.TryParse(parts[1], out int playerIndex)) return;
            if (!int.TryParse(parts[2], out int tick)) return;
            if (!int.TryParse(parts[3], out int cmdCount)) return;

            // Parse commands
            var commands = new List<LockstepCommand>();
            int cmdStartIndex = 4;
            for (int i = 0; i < cmdCount && cmdStartIndex < parts.Length; i++)
            {
                var cmd = LockstepCommand.Deserialize(parts[cmdStartIndex]);
                if (cmd != null)
                {
                    cmd.PlayerIndex = playerIndex;
                    cmd.Tick = tick;
                    commands.Add(cmd);
                }
                cmdStartIndex++;
            }

            // Store commands
            if (!_remoteCommands.ContainsKey(tick))
                _remoteCommands[tick] = new Dictionary<int, List<LockstepCommand>>();

            _remoteCommands[tick][playerIndex] = commands;

            // Update confirmed tick
            _confirmedTicks[playerIndex] = Math.Max(_confirmedTicks.GetValueOrDefault(playerIndex, -1), tick);

            if (LogCommands)
                Debug.Log($"[Lockstep] Received tick {tick} from player {playerIndex} with {cmdCount} commands");

            // If host, relay to other clients
            if (_isHost)
            {
                // Reconstruct message from parts
                string originalMessage = string.Join("|", parts);
                RelayTickMessage(originalMessage, playerIndex);
            }
        }

        private void ProcessSyncMessage(string[] parts, IPEndPoint sender)
        {
            // Format: SYNC|tick|checksum
            if (parts.Length < 3) return;

            if (!int.TryParse(parts[1], out int tick)) return;
            if (!uint.TryParse(parts[2], out uint remoteChecksum)) return;

            // Compare with our checksum
            if (_checksums.TryGetValue(tick, out uint localChecksum))
            {
                if (localChecksum != remoteChecksum)
                {
                    Debug.LogError($"[Lockstep] DESYNC DETECTED at tick {tick}! Local: {localChecksum}, Remote: {remoteChecksum}");
                    OnDesyncDetected?.Invoke($"Desync at tick {tick}");
                }
            }
        }

        private void ProcessPing(string[] parts, IPEndPoint sender)
        {
            // Respond with pong
            string pong = $"PONG|{parts[1]}";
            byte[] data = Encoding.UTF8.GetBytes(pong);
            _udpClient.Send(data, data.Length, sender);
        }

        private void ProcessPong(string[] parts, IPEndPoint sender)
        {
            // Calculate latency
            if (long.TryParse(parts[1], out long timestamp))
            {
                long now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                long latency = now - timestamp;
                
                // Update player latency
                foreach (var player in _remotePlayers)
                {
                    if (player.EndPoint.Equals(sender))
                    {
                        player.Latency = (int)latency;
                        break;
                    }
                }
            }
        }

        private void BroadcastTickCommands(int tick)
        {
            if (!_localCommands.TryGetValue(tick, out var commands))
                commands = new List<LockstepCommand>();

            // Build message
            var sb = new StringBuilder();
            sb.Append($"TICK|{_localPlayerIndex}|{tick}|{commands.Count}");

            for (int i = 0; i < commands.Count; i++)
            {
                commands[i].CommandIndex = i;
                sb.Append("|");
                sb.Append(commands[i].Serialize());
            }

            string message = sb.ToString();
            byte[] data = Encoding.UTF8.GetBytes(message);

            // Send to all remote players
            foreach (var player in _remotePlayers)
            {
                try
                {
                    _udpClient.Send(data, data.Length, player.EndPoint);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Lockstep] Failed to send to player {player.PlayerIndex}: {e.Message}");
                }
            }
        }

        private void RelayTickMessage(string message, int sourcePlayer)
        {
            byte[] data = Encoding.UTF8.GetBytes(message);

            foreach (var player in _remotePlayers)
            {
                if (player.PlayerIndex == sourcePlayer) continue;

                try
                {
                    _udpClient.Send(data, data.Length, player.EndPoint);
                }
                catch { }
            }
        }

        private void BroadcastSync(int tick, uint checksum)
        {
            string message = $"SYNC|{tick}|{checksum}";
            byte[] data = Encoding.UTF8.GetBytes(message);

            foreach (var player in _remotePlayers)
            {
                try
                {
                    _udpClient.Send(data, data.Length, player.EndPoint);
                }
                catch { }
            }
        }

        // ==================== HELPERS ====================

        private Entity FindEntityByNetworkId(int networkId)
        {
            if (networkId <= 0) return Entity.Null;

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return Entity.Null;

            var em = world.EntityManager;

            // Query for NetworkedEntity component
            var query = em.CreateEntityQuery(typeof(NetworkedEntity));
            var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            var networkIds = query.ToComponentDataArray<NetworkedEntity>(Unity.Collections.Allocator.Temp);

            Entity result = Entity.Null;
            for (int i = 0; i < entities.Length; i++)
            {
                if (networkIds[i].NetworkId == networkId)
                {
                    result = entities[i];
                    break;
                }
            }

            entities.Dispose();
            networkIds.Dispose();

            return result;
        }

        private uint ComputeGameStateChecksum()
        {
            // Simple checksum of entity positions and health
            uint checksum = 0;

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return checksum;

            var em = world.EntityManager;
            var query = em.CreateEntityQuery(
                typeof(Unity.Transforms.LocalTransform),
                typeof(FactionTag),
                typeof(Health));

            var transforms = query.ToComponentDataArray<Unity.Transforms.LocalTransform>(Unity.Collections.Allocator.Temp);
            var healths = query.ToComponentDataArray<Health>(Unity.Collections.Allocator.Temp);

            for (int i = 0; i < transforms.Length; i++)
            {
                var pos = transforms[i].Position;
                checksum ^= (uint)(pos.x * 100) ^ (uint)(pos.z * 100) << 8;
                checksum ^= (uint)healths[i].Value << 16;
            }

            transforms.Dispose();
            healths.Dispose();

            return checksum;
        }

        private void CleanupOldTickData(int beforeTick)
        {
            var ticksToRemove = new List<int>();

            foreach (var tick in _localCommands.Keys)
                if (tick < beforeTick) ticksToRemove.Add(tick);
            foreach (var tick in ticksToRemove)
                _localCommands.Remove(tick);

            ticksToRemove.Clear();
            foreach (var tick in _remoteCommands.Keys)
                if (tick < beforeTick) ticksToRemove.Add(tick);
            foreach (var tick in ticksToRemove)
                _remoteCommands.Remove(tick);

            ticksToRemove.Clear();
            foreach (var tick in _checksums.Keys)
                if (tick < beforeTick) ticksToRemove.Add(tick);
            foreach (var tick in ticksToRemove)
                _checksums.Remove(tick);
        }

        private void Cleanup()
        {
            if (_udpClient != null)
            {
                try { _udpClient.Close(); } catch { }
                _udpClient = null;
            }

            _remotePlayers.Clear();
            _localCommands.Clear();
            _remoteCommands.Clear();
            _confirmedTicks.Clear();
            _checksums.Clear();
            _simulationStarted = false;
        }

        // ==================== PUBLIC API ====================

        public int CurrentTick => _currentTick;
        public bool IsSimulationRunning => _simulationStarted;
        public bool IsHost => _isHost;
        public int LocalPlayerIndex => _localPlayerIndex;
        public Faction LocalFaction => _localFaction;

        public void ConfirmCurrentInputTick()
        {
            ConfirmTick(_currentTick + INPUT_DELAY_TICKS);
        }
    }

    // Types moved to LockstepTypes.cs:
    // - RemotePlayer
    // - RemotePlayerInfo
    // - LockstepCommandType
    // - LockstepCommand
}