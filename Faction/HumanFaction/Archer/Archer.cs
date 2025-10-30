// Humans/Archer.cs
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
                typeof(Health),
                typeof(MoveSpeed),
                typeof(Damage),
                typeof(ArcherState)  // NEW: Archer AI state
            );

            em.SetComponentData(e, new PresentationId { Id = 202 });
            em.SetComponentData(e, LocalTransform.FromPositionRotationScale(pos, quaternion.identity, 1f));
            em.SetComponentData(e, new FactionTag { Value = fac });
            em.SetComponentData(e, new UnitTag { Class = UnitClass.Ranged });

            // Baselines; we'll overwrite from TechTreeDB after spawn
            em.SetComponentData(e, new Health { Value = 90, Max = 90 });
            em.SetComponentData(e, new MoveSpeed { Value = 5.2f });
            em.SetComponentData(e, new Damage { Value = 9 });

            // Archer-specific settings
            em.SetComponentData(e, new ArcherState
            {
                CurrentTarget = Entity.Null,
                AimTimer = 0,
                AimTimeRequired = 0.5f,
                CooldownTimer = 0,
                MinRange = 6f,          // Don't shoot closer than this
                MaxRange = 18f,         // Base shooting range
                HeightRangeMod = 4f,    // +/- 4 units range per height difference
                IsRetreating = 0,
                IsFiring = 0
            });

            if (!em.HasComponent<LineOfSight>(e))
                em.AddComponentData(e, new LineOfSight { Radius = 22f }); // Slightly longer sight than range

            if (!em.HasComponent<Target>(e))
                em.AddComponentData(e, new Target { Value = Entity.Null });

            return e;
        }
    }
}