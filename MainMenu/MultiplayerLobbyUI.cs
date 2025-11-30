// Assets/Scripts/MainMenu/MultiplayerLobbyUI.cs
using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.IO;
using System.Collections.Generic;

namespace TheWaningBorder.Menu
{
    /// <summary>
    /// Multiplayer lobby UI with player slots.
    /// Handles hosting, joining, and player slot management.
    /// Players join empty slots or replace AI players.
    /// </summary>
    public class MultiplayerLobbyUI : MonoBehaviour
    {
        public event Action OnBackPressed;

        private const string GameSceneName = "Game";

        // Lobby state machine
        private enum LobbyState
        {
            MainChoice,     // Choose Host or Join
            HostSetup,      // Configure host settings
            HostLobby,      // Waiting for players as host
            BrowseGames,    // Browsing available games
            ClientLobby     // In lobby as client
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

        // Network discovery (simulated for now - integrate with your LanNetworkDiscovery)
        private List<DiscoveredGame> _discoveredGames = new List<DiscoveredGame>();
        private float _discoveryTimer = 0f;

        // Error/status
        private string _error;
        private string _status;

        // Styles
        private GUIStyle _headerStyle;
        private GUIStyle _slotStyle;
        private GUIStyle _factionLabelStyle;
        private GUIStyle _gameListStyle;
        private bool _stylesInit = false;

        // Simple data class for discovered games (replace with your LanDiscoveredGame)
        private class DiscoveredGame
        {
            public string GameName;
            public string HostName;
            public string IPAddress;
            public ushort Port;
            public int CurrentPlayers;
            public int MaxPlayers;
        }

        void OnEnable()
        {
            _state = LobbyState.MainChoice;
            _error = null;
            _status = null;
            
            // Sync settings
            _layout = GameSettings.SpawnLayout;
            _twoSides = GameSettings.TwoSides;
            _spawnSeed = GameSettings.SpawnSeed;
            _fogOfWar = GameSettings.FogOfWarEnabled;
            _mapHalfSize = Mathf.Clamp(GameSettings.MapHalfSize, 64, 512);
        }

        void OnDisable()
        {
            // Stop any network discovery here
            _discoveredGames.Clear();
        }

        void Update()
        {
            if (_state == LobbyState.BrowseGames)
            {
                // Simulate game discovery (replace with real network discovery)
                _discoveryTimer += Time.deltaTime;
            }
        }

        void OnGUI()
        {
            InitStyles();
            _windowRect = GUI.Window(10003, _windowRect, DrawWindow, "Multiplayer");
            
            if (!string.IsNullOrEmpty(_error))
            {
                GUI.color = new Color(1, 0.5f, 0.5f, 1);
                GUI.Box(new Rect(_windowRect.x, _windowRect.yMax + 8, _windowRect.width, 50), _error);
                GUI.color = Color.white;
            }
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
                padding = new RectOffset(10, 10, 5, 5)
            };
            
            _stylesInit = true;
        }

        private void DrawWindow(int windowId)
        {
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
            }
            
            GUI.DragWindow(new Rect(0, 0, 10000, 25));
        }

        // ==================== MAIN CHOICE ====================
        private void DrawMainChoice()
        {
            GUILayout.Space(30);
            GUILayout.Label("<b>LAN Multiplayer</b>", _headerStyle);
            GUILayout.Space(30);
            
            if (GUILayout.Button("Host Game", GUILayout.Height(50)))
            {
                _state = LobbyState.HostSetup;
                LobbyConfig.SetupMultiplayer(LobbyConfig.ActiveSlotCount);
            }
            
            GUILayout.Space(15);
            
            if (GUILayout.Button("Join Game", GUILayout.Height(50)))
            {
                _state = LobbyState.BrowseGames;
                StartDiscovery();
            }
            
            GUILayout.FlexibleSpace();
            
            if (GUILayout.Button("Back", GUILayout.Height(40)))
            {
                OnBackPressed?.Invoke();
            }
            
            GUILayout.Space(10);
        }

        // ==================== HOST SETUP ====================
        private void DrawHostSetup()
        {
            GUILayout.Label("<b>Host Game Setup</b>", _headerStyle);
            GUILayout.Space(10);
            
            // Game name
            GUILayout.BeginHorizontal();
            GUILayout.Label("Game Name:", GUILayout.Width(100));
            _gameName = GUILayout.TextField(_gameName);
            GUILayout.EndHorizontal();
            
            // Player name
            GUILayout.BeginHorizontal();
            GUILayout.Label("Your Name:", GUILayout.Width(100));
            _playerName = GUILayout.TextField(_playerName);
            GUILayout.EndHorizontal();
            
            // Port
            GUILayout.BeginHorizontal();
            GUILayout.Label("Port:", GUILayout.Width(100));
            string portStr = GUILayout.TextField(_port.ToString(), GUILayout.Width(80));
            if (ushort.TryParse(portStr, out ushort p)) _port = p;
            GUILayout.EndHorizontal();
            
            GUILayout.Space(15);
            
            // Player count
            GUILayout.Label("<b>Number of Players</b>", _headerStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(" - ", GUILayout.Width(40)))
                SetPlayerCount(LobbyConfig.ActiveSlotCount - 1);
            GUILayout.Label(LobbyConfig.ActiveSlotCount.ToString(), GUILayout.Width(40));
            if (GUILayout.Button(" + ", GUILayout.Width(40)))
                SetPlayerCount(LobbyConfig.ActiveSlotCount + 1);
            GUILayout.EndHorizontal();
            
            GUILayout.Space(10);
            
            // Map options (collapsed)
            DrawMapOptions();
            
            GUILayout.FlexibleSpace();
            
            // Buttons
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

        // ==================== HOST LOBBY ====================
        private void DrawHostLobby()
        {
            GUILayout.Label($"<b>Hosting: {_gameName}</b>", _headerStyle);
            GUILayout.Label($"Port: {_port} | Waiting for players...");
            
            if (!string.IsNullOrEmpty(_status))
            {
                GUILayout.Label(_status);
            }
            
            GUILayout.Space(10);
            
            // Player slots
            GUILayout.Label("<b>Player Slots</b>", _headerStyle);
            
            _slotsScrollPos = GUILayout.BeginScrollView(_slotsScrollPos, GUILayout.Height(200));
            for (int i = 0; i < LobbyConfig.ActiveSlotCount; i++)
            {
                DrawMultiplayerSlot(i, isHost: true);
            }
            GUILayout.EndScrollView();
            
            GUILayout.Space(10);
            
            // Map settings (read-only display)
            GUILayout.Label("<b>Map Settings</b>", _headerStyle);
            GUILayout.Label($"Layout: {_layout} | FoW: {(_fogOfWar ? "On" : "Off")} | Size: {_mapHalfSize * 2}");
            
            GUILayout.FlexibleSpace();
            
            // Action buttons
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Cancel", GUILayout.Height(36), GUILayout.Width(100)))
            {
                StopHosting();
                _state = LobbyState.MainChoice;
            }
            GUILayout.FlexibleSpace();
            
            // Start game button (enabled when ready)
            bool canStart = CountActivePlayers() >= 2;
            GUI.enabled = canStart;
            if (GUILayout.Button("Start Game", GUILayout.Height(36), GUILayout.Width(150)))
            {
                StartMultiplayerGame();
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            
            GUILayout.Space(10);
        }

        // ==================== BROWSE GAMES ====================
        private void DrawBrowseGames()
        {
            GUILayout.Label("<b>Available Games</b>", _headerStyle);
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
                    GUILayout.Label($"{game.CurrentPlayers}/{game.MaxPlayers} players", GUILayout.Width(100));
                    if (GUILayout.Button("Join", GUILayout.Width(80)))
                    {
                        JoinGame(game);
                    }
                    GUILayout.EndHorizontal();
                }
                
                GUILayout.EndScrollView();
            }
            
            GUILayout.Space(10);
            
            // Player name
            GUILayout.BeginHorizontal();
            GUILayout.Label("Your Name:", GUILayout.Width(100));
            _playerName = GUILayout.TextField(_playerName);
            GUILayout.EndHorizontal();
            
            GUILayout.FlexibleSpace();
            
            // Refresh and Back buttons
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Back", GUILayout.Height(36), GUILayout.Width(100)))
            {
                StopDiscovery();
                _state = LobbyState.MainChoice;
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Refresh", GUILayout.Height(36), GUILayout.Width(100)))
            {
                RefreshDiscovery();
            }
            GUILayout.EndHorizontal();
            
            GUILayout.Space(10);
        }

        // ==================== CLIENT LOBBY ====================
        private void DrawClientLobby()
        {
            GUILayout.Label("<b>Joined Lobby</b>", _headerStyle);
            
            if (!string.IsNullOrEmpty(_status))
            {
                GUILayout.Label(_status);
            }
            
            GUILayout.Space(10);
            
            // Player slots (read-only for client)
            GUILayout.Label("<b>Player Slots</b>", _headerStyle);
            
            _slotsScrollPos = GUILayout.BeginScrollView(_slotsScrollPos, GUILayout.Height(200));
            for (int i = 0; i < LobbyConfig.ActiveSlotCount; i++)
            {
                DrawMultiplayerSlot(i, isHost: false);
            }
            GUILayout.EndScrollView();
            
            GUILayout.FlexibleSpace();
            
            // Leave button
            if (GUILayout.Button("Leave", GUILayout.Height(36), GUILayout.Width(100)))
            {
                LeaveLobby();
                _state = LobbyState.MainChoice;
            }
            
            GUILayout.Space(10);
        }

        // ==================== SLOT DRAWING ====================
        private void DrawMultiplayerSlot(int index, bool isHost)
        {
            var slot = LobbyConfig.Slots[index];
            
            GUILayout.BeginHorizontal(_slotStyle);
            
            // Faction color indicator
            Color oldColor = GUI.color;
            GUI.color = slot.GetFactionColor();
            GUILayout.Label("■", GUILayout.Width(20));
            GUI.color = oldColor;
            
            // Faction name
            GUILayout.Label(slot.GetFactionName(), _factionLabelStyle, GUILayout.Width(60));
            
            // Slot content
            if (slot.Type == SlotType.Human)
            {
                // Human player
                string label = string.IsNullOrEmpty(slot.PlayerName) ? "Player" : slot.PlayerName;
                GUILayout.Label(label, GUILayout.Width(150));
            }
            else if (slot.Type == SlotType.AI)
            {
                if (isHost)
                {
                    // Host can change AI to Empty or adjust difficulty
                    if (GUILayout.Button("AI", GUILayout.Width(50)))
                    {
                        // Cycle to Empty
                        slot.Type = SlotType.Empty;
                    }
                    
                    // Difficulty button
                    string[] diffs = { "Easy", "Normal", "Hard", "Expert" };
                    if (GUILayout.Button(diffs[(int)slot.AIDifficulty], GUILayout.Width(70)))
                    {
                        slot.AIDifficulty = (LobbyAIDifficulty)(((int)slot.AIDifficulty + 1) % 4);
                    }
                }
                else
                {
                    GUILayout.Label($"AI ({slot.AIDifficulty})", GUILayout.Width(120));
                }
            }
            else // Empty
            {
                if (isHost)
                {
                    if (GUILayout.Button("[Open Slot]", GUILayout.Width(100)))
                    {
                        // Convert to AI
                        slot.Type = SlotType.AI;
                        slot.AIDifficulty = LobbyAIDifficulty.Normal;
                    }
                }
                else
                {
                    GUILayout.Label("[Open Slot]", GUILayout.Width(100));
                }
            }
            
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        // ==================== MAP OPTIONS ====================
        private void DrawMapOptions()
        {
            GUILayout.Label("<b>Spawn Layout</b>", _headerStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(_layout == SpawnLayout.Circle, " Circle", "Button")) 
                _layout = SpawnLayout.Circle;
            if (GUILayout.Toggle(_layout == SpawnLayout.TwoSides, " Two Sides", "Button")) 
                _layout = SpawnLayout.TwoSides;
            if (GUILayout.Toggle(_layout == SpawnLayout.TwoEachSide8, " 2 Each Side", "Button")) 
                _layout = SpawnLayout.TwoEachSide8;
            GUILayout.EndHorizontal();

            if (_layout == SpawnLayout.TwoSides)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Toggle(_twoSides == TwoSidesPreset.LeftRight,  "LR", "Button")) 
                    _twoSides = TwoSidesPreset.LeftRight;
                if (GUILayout.Toggle(_twoSides == TwoSidesPreset.UpDown, "UD", "Button")) 
                    _twoSides = TwoSidesPreset.UpDown;
                if (GUILayout.Toggle(_twoSides == TwoSidesPreset.LeftUp, "LU", "Button")) 
                    _twoSides = TwoSidesPreset.LeftUp;
                if (GUILayout.Toggle(_twoSides == TwoSidesPreset.LeftDown, "LD", "Button")) 
                    _twoSides = TwoSidesPreset.LeftDown;
                if (GUILayout.Toggle(_twoSides == TwoSidesPreset.RightUp, "RU", "Button")) 
                    _twoSides = TwoSidesPreset.RightUp;
                if (GUILayout.Toggle(_twoSides == TwoSidesPreset.RightDown, "RD", "Button")) 
                    _twoSides = TwoSidesPreset.RightDown;
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(5);
            
            // FoW and Map Size
            GUILayout.BeginHorizontal();
            _fogOfWar = GUILayout.Toggle(_fogOfWar, " Fog of War");
            GUILayout.FlexibleSpace();
            GUILayout.Label($"Map: {_mapHalfSize * 2}");
            if (GUILayout.Button("-", GUILayout.Width(25))) 
                _mapHalfSize = Mathf.Max(64, _mapHalfSize - 16);
            if (GUILayout.Button("+", GUILayout.Width(25))) 
                _mapHalfSize = Mathf.Min(512, _mapHalfSize + 16);
            GUILayout.EndHorizontal();
        }

        // ==================== HELPER METHODS ====================
        private void SetPlayerCount(int count)
        {
            int newCount = Mathf.Clamp(count, 2, 8);
            LobbyConfig.ActiveSlotCount = newCount;
            LobbyConfig.SetupMultiplayer(newCount);
        }

        private int CountActivePlayers()
        {
            int count = 0;
            for (int i = 0; i < LobbyConfig.ActiveSlotCount; i++)
            {
                if (LobbyConfig.Slots[i].Type != SlotType.Empty)
                    count++;
            }
            return count;
        }

        // ==================== NETWORK STUBS ====================
        // Replace these with actual network implementation
        
        private void CreateHostLobby()
        {
            _status = $"Creating lobby on port {_port}...";
            
            // Set host as first human player
            LobbyConfig.Slots[0].Type = SlotType.Human;
            LobbyConfig.Slots[0].PlayerName = _playerName;
            
            GameSettings.IsMultiplayer = true;
            GameSettings.NetworkRole = NetworkRole.Server;
            GameSettings.LocalPlayerFaction = Faction.Blue;
            
            // TODO: Start actual network hosting here
            // Example: _networkDiscovery.StartBroadcasting(_gameName, _playerName, _port);
            
            _state = LobbyState.HostLobby;
            _status = "Waiting for players to join...";
        }

        private void StopHosting()
        {
            // TODO: Stop network hosting
            GameSettings.ResetToSinglePlayer();
        }

        private void StartDiscovery()
        {
            _discoveredGames.Clear();
            _discoveryTimer = 0f;
            // TODO: Start actual network discovery
            // Example: _networkDiscovery.StartDiscovery();
        }

        private void StopDiscovery()
        {
            // TODO: Stop network discovery
        }

        private void RefreshDiscovery()
        {
            _discoveredGames.Clear();
            _discoveryTimer = 0f;
            // TODO: Refresh discovery
        }

        private void JoinGame(DiscoveredGame game)
        {
            _status = $"Joining {game.GameName}...";
            
            GameSettings.IsMultiplayer = true;
            GameSettings.NetworkRole = NetworkRole.Client;
            
            // TODO: Actually connect to the game
            // The server will assign us a slot
            
            // For now, simulate joining
            _state = LobbyState.ClientLobby;
            _status = "Connected! Waiting for host to start...";
        }

        private void LeaveLobby()
        {
            // TODO: Disconnect from server
            GameSettings.ResetToSinglePlayer();
        }

        private void StartMultiplayerGame()
        {
            // Apply all settings
            GameSettings.FogOfWarEnabled = _fogOfWar;
            GameSettings.MapHalfSize = _mapHalfSize;
            GameSettings.SpawnLayout = _layout;
            GameSettings.TwoSides = _twoSides;
            GameSettings.SpawnSeed = _spawnSeed;
            GameSettings.TotalPlayers = CountActivePlayers();
            
            LobbyConfig.ApplyToGameSettings();
            
            // TODO: Send start signal to all clients
            
            // Load game scene
            int sceneIndex = FindSceneIndexByName(GameSceneName);
            if (sceneIndex < 0)
            {
                _error = $"Scene '{GameSceneName}' not found!";
                return;
            }
            
            SceneManager.LoadScene(sceneIndex);
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
