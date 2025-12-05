// LockstepManager.cs
// Lockstep multiplayer manager for deterministic simulation
// Part of: Multiplayer/Lockstep/

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
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
    /// - TICK|playerIndex|tickNumber|commandCount|cmd1|cmd2|...  (Player sends commands)
    /// - SYNC|tickNumber|checksum                                 (Periodic sync check)
    /// - PING|timestamp                                           (Latency measurement)
    /// - PONG|timestamp                                           (Latency response)
    /// </summary>
    public class LockstepManager : MonoBehaviour
    {
        // ═══════════════════════════════════════════════════════════════════════
        // SINGLETON
        // ═══════════════════════════════════════════════════════════════════════
        
        public static LockstepManager Instance { get; private set; }

        // ═══════════════════════════════════════════════════════════════════════
        // CONFIGURATION
        // ═══════════════════════════════════════════════════════════════════════
        
        public const int TICKS_PER_SECOND = 10;
        public const float TICK_DURATION = 1f / TICKS_PER_SECOND;
        public const int INPUT_DELAY_TICKS = 2;
        public const int MAX_TICK_BUFFER = 60;

        // ═══════════════════════════════════════════════════════════════════════
        // NETWORK STATE
        // ═══════════════════════════════════════════════════════════════════════
        
        private UdpClient _udpClient;
        private int _localPort;
        private bool _isHost;
        private List<RemotePlayer> _remotePlayers = new List<RemotePlayer>();
        
        // ═══════════════════════════════════════════════════════════════════════
        // SIMULATION STATE
        // ═══════════════════════════════════════════════════════════════════════
        
        private int _currentTick = 0;
        private float _tickAccumulator = 0f;
        private bool _simulationStarted = false;
        private bool _waitingForPlayers = false;

        // Command buffers
        private Dictionary<int, List<LockstepCommand>> _localCommands = new();
        private Dictionary<int, Dictionary<int, List<LockstepCommand>>> _remoteCommands = new();

        // Confirmed ticks per player
        private Dictionary<int, int> _confirmedTicks = new();

        // Local player info
        private int _localPlayerIndex = 0;
        private Faction _localFaction = Faction.Blue;

        // Sync verification
        private Dictionary<int, uint> _checksums = new();

        // ═══════════════════════════════════════════════════════════════════════
        // EVENTS
        // ═══════════════════════════════════════════════════════════════════════
        
        public event Action<int> OnTickAdvanced;
        public event Action<string> OnDesyncDetected;

        // ═══════════════════════════════════════════════════════════════════════
        // DEBUG
        // ═══════════════════════════════════════════════════════════════════════
        
        [Header("Debug")]
        public bool LogCommands = true;
        public bool LogTicks = true;
        public bool LogNetwork = true;

        // ═══════════════════════════════════════════════════════════════════════
        // PUBLIC PROPERTIES
        // ═══════════════════════════════════════════════════════════════════════
        
        public bool IsHost => _isHost;
        public bool IsSimulationRunning => _simulationStarted;
        public int CurrentTick => _currentTick;
        public Faction LocalFaction => _localFaction;
        public int LocalPlayerIndex => _localPlayerIndex;

        // ═══════════════════════════════════════════════════════════════════════
        // LIFECYCLE
        // ═══════════════════════════════════════════════════════════════════════

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
            Shutdown();
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (!_simulationStarted) return;

            ReceiveMessages();

            // Confirm our commands for the current input tick
            int inputTick = _currentTick + INPUT_DELAY_TICKS;
            if (_confirmedTicks.GetValueOrDefault(_localPlayerIndex, -1) < inputTick)
            {
                ConfirmTick(inputTick);
            }

            // Advance simulation
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
                        if (LogTicks) Debug.Log($"[Lockstep] Waiting for players at tick {_currentTick}");
                    }
                    break;
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // INITIALIZATION
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Initialize as the game host.
        /// </summary>
        public void InitializeAsHost(int port, List<RemotePlayerInfo> remotePlayerInfos)
        {
            _isHost = true;
            _localPlayerIndex = 0;
            _localFaction = Faction.Blue;
            _localPort = port;

            try
            {
                _udpClient = new UdpClient(port);
                _udpClient.Client.ReceiveTimeout = 1;
            }
            catch (SocketException e)
            {
                Debug.LogError($"[Lockstep] Failed to bind port {port}: {e.Message}. Using auto-assign...");
                _udpClient = new UdpClient(0);
                _localPort = ((IPEndPoint)_udpClient.Client.LocalEndPoint).Port;
            }

            // Add remote players
            _remotePlayers.Clear();
            int playerIndex = 1;
            foreach (var info in remotePlayerInfos)
            {
                _remotePlayers.Add(new RemotePlayer
                {
                    PlayerIndex = playerIndex,
                    Faction = info.Faction,
                    EndPoint = new IPEndPoint(IPAddress.Parse(info.IP), info.Port),
                    LastConfirmedTick = -1
                });
                _confirmedTicks[playerIndex] = -1;
                playerIndex++;
            }

            _confirmedTicks[0] = -1;
            Debug.Log($"[Lockstep] Initialized as HOST on port {_localPort} with {_remotePlayers.Count} remote players");
        }

        /// <summary>
        /// Initialize as a client connecting to host.
        /// </summary>
        public void InitializeAsClient(int localPort, string hostIP, int hostPort, int playerIndex, Faction faction)
        {
            _isHost = false;
            _localPlayerIndex = playerIndex;
            _localFaction = faction;

            try
            {
                _udpClient = new UdpClient(localPort);
                _localPort = localPort;
            }
            catch (SocketException e)
            {
                Debug.LogWarning($"[Lockstep] Port {localPort} unavailable: {e.Message}. Using auto-assign...");
                _udpClient = new UdpClient(0);
                _localPort = ((IPEndPoint)_udpClient.Client.LocalEndPoint).Port;
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

            Debug.Log($"[Lockstep] Initialized as CLIENT (player {playerIndex}) on port {_localPort}, host at {hostIP}:{hostPort}");
        }

        /// <summary>
        /// Start the lockstep simulation.
        /// </summary>
        public void StartSimulation()
        {
            _currentTick = 0;
            _tickAccumulator = 0f;
            _simulationStarted = true;
            _waitingForPlayers = false;

            // Pre-confirm initial ticks
            for (int t = 0; t < INPUT_DELAY_TICKS + 1; t++)
            {
                _localCommands[t] = new List<LockstepCommand>();
                ConfirmTick(t);
            }

            Debug.Log("[Lockstep] Simulation started");
        }

        /// <summary>
        /// Shutdown the lockstep system.
        /// </summary>
        public void Shutdown()
        {
            _simulationStarted = false;
            
            try
            {
                _udpClient?.Close();
            }
            catch { }
            
            _udpClient = null;
            _remotePlayers.Clear();
            _localCommands.Clear();
            _remoteCommands.Clear();
            _confirmedTicks.Clear();
            _checksums.Clear();
            
            Debug.Log("[Lockstep] Shutdown complete");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // COMMAND QUEUEING
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Queue a command to be executed in the future.
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

        // ═══════════════════════════════════════════════════════════════════════
        // TICK MANAGEMENT
        // ═══════════════════════════════════════════════════════════════════════

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

            // Check all remote players have confirmed
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

            // Periodic sync check
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

            // Collect local commands
            if (_localCommands.TryGetValue(tick, out var localCmds))
                allCommands.AddRange(localCmds);

            // Collect remote commands
            if (_remoteCommands.TryGetValue(tick, out var remoteCmdsByPlayer))
            {
                foreach (var kvp in remoteCmdsByPlayer)
                    allCommands.AddRange(kvp.Value);
            }

            // Sort for determinism
            allCommands.Sort((a, b) =>
            {
                int cmp = a.PlayerIndex.CompareTo(b.PlayerIndex);
                return cmp != 0 ? cmp : a.Type.CompareTo(b.Type);
            });

            if (LogTicks && allCommands.Count > 0)
                Debug.Log($"[Lockstep] Tick {tick} executing {allCommands.Count} commands");

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
            if (entity == Entity.Null && cmd.Type != LockstepCommandType.SetRally)
            {
                if (LogCommands)
                    Debug.LogWarning($"[Lockstep] Entity not found for network ID {cmd.EntityNetworkId}");
                return;
            }

            Entity targetEntity = cmd.TargetEntityId > 0 ? FindEntityByNetworkId(cmd.TargetEntityId) : Entity.Null;

            switch (cmd.Type)
            {
                case LockstepCommandType.Move:
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
                    Entity depositEntity = cmd.SecondaryTargetId > 0 ? FindEntityByNetworkId(cmd.SecondaryTargetId) : Entity.Null;
                    if (targetEntity != Entity.Null)
                    {
                        CommandGateway.IssueGather(em, entity, targetEntity, depositEntity);
                        if (LogCommands) Debug.Log($"[Lockstep] Executed Gather from player {cmd.PlayerIndex}");
                    }
                    break;

                case LockstepCommandType.Build:
                    Entity buildTarget = cmd.TargetEntityId > 0 ? FindEntityByNetworkId(cmd.TargetEntityId) : Entity.Null;
                    CommandGateway.IssueBuild(em, entity, buildTarget, cmd.BuildingId, cmd.TargetPosition);
                    if (LogCommands) Debug.Log($"[Lockstep] Executed Build from player {cmd.PlayerIndex}");
                    break;

                case LockstepCommandType.Heal:
                    if (targetEntity != Entity.Null)
                    {
                        CommandGateway.IssueHeal(em, entity, targetEntity);
                        if (LogCommands) Debug.Log($"[Lockstep] Executed Heal from player {cmd.PlayerIndex}");
                    }
                    break;

                case LockstepCommandType.SetRally:
                    if (entity != Entity.Null)
                    {
                        CommandGateway.SetRallyPoint(em, entity, cmd.TargetPosition);
                        if (LogCommands) Debug.Log($"[Lockstep] Executed RallyPoint from player {cmd.PlayerIndex}");
                    }
                    break;
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // NETWORK - SEND
        // ═══════════════════════════════════════════════════════════════════════

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
                        Debug.Log($"[Lockstep] Sent tick {tick} to player {player.PlayerIndex}");
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

        // ═══════════════════════════════════════════════════════════════════════
        // NETWORK - RECEIVE
        // ═══════════════════════════════════════════════════════════════════════

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

                    if (LogNetwork)
                        Debug.Log($"[Lockstep] Received: {message.Substring(0, Math.Min(50, message.Length))}...");

                    ProcessMessage(message, remoteEP);
                }
            }
            catch (SocketException) { }
            catch (Exception e)
            {
                Debug.LogError($"[Lockstep] Receive error: {e.Message}");
            }
        }

        private void ProcessMessage(string message, IPEndPoint sender)
        {
            string[] parts = message.Split('|');
            if (parts.Length < 1) return;

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

            // Host relays to other clients
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

        // ═══════════════════════════════════════════════════════════════════════
        // HELPERS
        // ═══════════════════════════════════════════════════════════════════════

        private Entity FindEntityByNetworkId(int networkId)
        {
            if (networkId <= 0) return Entity.Null;

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return Entity.Null;

            var em = world.EntityManager;
            var query = em.CreateEntityQuery(typeof(NetworkedEntity));
            var entities = query.ToEntityArray(Allocator.Temp);

            Entity result = Entity.Null;
            for (int i = 0; i < entities.Length; i++)
            {
                if (em.GetComponentData<NetworkedEntity>(entities[i]).NetworkId == networkId)
                {
                    result = entities[i];
                    break;
                }
            }

            entities.Dispose();
            return result;
        }

        private uint ComputeChecksum()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return 0;

            var em = world.EntityManager;
            var query = em.CreateEntityQuery(typeof(FactionTag));
            int count = query.CalculateEntityCount();
            return (uint)(count * 31 + _currentTick);
        }

        private void CleanupOldData(int beforeTick)
        {
            if (beforeTick < 0) return;

            var keysToRemove = new List<int>();
            
            foreach (var key in _localCommands.Keys)
                if (key < beforeTick) keysToRemove.Add(key);
            foreach (var key in keysToRemove)
                _localCommands.Remove(key);

            keysToRemove.Clear();
            foreach (var key in _remoteCommands.Keys)
                if (key < beforeTick) keysToRemove.Add(key);
            foreach (var key in keysToRemove)
                _remoteCommands.Remove(key);

            keysToRemove.Clear();
            foreach (var key in _checksums.Keys)
                if (key < beforeTick) keysToRemove.Add(key);
            foreach (var key in keysToRemove)
                _checksums.Remove(key);
        }
    }
}