// File: Assets/Scripts/Entities/Buildings/BuildingFactory.cs
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace TheWaningBorder.Entities
{
    /// <summary>
    /// Unified factory for creating all building types.
    /// 
    /// Provides a single entry point for spawning buildings by ID,
    /// with automatic stat loading from TechTreeDB.
    /// 
    /// Usage:
    ///   Entity building = BuildingFactory.Create(em, "Barracks", position, faction);
    /// </summary>
    public static class BuildingFactory
    {
        /// <summary>
        /// Create a building by its ID string.
        /// Automatically loads stats from TechTreeDB if available.
        /// </summary>
        /// <param name="em">EntityManager</param>
        /// <param name="buildingId">Building type: "Hall", "Barracks", "Hut", "GatherersHut", etc.</param>
        /// <param name="position">World position to spawn at</param>
        /// <param name="faction">Faction the building belongs to</param>
        /// <returns>Created entity</returns>
        public static Entity Create(EntityManager em, string buildingId, float3 position, Faction faction)
        {
            return buildingId switch
            {
                "Hall" => Hall.Create(em, position, faction),
                "Barracks" => Barracks.Create(em, position, faction),
                "Hut" => Hut.Create(em, position, faction),
                "GatherersHut" => GatherersHut.Create(em, position, faction),
                "TempleOfRidan" => CreateGenericBuilding(em, "TempleOfRidan", position, faction, 800f, 16f, 1.8f, new TempleTag()),
                "VaultOfAlmierra" => CreateGenericBuilding(em, "VaultOfAlmierra", position, faction, 1200f, 14f, 2.0f, new VaultTag()),
                "FiendstoneKeep" => CreateFiendstoneKeep(em, position, faction),
                _ => CreateDefault(em, buildingId, position, faction)
            };
        }

        /// <summary>
        /// Create a building using EntityCommandBuffer for deferred creation.
        /// </summary>
        public static Entity Create(EntityCommandBuffer ecb, string buildingId, float3 position, Faction faction)
        {
            return buildingId switch
            {
                "Hall" => Hall.Create(ecb, position, faction),
                "Barracks" => Barracks.Create(ecb, position, faction),
                "Hut" => Hut.Create(ecb, position, faction),
                "GatherersHut" => GatherersHut.Create(ecb, position, faction),
                _ => CreateDefault(ecb, buildingId, position, faction)
            };
        }

        /// <summary>
        /// Get the PresentationId for a building type.
        /// </summary>
        public static int GetPresentationId(string buildingId)
        {
            return buildingId switch
            {
                "Hall" => 100,
                "Hut" => 102,
                "GatherersHut" => 500,
                "Barracks" => 510,
                "TempleOfRidan" => 520,
                "VaultOfAlmierra" => 530,
                "FiendstoneKeep" => 540,
                _ => 100
            };
        }

        /// <summary>
        /// Get population provided by a building type.
        /// </summary>
        public static int GetPopulationProvided(string buildingId)
        {
            return buildingId switch
            {
                "Hall" => 20,
                "Hut" => 10,
                _ => 0
            };
        }

        /// <summary>
        /// Check if building type can train units.
        /// </summary>
        public static bool CanTrainUnits(string buildingId)
        {
            return buildingId switch
            {
                "Hall" => true,
                "Barracks" => true,
                _ => false
            };
        }

        /// <summary>
        /// Create a generic building with specified tag.
        /// </summary>
        private static Entity CreateGenericBuilding<T>(EntityManager em, string buildingId, float3 position, 
            Faction faction, float defaultHp, float defaultLoS, float defaultRadius, T tag) where T : unmanaged, IComponentData
        {
            float hp = defaultHp;
            float los = defaultLoS;
            float radius = defaultRadius;

            if (TechTreeDB.Instance != null && TechTreeDB.Instance.TryGetBuilding(buildingId, out var def))
            {
                if (def.hp > 0) hp = def.hp;
                if (def.lineOfSight > 0) los = def.lineOfSight;
                if (def.radius > 0) radius = def.radius;
            }

            var entity = em.CreateEntity(
                typeof(PresentationId),
                typeof(LocalTransform),
                typeof(FactionTag),
                typeof(BuildingTag),
                typeof(Health),
                typeof(LineOfSight),
                typeof(Radius)
            );

            em.SetComponentData(entity, new PresentationId { Id = GetPresentationId(buildingId) });
            em.SetComponentData(entity, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
            em.SetComponentData(entity, new FactionTag { Value = faction });
            em.SetComponentData(entity, new BuildingTag { IsBase = 0 });
            em.SetComponentData(entity, new Health { Value = (int)hp, Max = (int)hp });
            em.SetComponentData(entity, new LineOfSight { Radius = los });
            em.SetComponentData(entity, new Radius { Value = radius });
            
            // Add specific tag
            em.AddComponentData(entity, tag);

            return entity;
        }

        /// <summary>
        /// Create Fiendstone Keep (Feraldis capital).
        /// </summary>
        private static Entity CreateFiendstoneKeep(EntityManager em, float3 position, Faction faction)
        {
            float hp = 2000f;
            float los = 18f;
            float radius = 2.4f;

            if (TechTreeDB.Instance != null && TechTreeDB.Instance.TryGetBuilding("FiendstoneKeep", out var def))
            {
                if (def.hp > 0) hp = def.hp;
                if (def.lineOfSight > 0) los = def.lineOfSight;
                if (def.radius > 0) radius = def.radius;
            }

            var entity = em.CreateEntity(
                typeof(PresentationId),
                typeof(LocalTransform),
                typeof(FactionTag),
                typeof(BuildingTag),
                typeof(Health),
                typeof(LineOfSight),
                typeof(Radius),
                typeof(PopulationProvider)
            );

            em.SetComponentData(entity, new PresentationId { Id = 540 });
            em.SetComponentData(entity, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
            em.SetComponentData(entity, new FactionTag { Value = faction });
            em.SetComponentData(entity, new BuildingTag { IsBase = 1 }); // Is a base building
            em.SetComponentData(entity, new Health { Value = (int)hp, Max = (int)hp });
            em.SetComponentData(entity, new LineOfSight { Radius = los });
            em.SetComponentData(entity, new Radius { Value = radius });
            em.SetComponentData(entity, new PopulationProvider { Amount = 20 });

            return entity;
        }

        /// <summary>
        /// Default building creation for unknown types.
        /// </summary>
        private static Entity CreateDefault(EntityManager em, string buildingId, float3 position, Faction faction)
        {
            UnityEngine.Debug.LogWarning($"[BuildingFactory] Unknown building type '{buildingId}', creating generic structure");
            
            var entity = em.CreateEntity(
                typeof(PresentationId),
                typeof(LocalTransform),
                typeof(FactionTag),
                typeof(BuildingTag),
                typeof(Health),
                typeof(LineOfSight),
                typeof(Radius)
            );

            em.SetComponentData(entity, new PresentationId { Id = 100 });
            em.SetComponentData(entity, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
            em.SetComponentData(entity, new FactionTag { Value = faction });
            em.SetComponentData(entity, new BuildingTag { IsBase = 0 });
            em.SetComponentData(entity, new Health { Value = 500, Max = 500 });
            em.SetComponentData(entity, new LineOfSight { Radius = 10f });
            em.SetComponentData(entity, new Radius { Value = 1.5f });

            return entity;
        }

        private static Entity CreateDefault(EntityCommandBuffer ecb, string buildingId, float3 position, Faction faction)
        {
            UnityEngine.Debug.LogWarning($"[BuildingFactory] Unknown building type '{buildingId}', creating generic structure");
            
            var entity = ecb.CreateEntity();

            ecb.AddComponent(entity, new PresentationId { Id = 100 });
            ecb.AddComponent(entity, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
            ecb.AddComponent(entity, new FactionTag { Value = faction });
            ecb.AddComponent(entity, new BuildingTag { IsBase = 0 });
            ecb.AddComponent(entity, new Health { Value = 500, Max = 500 });
            ecb.AddComponent(entity, new LineOfSight { Radius = 10f });
            ecb.AddComponent(entity, new Radius { Value = 1.5f });

            return entity;
        }
    }

    // Building tag components
    public struct TempleTag : IComponentData { }
    public struct VaultTag : IComponentData { }
}