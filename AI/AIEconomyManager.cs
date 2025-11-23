// AIEconomyManager.cs
// Manages AI economy: miners, gatherers huts, resource allocation
using System.Collections.Generic;
using TheWaningBorder.Humans;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace TheWaningBorder.AI
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Unity.Transforms.TransformSystemGroup))]
    public partial struct AIEconomyManager : ISystem
    {
        private const float MINE_CHECK_INTERVAL = 5.0f;
        private const int TARGET_MINERS_PER_MINE = 3;
        private const int MIN_SUPPLIES_THRESHOLD = 200;
        private const int TARGET_GATHERERS_HUTS = 3;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<AIBrain>();
        }

        public void OnUpdate(ref SystemState state)
        {
            float time = (float)SystemAPI.Time.ElapsedTime;
            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Process each AI player's economy
            foreach (var (brain, economyState, resourceReqs, entity) 
                in SystemAPI.Query<RefRO<AIBrain>, RefRW<AIEconomyState>, DynamicBuffer<ResourceRequest>>()
                .WithEntityAccess())
            {
                if (brain.ValueRO.IsActive == 0) continue;

                var economy = economyState.ValueRW;
                
                // Check if it's time for mine assignment update
                if (time >= economy.LastMineAssignmentCheck + economy.MineCheckInterval)
                {
                    economy.LastMineAssignmentCheck = time;
                    UpdateMineAssignments(ref state, brain.ValueRO.Owner, ref economy, ecb);
                }

                // Check economy needs
                CheckEconomyNeeds(ref state, brain.ValueRO.Owner, ref economy);

                // Request gatherers huts if needed
                if (economy.NeedsMoreSupplyIncome == 1 && economy.ActiveGatherersHuts < economy.DesiredGatherersHuts)
                {
                    RequestGatherersHut(ref state, brain.ValueRO.Owner, ecb);
                }

                // Process resource allocation requests
                ProcessResourceRequests(ref state, brain.ValueRO.Owner, resourceReqs);
            }

            ecb.Playback(em);
            ecb.Dispose();
        }

        private void UpdateMineAssignments(ref SystemState state, Faction faction, 
            ref AIEconomyState economy, EntityCommandBuffer ecb)
        {
            var em = state.EntityManager;

            // Find all iron mines
            var mineQuery = SystemAPI.QueryBuilder()
                .WithAll<IronMineTag, LocalTransform>()
                .Build();

            
            var mines = mineQuery.ToEntityArray(Allocator.Temp);
            var minePositions = mineQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

            // Count current miners
            int currentMiners = 0;
            var minerQuery = SystemAPI.QueryBuilder()
                .WithAll<MinerTag, FactionTag>()
                .Build();

            foreach (var minerFaction in minerQuery.ToComponentDataArray<FactionTag>(Allocator.Temp))
            {
                if (minerFaction.Value == faction)
                    currentMiners++;
            }

            economy.AssignedMiners = currentMiners;
            economy.DesiredMiners = mines.Length * TARGET_MINERS_PER_MINE;

            // Assign unassigned miners to nearest mines
            var unassignedMiners = SystemAPI.QueryBuilder()
                .WithAll<MinerTag, FactionTag, LocalTransform>()
                .WithNone<MiningTarget>()
                .Build();

            var unassignedEntities = unassignedMiners.ToEntityArray(Allocator.Temp);
            var unassignedPositions = unassignedMiners.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var unassignedFactions = unassignedMiners.ToComponentDataArray<FactionTag>(Allocator.Temp);

            for (int i = 0; i < unassignedEntities.Length; i++)
            {
                if (unassignedFactions[i].Value != faction) continue;

                // Find nearest mine
                float nearestDist = float.MaxValue;
                int nearestMineIdx = -1;

                for (int m = 0; m < mines.Length; m++)
                {
                    float dist = math.distance(unassignedPositions[i].Position, minePositions[m].Position);
                    if (dist < nearestDist)
                    {
                        nearestDist = dist;
                        nearestMineIdx = m;
                    }
                }

                if (nearestMineIdx >= 0)
                {
                    // Assign this miner to the nearest mine
                    ecb.AddComponent(unassignedEntities[i], new MiningTarget 
                    { 
                        Mine = mines[nearestMineIdx],
                        TargetPosition = minePositions[nearestMineIdx].Position
                    });
                }
            }

            // Request more miners if needed
            if (currentMiners < economy.DesiredMiners)
            {
                int minersNeeded = economy.DesiredMiners - currentMiners;
                RequestMiners(ref state, faction, minersNeeded, ecb);
            }

            mines.Dispose();
            minePositions.Dispose();
            unassignedEntities.Dispose();
            unassignedPositions.Dispose();
            unassignedFactions.Dispose();
        }

        private void CheckEconomyNeeds(ref SystemState state, Faction faction, ref AIEconomyState economy)
        {
            var em = state.EntityManager;

            // Get faction resources
            var bankQuery = SystemAPI.QueryBuilder()
                .WithAll<FactionTag, FactionResources>()
                .Build();

            foreach (var (factionTag, resources) in SystemAPI.Query<RefRO<FactionTag>, RefRO<FactionResources>>())
            {
                if (factionTag.ValueRO.Value != faction) continue;

                // Check if we need more supply income
                economy.NeedsMoreSupplyIncome = resources.ValueRO.Supplies < MIN_SUPPLIES_THRESHOLD ? (byte)1 : (byte)0;
                
                // Check if we need more iron income
                economy.NeedsMoreIronIncome = resources.ValueRO.Iron < 100 ? (byte)1 : (byte)0;

                // Calculate desired gatherers huts based on resources
                if (resources.ValueRO.Supplies < MIN_SUPPLIES_THRESHOLD)
                {
                    economy.DesiredGatherersHuts = math.min(TARGET_GATHERERS_HUTS + 1, 5);
                }
                else
                {
                    economy.DesiredGatherersHuts = TARGET_GATHERERS_HUTS;
                }

                break;
            }

            // Count existing gatherers huts
            economy.ActiveGatherersHuts = 0;
            foreach (var (gathererTag, factionTag) in SystemAPI.Query<RefRO<GathererHutTag>, RefRO<FactionTag>>())
            {
                if (factionTag.ValueRO.Value == faction)
                    economy.ActiveGatherersHuts++;
            }
        }

        private void RequestGatherersHut(ref SystemState state, Faction faction, EntityCommandBuffer ecb)
        {
            var em = state.EntityManager;

            // Find the building manager entity for this faction
            foreach (var (brain, buildReqs, entity) in SystemAPI.Query<RefRO<AIBrain>, DynamicBuffer<BuildRequest>>()
                .WithEntityAccess())
            {
                if (brain.ValueRO.Owner != faction) continue;

                // Find a good location for the gatherers hut (near base)
                float3 buildLocation = FindBuildLocation(ref state, faction, "GatherersHut");

                // Add build request
                // buildReqs.Add(new BuildRequest
                // {
                //     BuildingType = "GatherersHut",
                //     DesiredPosition = buildLocation,
                //     Priority = 8, // High priority
                //     Assigned = 0,
                //     AssignedBuilder = Entity.Null
                // });

                break;
            }
        }

        private void RequestMiners(ref SystemState state, Faction faction, int count, EntityCommandBuffer ecb)
        {
            // Find the military manager to request miner training
            foreach (var (brain, recruitReqs, entity) in SystemAPI.Query<RefRO<AIBrain>, DynamicBuffer<RecruitmentRequest>>()
                .WithEntityAccess())
            {
                if (brain.ValueRO.Owner != faction) continue;

                // Add recruitment request for miners
                // Note: Miners use UnitClass.Economy
                recruitReqs.Add(new RecruitmentRequest
                {
                    UnitType = UnitClass.Economy,
                    Quantity = count,
                    Priority = 9, // Very high priority - economy is critical
                    RequestingManager = entity
                });

                break;
            }
        }

        private void ProcessResourceRequests(ref SystemState state, Faction faction, 
            DynamicBuffer<ResourceRequest> requests)
        {
            if (requests.Length == 0) return;

            var em = state.EntityManager;

            // Get faction resources
            FactionResources availableResources = default;
            Entity bankEntity = Entity.Null;

            foreach (var (factionTag, resources, entity) in SystemAPI.Query<RefRO<FactionTag>, RefRW<FactionResources>>()
                .WithEntityAccess())
            {
                if (factionTag.ValueRO.Value == faction)
                {
                    availableResources = resources.ValueRO;
                    bankEntity = entity;
                    break;
                }
            }

            if (bankEntity == Entity.Null) return;

            // Sort requests by priority (highest first)
            var sortedRequests = new NativeList<ResourceRequest>(Allocator.Temp);
            for (int i = 0; i < requests.Length; i++)
            {
                if (requests[i].Approved == 0) // Only pending requests
                    sortedRequests.Add(requests[i]);
            }

            sortedRequests.Sort(new ResourceRequestComparer());

            // Process requests in priority order
            for (int i = 0; i < sortedRequests.Length; i++)
            {
                var req = sortedRequests[i];

                // Check if we can afford this request
                bool canAfford = availableResources.Supplies >= req.Supplies &&
                                availableResources.Iron >= req.Iron &&
                                availableResources.Crystal >= req.Crystal &&
                                availableResources.Veilsteel >= req.Veilsteel &&
                                availableResources.Glow >= req.Glow;

                if (canAfford)
                {
                    // Approve and deduct resources
                    availableResources.Supplies -= req.Supplies;
                    availableResources.Iron -= req.Iron;
                    availableResources.Crystal -= req.Crystal;
                    availableResources.Veilsteel -= req.Veilsteel;
                    availableResources.Glow -= req.Glow;

                    req.Approved = 1; // Mark as approved
                }
                else
                {
                    req.Approved = 2; // Mark as denied
                }

                // Update the request in buffer
                for (int j = 0; j < requests.Length; j++)
                {
                    if (requests[j].Requester == req.Requester && requests[j].Approved == 0)
                    {
                        requests[j] = req;
                        break;
                    }
                }
            }

            // Write back updated resources
            em.SetComponentData(bankEntity, availableResources);

            sortedRequests.Dispose();
        }

        private float3 FindBuildLocation(ref SystemState state, Faction faction, FixedString64Bytes buildingType)
        {
            // Find faction's main base
            float3 basePos = new float3(0, 0, 0);
            bool foundBase = false;

            foreach (var (factionTag, transform, buildingTag) in 
                SystemAPI.Query<RefRO<FactionTag>, RefRO<LocalTransform>, RefRO<BuildingTag>>())
            {
                if (factionTag.ValueRO.Value == faction && buildingTag.ValueRO.IsBase == 1)
                {
                    basePos = transform.ValueRO.Position;
                    foundBase = true;
                    break;
                }
            }

            if (!foundBase)
                return new float3(10, 0, 10); // Fallback position

            // Offset from base in a random direction
            var random = new Unity.Mathematics.Random((uint)(SystemAPI.Time.ElapsedTime * 1000));
            float angle = random.NextFloat(0, math.PI * 2);
            float distance = random.NextFloat(15, 25);

            return basePos + new float3(
                math.cos(angle) * distance,
                0,
                math.sin(angle) * distance
            );
        }
    }

    // Comparer for sorting resource requests by priority
    struct ResourceRequestComparer : IComparer<ResourceRequest>
    {
        public int Compare(ResourceRequest a, ResourceRequest b)
        {
            return b.Priority.CompareTo(a.Priority); // Descending order
        }
    }
}