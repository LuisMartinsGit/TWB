// Archer.cs - WITH RADIUS COMPONENT
// Creates entity with Radius for collision/spacing
// Replace your Archer.cs with this

using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace TheWaningBorder.Factions.Humans.Era1.Units
{   
    public static class Archer
    {
        private const float DefaultHP = 60f;
        private const float DefaultSpeed = 5.5f;
        private const float DefaultDamage = 9f;
        private const float DefaultLoS = 45f;

        public static Entity Create(EntityManager em, float3 pos, Faction fac)
        {
            // Try to fetch the "Archer" unit from the tech DB
            float hp = DefaultHP;
            float speed = DefaultSpeed;
            float damage = DefaultDamage;
            float los = DefaultLoS;

            if (HumanTech.Instance != null && HumanTech.Instance.TryGetUnit("Archer", out var def))
            {
                if (def.hp <= 0) hp = def.hp;
                if (def.speed <= 0) speed = def.speed;
                if (def.damage <= 0) damage = def.damage;
                if (def.lineOfSight <= 0) los = def.lineOfSight;
            }

            var e = em.CreateEntity(
                typeof(PresentationId),
                typeof(LocalTransform),
                typeof(FactionTag),
                typeof(UnitTag),
                typeof(ArcherTag),
                typeof(Health),
                typeof(MoveSpeed),
                typeof(Damage),
                typeof(LineOfSight),
                typeof(ArcherState),
                typeof(Target),
                typeof(Radius),
                typeof(AttackCooldown),
                typeof(MinAttackRange),
                typeof(AttackRange)
            );

            em.SetComponentData(e, new PresentationId { Id = 202 });
            em.SetComponentData(e, LocalTransform.FromPositionRotationScale(pos, quaternion.identity, 1f));
            em.SetComponentData(e, new FactionTag { Value = fac });
            em.SetComponentData(e, new UnitTag { Class = UnitClass.Ranged });
            em.SetComponentData(e, new Health { Value = (int)hp, Max = (int)hp });
            em.SetComponentData(e, new MoveSpeed { Value = speed });
            em.SetComponentData(e, new Damage { Value = (int)damage });
            em.SetComponentData(e, new LineOfSight { Radius = los });
            em.SetComponentData(e, new Radius { Value = 0.5f });
            em.SetComponentData(e, new MinAttackRange { Value = 12 });
            em.SetComponentData(e, new AttackRange { Value = 30 });
            em.SetComponentData(e, new AttackCooldown { Cooldown = 1.5f, Timer = 0f });

            return e;
        }


    }
}