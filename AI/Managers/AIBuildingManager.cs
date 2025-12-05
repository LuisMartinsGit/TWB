// AIBuildingManager.cs
// Controls builders, processes build requests, manages construction
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace TheWaningBorder.AI
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(AIEconomyManager))]
    public partial struct AIBuildingManager : ISystem
    {
        private const float BUILD_CHECK_INTERVAL = 3.0f;
        private const int TARGET_BUILDERS = 3;
        private const int MAX_BUILDERS = 5;

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

            foreach (var (brain, buildingState, buildReqs, entity)
                     in SystemAPI.Query<RefRO<AIBrain>, RefRW<AIBuildingState>, DynamicBuffer<BuildRequest>>()
                                  .WithEntityAccess())
            {
                if (brain.ValueRO.IsActive == 0) continue;

                var state_val = buildingState.ValueRW;

                if (time >= state_val.LastBuildCheck + state_val.BuildCheckInterval)
                {
                    state_val.LastBuildCheck = time;
                    ManageBuilders(ref state, brain.ValueRO.Owner, ref state_val, ecb);
                }

                ProcessBuildRequests(ref state, brain.ValueRO.Owner, buildReqs, ecb);

                state_val.QueuedConstructions = 0;
                for (int i = 0; i < buildReqs.Length; i++)
                {
                    if (buildReqs[i].Assigned == 0)
                        state_val.QueuedConstructions++;
                }

                buildingState.ValueRW = state_val;
            }

            ecb.Playback(em);
            ecb.Dispose();
        }

        private void ManageBuilders(ref SystemState state, Faction faction,
            ref AIBuildingState buildingState, EntityCommandBuffer ecb)
        {
            var em = state.EntityManager;

            int builderCount = 0;
            foreach (var (canBuild, factionTag) in SystemAPI.Query<RefRO<CanBuild>, RefRO<FactionTag>>())
            {
                if (factionTag.ValueRO.Value == faction && canBuild.ValueRO.Value)
                    builderCount++;
            }

            buildingState.ActiveBuilders = builderCount;

            int queueSize = buildingState.QueuedConstructions;
            if (queueSize > 3)
                buildingState.DesiredBuilders = math.min(MAX_BUILDERS, TARGET_BUILDERS + 1);
            else
                buildingState.DesiredBuilders = TARGET_BUILDERS;

            int queuedBuilders = CountQueuedBuilders(ref state, faction);
            int totalBuilders = builderCount + queuedBuilders;

            if (totalBuilders < buildingState.DesiredBuilders)
            {
                int needed = buildingState.DesiredBuilders - totalBuilders;
                RequestBuilders(ref state, faction, needed);
            }

            AssignIdleBuildersToTasks(ref state, faction, ecb);
        }

        private int CountQueuedBuilders(ref SystemState state, Faction faction)
        {
            int count = 0;

            foreach (var (brain, recruitReqs) in SystemAPI.Query<RefRO<AIBrain>, DynamicBuffer<RecruitmentRequest>>())
            {
                if (brain.ValueRO.Owner != faction) continue;

                for (int i = 0; i < recruitReqs.Length; i++)
                {
                    if (recruitReqs[i].UnitType == UnitClass.Economy)
                        count += recruitReqs[i].Quantity;
                }
            }

            foreach (var (factionTag, trainQueue) in SystemAPI.Query<RefRO<FactionTag>, DynamicBuffer<TrainQueueItem>>())
            {
                if (factionTag.ValueRO.Value != faction) continue;

                for (int i = 0; i < trainQueue.Length; i++)
                {
                    if (trainQueue[i].UnitId == "Builder")
                        count++;
                }
            }

            return count;
        }

        private void ProcessBuildRequests(ref SystemState state, Faction faction,
            DynamicBuffer<BuildRequest> buildReqs, EntityCommandBuffer ecb)
        {
            if (buildReqs.Length == 0) return;

            var em = state.EntityManager;

            for (int i = 0; i < buildReqs.Length; i++)
            {
                var req = buildReqs[i];
                if (req.Assigned == 1) continue;

                if (!CanAffordBuilding(ref state, faction, req.BuildingType))
                    continue;

                Entity builder = FindAvailableBuilder(ref state, faction);
                if (builder == Entity.Null)
                    continue;

                StartConstruction(ref state, faction, req, builder, ecb);

                req.Assigned = 1;
                req.AssignedBuilder = builder;
                buildReqs[i] = req;
            }

            for (int i = buildReqs.Length - 1; i >= 0; i--)
            {
                var req = buildReqs[i];
                if (req.Assigned == 1 && !em.Exists(req.AssignedBuilder))
                {
                    buildReqs.RemoveAt(i);
                }
            }
        }

        private bool CanAffordBuilding(ref SystemState state, Faction faction, FixedString64Bytes buildingType)
        {
            foreach (var (factionTag, resources) in SystemAPI.Query<RefRO<FactionTag>, RefRO<FactionResources>>())
            {
                if (factionTag.ValueRO.Value != faction) continue;

                if (TechTreeDB.Instance != null &&
                    TechTreeDB.Instance.TryGetBuilding(buildingType.ToString(), out var buildingDef))
                {
                    return resources.ValueRO.Supplies >= buildingDef.cost.Supplies &&
                           resources.ValueRO.Iron >= buildingDef.cost.Iron &&
                           resources.ValueRO.Crystal >= buildingDef.cost.Crystal;
                }

                return resources.ValueRO.Supplies >= 100;
            }

            return false;
        }

        private Entity FindAvailableBuilder(ref SystemState state, Faction faction)
        {
            foreach (var (canBuild, factionTag, entity) in
                     SystemAPI.Query<RefRO<CanBuild>, RefRO<FactionTag>>()
                              .WithNone<BuildOrder>()
                              .WithEntityAccess())
            {
                if (factionTag.ValueRO.Value == faction && canBuild.ValueRO.Value)
                    return entity;
            }

            return Entity.Null;
        }

        private void StartConstruction(ref SystemState state, Faction faction, BuildRequest req,
            Entity builder, EntityCommandBuffer ecb)
        {
            DeductBuildingCost(ref state, faction, req.BuildingType);

            Entity buildingEntity = CreateBuilding(ref state, faction, req.BuildingType, req.DesiredPosition, ecb);

            if (buildingEntity == Entity.Null)
                return;

            ecb.AddComponent(builder, new BuildOrder { Site = buildingEntity });
        }

        private void DeductBuildingCost(ref SystemState state, Faction faction, FixedString64Bytes buildingType)
        {
            foreach (var (factionTag, resources, entity) in
                SystemAPI.Query<RefRO<FactionTag>, RefRW<FactionResources>>()
                .WithEntityAccess())
            {
                if (factionTag.ValueRO.Value != faction) continue;

                var res = resources.ValueRW;

                if (TechTreeDB.Instance != null && TechTreeDB.Instance.TryGetBuilding(buildingType.ToString(), out var buildingDef))
                {
                    res.Supplies -= buildingDef.cost.Supplies;
                    res.Iron -= buildingDef.cost.Iron;
                    res.Crystal -= buildingDef.cost.Crystal;
                    res.Veilsteel -= buildingDef.cost.Veilsteel;
                    res.Glow -= buildingDef.cost.Glow;
                }
                else
                {
                    res.Supplies -= 100;
                }

                break;
            }
        }

        private Entity CreateBuilding(ref SystemState state, Faction faction,
            FixedString64Bytes buildingType, float3 position, EntityCommandBuffer ecb)
        {
            var building = ecb.CreateEntity();

            ecb.AddComponent(building, LocalTransform.FromPositionRotationScale(
                position, quaternion.identity, 1f));
            ecb.AddComponent(building, new FactionTag { Value = faction });
            ecb.AddComponent(building, new BuildingTag { IsBase = 0 });

            if (TechTreeDB.Instance != null &&
                TechTreeDB.Instance.TryGetBuilding(buildingType.ToString(), out var buildingDef))
            {
                ecb.AddComponent(building, new Health { Value = 1, Max = (int)buildingDef.hp });
                ecb.AddComponent(building, new Buildable { BuildTimeSeconds = 10 });
                ecb.AddComponent(building, new UnderConstruction { Progress = 0, Total = 10 });
            }
            else
            {
                ecb.AddComponent(building, new Health { Value = 1, Max = 500 });
                ecb.AddComponent(building, new Buildable { BuildTimeSeconds = 30f });
                ecb.AddComponent(building, new UnderConstruction { Progress = 0, Total = 30f });
            }

            if (buildingType.Equals("GatherersHut"))
            {
                ecb.AddComponent(building, new GathererHutTag());
                ecb.AddComponent(building, new PresentationId { Id = 101 });
            }
            else if (buildingType.Equals("Hut"))
            {
                ecb.AddComponent(building, new HutTag());
                ecb.AddComponent(building, new PopulationProvider { Amount = 10 });
                ecb.AddComponent(building, new PresentationId { Id = 102 });
            }
            else if (buildingType.Equals("Barracks"))
            {
                ecb.AddComponent(building, new BarracksTag());
                ecb.AddComponent(building, new TrainingState { Busy = 0, Remaining = 0 });
                ecb.AddBuffer<TrainQueueItem>(building);
                ecb.AddComponent(building, new PresentationId { Id = 103 });
            }
            else if (buildingType.Equals("Hall"))
            {
                ecb.AddComponent(building, new BuildingTag { IsBase = 1 });
                ecb.AddComponent(building, new SuppliesIncome { PerMinute = 180 });
                ecb.AddComponent(building, new PopulationProvider { Amount = 20 });
                ecb.AddComponent(building, new PresentationId { Id = 100 });
            }

            return building;
        }

        private void AssignIdleBuildersToTasks(ref SystemState state, Faction faction, EntityCommandBuffer ecb)
        {
            var idleBuilders = new NativeList<Entity>(Allocator.Temp);

            foreach (var (canBuild, factionTag, entity) in
                     SystemAPI.Query<RefRO<CanBuild>, RefRO<FactionTag>>()
                              .WithNone<BuildOrder>()
                              .WithEntityAccess())
            {
                if (factionTag.ValueRO.Value == faction && canBuild.ValueRO.Value)
                    idleBuilders.Add(entity);
            }

            foreach (var (underConstruction, factionTag, entity) in
                     SystemAPI.Query<RefRO<UnderConstruction>, RefRO<FactionTag>>()
                              .WithEntityAccess())
            {
                if (factionTag.ValueRO.Value != faction) continue;
                if (idleBuilders.Length == 0) break;

                bool hasBuilder = false;
                foreach (var (buildOrder, _) in SystemAPI.Query<RefRO<BuildOrder>, RefRO<FactionTag>>())
                {
                    if (buildOrder.ValueRO.Site == entity)
                    {
                        hasBuilder = true;
                        break;
                    }
                }

                if (!hasBuilder)
                {
                    Entity builder = idleBuilders[0];
                    idleBuilders.RemoveAt(0);

                    ecb.AddComponent(builder, new BuildOrder { Site = entity });
                }
            }

            idleBuilders.Dispose();
        }

        private void RequestBuilders(ref SystemState state, Faction faction, int count)
        {
            foreach (var (brain, recruitReqs, entity) in
                     SystemAPI.Query<RefRO<AIBrain>, DynamicBuffer<RecruitmentRequest>>()
                              .WithEntityAccess())
            {
                if (brain.ValueRO.Owner != faction) continue;

                recruitReqs.Add(new RecruitmentRequest
                {
                    UnitType = UnitClass.Economy,
                    Quantity = count,
                    Priority = 7,
                    RequestingManager = entity
                });

                break;
            }
        }
    }
}