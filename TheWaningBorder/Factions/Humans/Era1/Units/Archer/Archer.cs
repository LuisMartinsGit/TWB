using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace TheWaningBorder.Factions.Humans.Era1.Units
{
    public static class Archer
    {
        public static int GetPopulationCost()
        {
            HumanTech.EnsureTechTreeDB();
            var tech = HumanTech.Instance;
            tech?.LoadFromJsonIfNeeded();
            if (tech != null && tech.TryGetUnit("Archer", out var def)) return def.popCost;
            else return 99999;
        }
        public static Entity Create(EntityManager em, float3 pos, Faction fac)
        {
            // Ensure the tech DB exists and is loaded before reading.

            float hp;
            float speed;
            float damage;
            float los;
            float minRange;
            float range;

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

            HumanTech.EnsureTechTreeDB();
            var tech = HumanTech.Instance;
            tech?.LoadFromJsonIfNeeded();

            if (tech != null && tech.TryGetUnit("Archer", out var def))
            {
                hp = def.hp;
                speed = def.speed;
                damage = def.damage;
                los = def.lineOfSight;
                minRange = def.minAttackRange;
                range = def.attackRange;

                em.SetComponentData(e, new PresentationId { Id = 202 });
                em.SetComponentData(e, LocalTransform.FromPositionRotationScale(pos, quaternion.identity, 1f));
                em.SetComponentData(e, new FactionTag { Value = fac });
                em.SetComponentData(e, new UnitTag { Class = UnitClass.Ranged });

                em.SetComponentData(e, new Health { Value = (int)hp, Max = (int)hp });
                em.SetComponentData(e, new MoveSpeed { Value = speed });
                em.SetComponentData(e, new Damage { Value = (int)damage });
                em.SetComponentData(e, new LineOfSight { Radius = los });
                em.SetComponentData(e, new Radius { Value = 0.5f });

                em.SetComponentData(e, new MinAttackRange { Value = minRange });
                em.SetComponentData(e, new AttackRange { Value = range });

                em.SetComponentData(e, new AttackCooldown { Cooldown = 1.5f, Timer = 0f });

            }

            return e;
        }
    }
}
