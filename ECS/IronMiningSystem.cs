// IronDeposit.cs and MiningSystem.cs - FIXED VERSION
// Iron deposits that miners can gather from
// FIXED: Properly handles zero-sized tag components + ECB for structural changes during iteration

using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using TheWaningBorder.Factions.Humans.Era1.Units;
using TheWaningBorder.Economy;

namespace TheWaningBorder.Resources
{
    /// <summary>
    /// Iron deposit that miners can work on
    /// Has no ownership - any faction can use it
    /// </summary>
    public static class IronDeposit
    {
        public static Entity Create(EntityManager em, float3 pos)
        {
            var e = em.CreateEntity(
                typeof(PresentationId),
                typeof(LocalTransform),
                typeof(IronDepositTag),      // Tag is included in CreateEntity
                typeof(IronDepositState),
                typeof(Radius)
            );

            em.SetComponentData(e, new PresentationId { Id = 301 }); // Iron deposit ID
            em.SetComponentData(e, LocalTransform.FromPositionRotationScale(pos, quaternion.identity, 0.8f));
            // FIXED: Don't call SetComponentData on IronDepositTag (it's zero-sized)
            em.SetComponentData(e, new IronDepositState
            {
                RemainingIron = 5000, // Large amount
                Depleted = 0
            });
            em.SetComponentData(e, new Radius { Value = 1.2f }); // Larger collision radius

            return e;
        }
    }

    /// <summary>
    /// Tag for iron deposits - ZERO SIZED (just a marker)
    /// </summary>
    public struct IronDepositTag : IComponentData { }

    /// <summary>
    /// Iron deposit state - HAS DATA
    /// </summary>
    public struct IronDepositState : IComponentData
    {
        public int RemainingIron;   // How much iron left
        public byte Depleted;       // 1 if empty
    }
}

/// <summary>
//— System that handles miners gathering iron from deposits
/// Miners automatically find nearby deposits and gather iron
/// </summary>
[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct MiningSystem : ISystem
{
    private const float GatherInterval = 2f;      // Seconds to gather one load
    private const int IronPerGather = 10;         // Iron per gather action
    private const float GatherRange = 2f;         // How close miner needs to be
    private const float SearchRadius = 50f;       // How far miners search for deposits

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<MinerTag>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var em = state.EntityManager;
        float dt = SystemAPI.Time.DeltaTime;

        // Create an ECB that plays back at EndSimulation (so structural changes are deferred)
        var endSimEcbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = endSimEcbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

        // Process all miners
        foreach (var (minerState, transform, faction, entity) in SystemAPI
                 .Query<RefRW<MinerState>, RefRO<LocalTransform>, RefRO<FactionTag>>()
                 .WithAll<MinerTag>()
                 .WithEntityAccess())
        {
            ref var miner = ref minerState.ValueRW;
            var pos = transform.ValueRO.Position;
            var fac = faction.ValueRO.Value;

            switch (miner.State)
            {
                case MinerWorkState.Idle:
                {
                    // Find nearest iron deposit
                    Entity nearestDeposit = FindNearestDeposit(em, pos);
                    if (nearestDeposit != Entity.Null)
                    {
                        miner.AssignedDeposit = nearestDeposit;
                        miner.State = MinerWorkState.MovingToDeposit;

                        // Give move command (defer structural change via ECB)
                        var depositPos = em.GetComponentData<LocalTransform>(nearestDeposit).Position;
                        if (em.HasComponent<DesiredDestination>(entity))
                        {
                            ecb.SetComponent(entity, new DesiredDestination
                            {
                                Position = depositPos,
                                Has = 1
                            });
                        }
                        else
                        {
                            ecb.AddComponent(entity, new DesiredDestination
                            {
                                Position = depositPos,
                                Has = 1
                            });
                        }
                    }
                    break;
                }

                case MinerWorkState.MovingToDeposit:
                {
                    // Check if reached deposit
                    if (miner.AssignedDeposit != Entity.Null && em.Exists(miner.AssignedDeposit))
                    {
                        var depositPos = em.GetComponentData<LocalTransform>(miner.AssignedDeposit).Position;
                        float dist = math.distance(pos, depositPos);

                        if (dist <= GatherRange)
                        {
                            // Reached deposit - start gathering
                            miner.State = MinerWorkState.Gathering;
                            miner.GatherTimer = 0f;

                            // Stop moving (use ECB for safety)
                            if (em.HasComponent<DesiredDestination>(entity))
                            {
                                ecb.SetComponent(entity, new DesiredDestination { Has = 0 });
                            }
                        }
                    }
                    else
                    {
                        // Deposit gone - go idle
                        miner.State = MinerWorkState.Idle;
                        miner.AssignedDeposit = Entity.Null;
                    }
                    break;
                }

                case MinerWorkState.Gathering:
                {
                    // Accumulate gather time
                    miner.GatherTimer += dt;

                    if (miner.GatherTimer >= GatherInterval)
                    {
                        // Gathered some iron
                        miner.GatherTimer = 0f;
                        miner.CurrentLoad += IronPerGather;

                        // Check/update deposit
                        if (miner.AssignedDeposit != Entity.Null && em.Exists(miner.AssignedDeposit))
                        {
                            if (em.HasComponent<TheWaningBorder.Resources.IronDepositState>(miner.AssignedDeposit))
                            {
                                var depState = em.GetComponentData<TheWaningBorder.Resources.IronDepositState>(miner.AssignedDeposit);
                                depState.RemainingIron -= IronPerGather;

                                if (depState.RemainingIron <= 0)
                                {
                                    depState.RemainingIron = 0;
                                    depState.Depleted = 1;
                                }

                                // This is NOT a structural change; SetComponentData is fine here
                                em.SetComponentData(miner.AssignedDeposit, depState);

                                // If depleted, find new deposit
                                if (depState.Depleted == 1)
                                {
                                    miner.AssignedDeposit = Entity.Null;
                                    miner.State = MinerWorkState.Idle;
                                }
                            }
                        }

                        // Immediately add iron to faction (still just SetComponentData)
                        if (FactionEconomy.TryGetBank(em, fac, out var bank))
                        {
                            var resources = em.GetComponentData<FactionResources>(bank);
                            resources.Iron += IronPerGather;
                            em.SetComponentData(bank, resources);

                            miner.CurrentLoad = 0; // "Deposited"
                        }
                    }
                    break;
                }
            }
        }

        // No scheduled jobs writing to ECB in this version, so no AddJobHandleForProducer needed.
        // If you later schedule jobs that write to 'ecb.AsParallelWriter()', remember to call:
        // endSimEcbSingleton.AddJobHandleForProducer(state.Dependency);
    }

    /// <summary>
    /// Find the nearest non-depleted iron deposit
    /// </summary>
    private static Entity FindNearestDeposit(EntityManager em, float3 pos)
    {
        var query = em.CreateEntityQuery(
            ComponentType.ReadOnly<TheWaningBorder.Resources.IronDepositTag>(),
            ComponentType.ReadOnly<TheWaningBorder.Resources.IronDepositState>(),
            ComponentType.ReadOnly<LocalTransform>()
        );

        using var deposits = query.ToEntityArray(Unity.Collections.Allocator.Temp);
        using var states = query.ToComponentDataArray<TheWaningBorder.Resources.IronDepositState>(Unity.Collections.Allocator.Temp);
        using var transforms = query.ToComponentDataArray<LocalTransform>(Unity.Collections.Allocator.Temp);

        Entity nearest = Entity.Null;
        float nearestDist = float.MaxValue;

        for (int i = 0; i < deposits.Length; i++)
        {
            // Skip depleted deposits
            if (states[i].Depleted == 1) continue;

            float dist = math.distance(pos, transforms[i].Position);
            if (dist < nearestDist && dist <= SearchRadius)
            {
                nearest = deposits[i];
                nearestDist = dist;
            }
        }

        return nearest;
    }
}
