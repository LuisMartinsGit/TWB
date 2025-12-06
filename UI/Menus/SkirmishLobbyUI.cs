// File: Assets/Scripts/UI/Menus/SkirmishLobbyUI.cs
// Single-player skirmish lobby with player slots and map configuration

using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using TheWaningBorder.Multiplayer;

namespace TheWaningBorder.UI.Menus
{
    /// <summary>
    /// Single-player skirmish lobby.
    /// Allows configuration of player slots (1 human, rest AI/empty) and map options.
    /// </summary>
    public class SkirmishLobbyUI : MonoBehaviour
    {
        public event Action OnBackPressed;

        private const string GameSceneName = "Game";

        // Window layout
        private Rect _windowRect = new Rect(40, 40, 520, 580);
        private Vector2 _slotsScrollPos;

        // Map settings
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
            // Player count
            GUILayout.Label("<b>Number of Players</b>", _headerStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(" - ", GUILayout.Width(40)))
                SetPlayerCount(LobbyConfig.ActiveSlotCount - 1);
            GUILayout.Label(LobbyConfig.ActiveSlotCount.ToString(), GUILayout.Width(40));
            if (GUILayout.Button(" + ", GUILayout.Width(40)))
                SetPlayerCount(LobbyConfig.ActiveSlotCount + 1);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            // Player slots
            GUILayout.Label("<b>Player Slots</b>", _headerStyle);

            _slotsScrollPos = GUILayout.BeginScrollView(_slotsScrollPos, GUILayout.Height(180));

            for (int i = 0; i < LobbyConfig.ActiveSlotCount; i++)
            {
                DrawPlayerSlot(i);
            }

            GUILayout.EndScrollView();

            GUILayout.Space(10);

            // Spawn layout
            GUILayout.Label("<b>Spawn Layout</b>", _headerStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(_layout == SpawnLayout.Circle, " Circle", "Button"))
                _layout = SpawnLayout.Circle;
            if (GUILayout.Toggle(_layout == SpawnLayout.TwoSides, " Two Sides", "Button"))
                _layout = SpawnLayout.TwoSides;
            if (GUILayout.Toggle(_layout == SpawnLayout.FreeForAll, " Free For All", "Button"))
                _layout = SpawnLayout.FreeForAll;
            GUILayout.EndHorizontal();

            // Two sides preset
            if (_layout == SpawnLayout.TwoSides)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Preset:", GUILayout.Width(60));
                if (GUILayout.Toggle(_twoSides == TwoSidesPreset.EastWest, " E/W", "Button"))
                    _twoSides = TwoSidesPreset.EastWest;
                if (GUILayout.Toggle(_twoSides == TwoSidesPreset.NorthSouth, " N/S", "Button"))
                    _twoSides = TwoSidesPreset.NorthSouth;
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(10);

            // Spawn seed
            GUILayout.Label("<b>Spawn Seed</b>", _headerStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Random", GUILayout.Width(80)))
                _spawnSeed = UnityEngine.Random.Range(1, 99999);
            var seedStr = GUILayout.TextField(_spawnSeed.ToString(), GUILayout.Width(100));
            if (int.TryParse(seedStr, out int newSeed))
                _spawnSeed = Mathf.Max(0, newSeed);
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            // Fog of war
            GUILayout.Label("<b>Fog of War</b>", _headerStyle);
            _fogOfWar = GUILayout.Toggle(_fogOfWar, _fogOfWar ? " Enabled" : " Disabled");

            GUILayout.Space(10);

            // Map size
            GUILayout.Label("<b>Map Size</b> (total side ≈ 2×HalfSize)", _headerStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(" - ", GUILayout.Width(40)))
                _mapHalfSize = Mathf.Max(64, _mapHalfSize - 16);
            GUILayout.Label($"Half Size: {_mapHalfSize}  (Total ≈ {_mapHalfSize * 2})", GUILayout.Width(240));
            if (GUILayout.Button(" + ", GUILayout.Width(40)))
                _mapHalfSize = Mathf.Min(512, _mapHalfSize + 16);
            GUILayout.EndHorizontal();

            GUILayout.FlexibleSpace();

            // Action buttons
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

            // AI difficulty (only for AI slots)
            if (slot.Type == SlotType.AI)
            {
                string[] difficulties = { "Easy", "Normal", "Hard", "Expert" };
                int diffIndex = (int)slot.AIDifficulty;

                if (GUILayout.Button(difficulties[diffIndex], GUILayout.Width(70)))
                {
                    slot.AIDifficulty = (LobbyAIDifficulty)((diffIndex + 1) % difficulties.Length);
                }
            }
            else
            {
                GUILayout.Label("", GUILayout.Width(70));
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void SetPlayerCount(int count)
        {
            count = Mathf.Clamp(count, 2, 8);
            LobbyConfig.SetupSinglePlayer(count);
        }

        private void StartGame()
        {
            // Apply settings
            GameSettings.SpawnLayout = _layout;
            GameSettings.TwoSides = _twoSides;
            GameSettings.SpawnSeed = _spawnSeed;
            GameSettings.FogOfWarEnabled = _fogOfWar;
            GameSettings.MapHalfSize = _mapHalfSize;

            // Count active players
            int humanCount = 0;
            int aiCount = 0;

            for (int i = 0; i < LobbyConfig.ActiveSlotCount; i++)
            {
                var slot = LobbyConfig.Slots[i];
                if (slot.Type == SlotType.Human) humanCount++;
                else if (slot.Type == SlotType.AI) aiCount++;
            }

            if (humanCount == 0)
            {
                _error = "Need at least 1 human player!";
                return;
            }

            GameSettings.TotalPlayers = humanCount + aiCount;
            GameSettings.IsMultiplayer = false;
            GameSettings.NetworkRole = NetworkRole.None;
            GameSettings.LocalPlayerFaction = Faction.Blue;

            _error = null;

            Debug.Log($"[SkirmishLobby] Starting game with {GameSettings.TotalPlayers} players");
            SceneManager.LoadScene(GameSceneName);
        }
    }
}