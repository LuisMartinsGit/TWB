// Fixed Builder.cs - Loads stats from JSON in EntityManager version
// Key change: Already loads from JSON, no changes needed, but documenting for completeness

using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace TheWaningBorder.Humans
{
    public static class Builder
    {
        // Defaults if JSON is missing
        private const float DefaultHP = 60f;
        private const float DefaultSpeed = 4f;
        private const float DefaultDamage = 2f;
        private const float DefaultLoS = 12f;

        // EntityManager version - Already loads from JSON correctly
        public static Entity Create(EntityManager em, float3 pos, Faction fac)
        {
            // Try to fetch the "Builder" unit from the tech DB
            float hp = DefaultHP;
            float speed = DefaultSpeed;
            float damage = DefaultDamage;
            float los = DefaultLoS;

            if (TechTreeDB.Instance != null && TechTreeDB.Instance.TryGetUnit("Builder", out var def))
            {
                if (def.hp > 0) hp = def.hp;
                if (def.speed > 0) speed = def.speed;
                if (def.damage > 0) damage = def.damage;
                if (def.lineOfSight > 0) los = def.lineOfSight;
            }

            var e = em.CreateEntity(
                typeof(PresentationId),
                typeof(LocalTransform),
                typeof(FactionTag),
                typeof(UnitTag),
                typeof(Health),
                typeof(MoveSpeed),
                typeof(Damage),
                typeof(CanBuild),
                typeof(LineOfSight)
            );

            em.SetComponentData(e, new PresentationId { Id = 200 });
            em.SetComponentData(e, LocalTransform.FromPositionRotationScale(pos, quaternion.identity, 1f));
            em.SetComponentData(e, new FactionTag { Value = fac });
            em.SetComponentData(e, new UnitTag { Class = UnitClass.Economy });

            em.SetComponentData(e, new Health { Value = (int)hp, Max = (int)hp });
            em.SetComponentData(e, new MoveSpeed { Value = speed });
            em.SetComponentData(e, new Damage { Value = (int)damage });
            em.SetComponentData(e, new CanBuild { Value = true });
            em.SetComponentData(e, new LineOfSight { Radius = los });

            return e;
        }
    }
}