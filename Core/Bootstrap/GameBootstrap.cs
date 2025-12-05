// GameBootstrap.cs
// Main game initialization - coordinates all bootstrap systems
// Location: Assets/Scripts/Core/Bootstrap/GameBootstrap.cs

using UnityEngine;
using UnityEngine.SceneManagement;
using TheWaningBorder.GameCamera;
using TheWaningBorder.Gameplay;
using TheWaningBorder.AI;
using TheWaningBorder.UI;
using TheWaningBorder.Economy;

namespace TheWaningBorder.Core.Bootstrap
{
    /// <summary>
    /// Main game bootstrap - initializes all game systems when the Game scene loads.
    /// Uses [RuntimeInitializeOnLoadMethod] to auto-run without scene dependencies.
    /// </summary>
    public static class GameBootstrap
    {
        private static bool _didSetupThisScene;

        // ═══════════════════════════════════════════════════════════════
        // INITIALIZATION
        // ═══════════════════════════════════════════════════════════════

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            SceneManager.sceneLoaded -= OnSceneLoadedHandler;
            SceneManager.sceneLoaded += OnSceneLoadedHandler;
            OnSceneLoadedHandler(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void OnSceneLoadedHandler(Scene scene, LoadSceneMode mode)
        {
            // Only bootstrap the Game scene
            if (!string.Equals(scene.name, "Game")) return;
            if (_didSetupThisScene) return;
            _didSetupThisScene = true;

            Debug.Log("[GameBootstrap] Initializing game systems...");

            // 1. Initialize core data systems (TechTreeDB)
            InitializeDataSystems();

            // 2. Initialize camera
            GameCamera.Ensure();

            // 3. Create runtime managers GameObject
            CreateManagersObject();

            // 4. Initialize game world (terrain, fog of war)
            InitializeWorld();

            // 5. Initialize factions and economy
            InitializeFactions();

            // 6. Initialize AI for non-human players
            InitializeAI();

            // 7. Sync systems after all initialization
            PostInitializationSync();

            Debug.Log($"[GameBootstrap] Game initialized - IsMultiplayer: {GameSettings.IsMultiplayer}, " +
                      $"LocalFaction: {GameSettings.LocalPlayerFaction}, Players: {GameSettings.TotalPlayers}");
        }

        // ═══════════════════════════════════════════════════════════════
        // DATA SYSTEMS
        // ═══════════════════════════════════════════════════════════════

        private static void InitializeDataSystems()
        {
            EnsureTechTreeDB();
        }

        private static void EnsureTechTreeDB()
        {
            if (TechTreeDB.Instance != null)
            {
                Debug.Log("[GameBootstrap] TechTreeDB already initialized");
                return;
            }

            // Try to load from Resources
            TextAsset json = null;
            string[] possiblePaths = 
            {
                "TechTree",           // Resources/TechTree.json
                "Data/TechTree",      // Resources/Data/TechTree.json
                "Config/TechTree",    // Resources/Config/TechTree.json
            };

            foreach (var path in possiblePaths)
            {
                json = Resources.Load<TextAsset>(path);
                if (json != null)
                {
                    Debug.Log($"[GameBootstrap] Loaded TechTree from Resources/{path}");
                    break;
                }
            }

            if (json == null)
            {
                Debug.LogError("[GameBootstrap] Could not find TechTree.json in Resources!");
                return;
            }

            // Create TechTreeDB GameObject
            var go = new GameObject("TechTreeDB");
            Object.DontDestroyOnLoad(go);
            var db = go.AddComponent<TechTreeDB>();
            db.humanTechJson = json;

            Debug.Log("[GameBootstrap] TechTreeDB created and initialized");
        }

        // ═══════════════════════════════════════════════════════════════
        // MANAGERS
        // ═══════════════════════════════════════════════════════════════

        private static void CreateManagersObject()
        {
            var go = new GameObject("RTS_Managers");

            // Input & Selection
            TryAddComponent<RTSInput>(go);
            TryAddComponent<SelectionDecalManager>(go);

            // World rendering
            TryAddComponent<ProceduralTerrain>(go);
            TryAddComponent<MinimapFlat>(go);

            // UI systems
            TryAddComponent<UnifiedUIManager>(go);
            TryAddComponent<EntityViewManager>(go);
            TryAddComponent<BuilderCommandPanel>(go);
            TryAddComponent<HealthbarOverlay>(go);
            TryAddComponent<ResourceHUD_IMGUI>(go);

            Debug.Log("[GameBootstrap] Created RTS_Managers with all components");
        }

        // ═══════════════════════════════════════════════════════════════
        // WORLD INITIALIZATION
        // ═══════════════════════════════════════════════════════════════

        private static void InitializeWorld()
        {
            var fow = Object.FindObjectOfType<FogOfWarManager>();
            if (fow == null)
            {
                FogOfWarManager.SetupFogOfWar();
                Debug.Log("[GameBootstrap] Created FogOfWar system");
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // FACTION INITIALIZATION
        // ═══════════════════════════════════════════════════════════════

        private static void InitializeFactions()
        {
            // Generate all player starting positions and units
            HumanFaction.GeneratePlayers(GameSettings.TotalPlayers);

            // Create economy banks for all factions
            EconomyBootstrap.EnsureFactionBanks(GameSettings.TotalPlayers);

            Debug.Log($"[GameBootstrap] Initialized {GameSettings.TotalPlayers} factions");
        }

        // ═══════════════════════════════════════════════════════════════
        // AI INITIALIZATION
        // ═══════════════════════════════════════════════════════════════

        private static void InitializeAI()
        {
            AIBootstrap.InitializeAIPlayers(
                GameSettings.TotalPlayers, 
                GameSettings.LocalPlayerFaction
            );
        }

        // ═══════════════════════════════════════════════════════════════
        // POST-INITIALIZATION
        // ═══════════════════════════════════════════════════════════════

        private static void PostInitializationSync()
        {
            SyncFoWToTerrain();
        }

        private static void SyncFoWToTerrain()
        {
            var fow = Object.FindObjectOfType<FogOfWarManager>();
            var terrain = Object.FindObjectOfType<ProceduralTerrain>();

            if (fow != null && terrain != null)
            {
                fow.ForceRebuildGrid(clearRevealed: false);
                Debug.Log("[GameBootstrap] Synced FoW to terrain bounds");
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // UTILITIES
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Safely add a component, checking if it already exists in the scene
        /// </summary>
        private static T TryAddComponent<T>(GameObject go) where T : Component
        {
            var existing = Object.FindObjectOfType<T>();
            if (existing != null) return existing;
            return go.AddComponent<T>();
        }

        /// <summary>
        /// Reset bootstrap state - call when returning to main menu
        /// </summary>
        public static void ResetBootstrapState()
        {
            _didSetupThisScene = false;
        }
    }
}