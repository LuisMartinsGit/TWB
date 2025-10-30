using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace TheWaningBorder.Humans
{
    public static class Hut
    {
        // Defaults if JSON is missing
        private const float DefaultHP  = 600f;
        private const float DefaultLoS = 14f;
        private const float DefaultRadius = 1.6f;

        // Pick an id your presentation system knows how to render
        private const int Presentation = 100; // <-- change if you use a different model id

        public static Entity Create(EntityManager em, float3 pos, Faction fac)
        {
            float hp  = DefaultHP;
            float los = DefaultLoS;
            float radius = DefaultRadius;

            if (TechTreeDB.Instance != null && TechTreeDB.Instance.TryGetBuilding("Hut", out var def))
            {
                if (def.hp > 0) hp = def.hp;
                if (def.lineOfSight > 0) los = def.lineOfSight;
                if (def.radius > 0) radius = def.radius;
            }

            var e = em.CreateEntity(
                typeof(PresentationId),
                typeof(LocalTransform),
                typeof(FactionTag),
                typeof(BuildingTag),
                typeof(HutTag),
                typeof(Health),
                typeof(LineOfSight),
                typeof(Radius),
                typeof(SuppliesIncome)
            );

            em.SetComponentData(e, new PresentationId { Id = Presentation });
            em.SetComponentData(e, LocalTransform.FromPositionRotationScale(pos, quaternion.identity, 1f));
            em.SetComponentData(e, new FactionTag { Value = fac });

            em.SetComponentData(e, new Health { Value = (int)hp, Max = (int)hp });
            em.SetComponentData(e, new LineOfSight { Radius = los });
            em.SetComponentData(e, new Radius { Value = radius });
            em.SetComponentData(e, new SuppliesIncome { PerMinute = 180 });
            // Add any other gameplay components your hut needs here
            // e.g., ResourceDropoff, GatherBoost, ConstructionState, etc.

            return e;
        }
    }
}
