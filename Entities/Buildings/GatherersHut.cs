// File: Assets/Scripts/Entities/Buildings/GatherersHut.cs
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace TheWaningBorder.Entities
{
    /// <summary>
    /// GatherersHut building - resource dropoff point.
    /// Miners deposit gathered resources here.
    /// Also generates passive Supplies income.
    /// </summary>
    public static class GatherersHut
    {
        // Default stats (used if TechTreeDB unavailable)
        private const float DefaultHP = 800f;
        private const float DefaultLoS = 16f;
        private const float DefaultRadius = 2.0f;
        private const int DefaultSuppliesPerMinute = 120;
        private const float DefaultBuildTime = 25f;
        private const int PresentationID = 101;

        /// <summary>
        /// Create GatherersHut using EntityManager.
        /// </summary>
        public static Entity Create(EntityManager em, float3 position, Faction faction)
        {
            // Load stats from TechTreeDB
            float hp = DefaultHP;
            float los = DefaultLoS;
            float radius = DefaultRadius;
            int suppliesPerMin = DefaultSuppliesPerMinute;

            if (TechTreeDB.Instance != null && TechTreeDB.Instance.TryGetBuilding("GatherersHut", out var def))
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
                typeof(GathererHutTag),
                typeof(Health),
                typeof(LineOfSight),
                typeof(Radius),
                typeof(SuppliesIncome)
            );

            em.SetComponentData(entity, new PresentationId { Id = PresentationID });
            em.SetComponentData(entity, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
            em.SetComponentData(entity, new FactionTag { Value = faction });
            em.SetComponentData(entity, new BuildingTag { IsBase = 0 });
            em.SetComponentData(entity, new Health { Value = (int)hp, Max = (int)hp });
            em.SetComponentData(entity, new LineOfSight { Radius = los });
            em.SetComponentData(entity, new Radius { Value = radius });
            em.SetComponentData(entity, new SuppliesIncome { PerMinute = suppliesPerMin });

            return entity;
        }

        /// <summary>
        /// Create GatherersHut using EntityCommandBuffer for deferred creation.
        /// </summary>
        public static Entity Create(EntityCommandBuffer ecb, float3 position, Faction faction)
        {
            // Load stats from TechTreeDB
            float hp = DefaultHP;
            float los = DefaultLoS;
            float radius = DefaultRadius;
            int suppliesPerMin = DefaultSuppliesPerMinute;

            if (TechTreeDB.Instance != null && TechTreeDB.Instance.TryGetBuilding("GatherersHut", out var def))
            {
                if (def.hp > 0) hp = def.hp;
                if (def.lineOfSight > 0) los = def.lineOfSight;
                if (def.radius > 0) radius = def.radius;
            }

            var entity = ecb.CreateEntity();

            ecb.AddComponent(entity, new PresentationId { Id = PresentationID });
            ecb.AddComponent(entity, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
            ecb.AddComponent(entity, new FactionTag { Value = faction });
            ecb.AddComponent(entity, new BuildingTag { IsBase = 0 });
            ecb.AddComponent(entity, new GathererHutTag());
            ecb.AddComponent(entity, new Health { Value = (int)hp, Max = (int)hp });
            ecb.AddComponent(entity, new LineOfSight { Radius = los });
            ecb.AddComponent(entity, new Radius { Value = radius });
            ecb.AddComponent(entity, new SuppliesIncome { PerMinute = suppliesPerMin });

            return entity;
        }

        /// <summary>
        /// Create GatherersHut under construction using EntityCommandBuffer.
        /// </summary>
        public static Entity CreateUnderConstruction(EntityCommandBuffer ecb, float3 position, Faction faction)
        {
            // Load stats from TechTreeDB
            float hp = DefaultHP;
            float los = DefaultLoS;
            float radius = DefaultRadius;
            float buildTime = DefaultBuildTime;

            if (TechTreeDB.Instance != null && TechTreeDB.Instance.TryGetBuilding("GatherersHut", out var def))
            {
                if (def.hp > 0) hp = def.hp;
                if (def.lineOfSight > 0) los = def.lineOfSight;
                if (def.radius > 0) radius = def.radius;
                // Note: BuildingDef doesn't have buildTime, using default
            }

            var entity = ecb.CreateEntity();

            ecb.AddComponent(entity, new PresentationId { Id = PresentationID });
            ecb.AddComponent(entity, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
            ecb.AddComponent(entity, new FactionTag { Value = faction });
            ecb.AddComponent(entity, new BuildingTag { IsBase = 0 });
            ecb.AddComponent(entity, new GathererHutTag());
            ecb.AddComponent(entity, new Health { Value = 1, Max = (int)hp });
            ecb.AddComponent(entity, new LineOfSight { Radius = los });
            ecb.AddComponent(entity, new Radius { Value = radius });
            ecb.AddComponent(entity, new UnderConstruction { Progress = 0f, Total = buildTime });
            ecb.AddComponent(entity, new Buildable { BuildTimeSeconds = buildTime });

            return entity;
        }
    }
}