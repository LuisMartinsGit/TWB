using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Unified work system that handles Build, Gather, and Heal commands.
/// Processes non-combat unit actions issued through CommandGateway.
/// </summary>
[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(UnifiedCombatSystem))]
public partial struct UnifiedWorkSystem : ISystem
{
    private const float BuildRange = 2f;
    private const float GatherRange = 1.5f;
    private const float HealRange = 3f;

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
        var dt = SystemAPI.Time.DeltaTime;
        var em = state.EntityManager;

        // Process Build Commands
        ProcessBuildCommands(ref state, ref ecb);

        // Process Gather Commands
        ProcessGatherCommands(ref state, ref ecb, dt);

        // Process Heal Commands
        ProcessHealCommands(ref state, ref ecb, dt);
    }

    [BurstCompile]
    private void ProcessBuildCommands(ref SystemState state, ref EntityCommandBuffer ecb)
    {
        var em = state.EntityManager;

        foreach (var (transform, buildCmd, entity) in SystemAPI
            .Query<RefRO<LocalTransform>, RefRO<BuildCommand>>()
            .WithAll<CanBuild>()
            .WithEntityAccess())
        {
            var myPos = transform.ValueRO.Position;
            var targetPos = buildCmd.ValueRO.Position;
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
                // In range - stop moving and build
                if (em.HasComponent<DesiredDestination>(entity))
                {
                    ecb.SetComponent(entity, new DesiredDestination { Has = 0 });
                }

                // TODO: Implement actual building construction logic here
                // For now, just a placeholder that would:
                // 1. Check if TargetBuilding exists
                // 2. Update construction progress
                // 3. Complete building when done
                // 4. Remove BuildCommand when finished
            }
        }
    }

    [BurstCompile]
    private void ProcessGatherCommands(ref SystemState state, ref EntityCommandBuffer ecb, float dt)
    {
        var em = state.EntityManager;

        foreach (var (transform, gatherCmd, entity) in SystemAPI
            .Query<RefRO<LocalTransform>, RefRO<GatherCommand>>()
            .WithEntityAccess())
        {
            var resourceNode = gatherCmd.ValueRO.ResourceNode;
            var depositLocation = gatherCmd.ValueRO.DepositLocation;

            // Validate resource node still exists
            if (!em.Exists(resourceNode))
            {
                ecb.RemoveComponent<GatherCommand>(entity);
                continue;
            }

            // Check if miner has MinerState component
            if (!em.HasComponent<TheWaningBorder.Humans.MinerState>(entity))
            {
                ecb.RemoveComponent<GatherCommand>(entity);
                continue;
            }

            var minerState = em.GetComponentData<TheWaningBorder.Humans.MinerState>(entity);
            var myPos = transform.ValueRO.Position;

            // Determine where to go based on miner state
            if (minerState.State == TheWaningBorder.Humans.MinerWorkState.Idle ||
                minerState.State == TheWaningBorder.Humans.MinerWorkState.MovingToDeposit)
            {
                // Move to resource node
                var nodePos = em.GetComponentData<LocalTransform>(resourceNode).Position;
                var dist = math.distance(myPos, nodePos);

                if (dist > GatherRange)
                {
                    minerState.State = TheWaningBorder.Humans.MinerWorkState.MovingToDeposit;
                    ecb.SetComponent(entity, minerState);

                    if (!em.HasComponent<DesiredDestination>(entity))
                    {
                        ecb.AddComponent(entity, new DesiredDestination
                        {
                            Position = nodePos,
                            Has = 1
                        });
                    }
                    else
                    {
                        ecb.SetComponent(entity, new DesiredDestination
                        {
                            Position = nodePos,
                            Has = 1
                        });
                    }
                }
                else
                {
                    // Start gathering
                    minerState.State = TheWaningBorder.Humans.MinerWorkState.Gathering;
                    minerState.GatherTimer = 3f; // 3 seconds to gather
                    ecb.SetComponent(entity, minerState);

                    if (em.HasComponent<DesiredDestination>(entity))
                    {
                        ecb.SetComponent(entity, new DesiredDestination { Has = 0 });
                    }
                }
            }
            else if (minerState.State == TheWaningBorder.Humans.MinerWorkState.Gathering)
            {
                // Gathering in progress
                minerState.GatherTimer -= dt;

                if (minerState.GatherTimer <= 0)
                {
                    // Gathered full load
                    minerState.CurrentLoad = 10; // Gathered 10 units
                    minerState.State = TheWaningBorder.Humans.MinerWorkState.ReturningToBase;
                }

                ecb.SetComponent(entity, minerState);
            }
            else if (minerState.State == TheWaningBorder.Humans.MinerWorkState.ReturningToBase)
            {
                // Return to deposit location
                if (!em.Exists(depositLocation))
                {
                    // No deposit location, cancel gathering
                    ecb.RemoveComponent<GatherCommand>(entity);
                    minerState.State = TheWaningBorder.Humans.MinerWorkState.Idle;
                    ecb.SetComponent(entity, minerState);
                    continue;
                }

                var depositPos = em.GetComponentData<LocalTransform>(depositLocation).Position;
                var dist = math.distance(myPos, depositPos);

                if (dist > GatherRange)
                {
                    if (!em.HasComponent<DesiredDestination>(entity))
                    {
                        ecb.AddComponent(entity, new DesiredDestination
                        {
                            Position = depositPos,
                            Has = 1
                        });
                    }
                    else
                    {
                        ecb.SetComponent(entity, new DesiredDestination
                        {
                            Position = depositPos,
                            Has = 1
                        });
                    }
                }
                else
                {
                    // At deposit location - deposit resources
                    // TODO: Add resources to faction bank
                    minerState.CurrentLoad = 0;
                    minerState.State = TheWaningBorder.Humans.MinerWorkState.MovingToDeposit;
                    ecb.SetComponent(entity, minerState);

                    if (em.HasComponent<DesiredDestination>(entity))
                    {
                        ecb.SetComponent(entity, new DesiredDestination { Has = 0 });
                    }
                }
            }
        }
    }

    [BurstCompile]
    private void ProcessHealCommands(ref SystemState state, ref EntityCommandBuffer ecb, float dt)
    {
        var em = state.EntityManager;

        foreach (var (transform, healCmd, faction, entity) in SystemAPI
            .Query<RefRO<LocalTransform>, RefRO<HealCommand>, RefRO<FactionTag>>()
            .WithEntityAccess())
        {
            var target = healCmd.ValueRO.Target;

            // Validate target still exists and is alive
            if (!em.Exists(target))
            {
                ecb.RemoveComponent<HealCommand>(entity);
                continue;
            }

            if (!em.HasComponent<Health>(target))
            {
                ecb.RemoveComponent<HealCommand>(entity);
                continue;
            }

            var targetHealth = em.GetComponentData<Health>(target);
            if (targetHealth.Value <= 0 || targetHealth.Value >= targetHealth.Max)
            {
                // Target is dead or already at full health
                ecb.RemoveComponent<HealCommand>(entity);
                continue;
            }

            // Verify target is still friendly
            if (em.HasComponent<FactionTag>(target))
            {
                var targetFaction = em.GetComponentData<FactionTag>(target).Value;
                if (targetFaction != faction.ValueRO.Value)
                {
                    // Target changed faction somehow, cancel
                    ecb.RemoveComponent<HealCommand>(entity);
                    continue;
                }
            }

            var myPos = transform.ValueRO.Position;
            var targetPos = em.GetComponentData<LocalTransform>(target).Position;
            var dist = math.distance(myPos, targetPos);

            // Move to heal range if not in range
            if (dist > HealRange)
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
                // In range - stop moving and heal
                if (em.HasComponent<DesiredDestination>(entity))
                {
                    ecb.SetComponent(entity, new DesiredDestination { Has = 0 });
                }

                // TODO: Implement actual healing logic here
                // For now, placeholder that would:
                // 1. Have a heal cooldown timer
                // 2. Heal X HP per tick
                // 3. Remove HealCommand when target is at full health

                // Simple heal demonstration (would need HealState component for proper implementation)
                targetHealth.Value = math.min(targetHealth.Value + (int)(20f * dt), targetHealth.Max);
                ecb.SetComponent(target, targetHealth);

                // If target is now at full health, stop healing
                if (targetHealth.Value >= targetHealth.Max)
                {
                    ecb.RemoveComponent<HealCommand>(entity);
                }
            }
        }
    }
}
