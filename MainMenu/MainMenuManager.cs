// Assets/Scripts/MainMenu/MainMenuManager.cs
using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO;
using TheWaningBorder.Menu;

namespace TheWaningBorder.Menu
{
    /// <summary>
    /// Central manager for the main menu system.
    /// Handles navigation between: Main Menu, Skirmish Lobby, Multiplayer Lobby.
    /// </summary>
    public class MainMenuManager : MonoBehaviour
    {
        public enum MenuState
        {
            MainMenu,
            SkirmishLobby,
            MultiplayerLobby
        }

        private MenuState _currentState = MenuState.MainMenu;
        
        // Sub-components
        private SkirmishLobby _skirmishLobby;
        private MultiplayerLobbyUI _multiplayerLobby;
        
        // Window styling
        private Rect _mainMenuRect = new Rect(40, 40, 320, 340);
        private GUIStyle _titleStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _disabledStyle;
        private bool _stylesInitialized = false;

        void Awake()
        {
            MenuBootstrap.EnsureMenuCamera();
            
            // Create sub-components
            _skirmishLobby = gameObject.AddComponent<SkirmishLobby>();
            _skirmishLobby.enabled = false;
            
            _multiplayerLobby = gameObject.AddComponent<MultiplayerLobbyUI>();
            _multiplayerLobby.enabled = false;
            
            // Subscribe to back events
            _skirmishLobby.OnBackPressed += () => SetState(MenuState.MainMenu);
            _multiplayerLobby.OnBackPressed += () => SetState(MenuState.MainMenu);
        }

        void OnGUI()
        {
            InitStyles();
            
            if (_currentState == MenuState.MainMenu)
            {
                _mainMenuRect = GUI.Window(10001, _mainMenuRect, DrawMainMenu, "The Waning Border");
            }
        }

        private void InitStyles()
        {
            if (_stylesInitialized) return;
            
            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            
            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14
            };
            
            _disabledStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14
            };
            _disabledStyle.normal.textColor = Color.gray;
            
            _stylesInitialized = true;
        }

        private void DrawMainMenu(int windowId)
        {
            GUILayout.Space(20);
            
            // Title
            GUILayout.Label("Main Menu", _titleStyle);
            GUILayout.Space(30);
            
            // Skirmish button
            if (GUILayout.Button("Skirmish", GUILayout.Height(45)))
            {
                SetState(MenuState.SkirmishLobby);
            }
            GUILayout.Space(10);
            
            // Multiplayer button
            if (GUILayout.Button("Multiplayer", GUILayout.Height(45)))
            {
                SetState(MenuState.MultiplayerLobby);
            }
            GUILayout.Space(10);
            
            // Campaign button (placeholder - disabled)
            GUI.enabled = false;
            if (GUILayout.Button("Campaign (Coming Soon)", GUILayout.Height(45)))
            {
                // Placeholder - no action
            }
            GUI.enabled = true;
            GUILayout.Space(10);
            
            // Options button (placeholder - disabled)
            GUI.enabled = false;
            if (GUILayout.Button("Options (Coming Soon)", GUILayout.Height(45)))
            {
                // Placeholder - no action
            }
            GUI.enabled = true;
            
            GUILayout.FlexibleSpace();
            
            // Exit button
            if (GUILayout.Button("Exit", GUILayout.Height(40)))
            {
                ExitGame();
            }
            
            GUILayout.Space(10);
            
            GUI.DragWindow(new Rect(0, 0, 10000, 25));
        }

        private void SetState(MenuState newState)
        {
            _currentState = newState;
            
            // Enable/disable sub-components based on state
            _skirmishLobby.enabled = (newState == MenuState.SkirmishLobby);
            _multiplayerLobby.enabled = (newState == MenuState.MultiplayerLobby);
            
            // Initialize lobbies when entering
            if (newState == MenuState.SkirmishLobby)
            {
                GameSettings.IsMultiplayer = false;
                GameSettings.NetworkRole = NetworkRole.None;
                LobbyConfig.SetupSinglePlayer(GameSettings.TotalPlayers);
            }
            else if (newState == MenuState.MultiplayerLobby)
            {
                GameSettings.IsMultiplayer = true;
                LobbyConfig.SetupMultiplayer(GameSettings.TotalPlayers);
            }
        }

        private void ExitGame()
        {
            #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
            #else
                Application.Quit();
            #endif
        }
    }
}
