// Fixed Swordsman.cs - Loads stats from JSON in EntityManager version
// Key change: EntityManager version now loads actual stats from TechTreeDB

using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace TheWaningBorder.Humans
{
    public class Swordsman
    {
        // ECB version - creates entity structure with PLACEHOLDER values
        // Real stats are applied by BarracksTrainingSystem from JSON
        public static Entity Create(EntityCommandBuffer ecb, float3 pos, Faction fac)
        {
            var e = ecb.CreateEntity();
            
            // Add all components
            ecb.AddComponent(e, new PresentationId { Id = 201 });
            ecb.AddComponent(e, LocalTransform.FromPositionRotationScale(pos, quaternion.identity, 1f));
            ecb.AddComponent(e, new FactionTag { Value = fac });
            ecb.AddComponent(e, new UnitTag { Class = UnitClass.Melee });
            
            // PLACEHOLDER values - will be overwritten by JSON stats
            ecb.AddComponent(e, new Health { Value = 1, Max = 1 });
            ecb.AddComponent(e, new MoveSpeed { Value = 1f });
            ecb.AddComponent(e, new Damage { Value = 1 });
            ecb.AddComponent(e, new LineOfSight { Radius = 1f });
            ecb.AddComponent(e, new Target { Value = Entity.Null });
            
            return e;
        }

        // EntityManager version - FIXED: Now loads stats from JSON
        // Used for initial spawn units
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
                typeof(Target)
            );

            em.SetComponentData(e, new PresentationId { Id = 201 });
            em.SetComponentData(e, LocalTransform.FromPositionRotationScale(pos, quaternion.identity, 1f));
            em.SetComponentData(e, new FactionTag { Value = fac });
            em.SetComponentData(e, new UnitTag { Class = UnitClass.Melee });

            // FIX: Load actual stats from JSON instead of using placeholders
            if (TechTreeDB.Instance != null && TechTreeDB.Instance.TryGetUnit("Swordsman", out var udef))
            {
                em.SetComponentData(e, new Health { Value = (int)udef.hp, Max = (int)udef.hp });
                em.SetComponentData(e, new MoveSpeed { Value = udef.speed });
                em.SetComponentData(e, new Damage { Value = (int)udef.damage });
                em.SetComponentData(e, new LineOfSight { Radius = udef.lineOfSight });
            }
            else
            {
                // Fallback if JSON not loaded yet
                UnityEngine.Debug.LogWarning("[Swordsman] TechTreeDB not available, using fallback stats");
                em.SetComponentData(e, new Health { Value = 100, Max = 100 });
                em.SetComponentData(e, new MoveSpeed { Value = 4f });
                em.SetComponentData(e, new Damage { Value = 10 });
                em.SetComponentData(e, new LineOfSight { Radius = 12f });
            }

            em.SetComponentData(e, new Target { Value = Entity.Null });
            
            return e;
        }
    }
}