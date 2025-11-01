// Fixed Archer.cs - Adds missing ArcherTag to ECB version
// Key change: ecb.AddComponent<ArcherTag>(e) added to EntityCommandBuffer version

using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace TheWaningBorder.Humans
{
    public static class Archer
    {
        // ECB version - FIXED: Now includes ArcherTag
        public static Entity Create(EntityCommandBuffer ecb, float3 pos, Faction fac)
        {
            var e = ecb.CreateEntity();
            
            // Add all components
            ecb.AddComponent(e, new PresentationId { Id = 202 });
            ecb.AddComponent(e, LocalTransform.FromPositionRotationScale(pos, quaternion.identity, 1f));
            ecb.AddComponent(e, new FactionTag { Value = fac });
            ecb.AddComponent(e, new UnitTag { Class = UnitClass.Ranged });
            
            // FIX: Add ArcherTag so ProcessRangedCombat can find this archer!
            ecb.AddComponent<ArcherTag>(e);
            
            // PLACEHOLDER values - will be overwritten by JSON stats in BarracksTrainingSystem
            ecb.AddComponent(e, new Health { Value = 1, Max = 1 });
            ecb.AddComponent(e, new MoveSpeed { Value = 1f });
            ecb.AddComponent(e, new Damage { Value = 1 });
            ecb.AddComponent(e, new LineOfSight { Radius = 1f });
            
            // Archer-specific state - PLACEHOLDER values
            // Real values (MinRange, MaxRange) set by BarracksTrainingSystem from JSON
            ecb.AddComponent(e, new ArcherState
            {
                CurrentTarget = Entity.Null,
                AimTimer = 0,
                AimTimeRequired = 0.5f,
                CooldownTimer = 0,
                MinRange = 0f,          // ← Set by BarracksTrainingSystem from JSON minAttackRange
                MaxRange = 0f,          // ← Set by BarracksTrainingSystem from JSON attackRange
                HeightRangeMod = 4f,
                IsRetreating = 0,
                IsFiring = 0
            });
            
            ecb.AddComponent(e, new Target { Value = Entity.Null });
            
            return e;
        }

        // EntityManager version - for backward compatibility
        // Already has ArcherTag, so no changes needed here
        public static Entity Create(EntityManager em, float3 pos, Faction fac)
        {
            var e = em.CreateEntity(
                typeof(PresentationId),
                typeof(LocalTransform),
                typeof(FactionTag),
                typeof(UnitTag),
                typeof(ArcherTag),      // Already present in this version
                typeof(Health),
                typeof(MoveSpeed),
                typeof(Damage),
                typeof(LineOfSight),
                typeof(ArcherState),
                typeof(Target)
            );

            em.SetComponentData(e, new PresentationId { Id = 202 });
            em.SetComponentData(e, LocalTransform.FromPositionRotationScale(pos, quaternion.identity, 1f));
            em.SetComponentData(e, new FactionTag { Value = fac });
            em.SetComponentData(e, new UnitTag { Class = UnitClass.Ranged });

            // Load from JSON if available
            if (TechTreeDB.Instance != null && TechTreeDB.Instance.TryGetUnit("Archer", out var udef))
            {
                em.SetComponentData(e, new Health { Value = (int)udef.hp, Max = (int)udef.hp });
                em.SetComponentData(e, new MoveSpeed { Value = udef.speed });
                em.SetComponentData(e, new Damage { Value = (int)udef.damage });
                em.SetComponentData(e, new LineOfSight { Radius = udef.lineOfSight });
                
                em.SetComponentData(e, new ArcherState
                {
                    CurrentTarget = Entity.Null,
                    AimTimer = 0,
                    AimTimeRequired = 0.5f,
                    CooldownTimer = 0,
                    MinRange = udef.minAttackRange,
                    MaxRange = udef.attackRange,
                    HeightRangeMod = 4f,
                    IsRetreating = 0,
                    IsFiring = 0
                });
            }
            else
            {
                // Fallback if JSON not loaded
                em.SetComponentData(e, new Health { Value = 80, Max = 80 });
                em.SetComponentData(e, new MoveSpeed { Value = 3.5f });
                em.SetComponentData(e, new Damage { Value = 15 });
                em.SetComponentData(e, new LineOfSight { Radius = 20f });
                
                em.SetComponentData(e, new ArcherState
                {
                    CurrentTarget = Entity.Null,
                    AimTimer = 0,
                    AimTimeRequired = 0.5f,
                    CooldownTimer = 0,
                    MinRange = 6f,
                    MaxRange = 18f,
                    HeightRangeMod = 4f,
                    IsRetreating = 0,
                    IsFiring = 0
                });
            }

            em.SetComponentData(e, new Target { Value = Entity.Null });

            return e;
        }
    }
}