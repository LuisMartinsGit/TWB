using Unity.Entities;
using Unity.NetCode;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using UnityEngine;

namespace TheWaningBorder.Multiplayer.Systems
{
    /// <summary>
    /// Server-side system that handles spawning of networked entities (units, buildings).
    /// Assigns network IDs and marks entities as ghosts for replication.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class GhostSpawnSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            // This system is primarily called manually when spawning entities
            // For example, when the game starts, or when a building produces a unit

            // Auto-assign network IDs to any entity that has GhostOwner but not NetworkedEntity
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            
            foreach (var (ghostOwner, entity) in SystemAPI.Query<RefRO<GhostOwner>>().WithNone<NetworkedEntity>().WithEntityAccess())
            {
                int networkId = AllocateNetworkId();
                ecb.AddComponent(entity, new NetworkedEntity { NetworkId = networkId });
                Debug.Log($"[GhostSpawn] Auto-assigned network ID {networkId} to ghost entity");
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }

        /// <summary>
        /// Spawn a unit on the server and mark it for replication.
        /// TODO: Integrate with actual unit factories once they're identified.
        /// </summary>
        public Entity SpawnUnit(string unitType, Faction faction, float3 position)
        {
            // Placeholder - actual unit creation should use existing factories
            // For now, create a basic entity as a stub
            Entity entity = EntityManager.CreateEntity(
                typeof(LocalTransform),
                typeof(FactionTag),
                typeof(UnitTag),
                typeof(Health)
            );

            EntityManager.SetComponentData(entity, LocalTransform.FromPosition(position));
            EntityManager.SetComponentData(entity, new FactionTag { Value = faction });
            EntityManager.SetComponentData(entity, new Health { Value = 100, Max = 100 });

            // Mark as networked entity
            int networkId = AllocateNetworkId();
            EntityManager.AddComponentData(entity, new NetworkedEntity { NetworkId = networkId });
            
            // Add GhostOwner component to enable replication
            EntityManager.AddComponentData(entity, new GhostOwner { NetworkId = networkId });

            Debug.Log($"[GhostSpawn] Spawned {unitType} with network ID {networkId}");
            return entity;
        }

        /// <summary>
        /// Spawn a building on the server and mark it for replication.
        /// TODO: Integrate with actual building factories once they're identified.
        /// </summary>
        public Entity SpawnBuilding(string buildingType, Faction faction, float3 position)
        {
            // Placeholder - actual building creation should use existing factories
            Entity entity = EntityManager.CreateEntity(
                typeof(LocalTransform),
                typeof(FactionTag),
                typeof(Health)
            );

            EntityManager.SetComponentData(entity, LocalTransform.FromPosition(position));
            EntityManager.SetComponentData(entity, new FactionTag { Value = faction });
            EntityManager.SetComponentData(entity, new Health { Value = 500, Max = 500 });

            // Mark as networked entity
            int networkId = AllocateNetworkId();
            EntityManager.AddComponentData(entity, new NetworkedEntity { NetworkId = networkId });
            
            // Add GhostOwner component to enable replication
            EntityManager.AddComponentData(entity, new GhostOwner { NetworkId = networkId });

            Debug.Log($"[GhostSpawn] Spawned {buildingType} with network ID {networkId}");
            return entity;
        }

        private int AllocateNetworkId()
        {
            RefRW<NetworkIdAllocator> allocator = SystemAPI.GetSingletonRW<NetworkIdAllocator>();
            int id = allocator.ValueRO.NextId;
            allocator.ValueRW.NextId++;
            return id;
        }

        /// <summary>
        /// Find entity by network ID.
        /// </summary>
        public Entity FindEntityByNetworkId(int networkId)
        {
            foreach (var (netEntity, entity) in SystemAPI.Query<RefRO<NetworkedEntity>>().WithEntityAccess())
            {
                if (netEntity.ValueRO.NetworkId == networkId)
                    return entity;
            }
            return Entity.Null;
        }
    }
}
