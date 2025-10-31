// Swordsman.cs - NO HARDCODED VALUES VERSION
// Creates entity structure only - all stats loaded from JSON by BarracksTrainingSystem
// Replace your Swordsman.cs with this

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

        // LEGACY EntityManager version - for backward compatibility
        // Also uses placeholder values - real stats from JSON
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

            // PLACEHOLDER values - will be overwritten by JSON stats
            em.SetComponentData(e, new Health { Value = 1, Max = 1 });
            em.SetComponentData(e, new MoveSpeed { Value = 1f });
            em.SetComponentData(e, new Damage { Value = 1 });
            em.SetComponentData(e, new LineOfSight { Radius = 1f });
            em.SetComponentData(e, new Target { Value = Entity.Null });
            
            return e;
        }
    }
}