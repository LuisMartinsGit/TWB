// File: Assets/Scripts/Systems/Work/MiningSystem.cs
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using TheWaningBorder.Humans;
using TheWaningBorder.Economy;
using TheWaningBorder.Resources;

namespace TheWaningBorder.Systems.Work
{
    /// <summary>
    /// Handles miners gathering iron from deposits.
    /// 
    /// Miners automatically:
    /// - Find nearby non-depleted deposits when idle
    /// - Move to assigned deposit
    /// - Gather iron over time
    /// - Deposit iron directly to faction economy (simplified - no return trip)
    /// 
    /// State machine: Idle -> MovingToDeposit -> Gathering -> (back to Idle or find new deposit)
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
                        ProcessIdleState(ref miner, em, entity, pos);
                        break;

                    case MinerWorkState.MovingToDeposit:
                        ProcessMovingState(ref miner, em, entity, pos);
                        break;

                    case MinerWorkState.Gathering:
                        ProcessGatheringState(ref miner, em, entity, fac, dt);
                        break;

                    case MinerWorkState.ReturningToBase:
                        // Currently not used - iron deposited immediately
                        miner.State = MinerWorkState.Idle;
                        break;
                }
            }
        }

        private void ProcessIdleState(ref MinerState miner, EntityManager em, Entity entity, float3 pos)
        {
            // Find nearest iron deposit
            Entity nearestDeposit = FindNearestDeposit(em, pos);
            if (nearestDeposit != Entity.Null)
            {
                miner.AssignedDeposit = nearestDeposit;
                miner.State = MinerWorkState.MovingToDeposit;

                // Give move command
                var depositPos = em.GetComponentData<LocalTransform>(nearestDeposit).Position;
                if (em.HasComponent<DesiredDestination>(entity))
                {
                    em.SetComponentData(entity, new DesiredDestination
                    {
                        Position = depositPos,
                        Has = 1
                    });
                }
                else
                {
                    em.AddComponentData(entity, new DesiredDestination
                    {
                        Position = depositPos,
                        Has = 1
                    });
                }
            }
        }

        private void ProcessMovingState(ref MinerState miner, EntityManager em, Entity entity, float3 pos)
        {
            // Check if deposit still exists
            if (miner.AssignedDeposit == Entity.Null || !em.Exists(miner.AssignedDeposit))
            {
                miner.State = MinerWorkState.Idle;
                miner.AssignedDeposit = Entity.Null;
                return;
            }

            var depositPos = em.GetComponentData<LocalTransform>(miner.AssignedDeposit).Position;
            float dist = math.distance(pos, depositPos);

            if (dist <= GatherRange)
            {
                // Reached deposit - start gathering
                miner.State = MinerWorkState.Gathering;
                miner.GatherTimer = 0f;

                // Stop moving
                if (em.HasComponent<DesiredDestination>(entity))
                {
                    em.SetComponentData(entity, new DesiredDestination { Has = 0 });
                }
            }
        }

        private void ProcessGatheringState(ref MinerState miner, EntityManager em, Entity entity, Faction fac, float dt)
        {
            // Accumulate gather time
            miner.GatherTimer += dt;

            if (miner.GatherTimer >= GatherInterval)
            {
                // Gathered some iron
                miner.GatherTimer = 0f;
                miner.CurrentLoad += IronPerGather;

                // Check if deposit still has iron
                if (miner.AssignedDeposit != Entity.Null && em.Exists(miner.AssignedDeposit))
                {
                    if (em.HasComponent<IronDepositState>(miner.AssignedDeposit))
                    {
                        var depState = em.GetComponentData<IronDepositState>(miner.AssignedDeposit);
                        depState.RemainingIron -= IronPerGather;

                        if (depState.RemainingIron <= 0)
                        {
                            depState.RemainingIron = 0;
                            depState.Depleted = 1;
                        }

                        em.SetComponentData(miner.AssignedDeposit, depState);

                        // If depleted, find new deposit
                        if (depState.Depleted == 1)
                        {
                            miner.AssignedDeposit = Entity.Null;
                            miner.State = MinerWorkState.Idle;
                        }
                    }
                }

                // Immediately add iron to faction (simplified - no return trip)
                if (FactionEconomy.TryGetBank(em, fac, out var bank))
                {
                    var resources = em.GetComponentData<FactionResources>(bank);
                    resources.Iron += IronPerGather;
                    em.SetComponentData(bank, resources);

                    miner.CurrentLoad = 0; // "Deposited"
                }
            }
        }

        /// <summary>
        /// Find the nearest non-depleted iron deposit within search radius.
        /// </summary>
        private static Entity FindNearestDeposit(EntityManager em, float3 pos)
        {
            var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<IronDepositTag>(),
                ComponentType.ReadOnly<IronDepositState>(),
                ComponentType.ReadOnly<LocalTransform>()
            );

            using var deposits = query.ToEntityArray(Allocator.Temp);
            using var states = query.ToComponentDataArray<IronDepositState>(Allocator.Temp);
            using var transforms = query.ToComponentDataArray<LocalTransform>(Allocator.Temp);

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
}