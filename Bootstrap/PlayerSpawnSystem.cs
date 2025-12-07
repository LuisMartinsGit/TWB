// PlayerSpawnSystem.cs
// Spawns initial units and buildings for each faction at game start
// Location: Assets/Scripts/Bootstrap/PlayerSpawnSystem.cs

using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using TheWaningBorder.Entities;
using TheWaningBorder.Economy;
using TheWaningBorder.Core.Config;

namespace TheWaningBorder.Bootstrap
{
    public static class PlayerSpawnSystem
    {
        /// <summary>
        /// Spawn starting bases and units for all active factions.
        /// Call from GameBootstrap after world initialization.
        /// </summary>
        public static void SpawnAllFactions()
        {
            var world = Unity.Entities.World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                Debug.LogError("[PlayerSpawnSystem] No ECS World available!");
                return;
            }

            var em = world.EntityManager;
            int playerCount = GameSettings.TotalPlayers;
            
            // Calculate spawn positions based on layout
            var positions = CalculateSpawnPositions(playerCount);

            for (int i = 0; i < playerCount; i++)
            {
                var slot = LobbyConfig.Slots[i];
                if (slot == null || slot.Type == SlotType.Empty) continue;

                var faction = slot.Faction;
                var spawnPos = positions[i];

                SpawnFactionBase(em, faction, spawnPos);
                Debug.Log($"[PlayerSpawnSystem] Spawned {faction} at {spawnPos}");
            }
        }

        private static void SpawnFactionBase(EntityManager em, Faction faction, float3 position)
        {
            // Adjust Y to terrain height
            float y = GetTerrainHeightStatic(position.x, position.z);
            float3 spawnPos = new float3(position.x, y, position.z);
            
            // Spawn Hall (main base)
            Hall.Create(em, spawnPos, faction);

            // Spawn starting Builders around the Hall
            float offset = 3f;
            Builder.Create(em, spawnPos + new float3(offset, 0, 0), faction);
            Builder.Create(em, spawnPos + new float3(-offset, 0, 0), faction);
            Builder.Create(em, spawnPos + new float3(0, 0, offset), faction);
        }

        private static float GetTerrainHeightStatic(float x, float z)
        {
            var terrain = Terrain.activeTerrain;
            if (terrain != null && terrain.terrainData != null)
            {
                return terrain.SampleHeight(new Vector3(x, 0, z)) + terrain.transform.position.y;
            }
            
            if (Physics.Raycast(new Vector3(x, 1000f, z), Vector3.down, out RaycastHit hit, 2000f))
            {
                return hit.point.y;
            }
            
            return 0f;
        }

        private static float3[] CalculateSpawnPositions(int playerCount)
        {
            var positions = new float3[playerCount];
            int half = GameSettings.MapHalfSize;
            float spawnRadius = half * 0.7f; // Spawn at 70% from center

            for (int i = 0; i < playerCount; i++)
            {
                float angle = (i / (float)playerCount) * math.PI * 2f;
                positions[i] = new float3(
                    math.cos(angle) * spawnRadius,
                    0,
                    math.sin(angle) * spawnRadius
                );
            }

            return positions;
        }
    }
}