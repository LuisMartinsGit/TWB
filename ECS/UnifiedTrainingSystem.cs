// UnifiedTrainingSystem.cs - WITH POPULATION INTEGRATION (fixed)
// - No SystemAPI.Query in static methods (EA0006)
// - Correct ECB API: AddComponent(...) instead of AddComponentData(...)
// - Minor robustness tweaks

using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using TheWaningBorder.Humans;

[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct UnifiedTrainingSystem : ISystem
{

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<TrainingState>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var db = TechTreeDB.Instance;
        if (db == null) return;

        float dt = SystemAPI.Time.DeltaTime;

        // Use Temp (or TempJob if you playback on main thread same frame)
        var ecb = new EntityCommandBuffer(Allocator.Temp);

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
                ts.ValueRW.Remaining = t;
                // Optionally log: UnityEngine.Debug.Log($"Started training {unitId} (duration: {t}s)");
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
                    int requiredPop = GetUnitPopulationCost(unitId);

                    if (HasPopulationCapacity(ref state, fac, requiredPop))
                    {
                        // Enough population - spawn the unit
                        SpawnUnit(ref state, ecb, e, unitId);

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

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }

    /// <summary>
    /// Check if faction has enough population capacity.
    /// NOTE: Must NOT be static if using SystemAPI.Query (EA0006).
    /// </summary>
    private bool HasPopulationCapacity(ref SystemState state, Faction faction, int requiredPop)
    {
        // Fast path using ComponentLookups is also possible, but this is clear and fine here.
        foreach (var (tag, pop) in SystemAPI.Query<RefRO<FactionTag>, RefRO<FactionPopulation>>())
        {
            if (tag.ValueRO.Value == faction)
            {
                return (pop.ValueRO.Current + requiredPop) <= pop.ValueRO.Max;
            }
        }
        // No population tracking found - allow by default (back-compat)
        return true;
    }

    /// <summary>
    /// Population cost for a unit type.
    /// </summary>
    private static int GetUnitPopulationCost(string unitId)
    {
        return unitId switch
        {
            "Builder"   => 1,
            "Archer"    => 1,
            "Swordsman" => 1,
            "Miner"     => 1,
            "Scout"     => 1,
            _           => 1 // Default
        };
    }

    /// <summary>
    /// Spawns a unit from its ID. Cost has already been paid when queuing.
    /// Can remain static (doesn't use SystemAPI.Query).
    /// </summary>
    private static void SpawnUnit(ref SystemState state, EntityCommandBuffer ecb, Entity building, string unitId)
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

        // Create unit based on type
        Entity unit;
        switch (unitId)
        {
            case "Swordsman":
                unit = Swordsman.Create(em, finalPos, fac);
                break;
            case "Archer":
                unit = Archer.Create(em, finalPos, fac);
                break;
            case "Builder":
                unit = Builder.Create(em, finalPos, fac);
                break;
            case "Miner":
                unit = Miner.Create(em, finalPos, fac);
                break;
            case "Scout":
                unit = Scout.Create(em, finalPos, fac);
                break;
            default:
                unit = Swordsman.Create(em, finalPos, fac);
                UnityEngine.Debug.LogWarning($"Unknown unit type: {unitId}, defaulting to Swordsman");
                break;
        }

        // Add PopulationCost with the correct ECB API
        int popCost = GetUnitPopulationCost(unitId);
        ecb.AddComponent(unit, new PopulationCost { Amount = popCost });

        // Apply stats from TechTreeDB (SetComponent assumes these components exist on the created archetype)
        if (TechTreeDB.Instance != null &&
            TechTreeDB.Instance.TryGetUnit(unitId, out var udef))
        {
            ecb.SetComponent(unit, new Health     { Value = (int)udef.hp,   Max = (int)udef.hp });
            ecb.SetComponent(unit, new MoveSpeed  { Value = udef.speed });
            ecb.SetComponent(unit, new Damage     { Value = (int)udef.damage });
            ecb.SetComponent(unit, new LineOfSight{ Radius = udef.lineOfSight });
            ecb.SetComponent(unit, new Radius     { Value = 0.5f });

            if (unitId == "Archer")
            {
                var archerState = new ArcherState
                {
                    CurrentTarget   = Entity.Null,
                    AimTimer        = 0,
                    AimTimeRequired = 0.5f,
                    CooldownTimer   = 0,
                    MinRange        = udef.minAttackRange,
                    MaxRange        = udef.attackRange,
                    HeightRangeMod  = 4f,
                    IsRetreating    = 0,
                    IsFiring        = 0
                };
                ecb.SetComponent(unit, archerState);
            }
        }

        // If building has rally point, move unit there
        if (em.HasComponent<RallyPoint>(building))
        {
            var rally = em.GetComponentData<RallyPoint>(building);
            if (rally.Has != 0)
            {
                ecb.AddComponent(unit, new DesiredDestination
                {
                    Position = rally.Position,
                    Has = 1
                });
            }
        }
    }
}

/*
 * INTEGRATION NOTES:
 * - EA0006 fix: HasPopulationCapacity is now an instance method (not static) so it may use SystemAPI.Query.
 * - ECB fix: use ecb.AddComponent(entity, component) instead of AddComponentData.
 * - If your created unit archetypes DON'T already include components like Health/MoveSpeed/etc.,
 *   switch those SetComponent calls to AddComponent for first-time add.
 */
