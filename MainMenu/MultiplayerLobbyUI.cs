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
    /// 
    /// Architecture:
    /// - Host listens on BROADCAST_PORT (47777) for broadcasts AND join requests
    /// - Client uses TWO sockets:
    ///   1. Broadcast listener on 47777 (with ReuseAddress) to discover games
    ///   2. Private socket on random port for receiving direct messages (ACCEPT, LOBBY, START)
    /// - Client includes its private port in JOIN message so host knows where to respond
    /// 
    /// Protocol:
    /// - TWB_GAME|GameName|HostName|GamePort         (Host → Broadcast, advertising game)
    /// - TWB_JOIN|PlayerName|ClientPort              (Client → Host, request to join)
    /// - TWB_ACCEPT|SlotIndex                        (Host → Client's private port)
    /// - TWB_LOBBY|SlotCount|Slot0|Slot1|...         (Host → Client's private port)
    /// - TWB_LEAVE|SlotIndex                         (Client → Host)
    /// - TWB_START|Port                              (Host → Client's private port)
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
        private const string MSG_DISCOVER = "TWB_DISCOVER|"; // Client asks for game info

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

        // Network state - Host uses single socket, Client uses two
        private UdpClient _hostSocket;           // Host: listens on 47777
        private UdpClient _broadcastListener;    // Client: listens on 47777 for game discovery
        private UdpClient _clientSocket;         // Client: random port for direct messages
        private int _clientPort;                 // Client's private port
        
        private bool _isHost;
        private bool _isConnected;
        private float _lastBroadcastTime;
        private float _lastLobbySyncTime;
        private string _hostIP;
        private int _mySlotIndex = -1;
        
        // Client tracking (host only) - now includes client's port
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
            public string IP;
            public int Port;  // Client's private port for direct messages
            public DateTime LastSeen;
        }

        public class NetworkSlot
        {
            public SlotType Type = SlotType.Empty;
            public string PlayerName = "";
            public LobbyAIDifficulty AIDifficulty = LobbyAIDifficulty.Normal;
            public string ClientKey = ""; // "IP:Port" for tracking
        }

        void Awake()
        {
            Application.runInBackground = true;
            
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
            // Send leave message if connected as client
            if (!_isHost && _isConnected && _mySlotIndex >= 0 && _clientSocket != null)
            {
                try
                {
                    string msg = $"{MSG_LEAVE}{_mySlotIndex}";
                    byte[] data = Encoding.UTF8.GetBytes(msg);
                    _clientSocket.Send(data, data.Length, _hostIP, BROADCAST_PORT);
                }
                catch { }
            }

            if (_hostSocket != null)
            {
                try { _hostSocket.Close(); } catch { }
                _hostSocket = null;
            }
            if (_broadcastListener != null)
            {
                try { _broadcastListener.Close(); } catch { }
                _broadcastListener = null;
            }
            if (_clientSocket != null)
            {
                try { _clientSocket.Close(); } catch { }
                _clientSocket = null;
            }
            
            _isHost = false;
            _isConnected = false;
            _connectedClients.Clear();
            _discoveredGames.Clear();
        }

        void Update()
        {
            if (_isHost)
            {
                // Host: receive on main socket
                ReceiveOnSocket(_hostSocket, true);

                // Broadcast game availability
                if (Time.time - _lastBroadcastTime >= BROADCAST_INTERVAL)
                {
                    BroadcastGameAvailability();
                    _lastBroadcastTime = Time.time;
                }

                // Send lobby state to all clients
                if (Time.time - _lastLobbySyncTime >= LOBBY_SYNC_INTERVAL)
                {
                    BroadcastLobbyState();
                    _lastLobbySyncTime = Time.time;
                }

                CleanupDisconnectedClients();
            }
            else if (_clientSocket != null)
            {
                // Client: receive all messages on single socket
                ReceiveOnSocket(_clientSocket, false);

                // Discovery mode: send discovery broadcasts to find games
                if (_state == LobbyState.BrowseGames)
                {
                    if (Time.time - _lastBroadcastTime >= BROADCAST_INTERVAL)
                    {
                        SendDiscoveryBroadcast();
                        _lastBroadcastTime = Time.time;
                    }
                    
                    // Cleanup stale games
                    _discoveredGames.RemoveAll(g => 
                        (DateTime.Now - g.LastSeen).TotalSeconds > DISCOVERY_TIMEOUT);
                }
                // Connected/Connecting: send heartbeat
                else if (_isConnected || _state == LobbyState.Connecting)
                {
                    if (Time.time - _lastBroadcastTime >= BROADCAST_INTERVAL)
                    {
                        SendHeartbeat();
                        _lastBroadcastTime = Time.time;
                    }
                }
            }
        }

        private void SendDiscoveryBroadcast()
        {
            if (_clientSocket == null) return;

            try
            {
                // Send discovery request to broadcast address
                string message = $"{MSG_DISCOVER}{_clientPort}";
                byte[] data = Encoding.UTF8.GetBytes(message);
                _clientSocket.Send(data, data.Length, new IPEndPoint(IPAddress.Broadcast, BROADCAST_PORT));
            }
            catch (Exception e)
            {
                Debug.LogError($"[MultiplayerLobbyUI] Discovery broadcast failed: {e.Message}");
            }
        }

        // ==================== NETWORK - SETUP ====================

        private void StartHost()
        {
            Cleanup();

            try
            {
                _hostSocket = new UdpClient(BROADCAST_PORT);
                _hostSocket.EnableBroadcast = true;
                _hostSocket.Client.ReceiveTimeout = 10;
                
                _isHost = true;
                _isConnected = true;
                _mySlotIndex = 0;

                SyncSlotsFromLobbyConfig();
                
                _networkSlots[0].Type = SlotType.Human;
                _networkSlots[0].PlayerName = _playerName;
                _networkSlots[0].ClientKey = "HOST";

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
                // Client only needs ONE socket on a random port
                // It can still receive broadcasts (UDP broadcasts reach all sockets)
                // AND receive direct messages from host
                _clientSocket = new UdpClient(0); // 0 = any available port
                _clientSocket.EnableBroadcast = true;
                _clientSocket.Client.ReceiveTimeout = 10;
                _clientPort = ((IPEndPoint)_clientSocket.Client.LocalEndPoint).Port;

                _isHost = false;
                _isConnected = false;

                Debug.Log($"[MultiplayerLobbyUI] Client started on port {_clientPort}");
            }
            catch (Exception e)
            {
                _error = $"Failed to start client: {e.Message}";
                Debug.LogError($"[MultiplayerLobbyUI] {_error}");
            }
        }

        // ==================== NETWORK - RECEIVE ====================

        private void ReceiveOnSocket(UdpClient socket, bool isHostSocket)
        {
            if (socket == null) return;

            try
            {
                while (socket.Available > 0)
                {
                    IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
                    byte[] data = socket.Receive(ref remoteEP);
                    string message = Encoding.UTF8.GetString(data);
                    
                    ProcessMessage(message, remoteEP, isHostSocket);
                }
            }
            catch (SocketException) { }
            catch (Exception e)
            {
                Debug.LogError($"[MultiplayerLobbyUI] Receive error: {e.Message}");
            }
        }

        private void ProcessMessage(string message, IPEndPoint sender, bool isHostSocket)
        {
            string senderIP = sender.Address.ToString();
            int senderPort = sender.Port;

            Debug.Log($"[MultiplayerLobbyUI] Received from {senderIP}:{senderPort}: {message.Substring(0, Math.Min(50, message.Length))}...");

            if (message.StartsWith(MSG_GAME))
            {
                if (!_isHost)
                    ProcessGameBroadcast(message, senderIP);
            }
            else if (message.StartsWith(MSG_DISCOVER))
            {
                // Host responds to discovery requests
                if (_isHost)
                    ProcessDiscoveryRequest(message, senderIP);
            }
            else if (message.StartsWith(MSG_JOIN))
            {
                if (_isHost)
                    ProcessJoinRequest(message, senderIP, senderPort);
            }
            else if (message.StartsWith(MSG_LOBBY))
            {
                if (!_isHost)
                    ProcessLobbyUpdate(message);
            }
            else if (message.StartsWith(MSG_ACCEPT))
            {
                if (!_isHost)
                    ProcessJoinAccepted(message, senderIP);
            }
            else if (message.StartsWith(MSG_LEAVE))
            {
                if (_isHost)
                    ProcessLeaveRequest(message, senderIP, senderPort);
            }
            else if (message.StartsWith(MSG_START))
            {
                if (!_isHost)
                    ProcessGameStart(message);
            }
        }

        private void ProcessDiscoveryRequest(string message, string senderIP)
        {
            // Format: TWB_DISCOVER|ClientPort
            string[] parts = message.Split('|');
            if (parts.Length < 2) return;

            if (!int.TryParse(parts[1], out int clientPort)) return;

            // Send game info directly to the client's port
            try
            {
                string response = $"{MSG_GAME}{_gameName}|{_playerName}|{_port}";
                byte[] data = Encoding.UTF8.GetBytes(response);
                _hostSocket.Send(data, data.Length, senderIP, clientPort);
                
                Debug.Log($"[MultiplayerLobbyUI] Sent game info to {senderIP}:{clientPort}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[MultiplayerLobbyUI] Failed to respond to discovery: {e.Message}");
            }
        }

        // ==================== HOST LOGIC ====================

        private void BroadcastGameAvailability()
        {
            if (_hostSocket == null) return;

            try
            {
                string message = $"{MSG_GAME}{_gameName}|{_playerName}|{_port}";
                byte[] data = Encoding.UTF8.GetBytes(message);
                _hostSocket.Send(data, data.Length, new IPEndPoint(IPAddress.Broadcast, BROADCAST_PORT));
            }
            catch (Exception e)
            {
                Debug.LogError($"[MultiplayerLobbyUI] Broadcast failed: {e.Message}");
            }
        }

        private void BroadcastLobbyState()
        {
            if (_hostSocket == null) return;

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

            // Send to each client's PRIVATE port
            foreach (var client in _connectedClients.Values)
            {
                try
                {
                    _hostSocket.Send(data, data.Length, client.IP, client.Port);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[MultiplayerLobbyUI] Failed to send lobby to {client.IP}:{client.Port}: {e.Message}");
                }
            }
        }

        private void ProcessJoinRequest(string message, string senderIP, int senderPort)
        {
            // Format: TWB_JOIN|PlayerName|ClientPort
            string[] parts = message.Split('|');
            if (parts.Length < 3) return;

            string playerName = parts[1];
            if (!int.TryParse(parts[2], out int clientPrivatePort)) return;

            string clientKey = $"{senderIP}:{clientPrivatePort}";

            Debug.Log($"[MultiplayerLobbyUI] JOIN from {playerName} at {senderIP}, private port {clientPrivatePort}");

            // Check if already connected
            if (_connectedClients.TryGetValue(clientKey, out var existingClient))
            {
                existingClient.LastSeen = DateTime.Now;
                
                // Re-send accept
                string acceptMsg = $"{MSG_ACCEPT}{existingClient.SlotIndex}";
                byte[] acceptData = Encoding.UTF8.GetBytes(acceptMsg);
                _hostSocket.Send(acceptData, acceptData.Length, senderIP, clientPrivatePort);
                
                Debug.Log($"[MultiplayerLobbyUI] Re-confirmed {playerName} in slot {existingClient.SlotIndex}");
                return;
            }

            // Find available slot
            int assignedSlot = -1;
            for (int i = 1; i < LobbyConfig.ActiveSlotCount; i++)
            {
                if (_networkSlots[i].Type == SlotType.Empty || _networkSlots[i].Type == SlotType.AI)
                {
                    if (string.IsNullOrEmpty(_networkSlots[i].ClientKey) || _networkSlots[i].Type == SlotType.AI)
                    {
                        assignedSlot = i;
                        break;
                    }
                }
            }

            if (assignedSlot >= 0)
            {
                _networkSlots[assignedSlot].Type = SlotType.Human;
                _networkSlots[assignedSlot].PlayerName = playerName;
                _networkSlots[assignedSlot].ClientKey = clientKey;

                _connectedClients[clientKey] = new ClientInfo
                {
                    PlayerName = playerName,
                    SlotIndex = assignedSlot,
                    IP = senderIP,
                    Port = clientPrivatePort,
                    LastSeen = DateTime.Now
                };

                // Send accept to client's PRIVATE port
                string acceptMsg = $"{MSG_ACCEPT}{assignedSlot}";
                byte[] data = Encoding.UTF8.GetBytes(acceptMsg);
                _hostSocket.Send(data, data.Length, senderIP, clientPrivatePort);

                Debug.Log($"[MultiplayerLobbyUI] Assigned {playerName} to slot {assignedSlot}, sending to port {clientPrivatePort}");
                
                BroadcastLobbyState();
            }
            else
            {
                Debug.Log($"[MultiplayerLobbyUI] No slots available for {playerName}");
            }
        }

        private void ProcessLeaveRequest(string message, string senderIP, int senderPort)
        {
            string[] parts = message.Split('|');
            if (parts.Length < 2) return;

            if (int.TryParse(parts[1], out int slotIndex) && slotIndex >= 0 && slotIndex < 8)
            {
                // Find and remove client
                var clientToRemove = _connectedClients.FirstOrDefault(c => c.Value.SlotIndex == slotIndex);
                if (!string.IsNullOrEmpty(clientToRemove.Key))
                {
                    Debug.Log($"[MultiplayerLobbyUI] Player left slot {slotIndex}");
                    
                    _networkSlots[slotIndex].Type = SlotType.AI;
                    _networkSlots[slotIndex].PlayerName = "";
                    _networkSlots[slotIndex].ClientKey = "";
                    
                    _connectedClients.Remove(clientToRemove.Key);
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

            foreach (var key in staleClients)
            {
                var client = _connectedClients[key];
                Debug.Log($"[MultiplayerLobbyUI] Client {client.PlayerName} timed out");
                
                if (client.SlotIndex >= 0 && client.SlotIndex < 8)
                {
                    _networkSlots[client.SlotIndex].Type = SlotType.AI;
                    _networkSlots[client.SlotIndex].PlayerName = "";
                    _networkSlots[client.SlotIndex].ClientKey = "";
                }
                
                _connectedClients.Remove(key);
            }
        }

        // ==================== CLIENT LOGIC ====================

        private void ProcessGameBroadcast(string message, string senderIP)
        {
            string[] parts = message.Split('|');
            if (parts.Length < 4) return;

            string gameName = parts[1];
            string hostName = parts[2];
            if (!ushort.TryParse(parts[3], out ushort port)) return;

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
            if (_clientSocket == null) return;

            _hostIP = game.IPAddress;
            _status = $"Joining {game.GameName}...";
            _state = LobbyState.Connecting;

            try
            {
                // Include our private port so host knows where to send responses
                string message = $"{MSG_JOIN}{_playerName}|{_clientPort}";
                byte[] data = Encoding.UTF8.GetBytes(message);
                _clientSocket.Send(data, data.Length, game.IPAddress, BROADCAST_PORT);
                
                Debug.Log($"[MultiplayerLobbyUI] Sent JOIN to {game.IPAddress}:{BROADCAST_PORT}, my private port is {_clientPort}");
            }
            catch (Exception e)
            {
                _error = $"Failed to send join request: {e.Message}";
                Debug.LogError($"[MultiplayerLobbyUI] {_error}");
                _state = LobbyState.BrowseGames;
            }
        }

        private void SendHeartbeat()
        {
            if (_clientSocket == null || string.IsNullOrEmpty(_hostIP)) return;

            try
            {
                string message = $"{MSG_JOIN}{_playerName}|{_clientPort}";
                byte[] data = Encoding.UTF8.GetBytes(message);
                _clientSocket.Send(data, data.Length, _hostIP, BROADCAST_PORT);
            }
            catch (Exception e)
            {
                Debug.LogError($"[MultiplayerLobbyUI] Heartbeat failed: {e.Message}");
            }
        }

        private void ProcessJoinAccepted(string message, string senderIP)
        {
            string[] parts = message.Split('|');
            if (parts.Length < 2) return;

            if (int.TryParse(parts[1], out int slotIndex))
            {
                _mySlotIndex = slotIndex;
                _hostIP = senderIP;
                _isConnected = true;
                _state = LobbyState.ClientLobby;
                _status = $"Connected! You are in slot {slotIndex + 1}";
                
                // Set multiplayer settings for client
                GameSettings.IsMultiplayer = true;
                GameSettings.NetworkRole = NetworkRole.Client;
                
                Debug.Log($"[MultiplayerLobbyUI] Join accepted, assigned to slot {slotIndex}");
            }
        }

        private void ProcessLobbyUpdate(string message)
        {
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
            // Format: TWB_START|Port|FactionIndex
            string[] parts = message.Split('|');
            
            if (parts.Length >= 3 && int.TryParse(parts[2], out int factionIndex))
            {
                GameSettings.LocalPlayerFaction = (Faction)factionIndex;
                Debug.Log($"[MultiplayerLobbyUI] Game starting! My faction: {GameSettings.LocalPlayerFaction}");
            }
            else
            {
                // Fallback: use slot index to determine faction
                GameSettings.LocalPlayerFaction = LobbyConfig.Slots[_mySlotIndex].Faction;
                Debug.Log($"[MultiplayerLobbyUI] Game starting! Faction from slot {_mySlotIndex}: {GameSettings.LocalPlayerFaction}");
            }
            
            ApplySettingsAndStart();
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
                case LobbyState.MainChoice: DrawMainChoice(); break;
                case LobbyState.HostSetup: DrawHostSetup(); break;
                case LobbyState.HostLobby: DrawHostLobby(); break;
                case LobbyState.BrowseGames: DrawBrowseGames(); break;
                case LobbyState.ClientLobby: DrawClientLobby(); break;
                case LobbyState.Connecting: DrawConnecting(); break;
            }

            GUI.DragWindow(new Rect(0, 0, 10000, 25));
        }

        private void DrawMainChoice()
        {
            GUILayout.Label("<b>Multiplayer</b>", _headerStyle);
            GUILayout.Space(20);

            if (GUILayout.Button("Host Game", GUILayout.Height(50)))
                _state = LobbyState.HostSetup;

            GUILayout.Space(10);

            if (GUILayout.Button("Join Game", GUILayout.Height(50)))
            {
                StartClient();
                _state = LobbyState.BrowseGames;
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Back", GUILayout.Height(40)))
                OnBackPressed?.Invoke();

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
                _state = LobbyState.MainChoice;
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Create Lobby", GUILayout.Height(36), GUILayout.Width(150)))
                CreateHostLobby();
            GUILayout.EndHorizontal();

            GUILayout.Space(10);
        }

        private void DrawHostLobby()
        {
            GUILayout.Label($"<b>Hosting: {_gameName}</b>", _headerStyle);

            GUI.color = Color.green;
            GUILayout.Label($"● Broadcasting on UDP port {BROADCAST_PORT}");
            GUILayout.Label($"● {_connectedClients.Count} client(s) connected");
            GUI.color = Color.white;

            GUILayout.Space(10);

            GUILayout.Label("<b>Player Slots</b>", _headerStyle);
            _slotsScrollPos = GUILayout.BeginScrollView(_slotsScrollPos, GUILayout.Height(200));
            for (int i = 0; i < LobbyConfig.ActiveSlotCount; i++)
                DrawNetworkSlot(i, isHost: true);
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
            GUI.enabled = humanCount >= 1;
            if (GUILayout.Button("Start Game", GUILayout.Height(36), GUILayout.Width(150)))
                StartMultiplayerGame();
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.Space(10);
        }

        private void DrawBrowseGames()
        {
            GUILayout.Label("<b>Available Games</b>", _headerStyle);

            GUI.color = Color.green;
            GUILayout.Label($"● Listening for broadcasts on port {BROADCAST_PORT}");
            GUILayout.Label($"● Private port: {_clientPort}");
            GUI.color = Color.white;

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
                        SendJoinRequest(game);
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
                GUILayout.Label(_status);

            GUILayout.Space(10);

            GUILayout.Label("<b>Player Slots</b>", _headerStyle);
            _slotsScrollPos = GUILayout.BeginScrollView(_slotsScrollPos, GUILayout.Height(200));
            for (int i = 0; i < LobbyConfig.ActiveSlotCount; i++)
                DrawNetworkSlot(i, isHost: false);
            GUILayout.EndScrollView();

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Leave", GUILayout.Height(36), GUILayout.Width(100)))
            {
                if (_clientSocket != null && !string.IsNullOrEmpty(_hostIP))
                {
                    try
                    {
                        string msg = $"{MSG_LEAVE}{_mySlotIndex}";
                        byte[] data = Encoding.UTF8.GetBytes(msg);
                        _clientSocket.Send(data, data.Length, _hostIP, BROADCAST_PORT);
                    }
                    catch { }
                }
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
                StartClient();
                _state = LobbyState.BrowseGames;
            }
        }

        private void DrawNetworkSlot(int index, bool isHost)
        {
            var slot = _networkSlots[index];
            var faction = LobbyConfig.Slots[index].Faction;

            GUILayout.BeginHorizontal(_slotStyle);

            Color oldColor = GUI.color;
            GUI.color = LobbyConfig.Slots[index].GetFactionColor();
            GUILayout.Label("■", GUILayout.Width(20));
            GUI.color = oldColor;

            GUILayout.Label(faction.ToString(), _factionLabelStyle, GUILayout.Width(60));

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
                        slot.Type = SlotType.Empty;
                    string[] difficulties = { "Easy", "Normal", "Hard", "Expert" };
                    if (GUILayout.Button(difficulties[(int)slot.AIDifficulty], GUILayout.Width(70)))
                        slot.AIDifficulty = (LobbyAIDifficulty)(((int)slot.AIDifficulty + 1) % 4);
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
                        slot.Type = SlotType.AI;
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
                _networkSlots[i].ClientKey = "";
            }
        }

        private void StartMultiplayerGame()
        {
            // Notify all clients with their assigned faction
            foreach (var client in _connectedClients.Values)
            {
                try
                {
                    // Get the faction for this client's slot
                    Faction clientFaction = LobbyConfig.Slots[client.SlotIndex].Faction;
                    string msg = $"{MSG_START}{_port}|{(int)clientFaction}";
                    byte[] data = Encoding.UTF8.GetBytes(msg);
                    _hostSocket.Send(data, data.Length, client.IP, client.Port);
                    
                    Debug.Log($"[MultiplayerLobbyUI] Sent START to {client.PlayerName} with faction {clientFaction}");
                }
                catch { }
            }

            // Host is always slot 0 = Blue
            GameSettings.LocalPlayerFaction = LobbyConfig.Slots[0].Faction;
            ApplySettingsAndStart();
        }

        private void ApplySettingsAndStart()
        {
            GameSettings.FogOfWarEnabled = _fogOfWar;
            GameSettings.MapHalfSize = _mapHalfSize;
            GameSettings.SpawnLayout = _layout;
            GameSettings.TwoSides = _twoSides;
            GameSettings.SpawnSeed = _spawnSeed;

            for (int i = 0; i < LobbyConfig.ActiveSlotCount; i++)
            {
                LobbyConfig.Slots[i].Type = _networkSlots[i].Type;
                LobbyConfig.Slots[i].PlayerName = _networkSlots[i].PlayerName;
                LobbyConfig.Slots[i].AIDifficulty = _networkSlots[i].AIDifficulty;
            }

            GameSettings.TotalPlayers = LobbyConfig.ActiveSlotCount;
            
            // Apply faction mappings for multiplayer
            LobbyConfig.ApplyToGameSettings();
            
            Debug.Log($"[MultiplayerLobbyUI] Starting game - LocalPlayerFaction: {GameSettings.LocalPlayerFaction}, IsMultiplayer: {GameSettings.IsMultiplayer}");

            int sceneIndex = FindSceneIndexByName(GameSceneName);
            if (sceneIndex >= 0)
                SceneManager.LoadScene(sceneIndex);
            else
                _error = $"Scene '{GameSceneName}' not found!";
        }

        private void SetPlayerCount(int count)
        {
            int newCount = Mathf.Clamp(count, 2, 8);
            LobbyConfig.ActiveSlotCount = newCount;
            LobbyConfig.SetupMultiplayer(newCount);
            SyncSlotsFromLobbyConfig();
            
            _networkSlots[0].Type = SlotType.Human;
            _networkSlots[0].PlayerName = _playerName;
            _networkSlots[0].ClientKey = "HOST";
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