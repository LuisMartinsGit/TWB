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
        // ECB version - creates entity structure with PLACEHOLDER values
        // Real stats are applied by BarracksTrainingSystem from JSON
        public static Entity Create(EntityCommandBuffer ecb, float3 pos, Faction fac)
        {
            var e = ecb.CreateEntity();
            
            // Add all components
            ecb.AddComponent(e, new PresentationId { Id = 202 });
            ecb.AddComponent(e, LocalTransform.FromPositionRotationScale(pos, quaternion.identity, 1f));
            ecb.AddComponent(e, new FactionTag { Value = fac });
            ecb.AddComponent(e, new UnitTag { Class = UnitClass.Ranged });
            ecb.AddComponent(e, new ArcherTag());
            
            // PLACEHOLDER values - will be overwritten by JSON stats
            ecb.AddComponent(e, new Health { Value = 1, Max = 1 });
            ecb.AddComponent(e, new MoveSpeed { Value = 1f });
            ecb.AddComponent(e, new Damage { Value = 1 });
            ecb.AddComponent(e, new LineOfSight { Radius = 1f });
            
            // Archer-specific state - PLACEHOLDER values
            ecb.AddComponent(e, new ArcherState
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
            
            ecb.AddComponent(e, new Target { Value = Entity.Null });
            
            // Radius for collision/spacing (archers same size as melee)
            ecb.AddComponent(e, new Radius { Value = 0.5f });
            
            return e;
        }

        // LEGACY EntityManager version - for backward compatibility
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