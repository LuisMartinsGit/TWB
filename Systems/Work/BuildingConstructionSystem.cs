// File: Assets/Scripts/Systems/Work/BuildingConstructionSystem.cs
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace TheWaningBorder.Systems.Work
{
    /// <summary>
    /// Handles building construction by builder units.
    /// 
    /// Construction workflow:
    /// 1. Player places building ghost (UnderConstruction component, low HP)
    /// 2. Builder receives BuildOrder component pointing to construction site
    /// 3. Builder moves to site and contributes build progress
    /// 4. When Progress >= Total, building completes:
    ///    - UnderConstruction removed
    ///    - Health set to max
    ///    - DeferredDefense applied as Defense component
    /// 
    /// Multiple builders can work on the same building simultaneously.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct BuildingConstructionSystem : ISystem
    {
        private const float BuildRange = 2.0f;
        private const float BuildRatePerBuilder = 1.0f; // Progress per second per builder

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BuildOrder>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            var em = state.EntityManager;

            // Snapshot all builders with orders
            var builderQuery = SystemAPI.QueryBuilder()
                .WithAll<CanBuild, LocalTransform, BuildOrder>()
                .Build();

            var builders = new NativeList<Entity>(Allocator.Temp);
            var builderPositions = new NativeList<float3>(Allocator.Temp);
            var builderOrders = new NativeList<BuildOrder>(Allocator.Temp);

            foreach (var (transform, order, entity) in SystemAPI
                .Query<RefRO<LocalTransform>, RefRO<BuildOrder>>()
                .WithAll<CanBuild>()
                .WithEntityAccess())
            {
                builders.Add(entity);
                builderPositions.Add(transform.ValueRO.Position);
                builderOrders.Add(order.ValueRO);
            }

            // Process each builder
            for (int i = 0; i < builders.Length; i++)
            {
                Entity builder = builders[i];
                float3 bPos = builderPositions[i];
                Entity site = builderOrders[i].Site;

                // Validate construction site exists
                if (!em.Exists(site))
                {
                    // Site destroyed - clear order
                    em.RemoveComponent<BuildOrder>(builder);
                    continue;
                }

                // Check if site is still under construction
                if (!em.HasComponent<UnderConstruction>(site))
                {
                    // Already finished - clear order
                    em.RemoveComponent<BuildOrder>(builder);
                    continue;
                }

                // Get site position
                float3 sitePos = em.GetComponentData<LocalTransform>(site).Position;
                float dist = math.distance(bPos, sitePos);

                if (dist > BuildRange)
                {
                    // Move toward site
                    if (em.HasComponent<DesiredDestination>(builder))
                    {
                        em.SetComponentData(builder, new DesiredDestination
                        {
                            Position = sitePos,
                            Has = 1
                        });
                    }
                    else
                    {
                        em.AddComponentData(builder, new DesiredDestination
                        {
                            Position = sitePos,
                            Has = 1
                        });
                    }
                }
                else
                {
                    // In range - stop moving and contribute to construction
                    if (em.HasComponent<DesiredDestination>(builder))
                    {
                        em.SetComponentData(builder, new DesiredDestination { Has = 0 });
                    }

                    // Add build progress
                    var uc = em.GetComponentData<UnderConstruction>(site);
                    uc.Progress += BuildRatePerBuilder * dt;

                    if (uc.Progress >= uc.Total)
                    {
                        // Construction complete!
                        CompleteConstruction(em, site);
                        em.RemoveComponent<BuildOrder>(builder);
                    }
                    else
                    {
                        em.SetComponentData(site, uc);
                    }
                }
            }

            builders.Dispose();
            builderPositions.Dispose();
            builderOrders.Dispose();
        }

        /// <summary>
        /// Finalizes building construction:
        /// - Removes UnderConstruction component
        /// - Sets health to maximum
        /// - Applies deferred defense stats
        /// </summary>
        private void CompleteConstruction(EntityManager em, Entity building)
        {
            // Remove construction marker
            em.RemoveComponent<UnderConstruction>(building);

            // Set health to max
            if (em.HasComponent<Health>(building))
            {
                var hp = em.GetComponentData<Health>(building);
                hp.Value = hp.Max;
                em.SetComponentData(building, hp);
            }

            // Apply deferred defense if present
            if (em.HasComponent<DeferredDefense>(building))
            {
                var def = em.GetComponentData<DeferredDefense>(building);
                
                if (!em.HasComponent<Defense>(building))
                {
                    em.AddComponentData(building, new Defense
                    {
                        Melee = def.Melee,
                        Ranged = def.Ranged,
                        Siege = def.Siege,
                        Magic = def.Magic
                    });
                }
                else
                {
                    em.SetComponentData(building, new Defense
                    {
                        Melee = def.Melee,
                        Ranged = def.Ranged,
                        Siege = def.Siege,
                        Magic = def.Magic
                    });
                }
                
                em.RemoveComponent<DeferredDefense>(building);
            }

            UnityEngine.Debug.Log($"Building {building.Index} construction complete!");
        }
    }

    /// <summary>
    /// Processes BuildCommand components issued through CommandGateway.
    /// Moves builders to construction sites and manages the build workflow.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(BuildingConstructionSystem))]
    public partial struct BuildCommandSystem : ISystem
    {
        private const float BuildRange = 2f;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);
            var em = state.EntityManager;

            foreach (var (transform, buildCmd, entity) in SystemAPI
                .Query<RefRO<LocalTransform>, RefRO<BuildCommand>>()
                .WithAll<CanBuild>()
                .WithEntityAccess())
            {
                var myPos = transform.ValueRO.Position;
                var targetPos = buildCmd.ValueRO.Position;
                var targetBuilding = buildCmd.ValueRO.TargetBuilding;
                var dist = math.distance(myPos, targetPos);

                // Move to build site if not in range
                if (dist > BuildRange)
                {
                    if (!em.HasComponent<DesiredDestination>(entity))
                    {
                        ecb.AddComponent(entity, new DesiredDestination
                        {
                            Position = targetPos,
                            Has = 1
                        });
                    }
                    else
                    {
                        ecb.SetComponent(entity, new DesiredDestination
                        {
                            Position = targetPos,
                            Has = 1
                        });
                    }
                }
                else
                {
                    // In range - stop moving
                    if (em.HasComponent<DesiredDestination>(entity))
                    {
                        ecb.SetComponent(entity, new DesiredDestination { Has = 0 });
                    }

                    // Convert BuildCommand to BuildOrder if target building exists
                    if (targetBuilding != Entity.Null && em.Exists(targetBuilding))
                    {
                        if (em.HasComponent<UnderConstruction>(targetBuilding))
                        {
                            // Add BuildOrder and remove BuildCommand
                            if (!em.HasComponent<BuildOrder>(entity))
                            {
                                ecb.AddComponent(entity, new BuildOrder { Site = targetBuilding });
                            }
                            else
                            {
                                ecb.SetComponent(entity, new BuildOrder { Site = targetBuilding });
                            }
                            
                            ecb.RemoveComponent<BuildCommand>(entity);
                        }
                        else
                        {
                            // Building already complete - clear command
                            ecb.RemoveComponent<BuildCommand>(entity);
                        }
                    }
                    else
                    {
                        // No valid target building - clear command
                        // (Building creation should happen elsewhere, e.g., UI system)
                        ecb.RemoveComponent<BuildCommand>(entity);
                    }
                }
            }
        }
    }
}