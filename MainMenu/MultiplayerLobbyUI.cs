// Assets/Scripts/MainMenu/MultiplayerLobbyUI.cs
using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Generic;
using System.Linq;

namespace TheWaningBorder.Menu
{
    /// <summary>
    /// Multiplayer lobby UI with full networking.
    /// Handles hosting, joining, and lobby state synchronization.
    /// 
    /// Network Protocol (all UDP on port 47777):
    /// - TWB_GAME|GameName|HostName|GamePort         (Host broadcasts availability)
    /// - TWB_JOIN|PlayerName                         (Client requests to join)
    /// - TWB_LOBBY|SlotCount|Slot0Data|Slot1Data|... (Host broadcasts lobby state)
    /// - TWB_ACCEPT|SlotIndex                        (Host accepts join)
    /// - TWB_LEAVE|SlotIndex                         (Client leaves)
    /// - TWB_START|Port                              (Host signals game start)
    /// </summary>
    public class MultiplayerLobbyUI : MonoBehaviour
    {
        public event Action OnBackPressed;

        private const string GameSceneName = "Game";
        private const int BROADCAST_PORT = 47777;
        private const float BROADCAST_INTERVAL = 1.0f;
        private const float LOBBY_SYNC_INTERVAL = 0.5f;
        private const float DISCOVERY_TIMEOUT = 5.0f;
        
        // Message prefixes
        private const string MSG_GAME = "TWB_GAME|";
        private const string MSG_JOIN = "TWB_JOIN|";
        private const string MSG_LOBBY = "TWB_LOBBY|";
        private const string MSG_LEAVE = "TWB_LEAVE|";
        private const string MSG_START = "TWB_START|";
        private const string MSG_ACCEPT = "TWB_ACCEPT|";

        // Lobby state machine
        private enum LobbyState
        {
            MainChoice,
            HostSetup,
            HostLobby,
            BrowseGames,
            ClientLobby,
            Connecting
        }
        private LobbyState _state = LobbyState.MainChoice;

        // Window layout
        private Rect _windowRect = new Rect(40, 40, 560, 620);
        private Vector2 _slotsScrollPos;
        private Vector2 _gamesScrollPos;

        // Host settings
        private string _gameName = "My Game";
        private string _playerName = System.Environment.MachineName;
        private ushort _port = 7979;

        // Map settings
        private SpawnLayout _layout = GameSettings.SpawnLayout;
        private TwoSidesPreset _twoSides = GameSettings.TwoSides;
        private int _spawnSeed = GameSettings.SpawnSeed;
        private bool _fogOfWar = GameSettings.FogOfWarEnabled;
        private int _mapHalfSize = GameSettings.MapHalfSize;

        // Network state
        private UdpClient _udpClient;
        private bool _isHost;
        private bool _isConnected;
        private float _lastBroadcastTime;
        private float _lastLobbySyncTime;
        private string _hostIP;
        private int _mySlotIndex = -1;
        
        // Client tracking (host only)
        private Dictionary<string, ClientInfo> _connectedClients = new Dictionary<string, ClientInfo>();
        
        // Discovery
        private List<DiscoveredGame> _discoveredGames = new List<DiscoveredGame>();

        // Lobby slots (synchronized)
        private NetworkSlot[] _networkSlots = new NetworkSlot[8];

        // Error/status
        private string _error;
        private string _status;

        // Styles
        private GUIStyle _headerStyle;
        private GUIStyle _slotStyle;
        private GUIStyle _factionLabelStyle;
        private GUIStyle _gameListStyle;
        private bool _stylesInit = false;

        // Data classes
        [Serializable]
        public class DiscoveredGame
        {
            public string GameName;
            public string HostName;
            public string IPAddress;
            public ushort Port;
            public DateTime LastSeen;
        }

        public class ClientInfo
        {
            public string PlayerName;
            public int SlotIndex;
            public IPEndPoint EndPoint;
            public DateTime LastSeen;
        }

        public class NetworkSlot
        {
            public SlotType Type = SlotType.Empty;
            public string PlayerName = "";
            public LobbyAIDifficulty AIDifficulty = LobbyAIDifficulty.Normal;
            public string ClientIP = ""; // For tracking which client owns this slot
        }

        void Awake()
        {
            // Keep running when window loses focus (essential for multiplayer testing)
            Application.runInBackground = true;
            
            // Initialize network slots
            for (int i = 0; i < 8; i++)
            {
                _networkSlots[i] = new NetworkSlot();
            }
        }

        void OnEnable()
        {
            _state = LobbyState.MainChoice;
            _error = null;
            _status = null;
            _isHost = false;
            _isConnected = false;
            _mySlotIndex = -1;
        }

        void OnDisable()
        {
            Cleanup();
        }

        void OnDestroy()
        {
            Cleanup();
        }

        private void Cleanup()
        {
            if (_udpClient != null)
            {
                // If we're a client, send leave message
                if (!_isHost && _isConnected && _mySlotIndex >= 0)
                {
                    SendToHost($"{MSG_LEAVE}{_mySlotIndex}");
                }
                
                try { _udpClient.Close(); } catch { }
                _udpClient = null;
            }
            _isHost = false;
            _isConnected = false;
            _connectedClients.Clear();
        }

        void Update()
        {
            if (_udpClient == null) return;

            // Receive messages
            ReceiveMessages();

            if (_isHost)
            {
                // Host: broadcast game availability
                if (Time.time - _lastBroadcastTime >= BROADCAST_INTERVAL)
                {
                    BroadcastGameAvailability();
                    _lastBroadcastTime = Time.time;
                }

                // Host: broadcast lobby state to connected clients
                if (Time.time - _lastLobbySyncTime >= LOBBY_SYNC_INTERVAL)
                {
                    BroadcastLobbyState();
                    _lastLobbySyncTime = Time.time;
                }

                // Cleanup disconnected clients
                CleanupDisconnectedClients();
            }
            else if (_isConnected || _state == LobbyState.Connecting)
            {
                // Client: send heartbeat (re-send join request) to keep connection alive
                if (Time.time - _lastBroadcastTime >= BROADCAST_INTERVAL)
                {
                    SendHeartbeat();
                    _lastBroadcastTime = Time.time;
                }
            }

            // Discovery: cleanup stale games
            if (_state == LobbyState.BrowseGames)
            {
                _discoveredGames.RemoveAll(g => 
                    (DateTime.Now - g.LastSeen).TotalSeconds > DISCOVERY_TIMEOUT);
            }
        }

        private void SendHeartbeat()
        {
            // Re-send join request as heartbeat - host will recognize us and update LastSeen
            if (!string.IsNullOrEmpty(_hostIP))
            {
                try
                {
                    string message = $"{MSG_JOIN}{_playerName}";
                    byte[] data = Encoding.UTF8.GetBytes(message);
                    IPEndPoint hostEP = new IPEndPoint(IPAddress.Parse(_hostIP), BROADCAST_PORT);
                    _udpClient.Send(data, data.Length, hostEP);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[MultiplayerLobbyUI] Heartbeat failed: {e.Message}");
                }
            }
        }

        // ==================== NETWORK - CORE ====================

        private void StartHost()
        {
            Cleanup();

            try
            {
                // Use SO_REUSEADDR to allow host and client on same machine (for testing)
                _udpClient = new UdpClient();
                _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, BROADCAST_PORT));
                _udpClient.EnableBroadcast = true;
                _udpClient.Client.ReceiveTimeout = 10;
                _isHost = true;
                _isConnected = true;
                _mySlotIndex = 0;

                // Initialize slots from LobbyConfig
                SyncSlotsFromLobbyConfig();
                
                // Set slot 0 as host
                _networkSlots[0].Type = SlotType.Human;
                _networkSlots[0].PlayerName = _playerName;
                _networkSlots[0].ClientIP = "HOST";

                Debug.Log($"[MultiplayerLobbyUI] Host started on UDP port {BROADCAST_PORT}");
            }
            catch (Exception e)
            {
                _error = $"Failed to start host: {e.Message}";
                Debug.LogError($"[MultiplayerLobbyUI] {_error}");
            }
        }

        private void StartClient()
        {
            Cleanup();

            try
            {
                // Client must listen on BROADCAST_PORT to receive game announcements
                // Use SO_REUSEADDR to allow multiple clients on same machine (for testing)
                _udpClient = new UdpClient();
                _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, BROADCAST_PORT));
                _udpClient.EnableBroadcast = true;
                _udpClient.Client.ReceiveTimeout = 10;
                _isHost = false;
                _isConnected = false;

                Debug.Log($"[MultiplayerLobbyUI] Client started, listening on UDP port {BROADCAST_PORT}");
            }
            catch (Exception e)
            {
                _error = $"Failed to start client: {e.Message}";
                Debug.LogError($"[MultiplayerLobbyUI] {_error}");
            }
        }

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
            catch (SocketException)
            {
                // Timeout - normal
            }
            catch (Exception e)
            {
                Debug.LogError($"[MultiplayerLobbyUI] Receive error: {e.Message}");
            }
        }

        private void ProcessMessage(string message, IPEndPoint sender)
        {
            string senderIP = sender.Address.ToString();

            // Debug: log all received messages
            Debug.Log($"[MultiplayerLobbyUI] Received from {senderIP}: {message.Substring(0, Math.Min(50, message.Length))}...");

            if (message.StartsWith(MSG_GAME))
            {
                // Game broadcast (client receives this)
                if (!_isHost)
                {
                    ProcessGameBroadcast(message, senderIP);
                }
            }
            else if (message.StartsWith(MSG_JOIN))
            {
                // Join request (host receives this)
                if (_isHost)
                {
                    ProcessJoinRequest(message, sender);
                }
            }
            else if (message.StartsWith(MSG_LOBBY))
            {
                // Lobby state update (client receives this)
                // Process even if not yet "connected" - we might be waiting for accept
                if (!_isHost)
                {
                    ProcessLobbyUpdate(message);
                }
            }
            else if (message.StartsWith(MSG_ACCEPT))
            {
                // Join accepted (client receives this)
                if (!_isHost)
                {
                    ProcessJoinAccepted(message, senderIP);
                }
            }
            else if (message.StartsWith(MSG_LEAVE))
            {
                // Player leaving (host receives this)
                if (_isHost)
                {
                    ProcessLeaveRequest(message, senderIP);
                }
            }
            else if (message.StartsWith(MSG_START))
            {
                // Game starting (client receives this)
                if (!_isHost)
                {
                    ProcessGameStart(message);
                }
            }
        }

        // ==================== HOST NETWORK LOGIC ====================

        private void BroadcastGameAvailability()
        {
            if (_udpClient == null) return;

            try
            {
                string message = $"{MSG_GAME}{_gameName}|{_playerName}|{_port}";
                byte[] data = Encoding.UTF8.GetBytes(message);
                IPEndPoint broadcast = new IPEndPoint(IPAddress.Broadcast, BROADCAST_PORT);
                _udpClient.Send(data, data.Length, broadcast);
            }
            catch (Exception e)
            {
                Debug.LogError($"[MultiplayerLobbyUI] Broadcast failed: {e.Message}");
            }
        }

        private void BroadcastLobbyState()
        {
            if (_udpClient == null) return;

            // Build lobby state message
            // Format: TWB_LOBBY|SlotCount|Type,Name,Difficulty|Type,Name,Difficulty|...
            var sb = new StringBuilder();
            sb.Append(MSG_LOBBY);
            sb.Append(LobbyConfig.ActiveSlotCount);

            for (int i = 0; i < LobbyConfig.ActiveSlotCount; i++)
            {
                var slot = _networkSlots[i];
                sb.Append($"|{(int)slot.Type},{slot.PlayerName},{(int)slot.AIDifficulty}");
            }

            string message = sb.ToString();
            byte[] data = Encoding.UTF8.GetBytes(message);

            // Send to all connected clients
            foreach (var client in _connectedClients.Values)
            {
                try
                {
                    _udpClient.Send(data, data.Length, client.EndPoint);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[MultiplayerLobbyUI] Failed to send to {client.EndPoint}: {e.Message}");
                }
            }
        }

        private void ProcessJoinRequest(string message, IPEndPoint sender)
        {
            // Format: TWB_JOIN|PlayerName
            string[] parts = message.Split('|');
            if (parts.Length < 2) return;

            string playerName = parts[1];
            string senderIP = sender.Address.ToString();

            // Check if this client already has a slot (reconnect or heartbeat)
            if (_connectedClients.TryGetValue(senderIP, out var existingClient))
            {
                // Update last seen time
                existingClient.LastSeen = DateTime.Now;
                existingClient.EndPoint = sender; // Update endpoint in case port changed
                
                // Re-send acceptance in case they missed it
                string acceptMsg = $"{MSG_ACCEPT}{existingClient.SlotIndex}";
                byte[] data = Encoding.UTF8.GetBytes(acceptMsg);
                _udpClient.Send(data, data.Length, sender);
                
                Debug.Log($"[MultiplayerLobbyUI] Re-confirmed {playerName} in slot {existingClient.SlotIndex}");
                return;
            }

            Debug.Log($"[MultiplayerLobbyUI] New join request from {playerName} at {senderIP}");

            // Find an empty or AI slot for this player
            int assignedSlot = -1;
            for (int i = 1; i < LobbyConfig.ActiveSlotCount; i++) // Start from 1, slot 0 is host
            {
                if (_networkSlots[i].Type == SlotType.Empty || _networkSlots[i].Type == SlotType.AI)
                {
                    // Check if not already taken by another client
                    if (string.IsNullOrEmpty(_networkSlots[i].ClientIP) || _networkSlots[i].Type == SlotType.AI)
                    {
                        assignedSlot = i;
                        break;
                    }
                }
            }

            if (assignedSlot >= 0)
            {
                // Assign player to slot
                _networkSlots[assignedSlot].Type = SlotType.Human;
                _networkSlots[assignedSlot].PlayerName = playerName;
                _networkSlots[assignedSlot].ClientIP = senderIP;

                // Track client
                _connectedClients[senderIP] = new ClientInfo
                {
                    PlayerName = playerName,
                    SlotIndex = assignedSlot,
                    EndPoint = sender,
                    LastSeen = DateTime.Now
                };

                // Send acceptance with slot assignment
                string acceptMsg = $"{MSG_ACCEPT}{assignedSlot}";
                byte[] data = Encoding.UTF8.GetBytes(acceptMsg);
                _udpClient.Send(data, data.Length, sender);

                Debug.Log($"[MultiplayerLobbyUI] Assigned {playerName} to slot {assignedSlot}");
                
                // Immediately broadcast updated lobby state
                BroadcastLobbyState();
            }
            else
            {
                Debug.Log($"[MultiplayerLobbyUI] No slots available for {playerName}");
                // TODO: Send rejection message
            }
        }

        private void ProcessLeaveRequest(string message, string senderIP)
        {
            // Format: TWB_LEAVE|SlotIndex
            string[] parts = message.Split('|');
            if (parts.Length < 2) return;

            if (int.TryParse(parts[1], out int slotIndex) && slotIndex >= 0 && slotIndex < 8)
            {
                // Verify this client owns this slot
                if (_networkSlots[slotIndex].ClientIP == senderIP)
                {
                    Debug.Log($"[MultiplayerLobbyUI] Player left slot {slotIndex}");
                    
                    _networkSlots[slotIndex].Type = SlotType.AI;
                    _networkSlots[slotIndex].PlayerName = "";
                    _networkSlots[slotIndex].ClientIP = "";
                    
                    _connectedClients.Remove(senderIP);
                    
                    BroadcastLobbyState();
                }
            }
        }

        private void CleanupDisconnectedClients()
        {
            var staleClients = _connectedClients
                .Where(kvp => (DateTime.Now - kvp.Value.LastSeen).TotalSeconds > 10)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var ip in staleClients)
            {
                var client = _connectedClients[ip];
                Debug.Log($"[MultiplayerLobbyUI] Client {client.PlayerName} timed out");
                
                // Free up their slot
                if (client.SlotIndex >= 0 && client.SlotIndex < 8)
                {
                    _networkSlots[client.SlotIndex].Type = SlotType.AI;
                    _networkSlots[client.SlotIndex].PlayerName = "";
                    _networkSlots[client.SlotIndex].ClientIP = "";
                }
                
                _connectedClients.Remove(ip);
            }
        }

        // ==================== CLIENT NETWORK LOGIC ====================

        private void ProcessGameBroadcast(string message, string senderIP)
        {
            // Format: TWB_GAME|GameName|HostName|Port
            string[] parts = message.Split('|');
            if (parts.Length < 4) return;

            string gameName = parts[1];
            string hostName = parts[2];
            if (!ushort.TryParse(parts[3], out ushort port)) return;

            // Update or add discovered game
            var existing = _discoveredGames.Find(g => g.IPAddress == senderIP);
            if (existing != null)
            {
                existing.GameName = gameName;
                existing.HostName = hostName;
                existing.Port = port;
                existing.LastSeen = DateTime.Now;
            }
            else
            {
                _discoveredGames.Add(new DiscoveredGame
                {
                    GameName = gameName,
                    HostName = hostName,
                    IPAddress = senderIP,
                    Port = port,
                    LastSeen = DateTime.Now
                });
                Debug.Log($"[MultiplayerLobbyUI] Discovered game: {gameName} at {senderIP}");
            }
        }

        private void SendJoinRequest(DiscoveredGame game)
        {
            if (_udpClient == null) return;

            _hostIP = game.IPAddress;
            _status = $"Joining {game.GameName}...";
            _state = LobbyState.Connecting;

            try
            {
                string message = $"{MSG_JOIN}{_playerName}";
                byte[] data = Encoding.UTF8.GetBytes(message);
                IPEndPoint hostEP = new IPEndPoint(IPAddress.Parse(game.IPAddress), BROADCAST_PORT);
                _udpClient.Send(data, data.Length, hostEP);
                
                Debug.Log($"[MultiplayerLobbyUI] Sent join request to {game.IPAddress}");
            }
            catch (Exception e)
            {
                _error = $"Failed to send join request: {e.Message}";
                Debug.LogError($"[MultiplayerLobbyUI] {_error}");
                _state = LobbyState.BrowseGames;
            }
        }

        private void ProcessJoinAccepted(string message, string senderIP)
        {
            // Format: TWB_ACCEPT|SlotIndex
            string[] parts = message.Split('|');
            if (parts.Length < 2) return;

            if (int.TryParse(parts[1], out int slotIndex))
            {
                _mySlotIndex = slotIndex;
                _hostIP = senderIP; // Store host IP for future communication
                _isConnected = true;
                _state = LobbyState.ClientLobby;
                _status = $"Connected! You are in slot {slotIndex + 1}";
                
                Debug.Log($"[MultiplayerLobbyUI] Join accepted from {senderIP}, assigned to slot {slotIndex}");
            }
        }

        private void ProcessLobbyUpdate(string message)
        {
            // Format: TWB_LOBBY|SlotCount|Type,Name,Difficulty|...
            string[] parts = message.Split('|');
            if (parts.Length < 2) return;

            if (!int.TryParse(parts[1], out int slotCount)) return;

            LobbyConfig.ActiveSlotCount = slotCount;

            for (int i = 0; i < slotCount && i + 2 < parts.Length; i++)
            {
                string[] slotParts = parts[i + 2].Split(',');
                if (slotParts.Length >= 3)
                {
                    if (int.TryParse(slotParts[0], out int type))
                        _networkSlots[i].Type = (SlotType)type;
                    
                    _networkSlots[i].PlayerName = slotParts[1];
                    
                    if (int.TryParse(slotParts[2], out int diff))
                        _networkSlots[i].AIDifficulty = (LobbyAIDifficulty)diff;
                }
            }
        }

        private void ProcessGameStart(string message)
        {
            Debug.Log("[MultiplayerLobbyUI] Game starting!");
            
            // Apply settings and load scene
            ApplySettingsAndStart();
        }

        private void SendToHost(string message)
        {
            if (_udpClient == null || string.IsNullOrEmpty(_hostIP)) return;

            try
            {
                byte[] data = Encoding.UTF8.GetBytes(message);
                IPEndPoint hostEP = new IPEndPoint(IPAddress.Parse(_hostIP), BROADCAST_PORT);
                _udpClient.Send(data, data.Length, hostEP);
            }
            catch (Exception e)
            {
                Debug.LogError($"[MultiplayerLobbyUI] Failed to send to host: {e.Message}");
            }
        }

        // ==================== GUI ====================

        void OnGUI()
        {
            InitStyles();
            _windowRect = GUI.Window(10002, _windowRect, DrawWindow, "Multiplayer Lobby");
        }

        private void InitStyles()
        {
            if (_stylesInit) return;

            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                richText = true
            };

            _slotStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(8, 8, 4, 4)
            };

            _factionLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };

            _gameListStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(8, 8, 4, 4)
            };

            _stylesInit = true;
        }

        private void DrawWindow(int windowId)
        {
            if (!string.IsNullOrEmpty(_error))
            {
                GUI.color = Color.red;
                GUILayout.Label(_error);
                GUI.color = Color.white;
            }

            switch (_state)
            {
                case LobbyState.MainChoice:
                    DrawMainChoice();
                    break;
                case LobbyState.HostSetup:
                    DrawHostSetup();
                    break;
                case LobbyState.HostLobby:
                    DrawHostLobby();
                    break;
                case LobbyState.BrowseGames:
                    DrawBrowseGames();
                    break;
                case LobbyState.ClientLobby:
                    DrawClientLobby();
                    break;
                case LobbyState.Connecting:
                    DrawConnecting();
                    break;
            }

            GUI.DragWindow(new Rect(0, 0, 10000, 25));
        }

        private void DrawMainChoice()
        {
            GUILayout.Label("<b>Multiplayer</b>", _headerStyle);
            GUILayout.Space(20);

            if (GUILayout.Button("Host Game", GUILayout.Height(50)))
            {
                _state = LobbyState.HostSetup;
            }

            GUILayout.Space(10);

            if (GUILayout.Button("Join Game", GUILayout.Height(50)))
            {
                StartClient();
                _state = LobbyState.BrowseGames;
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Back", GUILayout.Height(40)))
            {
                OnBackPressed?.Invoke();
            }

            GUILayout.Space(10);
        }

        private void DrawHostSetup()
        {
            GUILayout.Label("<b>Host Game Setup</b>", _headerStyle);
            GUILayout.Space(10);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Game Name:", GUILayout.Width(100));
            _gameName = GUILayout.TextField(_gameName);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Your Name:", GUILayout.Width(100));
            _playerName = GUILayout.TextField(_playerName);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Port:", GUILayout.Width(100));
            string portStr = GUILayout.TextField(_port.ToString(), GUILayout.Width(80));
            if (ushort.TryParse(portStr, out ushort p)) _port = p;
            GUILayout.EndHorizontal();

            GUILayout.Space(15);

            GUILayout.Label("<b>Number of Players</b>", _headerStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(" - ", GUILayout.Width(40)))
                SetPlayerCount(LobbyConfig.ActiveSlotCount - 1);
            GUILayout.Label(LobbyConfig.ActiveSlotCount.ToString(), GUILayout.Width(40));
            if (GUILayout.Button(" + ", GUILayout.Width(40)))
                SetPlayerCount(LobbyConfig.ActiveSlotCount + 1);
            GUILayout.EndHorizontal();

            GUILayout.Space(10);
            DrawMapOptions();

            GUILayout.FlexibleSpace();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Back", GUILayout.Height(36), GUILayout.Width(100)))
            {
                _state = LobbyState.MainChoice;
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Create Lobby", GUILayout.Height(36), GUILayout.Width(150)))
            {
                CreateHostLobby();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10);
        }

        private void DrawHostLobby()
        {
            GUILayout.Label($"<b>Hosting: {_gameName}</b>", _headerStyle);

            // Status
            GUI.color = Color.green;
            GUILayout.Label($"● Broadcasting on UDP port {BROADCAST_PORT}");
            GUILayout.Label($"● {_connectedClients.Count} client(s) connected");
            GUI.color = Color.white;

            GUILayout.Space(10);

            // Slots
            GUILayout.Label("<b>Player Slots</b>", _headerStyle);
            _slotsScrollPos = GUILayout.BeginScrollView(_slotsScrollPos, GUILayout.Height(200));
            for (int i = 0; i < LobbyConfig.ActiveSlotCount; i++)
            {
                DrawNetworkSlot(i, isHost: true);
            }
            GUILayout.EndScrollView();

            GUILayout.Space(10);
            GUILayout.Label($"<b>Map:</b> {_layout} | FoW: {(_fogOfWar ? "On" : "Off")} | Size: {_mapHalfSize * 2}");

            GUILayout.FlexibleSpace();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Cancel", GUILayout.Height(36), GUILayout.Width(100)))
            {
                Cleanup();
                _state = LobbyState.MainChoice;
            }
            GUILayout.FlexibleSpace();

            int humanCount = _networkSlots.Take(LobbyConfig.ActiveSlotCount).Count(s => s.Type == SlotType.Human);
            GUI.enabled = humanCount >= 1; // At least host
            if (GUILayout.Button("Start Game", GUILayout.Height(36), GUILayout.Width(150)))
            {
                StartMultiplayerGame();
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.Space(10);
        }

        private void DrawBrowseGames()
        {
            GUILayout.Label("<b>Available Games</b>", _headerStyle);

            if (_udpClient != null)
            {
                GUI.color = Color.green;
                GUILayout.Label($"● Listening for broadcasts");
                GUI.color = Color.white;
            }

            GUILayout.Space(10);

            if (_discoveredGames.Count == 0)
            {
                GUILayout.Label("Searching for games...");
            }
            else
            {
                _gamesScrollPos = GUILayout.BeginScrollView(_gamesScrollPos, GUILayout.Height(300));
                foreach (var game in _discoveredGames)
                {
                    GUILayout.BeginHorizontal(_gameListStyle);
                    GUILayout.Label($"{game.GameName} ({game.HostName})", GUILayout.Width(250));
                    GUILayout.Label(game.IPAddress, GUILayout.Width(120));
                    if (GUILayout.Button("Join", GUILayout.Width(80)))
                    {
                        SendJoinRequest(game);
                    }
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndScrollView();
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Back", GUILayout.Height(36), GUILayout.Width(100)))
            {
                Cleanup();
                _state = LobbyState.MainChoice;
            }

            GUILayout.Space(10);
        }

        private void DrawClientLobby()
        {
            GUILayout.Label("<b>Connected to Game</b>", _headerStyle);

            GUI.color = Color.green;
            GUILayout.Label($"● Connected to {_hostIP}");
            GUILayout.Label($"● You are Player {_mySlotIndex + 1}");
            GUI.color = Color.white;

            if (!string.IsNullOrEmpty(_status))
            {
                GUILayout.Label(_status);
            }

            GUILayout.Space(10);

            GUILayout.Label("<b>Player Slots</b>", _headerStyle);
            _slotsScrollPos = GUILayout.BeginScrollView(_slotsScrollPos, GUILayout.Height(200));
            for (int i = 0; i < LobbyConfig.ActiveSlotCount; i++)
            {
                DrawNetworkSlot(i, isHost: false);
            }
            GUILayout.EndScrollView();

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Leave", GUILayout.Height(36), GUILayout.Width(100)))
            {
                SendToHost($"{MSG_LEAVE}{_mySlotIndex}");
                Cleanup();
                _state = LobbyState.MainChoice;
            }

            GUILayout.Space(10);
        }

        private void DrawConnecting()
        {
            GUILayout.Label("<b>Connecting...</b>", _headerStyle);
            GUILayout.Space(20);
            GUILayout.Label(_status ?? "Please wait...");

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Cancel", GUILayout.Height(36), GUILayout.Width(100)))
            {
                Cleanup();
                _state = LobbyState.BrowseGames;
                StartClient();
            }
        }

        private void DrawNetworkSlot(int index, bool isHost)
        {
            var slot = _networkSlots[index];
            var faction = LobbyConfig.Slots[index].Faction;

            GUILayout.BeginHorizontal(_slotStyle);

            // Faction color
            Color oldColor = GUI.color;
            GUI.color = LobbyConfig.Slots[index].GetFactionColor();
            GUILayout.Label("■", GUILayout.Width(20));
            GUI.color = oldColor;

            // Faction name
            GUILayout.Label(faction.ToString(), _factionLabelStyle, GUILayout.Width(60));

            // Slot content
            if (slot.Type == SlotType.Human)
            {
                string label = string.IsNullOrEmpty(slot.PlayerName) ? "Player" : slot.PlayerName;
                if (index == 0) label += " (Host)";
                if (index == _mySlotIndex && !_isHost) label += " (You)";
                
                GUI.color = Color.cyan;
                GUILayout.Label(label);
                GUI.color = Color.white;
            }
            else if (slot.Type == SlotType.AI)
            {
                if (isHost)
                {
                    if (GUILayout.Button("AI", GUILayout.Width(50)))
                    {
                        slot.Type = SlotType.Empty;
                    }
                    string[] difficulties = { "Easy", "Normal", "Hard", "Expert" };
                    if (GUILayout.Button(difficulties[(int)slot.AIDifficulty], GUILayout.Width(70)))
                    {
                        slot.AIDifficulty = (LobbyAIDifficulty)(((int)slot.AIDifficulty + 1) % 4);
                    }
                }
                else
                {
                    GUILayout.Label($"AI ({slot.AIDifficulty})");
                }
            }
            else
            {
                if (isHost)
                {
                    if (GUILayout.Button("Empty", GUILayout.Width(50)))
                    {
                        slot.Type = SlotType.AI;
                    }
                    GUILayout.Label("(Open)");
                }
                else
                {
                    GUILayout.Label("Empty");
                }
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void DrawMapOptions()
        {
            GUILayout.Label("<b>Map Options</b>", _headerStyle);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Layout:", GUILayout.Width(60));
            if (GUILayout.Toggle(_layout == SpawnLayout.Circle, "Circle", "Button"))
                _layout = SpawnLayout.Circle;
            if (GUILayout.Toggle(_layout == SpawnLayout.TwoSides, "Two Sides", "Button"))
                _layout = SpawnLayout.TwoSides;
            GUILayout.EndHorizontal();

            _fogOfWar = GUILayout.Toggle(_fogOfWar, " Fog of War");

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Map Size: {_mapHalfSize * 2}", GUILayout.Width(120));
            if (GUILayout.Button("-", GUILayout.Width(25)))
                _mapHalfSize = Mathf.Max(64, _mapHalfSize - 16);
            if (GUILayout.Button("+", GUILayout.Width(25)))
                _mapHalfSize = Mathf.Min(512, _mapHalfSize + 16);
            GUILayout.EndHorizontal();
        }

        // ==================== ACTIONS ====================

        private void CreateHostLobby()
        {
            LobbyConfig.Slots[0].Type = SlotType.Human;
            LobbyConfig.Slots[0].PlayerName = _playerName;

            GameSettings.IsMultiplayer = true;
            GameSettings.NetworkRole = NetworkRole.Server;
            GameSettings.LocalPlayerFaction = Faction.Blue;

            StartHost();
            _state = LobbyState.HostLobby;
        }

        private void SyncSlotsFromLobbyConfig()
        {
            for (int i = 0; i < 8; i++)
            {
                _networkSlots[i].Type = LobbyConfig.Slots[i].Type;
                _networkSlots[i].PlayerName = LobbyConfig.Slots[i].PlayerName;
                _networkSlots[i].AIDifficulty = LobbyConfig.Slots[i].AIDifficulty;
                _networkSlots[i].ClientIP = "";
            }
        }

        private void StartMultiplayerGame()
        {
            // Notify all clients
            foreach (var client in _connectedClients.Values)
            {
                try
                {
                    string msg = $"{MSG_START}{_port}";
                    byte[] data = Encoding.UTF8.GetBytes(msg);
                    _udpClient.Send(data, data.Length, client.EndPoint);
                }
                catch { }
            }

            ApplySettingsAndStart();
        }

        private void ApplySettingsAndStart()
        {
            // Apply settings
            GameSettings.FogOfWarEnabled = _fogOfWar;
            GameSettings.MapHalfSize = _mapHalfSize;
            GameSettings.SpawnLayout = _layout;
            GameSettings.TwoSides = _twoSides;
            GameSettings.SpawnSeed = _spawnSeed;

            // Sync network slots back to LobbyConfig
            for (int i = 0; i < LobbyConfig.ActiveSlotCount; i++)
            {
                LobbyConfig.Slots[i].Type = _networkSlots[i].Type;
                LobbyConfig.Slots[i].PlayerName = _networkSlots[i].PlayerName;
                LobbyConfig.Slots[i].AIDifficulty = _networkSlots[i].AIDifficulty;
            }

            GameSettings.TotalPlayers = LobbyConfig.ActiveSlotCount;
            LobbyConfig.ApplyToGameSettings();

            // Load scene
            int sceneIndex = FindSceneIndexByName(GameSceneName);
            if (sceneIndex >= 0)
            {
                SceneManager.LoadScene(sceneIndex);
            }
            else
            {
                _error = $"Scene '{GameSceneName}' not found!";
            }
        }

        private void SetPlayerCount(int count)
        {
            int newCount = Mathf.Clamp(count, 2, 8);
            LobbyConfig.ActiveSlotCount = newCount;
            LobbyConfig.SetupMultiplayer(newCount);
            SyncSlotsFromLobbyConfig();
            
            // Keep host in slot 0
            _networkSlots[0].Type = SlotType.Human;
            _networkSlots[0].PlayerName = _playerName;
            _networkSlots[0].ClientIP = "HOST";
        }

        private static int FindSceneIndexByName(string name)
        {
            int count = SceneManager.sceneCountInBuildSettings;
            for (int i = 0; i < count; i++)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(i);
                string sceneName = Path.GetFileNameWithoutExtension(path);
                if (string.Equals(sceneName, name, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return -1;
        }
    }
}