using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using UnityEngine;
using TheWaningBorder.Core.Utils;
using TheWaningBorder.Units.Base;
using TheWaningBorder.Buildings.Base;
using TheWaningBorder.Resources.IronMining;

namespace TheWaningBorder.Core.GameManager
{
    public class GameManager_Bootstrap : ICustomBootstrap
    {
        public bool Initialize(string defaultWorldName)
        {
            // Load TechTree first - this is critical!
            if (!TechTreeLoader.LoadTechTree())
            {
                Debug.LogError("[Bootstrap] Failed to load TechTree.json - cannot start game!");
                return false;
            }
            
            Debug.Log("[Bootstrap] TechTree loaded successfully");
            
            // Create default world
            var world = new World(defaultWorldName);
            World.DefaultGameObjectInjectionWorld = world;
            
            // Create core systems
            var systems = DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Default);
            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, systems);
            
            // Initialize game state
            InitializeGameState(world);
            
            // Spawn initial entities for testing
            SpawnInitialEntities(world);
            
            ScriptBehaviourUpdateOrder.AppendWorldToCurrentPlayerLoop(world);
            
            return true;
        }
        
        private void InitializeGameState(World world)
        {
            var entityManager = world.EntityManager;
            
            // Create game state entity
            var gameStateEntity = entityManager.CreateEntity();
            entityManager.AddComponentData(gameStateEntity, new GameStateComponent
            {
                CurrentEra = 1,
                GameTime = 0f,
                IsPaused = false,
                Mode = GameMode.SoloVsCurse
            });
            
            // Create player entities
            CreatePlayer(entityManager, 0, "Player 1", true);
            CreatePlayer(entityManager, 1, "AI Player", false);
            
            Debug.Log("[Bootstrap] Game state initialized");
        }
        
        private void CreatePlayer(EntityManager entityManager, int playerId, string playerName, bool isHuman)
        {
            var playerEntity = entityManager.CreateEntity();
            
            entityManager.AddComponentData(playerEntity, new PlayerComponent
            {
                PlayerId = playerId,
                TeamId = playerId,
                IsHuman = isHuman,
                IsAlive = true,
                PlayerName = new FixedString64Bytes(playerName),
                SelectedCulture = new FixedString64Bytes("Default"),
                CurrentEra = 1
            });
            
            entityManager.AddComponentData(playerEntity, new ResourcesComponent
            {
                Supplies = 1000,
                Iron = 200,
                Crystal = 0,
                Veilsteel = 0,
                Glow = 0,
                Population = 0,
                PopulationMax = 20
            });
            
            Debug.Log($"[Bootstrap] Created player: {playerName} (ID: {playerId})");
        }
        
        private void SpawnInitialEntities(World world)
        {
            var entityManager = world.EntityManager;
            
            // Spawn some test units for player 0
            float3 startPos = new float3(0, 0, 0);
            
            // Create a few test units
            for (int i = 0; i < 3; i++)
            {
                var unitEntity = entityManager.CreateEntity();
                float3 unitPos = startPos + new float3(i * 2, 0, 0);
                
                entityManager.AddComponentData(unitEntity, new PositionComponent
                {
                    Position = unitPos
                });
                
                entityManager.AddComponentData(unitEntity, new HealthComponent
                {
                    CurrentHp = 100,
                    MaxHp = 100,
                    RegenRate = 0
                });
                
                entityManager.AddComponentData(unitEntity, new MovementComponent
                {
                    Destination = unitPos,
                    Speed = 5f,
                    IsMoving = false,
                    StoppingDistance = 1f
                });
                
                entityManager.AddComponentData(unitEntity, new OwnerComponent
                {
                    PlayerId = 0
                });
                
                entityManager.AddComponentData(unitEntity, new SelectableComponent
                {
                    IsSelected = false,
                    SelectionRadius = 1.5f
                });
                
                entityManager.AddComponentData(unitEntity, new CommandableComponent
                {
                    CanMove = true,
                    CanAttack = true,
                    CanBuild = false,
                    CanGather = false
                });
            }
            
            // Spawn some iron deposits
            for (int i = 0; i < 5; i++)
            {
                var depositEntity = entityManager.CreateEntity();
                float3 depositPos = new float3(10 + i * 5, 0, 10);
                
                entityManager.AddComponentData(depositEntity, new PositionComponent
                {
                    Position = depositPos
                });
                
                entityManager.AddComponentData(depositEntity, new IronDepositComponent
                {
                    RemainingOre = 5000,
                    ClaimedByMiner = Entity.Null,
                    MiningRadius = 2f
                });
            }
            
            Debug.Log("[Bootstrap] Initial entities spawned");
        }
    }
}
