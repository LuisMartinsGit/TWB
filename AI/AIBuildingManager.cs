// AIBuildingManager.cs
// Controls builders, processes build requests, manages construction
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using TheWaningBorder.Humans;
using TheWaningBorder.Economy;

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

            // Process each AI player's building operations
            foreach (var (brain, buildingState, buildReqs, entity) 
                in SystemAPI.Query<RefRO<AIBrain>, RefRW<AIBuildingState>, DynamicBuffer<BuildRequest>>()
                .WithEntityAccess())
            {
                if (brain.ValueRO.IsActive == 0) continue;

                var state_val = buildingState.ValueRW;

                // Periodic builder management
                if (time >= state_val.LastBuildCheck + state_val.BuildCheckInterval)
                {
                    state_val.LastBuildCheck = time;
                    ManageBuilders(ref state, brain.ValueRO.Owner, ref state_val, ecb);
                }

                // Process build requests
                ProcessBuildRequests(ref state, brain.ValueRO.Owner, buildReqs, ecb);

                // Update queued constructions count
                state_val.QueuedConstructions = 0;
                for (int i = 0; i < buildReqs.Length; i++)
                {
                    if (buildReqs[i].Assigned == 0)
                        state_val.QueuedConstructions++;
                }
            }

            ecb.Playback(em);
            ecb.Dispose();
        }

        private void ManageBuilders(ref SystemState state, Faction faction, 
            ref AIBuildingState buildingState, EntityCommandBuffer ecb)
        {
            var em = state.EntityManager;

            // Count active builders
            int builderCount = 0;
            foreach (var (canBuild, factionTag) in SystemAPI.Query<RefRO<CanBuild>, RefRO<FactionTag>>())
            {
                if (factionTag.ValueRO.Value == faction && canBuild.ValueRO.Value)
                    builderCount++;
            }

            buildingState.ActiveBuilders = builderCount;

            // Calculate desired builders based on queue size
            int queueSize = buildingState.QueuedConstructions;
            if (queueSize > 3)
                buildingState.DesiredBuilders = math.min(MAX_BUILDERS, TARGET_BUILDERS + 1);
            else
                buildingState.DesiredBuilders = TARGET_BUILDERS;

            // Request more builders if needed
            if (builderCount < buildingState.DesiredBuilders)
            {
                int buildersNeeded = buildingState.DesiredBuilders - builderCount;
                RequestBuilders(ref state, faction, buildersNeeded);
            }

            // Assign idle builders to pending construction tasks
            AssignIdleBuildersToTasks(ref state, faction, ecb);
        }

        private void ProcessBuildRequests(ref SystemState state, Faction faction,
            DynamicBuffer<BuildRequest> buildReqs, EntityCommandBuffer ecb)
        {
            if (buildReqs.Length == 0) return;

            var em = state.EntityManager;

            // Sort by priority and process
            for (int i = 0; i < buildReqs.Length; i++)
            {
                var req = buildReqs[i];
                if (req.Assigned == 1) continue; // Already being built

                // Check if we can afford this building
                if (!CanAffordBuilding(ref state, faction, req.BuildingType))
                    continue;

                // Try to find an available builder
                Entity builder = FindAvailableBuilder(ref state, faction);
                if (builder == Entity.Null)
                    continue;

                // Start construction
                StartConstruction(ref state, faction, req, builder, ecb);

                // Mark as assigned
                req.Assigned = 1;
                req.AssignedBuilder = builder;
                buildReqs[i] = req;
            }

            // Clean up completed requests
            for (int i = buildReqs.Length - 1; i >= 0; i--)
            {
                var req = buildReqs[i];
                if (req.Assigned == 1 && !em.Exists(req.AssignedBuilder))
                {
                    // Builder no longer exists, remove request
                    buildReqs.RemoveAt(i);
                }
            }
        }

        private bool CanAffordBuilding(ref SystemState state, Faction faction, FixedString64Bytes buildingType)
        {
            // Get faction resources
            foreach (var (factionTag, resources) in SystemAPI.Query<RefRO<FactionTag>, RefRO<FactionResources>>())
            {
                if (factionTag.ValueRO.Value != faction) continue;

                // Get building cost from TechTreeDB
                if (TechTreeDB.Instance != null && TechTreeDB.Instance.TryGetBuilding(buildingType.ToString(), out var buildingDef))
                {
                    return resources.ValueRO.Supplies >= buildingDef.cost.Supplies &&
                           resources.ValueRO.Iron >= buildingDef.cost.Iron &&
                           resources.ValueRO.Crystal >= buildingDef.cost.Crystal;
                }

                // Default costs if not in DB
                return resources.ValueRO.Supplies >= 100;
            }

            return false;
        }

        private Entity FindAvailableBuilder(ref SystemState state, Faction faction)
        {
            // Find builder that is not currently building
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
            var em = state.EntityManager;

            // Create the building entity
            Entity buildingEntity = CreateBuilding(ref state, faction, req.BuildingType, req.DesiredPosition, ecb);

            if (buildingEntity == Entity.Null)
                return;

            // Assign builder to this construction
            ecb.AddComponent(builder, new BuildOrder { Site = buildingEntity });

            // Deduct resources
            DeductBuildingCost(ref state, faction, req.BuildingType);
        }

        private Entity CreateBuilding(ref SystemState state, Faction faction, 
            FixedString64Bytes buildingType, float3 position, EntityCommandBuffer ecb)
        {
            // Create building entity based on type
            var building = ecb.CreateEntity();

            // Add common components
            ecb.AddComponent(building, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
            ecb.AddComponent(building, new FactionTag { Value = faction });
            ecb.AddComponent(building, new BuildingTag { IsBase = 0 });

            // Get building definition from TechTreeDB
            if (TechTreeDB.Instance != null && TechTreeDB.Instance.TryGetBuilding(buildingType.ToString(), out var buildingDef))
            {
                ecb.AddComponent(building, new Health { Value = 1, Max = (int)buildingDef.hp });
                ecb.AddComponent(building, new Buildable { BuildTimeSeconds = 10 });
                ecb.AddComponent(building, new UnderConstruction 
                { 
                    Progress = 0, 
                    Total = 10 
                });
            }
            else
            {
                // Default values
                ecb.AddComponent(building, new Health { Value = 1, Max = 500 });
                ecb.AddComponent(building, new Buildable { BuildTimeSeconds = 30f });
                ecb.AddComponent(building, new UnderConstruction { Progress = 0, Total = 30f });
            }

            // Add specific building tags
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

        private void DeductBuildingCost(ref SystemState state, Faction faction, FixedString64Bytes buildingType)
        {
            var em = state.EntityManager;

            // Get faction resources
            foreach (var (factionTag, resources, entity) in 
                SystemAPI.Query<RefRO<FactionTag>, RefRW<FactionResources>>()
                .WithEntityAccess())
            {
                if (factionTag.ValueRO.Value != faction) continue;

                var res = resources.ValueRW;

                // Get building cost from TechTreeDB
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
                    // Default cost
                    res.Supplies -= 100;
                }

                break;
            }
        }

        private void AssignIdleBuildersToTasks(ref SystemState state, Faction faction, EntityCommandBuffer ecb)
        {
            // Find builders without orders
            var idleBuilders = new NativeList<Entity>(Allocator.Temp);
            
            foreach (var (canBuild, factionTag, entity) in 
                SystemAPI.Query<RefRO<CanBuild>, RefRO<FactionTag>>()
                .WithNone<BuildOrder>()
                .WithEntityAccess())
            {
                if (factionTag.ValueRO.Value == faction && canBuild.ValueRO.Value)
                    idleBuilders.Add(entity);
            }

            // Find unfinished buildings without assigned builders
            foreach (var (underConstruction, factionTag, entity) in 
                SystemAPI.Query<RefRO<UnderConstruction>, RefRO<FactionTag>>()
                .WithEntityAccess())
            {
                if (factionTag.ValueRO.Value != faction) continue;
                if (idleBuilders.Length == 0) break;

                // Check if this building already has a builder
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
                    // Assign an idle builder
                    Entity builder = idleBuilders[0];
                    idleBuilders.RemoveAt(0);
                    
                    ecb.AddComponent(builder, new BuildOrder { Site = entity });
                }
            }

            idleBuilders.Dispose();
        }

        private void RequestBuilders(ref SystemState state, Faction faction, int count)
        {
            // Find the economy manager to request builder training
            foreach (var (brain, recruitReqs, entity) in 
                SystemAPI.Query<RefRO<AIBrain>, DynamicBuffer<RecruitmentRequest>>()
                .WithEntityAccess())
            {
                if (brain.ValueRO.Owner != faction) continue;

                // Add recruitment request for builders
                recruitReqs.Add(new RecruitmentRequest
                {
                    UnitType = UnitClass.Economy, // Builders are economy units
                    Quantity = count,
                    Priority = 7, // High priority
                    RequestingManager = entity
                });

                break;
            }
        }
    }
}