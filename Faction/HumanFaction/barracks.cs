using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace TheWaningBorder.Humans
{

    public class Barracks{

    public static Entity Create(EntityManager em, float3 pos, Faction fac)
    {
        var e = em.CreateEntity(
            typeof(PresentationId),
            typeof(LocalTransform),
            typeof(FactionTag),
            typeof(BuildingTag),
            typeof(Health),
            typeof(SuppliesIncome)
        );

        em.SetComponentData(e, new PresentationId { Id = 510 });
        em.SetComponentData(e, LocalTransform.FromPositionRotationScale(pos, quaternion.identity, .4f));
        em.SetComponentData(e, new FactionTag { Value = fac });
        em.SetComponentData(e, new BuildingTag { IsBase = 1 });
            em.SetComponentData(e, new Health { Value = 1400, Max = 1400 });
        // Ensure faction/culture is Era 1 (no culture) at start
        if (!em.HasComponent<BarracksTag>(e)) em.AddComponent<BarracksTag>(e);
        if (!em.HasComponent<TrainingState>(e)) em.AddComponentData(e, new TrainingState { Busy = 0, Remaining = 0 });
        if (!em.HasComponent<TrainQueueItem>(e)) em.AddBuffer<TrainQueueItem>(e);
        if (!em.HasComponent<FactionProgress>(e))
            em.AddComponentData(e, new FactionProgress { Culture = Cultures.None });

        // Optional: give LoS if your FoW system expects it; comment out if not used.
        if (!em.HasComponent<LineOfSight>(e))
            em.AddComponentData(e, new LineOfSight { Radius = 15f });

        return e;
    }
    }

}