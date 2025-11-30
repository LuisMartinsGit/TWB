// Assets/Scripts/MainMenu/SkirmishLobby.cs
using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.IO;

namespace TheWaningBorder.Menu
{
    /// <summary>
    /// Single-player skirmish lobby.
    /// Allows configuration of player slots (1 human, rest AI/empty) and map options.
    /// </summary>
    public class SkirmishLobby : MonoBehaviour
    {
        public event Action OnBackPressed;

        private const string GameSceneName = "Game";

        // Window layout
        private Rect _windowRect = new Rect(40, 40, 520, 580);
        private Vector2 _slotsScrollPos;
        
        // Map settings (mirrored from old MainMenu)
        private SpawnLayout _layout = GameSettings.SpawnLayout;
        private TwoSidesPreset _twoSides = GameSettings.TwoSides;
        private int _spawnSeed = GameSettings.SpawnSeed;
        private bool _fogOfWar = GameSettings.FogOfWarEnabled;
        private int _mapHalfSize = GameSettings.MapHalfSize;
        
        // Error display
        private string _error;
        
        // Styles
        private GUIStyle _headerStyle;
        private GUIStyle _slotStyle;
        private GUIStyle _factionLabelStyle;
        private bool _stylesInit = false;

        void OnEnable()
        {
            // Sync settings when entering lobby
            _layout = GameSettings.SpawnLayout;
            _twoSides = GameSettings.TwoSides;
            _spawnSeed = GameSettings.SpawnSeed;
            _fogOfWar = GameSettings.FogOfWarEnabled;
            _mapHalfSize = Mathf.Clamp(GameSettings.MapHalfSize, 64, 512);
            
            LobbyConfig.SetupSinglePlayer(LobbyConfig.ActiveSlotCount);
        }

        void OnGUI()
        {
            InitStyles();
            _windowRect = GUI.Window(10002, _windowRect, DrawWindow, "Skirmish Setup");
            
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
            
            _stylesInit = true;
        }

        private void DrawWindow(int windowId)
        {
            // ========== PLAYER COUNT ==========
            GUILayout.Label("<b>Number of Players</b>", _headerStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(" - ", GUILayout.Width(40)))
            {
                SetPlayerCount(LobbyConfig.ActiveSlotCount - 1);
            }
            GUILayout.Label(LobbyConfig.ActiveSlotCount.ToString(), GUILayout.Width(40));
            if (GUILayout.Button(" + ", GUILayout.Width(40)))
            {
                SetPlayerCount(LobbyConfig.ActiveSlotCount + 1);
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            // ========== PLAYER SLOTS ==========
            GUILayout.Label("<b>Player Slots</b>", _headerStyle);
            
            _slotsScrollPos = GUILayout.BeginScrollView(_slotsScrollPos, GUILayout.Height(180));
            
            for (int i = 0; i < LobbyConfig.ActiveSlotCount; i++)
            {
                DrawPlayerSlot(i);
            }
            
            GUILayout.EndScrollView();

            GUILayout.Space(10);

            // ========== SPAWN LAYOUT ==========
            GUILayout.Label("<b>Spawn Layout</b>", _headerStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(_layout == SpawnLayout.Circle, " Circle", "Button")) 
                _layout = SpawnLayout.Circle;
            if (GUILayout.Toggle(_layout == SpawnLayout.TwoSides, " Two Sides (4)", "Button")) 
                _layout = SpawnLayout.TwoSides;
            if (GUILayout.Toggle(_layout == SpawnLayout.TwoEachSide8, " 2 Each Side (8)", "Button")) 
                _layout = SpawnLayout.TwoEachSide8;
            GUILayout.EndHorizontal();

            if (_layout == SpawnLayout.TwoSides)
            {
                GUILayout.Space(4);
                GUILayout.Label("Two Sides Preset:");
                GUILayout.BeginHorizontal();
                if (GUILayout.Toggle(_twoSides == TwoSidesPreset.LeftRight,  " LR", "Button")) 
                    _twoSides = TwoSidesPreset.LeftRight;
                if (GUILayout.Toggle(_twoSides == TwoSidesPreset.UpDown, " UD", "Button")) 
                    _twoSides = TwoSidesPreset.UpDown;
                if (GUILayout.Toggle(_twoSides == TwoSidesPreset.LeftUp, " LU", "Button")) 
                    _twoSides = TwoSidesPreset.LeftUp;
                if (GUILayout.Toggle(_twoSides == TwoSidesPreset.LeftDown, " LD", "Button")) 
                    _twoSides = TwoSidesPreset.LeftDown;
                if (GUILayout.Toggle(_twoSides == TwoSidesPreset.RightUp, " RU", "Button")) 
                    _twoSides = TwoSidesPreset.RightUp;
                if (GUILayout.Toggle(_twoSides == TwoSidesPreset.RightDown, " RD", "Button")) 
                    _twoSides = TwoSidesPreset.RightDown;
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Spawn Seed:");
            string seedStr = GUILayout.TextField(_spawnSeed.ToString(), GUILayout.Width(90));
            if (int.TryParse(seedStr, out int parsed)) _spawnSeed = parsed;
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            // ========== FOG OF WAR ==========
            GUILayout.Label("<b>Fog of War</b>", _headerStyle);
            _fogOfWar = GUILayout.Toggle(_fogOfWar, _fogOfWar ? " Enabled" : " Disabled");

            GUILayout.Space(10);

            // ========== MAP SIZE ==========
            GUILayout.Label("<b>Map Size</b> (total side ≈ 2×HalfSize)", _headerStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(" - ", GUILayout.Width(40))) 
                _mapHalfSize = Mathf.Max(64, _mapHalfSize - 16);
            GUILayout.Label($"Half Size: {_mapHalfSize}  (Total ≈ {_mapHalfSize * 2})", GUILayout.Width(240));
            if (GUILayout.Button(" + ", GUILayout.Width(40))) 
                _mapHalfSize = Mathf.Min(512, _mapHalfSize + 16);
            GUILayout.EndHorizontal();

            GUILayout.FlexibleSpace();

            // ========== ACTION BUTTONS ==========
            GUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Back", GUILayout.Height(36), GUILayout.Width(100)))
            {
                OnBackPressed?.Invoke();
            }
            
            GUILayout.FlexibleSpace();
            
            if (GUILayout.Button("Start Game", GUILayout.Height(36), GUILayout.Width(150)))
            {
                StartGame();
            }
            
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            GUI.DragWindow(new Rect(0, 0, 10000, 25));
        }

        private void DrawPlayerSlot(int index)
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
            
            // Slot type selector
            if (index == 0)
            {
                // Slot 0 is always human in single-player
                GUILayout.Label("Player (You)", GUILayout.Width(120));
            }
            else
            {
                // Other slots can be AI or Empty
                string[] options = { "Empty", "AI" };
                int currentOption = slot.Type == SlotType.AI ? 1 : 0;
                
                int newOption = GUILayout.SelectionGrid(currentOption, options, 2, GUILayout.Width(120));
                slot.Type = newOption == 1 ? SlotType.AI : SlotType.Empty;
            }
            
            // AI difficulty selector (only for AI slots)
            if (slot.Type == SlotType.AI)
            {
                GUILayout.Space(10);
                string[] difficulties = { "Easy", "Normal", "Hard", "Expert" };
                int currentDiff = (int)slot.AIDifficulty;
                
                int newDiff = EditorPopup("", currentDiff, difficulties, 80);
                slot.AIDifficulty = (LobbyAIDifficulty)newDiff;
            }
            
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        // Simple popup replacement using buttons
        private int EditorPopup(string label, int selected, string[] options, float width)
        {
            if (!string.IsNullOrEmpty(label))
                GUILayout.Label(label);
            
            if (GUILayout.Button(options[selected], GUILayout.Width(width)))
            {
                // Cycle through options
                return (selected + 1) % options.Length;
            }
            return selected;
        }

        private void SetPlayerCount(int count)
        {
            int newCount = Mathf.Clamp(count, 2, 8);
            LobbyConfig.ActiveSlotCount = newCount;
            
            // Ensure slots are properly configured
            for (int i = 0; i < 8; i++)
            {
                if (i == 0)
                {
                    LobbyConfig.Slots[i].Type = SlotType.Human;
                }
                else if (i < newCount)
                {
                    // Keep existing type if already AI, otherwise set to AI
                    if (LobbyConfig.Slots[i].Type == SlotType.Empty)
                        LobbyConfig.Slots[i].Type = SlotType.AI;
                }
                else
                {
                    LobbyConfig.Slots[i].Type = SlotType.Empty;
                }
            }
        }

        private void StartGame()
        {
            // Validate: need at least 2 active players
            int activeCount = 0;
            for (int i = 0; i < LobbyConfig.ActiveSlotCount; i++)
            {
                if (LobbyConfig.Slots[i].Type != SlotType.Empty)
                    activeCount++;
            }
            
            if (activeCount < 2)
            {
                _error = "Need at least 2 players (human or AI) to start!";
                return;
            }
            
            // Apply settings
            GameSettings.IsMultiplayer = false;
            GameSettings.NetworkRole = NetworkRole.None;
            GameSettings.Mode = GameMode.FreeForAll;
            GameSettings.FogOfWarEnabled = _fogOfWar;
            GameSettings.MapHalfSize = _mapHalfSize;
            GameSettings.SpawnLayout = _layout;
            GameSettings.TwoSides = _twoSides;
            GameSettings.SpawnSeed = _spawnSeed;
            
            // Count actual players (non-empty slots)
            GameSettings.TotalPlayers = activeCount;
            
            // Apply lobby config
            LobbyConfig.ApplyToGameSettings();
            
            // Load scene
            int sceneIndex = FindSceneIndexByName(GameSceneName);
            if (sceneIndex < 0)
            {
                _error = $"Scene '{GameSceneName}' not found in build settings!";
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
