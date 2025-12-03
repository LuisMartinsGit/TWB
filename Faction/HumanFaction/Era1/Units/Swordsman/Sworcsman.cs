// Swordsman.cs - FIXED VERSION
// FIX: Renamed from Sworcsman.cs (typo)
// Creates entity with Radius for collision/spacing
// Melee infantry unit

using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace TheWaningBorder.Humans
{
    public class Swordsman
    {
        public static Entity Create(EntityManager em, float3 pos, Faction fac)
        {
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
                typeof(Radius)
            );

            em.SetComponentData(e, new PresentationId { Id = 201 });
            em.SetComponentData(e, LocalTransform.FromPositionRotationScale(pos, quaternion.identity, 1f));
            em.SetComponentData(e, new FactionTag { Value = fac });
            em.SetComponentData(e, new UnitTag { Class = UnitClass.Melee });

            // PLACEHOLDER values - will be overwritten by JSON stats
            em.SetComponentData(e, new Health { Value = 1, Max = 1 });
            em.SetComponentData(e, new MoveSpeed { Value = 1f });
            em.SetComponentData(e, new Damage { Value = 1 });
            em.SetComponentData(e, new LineOfSight { Radius = 1f });
            em.SetComponentData(e, new Target { Value = Entity.Null });

            // Radius for collision/spacing
            em.SetComponentData(e, new Radius { Value = 0.5f });

            return e;
        }
    }
}