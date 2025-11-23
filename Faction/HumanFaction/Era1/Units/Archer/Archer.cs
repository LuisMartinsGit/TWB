// Archer.cs - WITH RADIUS COMPONENT
// Creates entity with Radius for collision/spacing
// Replace your Archer.cs with this

using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace TheWaningBorder.Humans
{
    public static class Archer
    {
        public static Entity Create(EntityManager em, float3 pos, Faction fac)
        {
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
                typeof(Radius)
            );

            em.SetComponentData(e, new PresentationId { Id = 202 });
            em.SetComponentData(e, LocalTransform.FromPositionRotationScale(pos, quaternion.identity, 1f));
            em.SetComponentData(e, new FactionTag { Value = fac });
            em.SetComponentData(e, new UnitTag { Class = UnitClass.Ranged });

            // PLACEHOLDER values
            em.SetComponentData(e, new Health { Value = 1, Max = 1 });
            em.SetComponentData(e, new MoveSpeed { Value = 1f });
            em.SetComponentData(e, new Damage { Value = 1 });
            em.SetComponentData(e, new LineOfSight { Radius = 1f });

            em.SetComponentData(e, new ArcherState
            {
                CurrentTarget = Entity.Null,
                AimTimer = 0,
                AimTimeRequired = 0.5f,
                CooldownTimer = 0,
                MinRange = 0f,
                MaxRange = 0f,
                HeightRangeMod = 4f,
                IsRetreating = 0,
                IsFiring = 0
            });

            em.SetComponentData(e, new Target { Value = Entity.Null });
            
            // Radius for collision/spacing
            em.SetComponentData(e, new Radius { Value = 0.5f });

            return e;
        }
    }
}