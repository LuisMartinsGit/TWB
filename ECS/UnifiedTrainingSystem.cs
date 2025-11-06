using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using TheWaningBorder.Factions.Humans;
using TheWaningBorder.Factions.Humans.Era1.Units;

[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct UnifiedTrainingSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<TrainingState>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var db = HumanTech.Instance;
        if (db == null) return;

        float dt = SystemAPI.Time.DeltaTime;

        // Process ALL buildings with TrainingState (Hall, Barracks, etc.)
        foreach (var (ts, e) in SystemAPI
                     .Query<RefRW<TrainingState>>()
                     .WithEntityAccess())
        {
            var queue = state.EntityManager.GetBuffer<TrainQueueItem>(e);

            // Start if idle
            if (ts.ValueRO.Busy == 0)
            {
                if (queue.Length == 0) continue;

                var unitId = queue[0].UnitId.ToString();
                if (!db.TryGetUnit(unitId, out var udef))
                {
                    queue.RemoveAt(0);
                    UnityEngine.Debug.LogWarning($"Unknown unit ID in training queue: {unitId}");
                    continue;
                }

                float t = udef.trainingTime > 0 ? udef.trainingTime : 1f;
                ts.ValueRW.Busy = 1;
                ts.ValueRW.Remaining = t;;
            }
            else
            {
                // Tick current
                ts.ValueRW.Remaining -= dt;
                if (ts.ValueRO.Remaining <= 0f && queue.Length > 0)
                {
                    // Training complete - check population before spawning
                    var unitId = queue[0].UnitId.ToString();

                    var em = state.EntityManager;
                    var fac = em.GetComponentData<FactionTag>(e).Value;

                    if (HasPopulationCapacity(ref state, fac, unitId))
                    {
                        // Enough population - spawn the unit
                        SpawnUnit(ref state, e, unitId);

                        queue.RemoveAt(0);
                        ts.ValueRW.Busy = 0;
                        ts.ValueRW.Remaining = 0f;
                    }
                    else
                    {
                        // Not enough population - pause; keep item in queue for retry later
                        ts.ValueRW.Busy = 0;
                        ts.ValueRW.Remaining = 0f;
                        UnityEngine.Debug.LogWarning($"Cannot spawn {unitId}: Not enough population.");
                        // TODO: Show UI notification to player
                    }
                }
            }
        }
    }

    private bool HasPopulationCapacity(ref SystemState state, Faction faction, string unitId)
    {
        // Fast path using ComponentLookups is also possible, but this is clear and fine here.
        foreach (var (tag, pop) in SystemAPI.Query<RefRO<FactionTag>, RefRO<FactionPopulation>>())
        {
            if (tag.ValueRO.Value == faction)
            {
            int requiredpop;

            switch (unitId)
            {
                case "Swordsman":
                    requiredpop=Swordsman.GetPopulationCost();
                    break;
                case "Archer":
                    requiredpop=Archer.GetPopulationCost();
                    break;
                case "Builder":
                    requiredpop=Builder.GetPopulationCost();
                    break;
                case "Miner":
                    requiredpop=Miner.GetPopulationCost();
                    break;
                default:
                        requiredpop = 9999;
                    break;
            }

                return (pop.ValueRO.Current + requiredpop) <= pop.ValueRO.Max;
            }
        }
        return false;
    }

    private static void SpawnUnit(ref SystemState state, Entity building, string unitId)
    {
        var em = state.EntityManager;
        var tr = em.GetComponentData<LocalTransform>(building);
        var fac = em.GetComponentData<FactionTag>(building).Value;

        // Determine spawn pos (rally if present)
        float3 spawnPos;
        if (em.HasComponent<RallyPoint>(building))
        {
            var rally = em.GetComponentData<RallyPoint>(building);
            spawnPos = rally.Has != 0 ? rally.Position : tr.Position + new float3(1.6f, 0, 1.6f);
        }
        else
        {
            spawnPos = tr.Position + new float3(1.6f, 0, 1.6f);
        }

        // Find empty position near desired spawn point
        float spawnRadius = 0.5f;
        float3 finalPos = SpawnPlacementHelper.FindEmptyPosition(
            spawnPos,
            spawnRadius,
            em,
            maxAttempts: 16
        );

        // creates units

        switch (unitId)
        {
            case "Swordsman":
                Swordsman.Create(em, finalPos, fac);
                break;
            case "Archer":
                Archer.Create(em, finalPos, fac);
                break;
            case "Builder":
                Builder.Create(em, finalPos, fac);
                break;
            case "Miner":
                Miner.Create(em, finalPos, fac);
                break;
            default:
                Swordsman.Create(em, finalPos, fac);
                UnityEngine.Debug.LogWarning($"Unknown unit type: {unitId}, defaulting to Swordsman");
                break;
        }
    }
}
