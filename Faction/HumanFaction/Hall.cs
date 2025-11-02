using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace TheWaningBorder.Humans
{
    public class Hall
    {
        public static Entity Create(EntityManager em, float3 pos, Faction fac)
        {
            var e = em.CreateEntity(
                typeof(PresentationId),
                typeof(LocalTransform),
                typeof(FactionTag),
                typeof(BuildingTag),
                typeof(Health),
                typeof(SuppliesIncome),
                typeof(TrainingState),      // NEW: Allow training units
                typeof(LineOfSight)
            );

            em.SetComponentData(e, new PresentationId { Id = 100 }); // debug id
            em.SetComponentData(e, LocalTransform.FromPositionRotationScale(pos, quaternion.identity, .4f));
            em.SetComponentData(e, new FactionTag { Value = fac });
            em.SetComponentData(e, new BuildingTag { IsBase = 1 });     // Era 1 Hall for view resolver
            em.SetComponentData(e, new Health { Value = 2400, Max = 2400 });
            em.SetComponentData(e, new SuppliesIncome { PerMinute = 180 });
            em.SetComponentData(e, new LineOfSight { Radius = 35f });
            
            // NEW: Training system components
            em.SetComponentData(e, new TrainingState { Busy = 0, Remaining = 0 });
            em.AddBuffer<TrainQueueItem>(e); // Empty training queue

            // Ensure faction/culture is Era 1 (no culture) at start
            if (!em.HasComponent<FactionProgress>(e))
                em.AddComponentData(e, new FactionProgress { Culture = Cultures.None });

            return e;
        }
    }
}