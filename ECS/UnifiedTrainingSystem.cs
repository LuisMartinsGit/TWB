// UnifiedTrainingSystem.cs - WORKS FOR ALL BUILDINGS
// Processes ANY building with TrainingState (Hall, Barracks, etc.)
// NO LONGER requires BarracksTag

using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using TheWaningBorder.Humans;

[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct UnifiedTrainingSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        // CRITICAL FIX: Require TrainingState, NOT BarracksTag
        // This makes the system work for Hall, Barracks, and any training building
        state.RequireForUpdate<TrainingState>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var db = TechTreeDB.Instance;
        if (db == null) return;

        float dt = SystemAPI.Time.DeltaTime;
        var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

        // Process ALL buildings with TrainingState (Hall, Barracks, etc.)
        // NO BarracksTag requirement!
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
                    queue.RemoveAt(0); // unknown; drop
                    UnityEngine.Debug.LogWarning($"Unknown unit ID in training queue: {unitId}");
                    continue;
                }

                float t = udef.trainingTime > 0 ? udef.trainingTime : 1f;
                ts.ValueRW.Busy = 1;
                ts.ValueRW.Remaining = t;
                
                UnityEngine.Debug.Log($"Started training {unitId} (duration: {t}s)");
            }
            else
            {
                // Tick current
                ts.ValueRW.Remaining -= dt;
                if (ts.ValueRO.Remaining <= 0f)
                {
                    // Finish → spawn 1st queued, then pop
                    var unitId = queue[0].UnitId.ToString();
                    
                    UnityEngine.Debug.Log($"Training complete! Spawning {unitId}");
                    
                    // SPAWN WITHOUT COST CHECK (cost was paid when queuing)
                    SpawnUnit(ref state, ecb, e, unitId);
                    
                    queue.RemoveAt(0);
                    ts.ValueRW.Busy = 0;
                    ts.ValueRW.Remaining = 0f;
                }
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }

    /// <summary>
    /// Spawns a unit from its ID. Cost has already been paid when queuing.
    /// </summary>
    static void SpawnUnit(ref SystemState state, EntityCommandBuffer ecb, Entity building, string unitId)
    {
        var em = state.EntityManager;
        var tr = em.GetComponentData<LocalTransform>(building);
        var fac = em.GetComponentData<FactionTag>(building).Value;

        // Check if building has a rally point
        float3 spawnPos;
        if (em.HasComponent<RallyPoint>(building))
        {
            var rally = em.GetComponentData<RallyPoint>(building);
            if (rally.Has != 0)
            {
                // Spawn at rally point
                spawnPos = rally.Position;
            }
            else
            {
                // Default spawn position (in front of building)
                spawnPos = tr.Position + new float3(1.6f, 0, 1.6f);
            }
        }
        else
        {
            // Default spawn position (in front of building)
            spawnPos = tr.Position + new float3(1.6f, 0, 1.6f);
        }

        // Find empty position near desired spawn point
        float spawnRadius = 0.5f; // Default unit radius
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
                unit = Swordsman.Create(ecb, finalPos, fac);
                break;
            case "Archer":
                unit = Archer.Create(ecb, finalPos, fac);
                break;
            case "Builder":
                unit = TheWaningBorder.Humans.Builder.CreateWithECB(ecb, finalPos, fac);
                break;
            case "Miner":
                unit = TheWaningBorder.Humans.Miner.CreateWithECB(ecb, finalPos, fac);
                break;
            default:
                // Default to swordsman if unknown
                unit = Swordsman.Create(ecb, finalPos, fac);
                UnityEngine.Debug.LogWarning($"Unknown unit type: {unitId}, defaulting to Swordsman");
                break;
        }

        // Apply ALL stats from JSON
        if (TechTreeDB.Instance != null &&
            TechTreeDB.Instance.TryGetUnit(unitId, out var udef))
        {
            // Basic stats
            ecb.SetComponent(unit, new Health { Value = (int)udef.hp, Max = (int)udef.hp });
            ecb.SetComponent(unit, new MoveSpeed { Value = udef.speed });
            ecb.SetComponent(unit, new Damage { Value = (int)udef.damage });
            ecb.SetComponent(unit, new LineOfSight { Radius = udef.lineOfSight });
            
            // Set Radius for collision/spacing
            ecb.SetComponent(unit, new Radius { Value = 0.5f }); // Standard unit radius
            
            // Archer-specific stats from JSON
            if (unitId == "Archer")
            {
                var archerState = new ArcherState
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
                // Give unit a move command to rally point
                ecb.AddComponent(unit, new DesiredDestination 
                { 
                    Position = rally.Position,
                    Has = 1
                });
            }
        }
    }
}