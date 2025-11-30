using UnityEngine;
using System.Collections.Generic;
using Unity.NetCode;
using Unity.Entities;
using TheWaningBorder.Multiplayer;

namespace TheWaningBorder.Multiplayer
{
    /// <summary>
    /// Minimal lobby UI for Netcode for Entities.
    /// Handles host/join and triggers Netcode world creation.
    /// </summary>
    public class MultiplayerLobby : MonoBehaviour
    {
        private enum LobbyState { MainMenu, HostSetup, BrowserGames, InLobby }
        private LobbyState _currentState = LobbyState.MainMenu;

        private LanNetworkDiscovery _networkDiscovery;
        private string _gameName = "My Game";
        private string _playerName = System.Environment.MachineName;
        private ushort _port = 7979;

        private List<LanDiscoveredGame> _discoveredGames = new List<LanDiscoveredGame>();
        private Rect _windowRect = new Rect(Screen.width / 2 - 250, Screen.height / 2 - 200, 500, 400);

        void Awake()
        {
            // Ensure the discovery manager exists
            if (MultiplayerDiscoveryManager.Instance == null)
            {
                var mgrObj = new GameObject("MultiplayerDiscoveryManager");
                mgrObj.AddComponent<MultiplayerDiscoveryManager>();
                Debug.Log("[MultiplayerLobby] Created MultiplayerDiscoveryManager instance");
            }
            // Use persistent discovery manager
            _networkDiscovery = MultiplayerDiscoveryManager.Instance.GetDiscovery();
            _networkDiscovery.OnGameDiscovered += OnGameDiscovered;
            _networkDiscovery.OnGameLost += OnGameLost;
            Debug.Log("[MultiplayerLobby] Awake completed, discovery ready");
        }

        void OnDestroy()
        {
            // Unsubscribe but keep manager alive
            if (_networkDiscovery != null)
            {
                _networkDiscovery.OnGameDiscovered -= OnGameDiscovered;
                _networkDiscovery.OnGameLost -= OnGameLost;
            }
        }

        void OnGUI()
        {
            _windowRect = GUI.Window(2000, _windowRect, DrawWindow, "Multiplayer Lobby");
        }

        private void DrawWindow(int windowId)
        {
            GUILayout.BeginVertical();
            switch (_currentState)
            {
                case LobbyState.MainMenu: DrawMainMenu(); break;
                case LobbyState.HostSetup: DrawHostSetup(); break;
                case LobbyState.BrowserGames: DrawGameBrowser(); break;
            }
            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        private void DrawMainMenu()
        {
            GUILayout.Label("LAN Multiplayer", GUILayout.Height(30));
            GUILayout.Space(20);

            if (GUILayout.Button("Host Game", GUILayout.Height(50)))
            {
                _currentState = LobbyState.HostSetup;
            }

            if (GUILayout.Button("Join Game", GUILayout.Height(50)))
            {
                _currentState = LobbyState.BrowserGames;
                MultiplayerDiscoveryManager.Instance.StartDiscovery();
            }

            if (GUILayout.Button("Back", GUILayout.Height(40)))
            {
                Destroy(gameObject);
            }
        }

        private void DrawHostSetup()
        {
            GUILayout.Label("Host Game Setup");
            GUILayout.Space(10);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Game Name:", GUILayout.Width(100));
            _gameName = GUILayout.TextField(_gameName);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Player Name:", GUILayout.Width(100));
            _playerName = GUILayout.TextField(_playerName);
            GUILayout.EndHorizontal();

            GUILayout.Space(20);

            if (GUILayout.Button("Start Hosting", GUILayout.Height(40)))
            {
                HostGame();
            }

            if (GUILayout.Button("Back", GUILayout.Height(30)))
            {
                _currentState = LobbyState.MainMenu;
            }
        }

        private void DrawGameBrowser()
        {
            GUILayout.Label("Available Games");
            GUILayout.Space(10);

            if (_discoveredGames.Count == 0)
            {
                GUILayout.Label("Searching...");
            }
            else
            {
                foreach (var game in _discoveredGames)
                {
                    GUILayout.BeginHorizontal(GUI.skin.box);
                    GUILayout.Label($"{game.GameInfo.GameName} ({game.GameInfo.HostName})");
                    if (GUILayout.Button("Join", GUILayout.Width(80)))
                    {
                        JoinGame(game);
                    }
                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.Space(10);
            if (GUILayout.Button("Back", GUILayout.Height(30)))
            {
                MultiplayerDiscoveryManager.Instance.StopAll();
                _currentState = LobbyState.MainMenu;
            }
        }

        private void HostGame()
        {
            Debug.Log("[MultiplayerLobby] Starting host");
            GameSettings.IsMultiplayer = true;
            GameSettings.NetworkRole = NetworkRole.Server;
            GameSettings.LocalPlayerFaction = Faction.Blue;
            MultiplayerDiscoveryManager.Instance.StartBroadcasting(_gameName, _playerName, _port);
            Debug.Log($"[MultiplayerLobby] Broadcasting on UDP port {_port}");
            var server = ClientServerBootstrap.CreateServerWorld("ServerWorld");
            var endpoint = Unity.Networking.Transport.NetworkEndpoint.AnyIpv4.WithPort(_port);
            var listenRequest = server.EntityManager.CreateEntity(typeof(NetworkStreamRequestListen));
            server.EntityManager.SetComponentData(listenRequest, new NetworkStreamRequestListen { Endpoint = endpoint });
            Debug.Log("[MultiplayerLobby] Server started");
            UnityEngine.SceneManagement.SceneManager.LoadScene("Game");
        }

        private void JoinGame(LanDiscoveredGame game)
        {
            Debug.Log($"[MultiplayerLobby] Joining game at {game.IPAddress}");
            MultiplayerDiscoveryManager.Instance.StopAll();
            GameSettings.IsMultiplayer = true;
            GameSettings.NetworkRole = NetworkRole.Client;
            var client = ClientServerBootstrap.CreateClientWorld("ClientWorld");
            var endpoint = Unity.Networking.Transport.NetworkEndpoint.Parse(game.IPAddress, game.GameInfo.GamePort);
            var connectRequest = client.EntityManager.CreateEntity(typeof(NetworkStreamRequestConnect));
            client.EntityManager.SetComponentData(connectRequest, new NetworkStreamRequestConnect { Endpoint = endpoint });
            Debug.Log("[MultiplayerLobby] Client connected");
            UnityEngine.SceneManagement.SceneManager.LoadScene("Game");
        }

        private void OnGameDiscovered(LanDiscoveredGame game)
        {
            if (!_discoveredGames.Exists(g => g.IPAddress == game.IPAddress))
            {
                _discoveredGames.Add(game);
            }
        }

        private void OnGameLost(string ipAddress)
        {
            _discoveredGames.RemoveAll(g => g.IPAddress == ipAddress);
        }
    }
}