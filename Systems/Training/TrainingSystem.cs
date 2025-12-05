// File: Assets/Scripts/Systems/Training/TrainingSystem.cs
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using TheWaningBorder.Entities;

namespace TheWaningBorder.Systems.Training
{
    /// <summary>
    /// Unified training system that processes unit production for all buildings.
    /// 
    /// Training workflow:
    /// 1. UI adds TrainQueueItem to building's buffer (cost paid at queue time)
    /// 2. System starts training first item when building is idle
    /// 3. Timer counts down based on unit's trainingTime from TechTreeDB
    /// 4. When complete, checks population capacity before spawning
    /// 5. Unit spawns at rally point (or default position near building)
    /// 
    /// Works with: Hall, Barracks, and any building with TrainingState + TrainQueueItem buffer
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct TrainingSystem : ISystem
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
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Process all buildings with TrainingState
            foreach (var (ts, entity) in SystemAPI
                         .Query<RefRW<TrainingState>>()
                         .WithEntityAccess())
            {
                var queue = state.EntityManager.GetBuffer<TrainQueueItem>(entity);

                // Start training if idle and queue has items
                if (ts.ValueRO.Busy == 0)
                {
                    if (queue.Length == 0) continue;

                    var unitId = queue[0].UnitId.ToString();
                    if (!db.TryGetUnit(unitId, out var udef))
                    {
                        // Unknown unit - remove from queue
                        queue.RemoveAt(0);
                        UnityEngine.Debug.LogWarning($"Unknown unit ID in training queue: {unitId}");
                        continue;
                    }

                    // Start training
                    float trainingTime = udef.trainingTime > 0 ? udef.trainingTime : 1f;
                    ts.ValueRW.Busy = 1;
                    ts.ValueRW.Remaining = trainingTime;
                }
                else
                {
                    // Tick training timer
                    ts.ValueRW.Remaining -= dt;

                    if (ts.ValueRO.Remaining <= 0f && queue.Length > 0)
                    {
                        // Training complete - check population before spawning
                        var unitId = queue[0].UnitId.ToString();
                        var em = state.EntityManager;
                        var faction = em.GetComponentData<FactionTag>(entity).Value;
                        int requiredPop = GetUnitPopulationCost(unitId);

                        if (HasPopulationCapacity(ref state, faction, requiredPop))
                        {
                            // Spawn the unit
                            SpawnUnit(ref state, ecb, entity, unitId);

                            queue.RemoveAt(0);
                            ts.ValueRW.Busy = 0;
                            ts.ValueRW.Remaining = 0f;
                        }
                        else
                        {
                            // Not enough population - pause training
                            ts.ValueRW.Busy = 0;
                            ts.ValueRW.Remaining = 0f;
                            UnityEngine.Debug.LogWarning($"Cannot spawn {unitId}: Population cap reached.");
                        }
                    }
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        /// <summary>
        /// Check if faction has enough population capacity for the unit.
        /// </summary>
        private bool HasPopulationCapacity(ref SystemState state, Faction faction, int requiredPop)
        {
            foreach (var (tag, pop) in SystemAPI.Query<RefRO<FactionTag>, RefRO<FactionPopulation>>())
            {
                if (tag.ValueRO.Value == faction)
                {
                    return (pop.ValueRO.Current + requiredPop) <= pop.ValueRO.Max;
                }
            }
            // No population tracking found - allow by default
            return true;
        }

        /// <summary>
        /// Get population cost for a unit type.
        /// </summary>
        private static int GetUnitPopulationCost(string unitId)
        {
            return unitId switch
            {
                "Builder" => 1,
                "Archer" => 1,
                "Swordsman" => 1,
                "Miner" => 1,
                "Scout" => 1,
                _ => 1 // Default
            };
        }

        /// <summary>
        /// Spawns a unit from its ID. Cost already paid when queued.
        /// </summary>
        private static void SpawnUnit(ref SystemState state, EntityCommandBuffer ecb, Entity building, string unitId)
        {
            var em = state.EntityManager;
            var transform = em.GetComponentData<LocalTransform>(building);
            var faction = em.GetComponentData<FactionTag>(building).Value;

            // Determine spawn position (rally point or default offset)
            float3 spawnPos;
            if (em.HasComponent<RallyPoint>(building))
            {
                var rally = em.GetComponentData<RallyPoint>(building);
                spawnPos = rally.Has != 0 ? rally.Position : transform.Position + new float3(1.6f, 0, 1.6f);
            }
            else
            {
                spawnPos = transform.Position + new float3(1.6f, 0, 1.6f);
            }

            // Find empty position near spawn point to avoid overlap
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
                    unit = Swordsman.Create(em, finalPos, faction);
                    break;
                case "Archer":
                    unit = Archer.Create(em, finalPos, faction);
                    break;
                case "Builder":
                    unit = Builder.Create(em, finalPos, faction);
                    break;
                case "Miner":
                    unit = Miner.Create(em, finalPos, faction);
                    break;
                case "Scout":
                    unit = Scout.Create(em, finalPos, faction);
                    break;
                default:
                    unit = Swordsman.Create(em, finalPos, faction);
                    UnityEngine.Debug.LogWarning($"Unknown unit type '{unitId}', spawning Swordsman");
                    break;
            }

            // Apply stats from TechTreeDB
            if (TechTreeDB.Instance != null &&
                TechTreeDB.Instance.TryGetUnit(unitId, out var udef))
            {
                ecb.SetComponent(unit, new Health { Value = (int)udef.hp, Max = (int)udef.hp });
                ecb.SetComponent(unit, new MoveSpeed { Value = udef.speed });
                ecb.SetComponent(unit, new Damage { Value = (int)udef.damage });
                ecb.SetComponent(unit, new LineOfSight { Radius = udef.lineOfSight });
            }

            UnityEngine.Debug.Log($"Spawned {unitId} for {faction} at {finalPos}");
        }
    }
}