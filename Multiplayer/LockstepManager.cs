// Assets/Scripts/Multiplayer/LockstepManager.cs
// Fixed version with corrected property names to match LockstepTypes.cs

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using TheWaningBorder.Core;

namespace TheWaningBorder.Multiplayer
{
    /// <summary>
    /// Lockstep multiplayer manager.
    /// 
    /// How it works:
    /// 1. Game runs in discrete "ticks" (e.g., 10 ticks per second)
    /// 2. Player commands are collected locally but NOT executed immediately
    /// 3. Commands are sent to all players with a target tick number
    /// 4. Simulation only advances when ALL players have confirmed their commands for that tick
    /// 5. All players execute the same commands on the same tick = deterministic
    /// 
    /// Network Protocol:
    /// - TICK|playerIndex|tickNumber|commandCount|cmd1|cmd2|...  (Player sends their commands for a tick)
    /// - SYNC|tickNumber|checksum                                 (Periodic sync check)
    /// - PING|timestamp                                           (Latency measurement)
    /// - PONG|timestamp                                           (Latency response)
    /// </summary>
    public class LockstepManager : MonoBehaviour
    {
        public static LockstepManager Instance { get; private set; }

        // Configuration
        public const int TICKS_PER_SECOND = 10;
        public const float TICK_DURATION = 1f / TICKS_PER_SECOND;
        public const int INPUT_DELAY_TICKS = 2;
        public const int MAX_TICK_BUFFER = 60;

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

        // Command buffers
        private Dictionary<int, List<LockstepCommand>> _localCommands = new Dictionary<int, List<LockstepCommand>>();
        private Dictionary<int, Dictionary<int, List<LockstepCommand>>> _remoteCommands = new Dictionary<int, Dictionary<int, List<LockstepCommand>>>();

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

        // Public properties
        public int CurrentTick => _currentTick;
        public bool IsSimulationRunning => _simulationStarted;
        public bool IsHost => _isHost;

        // Debug
        [Header("Debug")]
        public bool LogCommands = true;
        public bool LogTicks = true;
        public bool LogNetwork = true;

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

            // Validate port
            if (port <= 0 || port > 65535)
            {
                Debug.LogWarning($"[Lockstep] Invalid host port {port}, using 7980");
                port = 7980;
            }

            // Create UDP socket with error handling
            try
            {
                _udpClient = new UdpClient(port);
                _localPort = port;
            }
            catch (SocketException ex)
            {
                Debug.LogWarning($"[Lockstep] Port {port} in use: {ex.Message}. Trying auto-assign...");
                _udpClient = new UdpClient(0);
                _localPort = ((IPEndPoint)_udpClient.Client.LocalEndPoint).Port;
            }
            
            _udpClient.Client.ReceiveTimeout = 1;

            // Add remote players
            _remotePlayers.Clear();
            for (int i = 0; i < players.Count; i++)
            {
                var player = new RemotePlayer
                {
                    PlayerIndex = i + 1,
                    Faction = players[i].Faction,
                    EndPoint = new IPEndPoint(IPAddress.Parse(players[i].IP), players[i].Port),
                    LastConfirmedTick = -1
                };
                _remotePlayers.Add(player);
                _confirmedTicks[player.PlayerIndex] = -1;
            }

            _confirmedTicks[_localPlayerIndex] = -1;

            Debug.Log($"[Lockstep] Initialized as HOST on port {_localPort} with {players.Count} remote players");
        }

        /// <summary>
        /// Initialize as client (player 1+)
        /// </summary>
        public void InitializeAsClient(int localPort, string hostIP, int hostPort, int playerIndex, Faction faction)
        {
            _isHost = false;
            _localPlayerIndex = playerIndex;
            _localFaction = faction;

            Debug.Log($"[Lockstep] InitializeAsClient called with localPort={localPort}, hostIP={hostIP}, hostPort={hostPort}, playerIndex={playerIndex}");

            // Validate port range
            if (localPort < 0 || localPort > 65535)
            {
                Debug.LogWarning($"[Lockstep] Invalid localPort {localPort}, will use auto-assign (0)");
                localPort = 0;
            }

            // Create UDP socket with error handling
            try
            {
                if (localPort == 0)
                {
                    _udpClient = new UdpClient(0);
                    _localPort = ((IPEndPoint)_udpClient.Client.LocalEndPoint).Port;
                    Debug.Log($"[Lockstep] Auto-assigned local port: {_localPort}");
                }
                else
                {
                    _udpClient = new UdpClient(localPort);
                    _localPort = localPort;
                }
            }
            catch (SocketException ex)
            {
                Debug.LogWarning($"[Lockstep] Could not bind to port {localPort}: {ex.Message}. Using auto-assign...");
                _udpClient = new UdpClient(0);
                _localPort = ((IPEndPoint)_udpClient.Client.LocalEndPoint).Port;
                Debug.Log($"[Lockstep] Auto-assigned local port after error: {_localPort}");
            }

            _udpClient.Client.ReceiveTimeout = 1;

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

            Debug.Log($"[Lockstep] Initialized as CLIENT (player {playerIndex}) on local port {_localPort}, connecting to host at {hostIP}:{hostPort}");
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

            for (int t = 0; t < INPUT_DELAY_TICKS + 1; t++)
            {
                _localCommands[t] = new List<LockstepCommand>();
                ConfirmTick(t);
            }

            Debug.Log("[Lockstep] Simulation started");
        }

        void Update()
        {
            if (!_simulationStarted) return;

            ReceiveMessages();

            int inputTick = _currentTick + INPUT_DELAY_TICKS;
            if (!_localCommands.ContainsKey(inputTick))
            {
                _localCommands[inputTick] = new List<LockstepCommand>();
            }

            if (_confirmedTicks.GetValueOrDefault(_localPlayerIndex, -1) < inputTick)
            {
                ConfirmTick(inputTick);
            }

            _tickAccumulator += Time.deltaTime;

            while (_tickAccumulator >= TICK_DURATION)
            {
                if (CanAdvanceTick())
                {
                    AdvanceTick();
                    _tickAccumulator -= TICK_DURATION;
                }
                else
                {
                    if (!_waitingForPlayers)
                    {
                        _waitingForPlayers = true;
                        if (LogTicks)
                            Debug.Log($"[Lockstep] Waiting for other players at tick {_currentTick}");
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

        private void ConfirmTick(int tick)
        {
            _confirmedTicks[_localPlayerIndex] = tick;

            if (!_localCommands.ContainsKey(tick))
                _localCommands[tick] = new List<LockstepCommand>();

            BroadcastTick(tick, _localCommands[tick]);
        }

        private bool CanAdvanceTick()
        {
            if (_remotePlayers.Count == 0)
                return true;

            foreach (var player in _remotePlayers)
            {
                if (_confirmedTicks.GetValueOrDefault(player.PlayerIndex, -1) < _currentTick)
                    return false;
            }

            return _confirmedTicks.GetValueOrDefault(_localPlayerIndex, -1) >= _currentTick;
        }

        private void AdvanceTick()
        {
            ExecuteCommands(_currentTick);

            if (_currentTick % 10 == 0)
            {
                uint checksum = ComputeChecksum();
                _checksums[_currentTick] = checksum;
                BroadcastSync(_currentTick, checksum);
            }

            _currentTick++;
            _waitingForPlayers = false;

            OnTickAdvanced?.Invoke(_currentTick);

            CleanupOldData(_currentTick - MAX_TICK_BUFFER);
        }

        private void ExecuteCommands(int tick)
        {
            var allCommands = new List<LockstepCommand>();

            if (_localCommands.TryGetValue(tick, out var localCmds))
                allCommands.AddRange(localCmds);

            if (_remoteCommands.TryGetValue(tick, out var remoteCmdsByPlayer))
            {
                foreach (var kvp in remoteCmdsByPlayer)
                    allCommands.AddRange(kvp.Value);
            }

            allCommands.Sort((a, b) => {
                int cmp = a.PlayerIndex.CompareTo(b.PlayerIndex);
                return cmp != 0 ? cmp : a.Type.CompareTo(b.Type);
            });

            if (LogTicks && allCommands.Count > 0)
                Debug.Log($"[Lockstep] Tick {tick} executed with {allCommands.Count} commands");

            foreach (var cmd in allCommands)
            {
                ExecuteCommand(cmd);
            }
        }

        private void ExecuteCommand(LockstepCommand cmd)
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return;

            var em = world.EntityManager;

            Entity entity = FindEntityByNetworkId(cmd.EntityNetworkId);
            // FIXED: SetRally instead of RallyPoint
            if (entity == Entity.Null && cmd.Type != LockstepCommandType.SetRally)
            {
                if (LogCommands)
                    Debug.LogWarning($"[Lockstep] Entity not found for network ID {cmd.EntityNetworkId}");
                return;
            }

            // FIXED: TargetEntityId instead of TargetNetworkId
            Entity targetEntity = cmd.TargetEntityId > 0 ? FindEntityByNetworkId(cmd.TargetEntityId) : Entity.Null;

            switch (cmd.Type)
            {
                case LockstepCommandType.Move:
                    // FIXED: TargetPosition instead of Position
                    CommandGateway.IssueMove(em, entity, cmd.TargetPosition);
                    if (LogCommands) Debug.Log($"[Lockstep] Executed Move from player {cmd.PlayerIndex}");
                    break;

                case LockstepCommandType.Attack:
                    if (targetEntity != Entity.Null)
                    {
                        CommandGateway.IssueAttack(em, entity, targetEntity);
                        if (LogCommands) Debug.Log($"[Lockstep] Executed Attack from player {cmd.PlayerIndex}");
                    }
                    break;

                case LockstepCommandType.Stop:
                    CommandGateway.IssueStop(em, entity);
                    if (LogCommands) Debug.Log($"[Lockstep] Executed Stop from player {cmd.PlayerIndex}");
                    break;

                case LockstepCommandType.Gather:
                    // FIXED: SecondaryTargetId instead of SecondaryNetworkId
                    Entity depositEntity = cmd.SecondaryTargetId > 0 ? FindEntityByNetworkId(cmd.SecondaryTargetId) : Entity.Null;
                    if (targetEntity != Entity.Null)
                    {
                        CommandGateway.IssueGather(em, entity, targetEntity, depositEntity);
                        if (LogCommands) Debug.Log($"[Lockstep] Executed Gather from player {cmd.PlayerIndex}");
                    }
                    break;

                case LockstepCommandType.Build:
                    // FIXED: TargetEntityId instead of TargetNetworkId, TargetPosition instead of Position
                    Entity buildingEntity = cmd.TargetEntityId > 0 ? FindEntityByNetworkId(cmd.TargetEntityId) : Entity.Null;
                    CommandGateway.IssueBuild(em, entity, buildingEntity, cmd.BuildingId, cmd.TargetPosition);
                    if (LogCommands) Debug.Log($"[Lockstep] Executed Build from player {cmd.PlayerIndex}");
                    break;

                case LockstepCommandType.Heal:
                    if (targetEntity != Entity.Null)
                    {
                        CommandGateway.IssueHeal(em, entity, targetEntity);
                        if (LogCommands) Debug.Log($"[Lockstep] Executed Heal from player {cmd.PlayerIndex}");
                    }
                    break;

                // FIXED: SetRally instead of RallyPoint, direct component manipulation instead of CommandGateway.SetRallyPoint
                case LockstepCommandType.SetRally:
                    if (entity != Entity.Null)
                    {
                        if (!em.HasComponent<RallyPoint>(entity))
                            em.AddComponent<RallyPoint>(entity);
                        em.SetComponentData(entity, new RallyPoint { Position = cmd.TargetPosition, Has = 1 });
                        if (LogCommands) Debug.Log($"[Lockstep] Executed SetRally from player {cmd.PlayerIndex}");
                    }
                    break;
            }
        }

        public Entity FindEntityByNetworkId(int networkId)
        {
            if (networkId <= 0) return Entity.Null;

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return Entity.Null;

            var em = world.EntityManager;
            var query = em.CreateEntityQuery(ComponentType.ReadOnly<NetworkedEntity>());

            using var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            using var netIds = query.ToComponentDataArray<NetworkedEntity>(Unity.Collections.Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                if (netIds[i].NetworkId == networkId)
                    return entities[i];
            }

            return Entity.Null;
        }

        private uint ComputeChecksum()
        {
            uint hash = 0;

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return hash;

            var em = world.EntityManager;

            // Simple checksum based on entity positions
            var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<Unity.Transforms.LocalTransform>(),
                ComponentType.ReadOnly<NetworkedEntity>()
            );

            using var transforms = query.ToComponentDataArray<Unity.Transforms.LocalTransform>(Unity.Collections.Allocator.Temp);

            for (int i = 0; i < transforms.Length; i++)
            {
                hash ^= (uint)(transforms[i].Position.x * 1000) * 31;
                hash ^= (uint)(transforms[i].Position.z * 1000) * 37;
            }

            return hash;
        }

        // ==================== Networking ====================

        private void ReceiveMessages()
        {
            if (_udpClient == null) return;

            try
            {
                while (_udpClient.Available > 0)
                {
                    IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                    byte[] data = _udpClient.Receive(ref sender);
                    string message = Encoding.UTF8.GetString(data);
                    ProcessMessage(message, sender);
                }
            }
            catch (SocketException)
            {
                // Timeout or no data - normal
            }
        }

        private void ProcessMessage(string message, IPEndPoint sender)
        {
            if (string.IsNullOrEmpty(message)) return;

            string[] parts = message.Split('|');
            if (parts.Length < 2) return;

            switch (parts[0])
            {
                case "TICK":
                    ProcessTickMessage(parts, sender);
                    break;
                case "SYNC":
                    ProcessSyncMessage(parts, sender);
                    break;
                case "PING":
                    SendPong(sender, parts.Length > 1 ? parts[1] : "0");
                    break;
                case "PONG":
                    // Latency measurement (optional)
                    break;
            }
        }

        private void ProcessTickMessage(string[] parts, IPEndPoint sender)
        {
            if (parts.Length < 4) return;

            if (!int.TryParse(parts[1], out int playerIndex)) return;
            if (!int.TryParse(parts[2], out int tick)) return;
            if (!int.TryParse(parts[3], out int cmdCount)) return;

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

            if (!_remoteCommands.ContainsKey(tick))
                _remoteCommands[tick] = new Dictionary<int, List<LockstepCommand>>();

            _remoteCommands[tick][playerIndex] = commands;
            _confirmedTicks[playerIndex] = Math.Max(_confirmedTicks.GetValueOrDefault(playerIndex, -1), tick);

            if (LogCommands)
                Debug.Log($"[Lockstep] Received tick {tick} from player {playerIndex} with {cmdCount} commands");

            if (_isHost)
            {
                string originalMessage = string.Join("|", parts);
                RelayTickMessage(originalMessage, playerIndex);
            }
        }

        private void ProcessSyncMessage(string[] parts, IPEndPoint sender)
        {
            if (parts.Length < 3) return;

            if (!int.TryParse(parts[1], out int tick)) return;
            if (!uint.TryParse(parts[2], out uint remoteChecksum)) return;

            if (_checksums.TryGetValue(tick, out uint localChecksum))
            {
                if (localChecksum != remoteChecksum)
                {
                    Debug.LogError($"[Lockstep] DESYNC DETECTED at tick {tick}! Local: {localChecksum}, Remote: {remoteChecksum}");
                    OnDesyncDetected?.Invoke($"Desync at tick {tick}");
                }
            }
        }

        private void SendPong(IPEndPoint target, string timestamp)
        {
            try
            {
                string message = $"PONG|{timestamp}";
                byte[] data = Encoding.UTF8.GetBytes(message);
                _udpClient.Send(data, data.Length, target);
            }
            catch { }
        }

        private void BroadcastTick(int tick, List<LockstepCommand> commands)
        {
            var sb = new StringBuilder();
            sb.Append($"TICK|{_localPlayerIndex}|{tick}|{commands.Count}");

            foreach (var cmd in commands)
            {
                sb.Append("|");
                sb.Append(cmd.Serialize());
            }

            string message = sb.ToString();
            byte[] data = Encoding.UTF8.GetBytes(message);

            foreach (var player in _remotePlayers)
            {
                try
                {
                    _udpClient.Send(data, data.Length, player.EndPoint);
                    if (LogNetwork)
                        Debug.Log($"[Lockstep] Sent tick {tick} to player {player.PlayerIndex} at {player.EndPoint}");
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

        private void CleanupOldData(int beforeTick)
        {
            var toRemove = new List<int>();

            foreach (var tick in _localCommands.Keys)
                if (tick < beforeTick) toRemove.Add(tick);
            foreach (var tick in toRemove)
                _localCommands.Remove(tick);

            toRemove.Clear();
            foreach (var tick in _remoteCommands.Keys)
                if (tick < beforeTick) toRemove.Add(tick);
            foreach (var tick in toRemove)
                _remoteCommands.Remove(tick);

            toRemove.Clear();
            foreach (var tick in _checksums.Keys)
                if (tick < beforeTick) toRemove.Add(tick);
            foreach (var tick in toRemove)
                _checksums.Remove(tick);
        }

        private void Cleanup()
        {
            _simulationStarted = false;
            _udpClient?.Close();
            _udpClient = null;
        }
    }
}