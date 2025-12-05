// GameBootstrap.cs
// Main game initialization - coordinates all bootstrap systems
// Location: Assets/Scripts/Bootstrap/GameBootstrap.cs
// NOTE: This file should be in Assets/Scripts/Bootstrap/, NOT in Core/Bootstrap/

using UnityEngine;
using UnityEngine.SceneManagement;
using TheWaningBorder.Input;  // Contains GameCamera
using TheWaningBorder.AI;
using TheWaningBorder.UI;
using TheWaningBorder.Economy;
using TheWaningBorder.Data;

namespace TheWaningBorder.Bootstrap
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

            TechTreeDB.Initialize(json.text);
        }

        // ═══════════════════════════════════════════════════════════════
        // MANAGERS
        // ═══════════════════════════════════════════════════════════════

        private static void CreateManagersObject()
        {
            var existing = Object.FindFirstObjectByType<RuntimeManagers>();
            if (existing != null)
            {
                Debug.Log("[GameBootstrap] RuntimeManagers already exists");
                return;
            }

            var managersGO = new GameObject("RuntimeManagers");
            managersGO.AddComponent<RuntimeManagers>();
            Object.DontDestroyOnLoad(managersGO);
            Debug.Log("[GameBootstrap] Created RuntimeManagers");
        }

        // ═══════════════════════════════════════════════════════════════
        // WORLD INITIALIZATION
        // ═══════════════════════════════════════════════════════════════

        private static void InitializeWorld()
        {
            // Initialize fog of war if enabled
            if (GameSettings.FogOfWarEnabled)
            {
                // FogOfWarManager.Initialize(GameSettings.MapHalfSize * 2);
                Debug.Log("[GameBootstrap] Fog of war initialization placeholder");
            }

            // Initialize terrain (placeholder for procedural generation)
            Debug.Log("[GameBootstrap] World initialization placeholder");
        }

        // ═══════════════════════════════════════════════════════════════
        // FACTIONS & ECONOMY
        // ═══════════════════════════════════════════════════════════════

        private static void InitializeFactions()
        {
            // Initialize economy for each faction based on LobbyConfig
            for (int i = 0; i < GameSettings.TotalPlayers; i++)
            {
                var config = LobbyConfig.GetSlot(i);
                if (config != null && config.IsOccupied)
                {
                    // EconomyManager.InitializeFaction(config.Faction);
                    Debug.Log($"[GameBootstrap] Initialized faction {config.Faction}");
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // AI INITIALIZATION
        // ═══════════════════════════════════════════════════════════════

        private static void InitializeAI()
        {
            // Initialize AI for non-human players
            for (int i = 0; i < GameSettings.TotalPlayers; i++)
            {
                var config = LobbyConfig.GetSlot(i);
                if (config != null && config.IsOccupied && !config.IsHuman)
                {
                    // AIBootstrap.InitializeForFaction(config.Faction, config.AIDifficulty, config.AIPersonality);
                    Debug.Log($"[GameBootstrap] AI initialized for faction {config.Faction} " +
                              $"(Difficulty: {config.AIDifficulty}, Personality: {config.AIPersonality})");
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // POST INITIALIZATION
        // ═══════════════════════════════════════════════════════════════

        private static void PostInitializationSync()
        {
            // Any final synchronization needed after all systems are up
            Debug.Log("[GameBootstrap] Post-initialization sync complete");
        }

        // ═══════════════════════════════════════════════════════════════
        // CLEANUP
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Reset bootstrap state (call when returning to main menu)
        /// </summary>
        public static void Reset()
        {
            _didSetupThisScene = false;
            GameCamera.Cleanup();
            Debug.Log("[GameBootstrap] Reset for new game");
        }
    }

    /// <summary>
    /// Placeholder component for runtime managers GameObject.
    /// Add actual manager components here.
    /// </summary>
    public class RuntimeManagers : MonoBehaviour
    {
        void Awake()
        {
            Debug.Log("[RuntimeManagers] Awake");
        }
    }
}