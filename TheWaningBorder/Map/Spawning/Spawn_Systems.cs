using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using TheWaningBorder.Core.GameManager;
using TheWaningBorder.Core.Settings;
using TheWaningBorder.Core.Utils;
using TheWaningBorder.Buildings.Hall;
using TheWaningBorder.Units.Builder;
using TheWaningBorder.Resources.IronMining;

namespace TheWaningBorder.Map.Spawning
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class SpawnSystem : SystemBase
    {
        protected override void OnUpdate() { }
        
        public void SpawnPlayer(int playerId, bool isHuman)
        {
            Debug.Log($"[Spawn] Spawning player {playerId} (Human: {isHuman})");
            
            // Calculate spawn position
            float3 spawnPosition = GetSpawnPosition(playerId);
            
            // Create player entity
            var playerEntity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(playerEntity, new PlayerComponent
            {
                PlayerId = playerId,
                TeamId = playerId,
                IsHuman = isHuman,
                IsAlive = true,
                PlayerName = new Unity.Collections.FixedString64Bytes($"Player {playerId + 1}"),
                SelectedCulture = new Unity.Collections.FixedString64Bytes(""),
                CurrentEra = 1
            });
            
            EntityManager.AddComponentData(playerEntity, new ResourcesComponent
            {
                Supplies = GameSettings.StartingSupplies,
                Iron = GameSettings.StartingIron,
                Crystal = GameSettings.StartingCrystal,
                Veilsteel = 0,
                Glow = 0,
                Population = 0,
                PopulationMax = 20
            });
            
            // Spawn Hall
            SpawnHall(playerId, spawnPosition);
            
            // Spawn starting units (builders)
            for (int i = 0; i < 3; i++)
            {
                float3 unitPos = spawnPosition + new float3(i * 2 - 2, 0, -5);
                SpawnBuilder(playerId, unitPos);
            }
            
            Debug.Log($"[Spawn] Player {playerId} spawned at {spawnPosition}");
        }
        
        private float3 GetSpawnPosition(int playerId)
        {
            float mapRadius = GameSettings.MapHalfSize * 0.8f;
            
            switch (GameSettings.SpawnLayout)
            {
                case SpawnLayout.Circle:
                    float angle = (playerId * 2 * math.PI) / GameSettings.TotalPlayers;
                    return new float3(
                        math.cos(angle) * mapRadius,
                        0,
                        math.sin(angle) * mapRadius
                    );
                    
                case SpawnLayout.Grid:
                    int gridSize = (int)math.ceil(math.sqrt(GameSettings.TotalPlayers));
                    float spacing = (mapRadius * 2) / gridSize;
                    int x = playerId % gridSize;
                    int y = playerId / gridSize;
                    return new float3(
                        -mapRadius + x * spacing + spacing/2,
                        0,
                        -mapRadius + y * spacing + spacing/2
                    );
                    
                default:
                    return new float3(0, 0, playerId * 20);
            }
        }
        
        private void SpawnHall(int playerId, float3 position)
        {
            var hallDef = TechTreeLoader.GetBuildingDef("Hall");
            if (hallDef == null)
            {
                Debug.LogError("[Spawn] Hall definition not found!");
                return;
            }
            
            var hallEntity = Hall_Entities.CreateHall(EntityManager, position, playerId);
            
            // Add resource drop-off point
            EntityManager.AddComponentData(hallEntity, new ResourceDropOffPointComponent
            {
                CanReceiveIron = true,
                CanReceiveCrystal = true,
                CanReceiveVeilsteel = true,
                CanReceiveGlow = true,
                DropOffPosition = position,
                OwnerId = playerId
            });
            
            // Add fog revealer
            EntityManager.AddComponentData(hallEntity, new Map.FogOfWar.FogRevealerComponent
            {
                RevealRadius = hallDef.lineOfSight,
                PlayerId = playerId,
                IsActive = true
            });
        }
        
        private void SpawnBuilder(int playerId, float3 position)
        {
            var builderEntity = Builder_Entities.CreateBuilder(EntityManager, position, playerId);
            
            // Add fog revealer for unit
            EntityManager.AddComponentData(builderEntity, new Map.FogOfWar.FogRevealerComponent
            {
                RevealRadius = 10f,
                PlayerId = playerId,
                IsActive = true
            });
        }
    }
}