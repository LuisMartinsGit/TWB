// Swordsman.cs - FIXED VERSION
// This version uses EntityCommandBuffer to defer structural changes
// Replace your Swordsman.cs with this

using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace TheWaningBorder.Humans
{
    public class Swordsman
    {
        // NEW METHOD - uses EntityCommandBuffer
        public static Entity Create(EntityCommandBuffer ecb, float3 pos, Faction fac)
        {
            var e = ecb.CreateEntity();
            
            // Add all components
            ecb.AddComponent(e, new PresentationId { Id = 201 });
            ecb.AddComponent(e, LocalTransform.FromPositionRotationScale(pos, quaternion.identity, 1f));
            ecb.AddComponent(e, new FactionTag { Value = fac });
            ecb.AddComponent(e, new UnitTag { Class = UnitClass.Melee });
            
            // Baseline melee stats
            ecb.AddComponent(e, new Health { Value = 120, Max = 120 });
            ecb.AddComponent(e, new MoveSpeed { Value = 5.5f });
            ecb.AddComponent(e, new Damage { Value = 10 });
            
            // Optional components
            ecb.AddComponent(e, new LineOfSight { Radius = 12f });
            ecb.AddComponent(e, new Target { Value = Entity.Null });
            
            return e;
        }

        // LEGACY METHOD - keep for backward compatibility if needed
        // But don't use during iteration!
        public static Entity Create(EntityManager em, float3 pos, Faction fac)
        {
            var e = em.CreateEntity(
                typeof(PresentationId),
                typeof(LocalTransform),
                typeof(FactionTag),
                typeof(UnitTag),
                typeof(Health),
                typeof(MoveSpeed),
                typeof(Damage)
            );

            em.SetComponentData(e, new PresentationId { Id = 201 });
            em.SetComponentData(e, LocalTransform.FromPositionRotationScale(pos, quaternion.identity, 1f));
            em.SetComponentData(e, new FactionTag { Value = fac });
            em.SetComponentData(e, new UnitTag { Class = UnitClass.Melee });

            em.SetComponentData(e, new Health { Value = 120, Max = 120 });
            em.SetComponentData(e, new MoveSpeed { Value = 5.5f });
            em.SetComponentData(e, new Damage { Value = 10 });

            if (!em.HasComponent<LineOfSight>(e))
                em.AddComponentData(e, new LineOfSight { Radius = 12f });

            if (!em.HasComponent<Target>(e)) 
                em.AddComponentData(e, new Target { Value = Entity.Null });
            
            return e;
        }
    }
}