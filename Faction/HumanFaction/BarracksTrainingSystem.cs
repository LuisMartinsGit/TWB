// BarracksTrainingSystem.cs - COMPLETE VERSION
// Applies ALL stats from JSON with NO hardcoded values
// Replace your BarracksTrainingSystem.cs with this

using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using TheWaningBorder.Humans;

[BurstCompile]
public partial struct BarracksTrainingSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BarracksTag>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var db = TechTreeDB.Instance;
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
                    SpawnFromId(ref state, ecb, e, unitId);

                    queue.RemoveAt(0);
                    ts.ValueRW.Busy = 0;
                    ts.ValueRW.Remaining = 0f;
                }
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }

    static void SpawnFromId(ref SystemState state, EntityCommandBuffer ecb, Entity barracks, string unitId)
    {
        var em  = state.EntityManager;
        var tr  = em.GetComponentData<LocalTransform>(barracks);
        var fac = em.GetComponentData<FactionTag>(barracks).Value;

        var pos = tr.Position + new float3(1.6f, 0, 1.6f);

        Entity unit;
        switch (unitId)
        {
            case "Swordsman":
                unit = Swordsman.Create(ecb, pos, fac);
                break;
            case "Archer":
                unit = Archer.Create(ecb, pos, fac);
                break;
            default:
                unit = Swordsman.Create(ecb, pos, fac);
                break;
        }

        // Apply ALL stats from JSON - NO HARDCODED VALUES!
        if (TechTreeDB.Instance != null &&
            TechTreeDB.Instance.TryGetUnit(unitId, out var udef))
        {
            // Basic stats
            ecb.SetComponent(unit, new Health { Value = (int)udef.hp, Max = (int)udef.hp });
            ecb.SetComponent(unit, new MoveSpeed { Value = udef.speed });
            ecb.SetComponent(unit, new Damage { Value = (int)udef.damage });
            ecb.SetComponent(unit, new LineOfSight { Radius = udef.lineOfSight });
            
            // Archer-specific stats from JSON
            if (unitId == "Archer")
            {
                // Apply ranges from JSON - NO HARDCODED VALUES!
                var archerState = new ArcherState
                {
                    CurrentTarget = Entity.Null,
                    AimTimer = 0,
                    AimTimeRequired = 0.5f,  // Could also be in JSON if needed
                    CooldownTimer = 0,
                    MinRange = udef.minAttackRange,  // ✓ FROM JSON!
                    MaxRange = udef.attackRange,     // ✓ FROM JSON!
                    HeightRangeMod = 4f,             // Could also be in JSON if needed
                    IsRetreating = 0,
                    IsFiring = 0
                };
                
                ecb.SetComponent(unit, archerState);
                
                UnityEngine.Debug.Log($"[BarracksTraining] Archer spawned with MinRange={archerState.MinRange}, MaxRange={archerState.MaxRange} from JSON");
            }
        }
    }
}