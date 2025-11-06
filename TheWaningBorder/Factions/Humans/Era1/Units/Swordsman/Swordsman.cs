using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace TheWaningBorder.Factions.Humans.Era1.Units
{
    public class Swordsman
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

            // Try to fetch the "Swordsman" unit from the tech DB
            float hp;
            float speed;
            float damage;
            float los;
            float range;

            var e = em.CreateEntity(
                typeof(PresentationId),
                typeof(LocalTransform),
                typeof(FactionTag),
                typeof(UnitTag),
                typeof(Health),
                typeof(MoveSpeed),
                typeof(Damage),
                typeof(LineOfSight),
                typeof(Target),
                typeof(AttackRange),
                typeof(Radius)
            );

            HumanTech.EnsureTechTreeDB();
            var tech = HumanTech.Instance;
            tech?.LoadFromJsonIfNeeded();

            if (HumanTech.Instance != null && HumanTech.Instance.TryGetUnit("Swordsman", out var def))
            {
                hp = def.hp;
                speed = def.speed;
                damage = def.damage;
                los = def.lineOfSight;
                range = def.attackRange;

                em.SetComponentData(e, new PresentationId { Id = 201 });
                em.SetComponentData(e, LocalTransform.FromPositionRotationScale(pos, quaternion.identity, 1f));
                em.SetComponentData(e, new FactionTag { Value = fac });
                em.SetComponentData(e, new UnitTag { Class = UnitClass.Melee });

                em.SetComponentData(e, new Health { Value = (int)hp, Max = (int)hp });
                em.SetComponentData(e, new MoveSpeed { Value = speed });
                em.SetComponentData(e, new Damage { Value = (int)damage });
                em.SetComponentData(e, new LineOfSight { Radius = los });
                em.SetComponentData(e, new Radius { Value = 0.5f });

                em.SetComponentData(e, new Target { Value = Entity.Null });
                em.SetComponentData(e, new AttackRange { Value = range });
                
            }
            
            return e;
        }
    }
}