using System;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using TheWaningBorder.Core.Settings;
using TheWaningBorder.Core.Utils;
using TheWaningBorder.Resources.IronMining;
using TheWaningBorder.Map.FogOfWar;
using TheWaningBorder.Map.Terrain;
using TheWaningBorder.Player.PlayerController;

namespace TheWaningBorder.Core.GameManager
{
    [DefaultExecutionOrder(-1000)]
    public class GameManager_Bootstrap : MonoBehaviour
    {
        private static GameManager_Bootstrap _instance;
        public static GameManager_Bootstrap Instance => _instance;

        private World _gameWorld;
        private EntityManager _entityManager;
        
        [Header("Configuration")]
        public bool validateDataOnStart = true;
        public bool enableDebugLogging = false;
        
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            
            InitializeWorld();
        }

        private void InitializeWorld()
        {
            Debug.Log("[GameManager] Initializing game world...");
            
            try
            {
                // Load TechTree data
                if (!TechTreeLoader.LoadTechTree())
                {
                    throw new Exception("Failed to load TechTree.json!");
                }
                
                // Get or create ECS world
                _gameWorld = World.DefaultGameObjectInjectionWorld ?? new World("GameWorld");
                _entityManager = _gameWorld.EntityManager;
                
                // Register all systems
                RegisterCoreSystems();
                RegisterResourceSystems();
                RegisterUnitSystems();
                RegisterBuildingSystems();
                RegisterMapSystems();
                RegisterPlayerSystems();
                RegisterAISystems();
                
                // Initialize game
                InitializeGame();
                
                Debug.Log("[GameManager] Game world initialized successfully!");
            }
            catch (Exception e)
            {
                Debug.LogError($"[GameManager] Critical initialization error: {e.Message}\n{e.StackTrace}");
                #if !UNITY_EDITOR
                Application.Quit();
                #endif
            }
        }

        private void RegisterCoreSystems()
        {
            _gameWorld.GetOrCreateSystemManaged<GameStateSystem>();
            _gameWorld.GetOrCreateSystemManaged<TimeSystem>();
        }

        private void RegisterResourceSystems()
        {
            _gameWorld.GetOrCreateSystemManaged<IronDepositGenerationSystem>();
            _gameWorld.GetOrCreateSystemManaged<MinerTargetingSystem>();
            _gameWorld.GetOrCreateSystemManaged<MiningSystem>();
            _gameWorld.GetOrCreateSystemManaged<OreDeliverySystem>();
            _gameWorld.GetOrCreateSystemManaged<ResourceManagerSystem>();
        }

        private void RegisterUnitSystems()
        {
            _gameWorld.GetOrCreateSystemManaged<UnitMovementSystem>();
            _gameWorld.GetOrCreateSystemManaged<UnitCombatSystem>();
            _gameWorld.GetOrCreateSystemManaged<UnitHealthSystem>();
            _gameWorld.GetOrCreateSystemManaged<ProjectileSystem>();
        }

        private void RegisterBuildingSystems()
        {
            _gameWorld.GetOrCreateSystemManaged<BuildingConstructionSystem>();
            _gameWorld.GetOrCreateSystemManaged<BuildingHealthSystem>();
            _gameWorld.GetOrCreateSystemManaged<TrainingQueueSystem>();
        }

        private void RegisterMapSystems()
        {
            _gameWorld.GetOrCreateSystemManaged<TerrainGenerationSystem>();
            _gameWorld.GetOrCreateSystemManaged<FogUpdateSystem>();
            _gameWorld.GetOrCreateSystemManaged<FogRenderSystem>();
            _gameWorld.GetOrCreateSystemManaged<SpawnSystem>();
        }

        private void RegisterPlayerSystems()
        {
            _gameWorld.GetOrCreateSystemManaged<PlayerControllerSystem>();
            _gameWorld.GetOrCreateSystemManaged<SelectionSystem>();
            _gameWorld.GetOrCreateSystemManaged<CommandSystem>();
        }

        private void RegisterAISystems()
        {
            _gameWorld.GetOrCreateSystemManaged<PathfindingSystem>();
            _gameWorld.GetOrCreateSystemManaged<AIControllerSystem>();
        }

        private void InitializeGame()
        {
            // Generate map
            var terrainSystem = _gameWorld.GetExistingSystemManaged<TerrainGenerationSystem>();
            terrainSystem?.GenerateTerrain();
            
            // Generate resource deposits
            var ironSystem = _gameWorld.GetExistingSystemManaged<IronDepositGenerationSystem>();
            ironSystem?.GenerateInitialDeposits();
            
            // Initialize fog of war
            var fogSystem = _gameWorld.GetExistingSystemManaged<FogUpdateSystem>();
            fogSystem?.InitializeFog();
            
            // Spawn players
            SpawnPlayers();
        }

        private void SpawnPlayers()
        {
            var spawnSystem = _gameWorld.GetExistingSystemManaged<SpawnSystem>();
            if (spawnSystem == null) return;

            int playerCount = GameSettings.TotalPlayers;
            for (int i = 0; i < playerCount; i++)
            {
                bool isHuman = (i == 0); // First player is human
                spawnSystem.SpawnPlayer(i, isHuman);
            }
        }

        public EntityManager GetEntityManager() => _entityManager;
        public World GetWorld() => _gameWorld;

        private void OnDestroy()
        {
            if (_gameWorld != null && _gameWorld.IsCreated)
            {
                _gameWorld.Dispose();
            }
        }
    }
}
