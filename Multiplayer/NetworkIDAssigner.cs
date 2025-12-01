// Assets/Scripts/Multiplayer/NetworkIdAssigner.cs
// Assigns unique network IDs to all entities for lockstep synchronization
using UnityEngine;
using Unity.Entities;
using Unity.Collections;

namespace TheWaningBorder.Multiplayer
{
    /// <summary>
    /// Assigns unique NetworkId to all units and buildings.
    /// This runs early in the simulation to ensure all entities have IDs before
    /// any commands are issued.
    /// 
    /// CRITICAL: NetworkIds must be assigned deterministically (same order on all clients)
    /// so entities get the same IDs everywhere.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class NetworkIdAssignerSystem : SystemBase
    {
        private int _nextNetworkId = 1;
        private bool _initialized = false;

        protected override void OnCreate()
        {
            // Only run in multiplayer
            if (!GameSettings.IsMultiplayer)
            {
                Enabled = false;
                return;
            }
        }

        protected override void OnUpdate()
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Assign IDs to units without NetworkedEntity component
            foreach (var (unitTag, factionTag, entity) in 
                SystemAPI.Query<RefRO<UnitTag>, RefRO<FactionTag>>()
                .WithNone<NetworkedEntity>()
                .WithEntityAccess())
            {
                int id = _nextNetworkId++;
                ecb.AddComponent(entity, new NetworkedEntity { NetworkId = id });
                
                if (!_initialized)
                    Debug.Log($"[NetworkId] Assigned ID {id} to unit of faction {factionTag.ValueRO.Value}");
            }

            // Assign IDs to buildings without NetworkedEntity component
            foreach (var (buildingTag, factionTag, entity) in 
                SystemAPI.Query<RefRO<BuildingTag>, RefRO<FactionTag>>()
                .WithNone<NetworkedEntity>()
                .WithEntityAccess())
            {
                int id = _nextNetworkId++;
                ecb.AddComponent(entity, new NetworkedEntity { NetworkId = id });
                
                if (!_initialized)
                    Debug.Log($"[NetworkId] Assigned ID {id} to building of faction {factionTag.ValueRO.Value}");
            }

            // Assign IDs to resource nodes (iron deposits, etc)
            foreach (var (ironTag, entity) in 
                SystemAPI.Query<RefRO<TheWaningBorder.AI.IronMineTag>>()
                .WithNone<NetworkedEntity>()
                .WithEntityAccess())
            {
                int id = _nextNetworkId++;
                ecb.AddComponent(entity, new NetworkedEntity { NetworkId = id });
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();

            _initialized = true;
        }

        /// <summary>
        /// Get the next available network ID (used when spawning new entities)
        /// </summary>
        public int AllocateNetworkId()
        {
            return _nextNetworkId++;
        }

        /// <summary>
        /// Find entity by network ID
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

    /// <summary>
    /// Helper class to find entities by network ID from MonoBehaviours
    /// </summary>
    public static class NetworkEntityLookup
    {
        public static Entity FindByNetworkId(int networkId)
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return Entity.Null;

            var em = world.EntityManager;
            var query = em.CreateEntityQuery(typeof(NetworkedEntity));
            var entities = query.ToEntityArray(Allocator.Temp);
            var networkIds = query.ToComponentDataArray<NetworkedEntity>(Allocator.Temp);

            Entity result = Entity.Null;
            for (int i = 0; i < entities.Length; i++)
            {
                if (networkIds[i].NetworkId == networkId)
                {
                    result = entities[i];
                    break;
                }
            }

            entities.Dispose();
            networkIds.Dispose();

            return result;
        }

        public static int GetNetworkId(Entity entity)
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return -1;

            var em = world.EntityManager;
            if (!em.HasComponent<NetworkedEntity>(entity)) return -1;

            return em.GetComponentData<NetworkedEntity>(entity).NetworkId;
        }
    }
}