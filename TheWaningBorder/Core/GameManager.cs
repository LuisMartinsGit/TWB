using System;
using Unity.Entities;
using UnityEngine;
using TheWaningBorder.Core.Utilities;
using TheWaningBorder.Core.Systems;

namespace TheWaningBorder.Core
{
    /// <summary>
    /// Main game manager for The Waning Border
    /// Initializes ECS world and manages game state
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        private static GameManager _instance;
        public static GameManager Instance => _instance;

        private World _gameWorld;
        private EntityManager _entityManager;

        [Header("Game Configuration")]
        [SerializeField] private bool validateOnStart = true;
        [SerializeField] private bool enableDebugLogging = false;

        [Header("Game State")]
        [ReadOnly] private int currentEra = 1;
        [ReadOnly] private string selectedCulture = "";
        [ReadOnly] private int playerPopulation = 0;
        [ReadOnly] private int playerPopulationMax = 0;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeGame();
        }

        private void InitializeGame()
        {
            try
            {
                // Load TechTree data first - CRITICAL!
                Debug.Log("Loading TechTree.json configuration...");
                var techTreeData = TechTreeLoader.Data;
                
                if (techTreeData == null)
                {
                    throw new InvalidOperationException("Failed to load TechTree data!");
                }

                // Initialize ECS World
                _gameWorld = World.DefaultGameObjectInjectionWorld;
                _entityManager = _gameWorld.EntityManager;

                // Register all systems
                RegisterSystems();

                // Validate game data if enabled
                if (validateOnStart)
                {
                    ValidateGameData();
                }

                Debug.Log($"Game initialized successfully! Faction: {techTreeData.faction}, Version: {techTreeData.version}");
            }
            catch (Exception e)
            {
                Debug.LogError($"CRITICAL ERROR: Failed to initialize game!\n{e.Message}\n{e.StackTrace}");
                
                // Show error dialog to player
                ShowCriticalError(e.Message);
                
                // Quit application in builds
                #if !UNITY_EDITOR
                Application.Quit();
                #endif
            }
        }

        private void RegisterSystems()
        {
            Debug.Log("Registering ECS systems...");

            // Core systems
            _gameWorld.GetOrCreateSystem<MovementSystem>();
            _gameWorld.GetOrCreateSystem<CombatSystem>();
            _gameWorld.GetOrCreateSystem<HealthSystem>();
            _gameWorld.GetOrCreateSystem<ProjectileSystem>();
            _gameWorld.GetOrCreateSystem<ArrowVisualSystem>();

            // Unit-specific systems would be registered here
            // They are generated for each unit type that needs them

            Debug.Log("All systems registered successfully");
        }

        private void ValidateGameData()
        {
            Debug.Log("Validating game data...");

            var techTree = TechTreeLoader.Data;

            // Validate resources
            if (techTree.resources == null || techTree.resources.Count == 0)
            {
                throw new InvalidOperationException("No resources defined in TechTree.json!");
            }

            // Validate combat profile
            if (techTree.combatProfile == null)
            {
                throw new InvalidOperationException("Combat profile not defined in TechTree.json!");
            }

            // Validate damage types
            foreach (var damageType in techTree.combatProfile.damageTypes)
            {
                if (!techTree.combatProfile.modifiers.ContainsKey(damageType))
                {
                    throw new InvalidOperationException($"Damage type '{damageType}' has no modifiers defined!");
                }
            }

            // Validate units
            int unitCount = TechTreeLoader.GetTotalUnitCount();
            if (unitCount == 0)
            {
                throw new InvalidOperationException("No units defined in TechTree.json!");
            }

            // Validate buildings
            int buildingCount = TechTreeLoader.GetTotalBuildingCount();
            if (buildingCount == 0)
            {
                throw new InvalidOperationException("No buildings defined in TechTree.json!");
            }

            Debug.Log($"Validation complete: {unitCount} units, {buildingCount} buildings");
        }

        /// <summary>
        /// Spawn a unit at the specified position
        /// </summary>
        public Entity SpawnUnit(string unitId, Vector3 position)
        {
            if (string.IsNullOrEmpty(unitId))
            {
                throw new ArgumentNullException(nameof(unitId), "Cannot spawn unit with null ID!");
            }

            try
            {
                // Get unit data from TechTree
                var unitData = TechTreeLoader.GetUnitData(unitId);

                // Use reflection to find the appropriate entity creator
                var unitTypeName = unitId.Replace("_", "");
                var entityClassName = $"TheWaningBorder.Units.{unitTypeName}.{unitTypeName}Entity";
                var entityType = Type.GetType(entityClassName);

                if (entityType == null)
                {
                    throw new InvalidOperationException($"Entity class not found for unit: {unitId}");
                }

                // Create instance and spawn unit
                var entitySystem = _gameWorld.GetOrCreateSystem(entityType);
                var createMethod = entityType.GetMethod($"Create{unitTypeName}");
                
                if (createMethod != null)
                {
                    var entity = (Entity)createMethod.Invoke(entitySystem, new object[] { position });
                    Debug.Log($"Spawned {unitId} at {position}");
                    return entity;
                }
                else
                {
                    throw new InvalidOperationException($"Create method not found for unit: {unitId}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to spawn unit '{unitId}': {e.Message}");
                throw;
            }
        }

        /// <summary>
        /// Advance to the next era
        /// </summary>
        public void AdvanceEra(string cultureName = null)
        {
            if (currentEra >= TechTreeLoader.Data.maxEra)
            {
                Debug.LogWarning($"Cannot advance past era {TechTreeLoader.Data.maxEra}");
                return;
            }

            currentEra++;

            if (currentEra == 2 && !string.IsNullOrEmpty(cultureName))
            {
                selectedCulture = cultureName;
                Debug.Log($"Advanced to Era {currentEra} with culture: {cultureName}");
            }
            else
            {
                Debug.Log($"Advanced to Era {currentEra}");
            }
        }

        /// <summary>
        /// Get the current era
        /// </summary>
        public int GetCurrentEra() => currentEra;

        /// <summary>
        /// Get the selected culture
        /// </summary>
        public string GetSelectedCulture() => selectedCulture;

        private void ShowCriticalError(string message)
        {
            // In a real implementation, this would show a UI dialog
            Debug.LogError($"CRITICAL ERROR:\n{message}");
        }

        private void OnDestroy()
        {
            if (_gameWorld != null && _gameWorld.IsCreated)
            {
                _gameWorld.Dispose();
            }
        }
    }

    /// <summary>
    /// Attribute for read-only fields in inspector
    /// </summary>
    public class ReadOnlyAttribute : PropertyAttribute { }

    #if UNITY_EDITOR
    [UnityEditor.CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
    public class ReadOnlyDrawer : UnityEditor.PropertyDrawer
    {
        public override void OnGUI(Rect position, UnityEditor.SerializedProperty property, GUIContent label)
        {
            GUI.enabled = false;
            UnityEditor.EditorGUI.PropertyField(position, property, label, true);
            GUI.enabled = true;
        }
    }
    #endif
}
