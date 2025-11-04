// BarracksTrainingSystem.cs - WITH ECONOMY INTEGRATION
// Deducts unit costs from faction resources when spawning units
// Replace your BarracksTrainingSystem.cs with this

using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using TheWaningBorder.Humans;
using TheWaningBorder.Economy;
using TheWaningBorder.Factions.Humans;
using TheWaningBorder.Factions.Humans.Era1.Units;

[BurstCompile]
public partial struct BarracksTrainingSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BarracksTag>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var db = HumanTech.Instance;
        if (db == null) return;

        float dt = SystemAPI.Time.DeltaTime;
        var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

        foreach (var (ts, e) in SystemAPI
                 .Query<RefRW<TrainingState>>()
                 .WithAll<BarracksTag>()
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
                    continue;
                }

                float t = udef.trainingTime > 0 ? udef.trainingTime : 1f;
                ts.ValueRW.Busy = 1;
                ts.ValueRW.Remaining = t;
            }
            else
            {
                // Tick current
                ts.ValueRW.Remaining -= dt;
                if (ts.ValueRO.Remaining <= 0f)
                {
                    // Finish → spawn 1st queued, then pop
                    var unitId = queue[0].UnitId.ToString();
                    
                    // CRITICAL FIX: Deduct cost before spawning
                    if (TrySpawnWithCost(ref state, ecb, e, unitId))
                    {
                        // Successfully spawned and paid
                        queue.RemoveAt(0);
                        ts.ValueRW.Busy = 0;
                        ts.ValueRW.Remaining = 0f;
                    }
                    else
                    {
                        // Can't afford - cancel training
                        // Note: In a real game, you might want to pause instead of cancel
                        queue.RemoveAt(0);
                        ts.ValueRW.Busy = 0;
                        ts.ValueRW.Remaining = 0f;
                        
                        // Optional: Log warning for debugging
                        UnityEngine.Debug.LogWarning($"Cannot afford to train {unitId} - training cancelled");
                    }
                }
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }

    /// <summary>
    /// Attempts to spawn a unit, deducting its cost from the faction's economy.
    /// Returns true if successful, false if the faction cannot afford it.
    /// </summary>
    static bool TrySpawnWithCost(ref SystemState state, EntityCommandBuffer ecb, Entity barracks, string unitId)
    {
        var em = state.EntityManager;
        var fac = em.GetComponentData<FactionTag>(barracks).Value;
        
        // Get unit cost from HumanTech
        if (!HumanTech.Instance.TryGetUnit(unitId, out var udef))
            return false;
        
        // Convert unit cost to Cost structure
        Cost unitCost = udef.cost.ToCost();
        
        // CRITICAL: Deduct cost from faction economy
        if (!FactionEconomy.Spend(em, fac, unitCost))
        {
            // Cannot afford this unit
            return false;
        }
        
        // Cost paid successfully - now spawn the unit
        var tr = em.GetComponentData<LocalTransform>(barracks);

        // Check if barracks has a rally point
        float3 spawnPos;
        if (em.HasComponent<RallyPoint>(barracks))
        {
            var rally = em.GetComponentData<RallyPoint>(barracks);
            if (rally.Has != 0)
            {
                // Spawn at rally point
                spawnPos = rally.Position;
            }
            else
            {
                // Default spawn position (in front of barracks)
                spawnPos = tr.Position + new float3(1.6f, 0, 1.6f);
            }
        }
        else
        {
            // Default spawn position (in front of barracks)
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

        Entity unit;
        switch (unitId)
        {
            case "Swordsman":
                unit = Swordsman.Create(em, finalPos, fac);
                break;
            case "Archer":
                unit = Archer.Create(em, finalPos, fac);
                break;
            default:
                unit = Swordsman.Create(em, finalPos, fac);
                break;
        }

        // Apply ALL stats from JSON
        if (HumanTech.Instance != null &&
            HumanTech.Instance.TryGetUnit(unitId, out var udefStats))
        {
            // Basic stats
            ecb.SetComponent(unit, new Health { Value = (int)udefStats.hp, Max = (int)udefStats.hp });
            ecb.SetComponent(unit, new MoveSpeed { Value = udefStats.speed });
            ecb.SetComponent(unit, new Damage { Value = (int)udefStats.damage });
            ecb.SetComponent(unit, new LineOfSight { Radius = udefStats.lineOfSight });
            
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
                    MinRange = udefStats.minAttackRange,
                    MaxRange = udefStats.attackRange,
                    HeightRangeMod = 4f,
                    IsRetreating = 0,
                    IsFiring = 0
                };
                
                ecb.SetComponent(unit, archerState);
            }
        }

        // If barracks has rally point, move unit there
        if (em.HasComponent<RallyPoint>(barracks))
        {
            var rally = em.GetComponentData<RallyPoint>(barracks);
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
        
        return true;
    }
}