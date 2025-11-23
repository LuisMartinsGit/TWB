using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

// ===== Components =====

public struct Buildable : IComponentData
{
    public float BuildTimeSeconds; // total construction time
}

public struct UnderConstruction : IComponentData
{
    public float Progress; // 0..Total
    public float Total;
}

public struct BuildOrder : IComponentData
{
    public Entity Site; // building to construct
}

// Store the finished-armor to be applied when construction completes
public struct DeferredDefense : IComponentData
{
    public float Melee, Ranged, Siege, Magic;
}

// Optional: simple Defense component you add ONLY on completion
public struct Defense : IComponentData
{
    public float Melee, Ranged, Siege, Magic;
}

// ===== Systems =====

[BurstCompile]
public partial struct BuilderConstructionSystem : ISystem
{
    const float BuildRange = 2.0f;      // distance to contribute
    const float BuildRatePerBuilder = 1.0f; // normalized per second (Progress/Total)

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float dt = SystemAPI.Time.DeltaTime;
        var em  = state.EntityManager;

        // Snapshot all builders with orders + transforms
        var builderQ = SystemAPI.QueryBuilder()
            .WithAll<CanBuild, LocalTransform, BuildOrder>()
            .Build();

        NativeList<Entity> builders = new NativeList<Entity>(Allocator.Temp);
        NativeList<float3> bPos     = new NativeList<float3>(Allocator.Temp);
        NativeList<Entity> bSite    = new NativeList<Entity>(Allocator.Temp);

        foreach (var (xf, order, e) in SystemAPI.Query<RefRO<LocalTransform>, RefRO<BuildOrder>>().WithAll<CanBuild>().WithEntityAccess())
        {
            builders.Add(e);
            bPos.Add(xf.ValueRO.Position);
            bSite.Add(order.ValueRO.Site);
        }

        // For each site under construction, count builders in range and advance progress
        foreach (var (xf, uc, h, e) in SystemAPI.Query<RefRO<LocalTransform>, RefRW<UnderConstruction>, RefRW<Health>>().WithEntityAccess())
        {
            float3 sitePos = xf.ValueRO.Position;
            int contributors = 0;

            for (int i = 0; i < builders.Length; i++)
            {
                if (bSite[i] != e) continue; // different site
                float d = math.distance(sitePos, bPos[i]);
                if (d <= BuildRange) contributors++;
            }

            if (contributors > 0 && uc.ValueRO.Total > 0.0001f)
            {
                float add = contributors * BuildRatePerBuilder * dt;
                var u = uc.ValueRO;
                u.Progress = math.min(u.Total, u.Progress + add);
                uc.ValueRW = u;

                // Raise HP proportionally
                var hh = h.ValueRO;
                int targetMax = math.max(1, hh.Max);
                int newHP = (int)math.round(math.saturate(u.Progress / u.Total) * targetMax);
                if (newHP > hh.Value)
                {
                    hh.Value = newHP;
                    h.ValueRW = hh;
                }

                // Arrived: construction finished
                if (u.Progress >= u.Total - 0.0001f)
                {
                    // Remove UnderConstruction
                    state.EntityManager.RemoveComponent<UnderConstruction>(e);

                    // Apply defense if we stored it deferred
                    if (em.HasComponent<DeferredDefense>(e))
                    {
                        var def = em.GetComponentData<DeferredDefense>(e);
                        if (!em.HasComponent<Defense>(e)) em.AddComponent<Defense>(e);
                        em.SetComponentData(e, new Defense { Melee = def.Melee, Ranged = def.Ranged, Siege = def.Siege, Magic = def.Magic });
                        em.RemoveComponent<DeferredDefense>(e);
                    }

                    // Clear any builders still targeting this site
                    ClearBuildOrdersTargeting(ref state, e);
                }
            }
        }

        builders.Dispose();
        bPos.Dispose();
        bSite.Dispose();
    }

    void ClearBuildOrdersTargeting(ref SystemState state, Entity site)
    {
        var em = state.EntityManager;
        foreach (var (order, e) in SystemAPI.Query<RefRO<BuildOrder>>().WithEntityAccess())
        {
            if (order.ValueRO.Site == site)
                em.RemoveComponent<BuildOrder>(e);
        }
    }
}
