using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Unified combat system handling both melee (swordsmen) and ranged (archers) units.
/// Implements proper RTS behavior with guard points, auto-acquire, and command-based combat.
/// FIXED: Respects active movement by checking DesiredDestination, height-compensated trajectories
/// </summary>
[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct UnifiedCombatSystem : ISystem
{
    private const float MaxGuardDistance = 20f; // How far units can stray before returning
    private const float GuardReturnThreshold = 2f; // Distance to consider "at guard point"
    
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
        var time = SystemAPI.Time.ElapsedTime;
        var em = state.EntityManager;

        // =============================================================================
        // PHASE 0: Initialize required components for combat
        // =============================================================================
        
        // Initialize GuardPoint for units that don't have one
        foreach (var (transform, entity) in SystemAPI.Query<RefRO<LocalTransform>>()
            .WithAll<UnitTag>()
            .WithNone<GuardPoint>()
            .WithEntityAccess())
        {
            ecb.AddComponent(entity, new GuardPoint 
            { 
                Position = transform.ValueRO.Position,
                Has = 1
            });
        }
        
        // Initialize AttackCooldown for units that don't have one
        foreach (var (tag, entity) in SystemAPI.Query<RefRO<UnitTag>>()
            .WithNone<AttackCooldown>()
            .WithEntityAccess())
        {
            ecb.AddComponent(entity, new AttackCooldown 
            { 
                Cooldown = 1.5f,
                Timer = 0f
            });
        }

        // =============================================================================
        // PHASE 1: Handle user attack commands - set up combat immediately
        // =============================================================================
        foreach (var (attackCmd, transform, entity) in SystemAPI
            .Query<RefRO<AttackCommand>, RefRO<LocalTransform>>()
            .WithAll<UnitTag>()
            .WithEntityAccess())
        {
            // FIXED: Check if unit is actively moving by player command
            // If DesiredDestination exists and was set by MoveCommand, skip this unit
            if (em.HasComponent<DesiredDestination>(entity))
            {
                var dd = em.GetComponentData<DesiredDestination>(entity);
                // If unit has active destination and no AttackCommand pointing at guard point,
                // it means player issued a move order - respect it
                if (dd.Has != 0)
                {
                    // Check if this is a return-to-guard movement (not player-ordered)
                    bool isReturningToGuard = false;
                    if (em.HasComponent<GuardPoint>(entity))
                    {
                        var gp = em.GetComponentData<GuardPoint>(entity);
                        if (gp.Has != 0)
                        {
                            var distToGuard = math.distance(dd.Position, gp.Position);
                            isReturningToGuard = distToGuard < 1f; // Moving to guard point
                        }
                    }
                    
                    // If not returning to guard, this is a player move order - skip attack processing
                    if (!isReturningToGuard && !em.HasComponent<Target>(entity))
                    {
                        // Player ordered unit to move - clear AttackCommand
                        ecb.RemoveComponent<AttackCommand>(entity);
                        continue;
                    }
                }
            }
            
            var target = attackCmd.ValueRO.Target;
            
            // Validate target
            if (target == Entity.Null || !em.Exists(target))
            {
                ecb.RemoveComponent<AttackCommand>(entity);
                continue;
            }

            // Check if target is dead
            if (em.HasComponent<Health>(target))
            {
                var targetHealth = em.GetComponentData<Health>(target);
                if (targetHealth.Value <= 0)
                {
                    ecb.RemoveComponent<AttackCommand>(entity);
                    continue;
                }
            }

            // Update or create guard point at current position
            if (em.HasComponent<GuardPoint>(entity))
            {
                var gp = em.GetComponentData<GuardPoint>(entity);
                if (gp.Has == 0)
                {
                    gp.Position = transform.ValueRO.Position;
                    gp.Has = 1;
                    ecb.SetComponent(entity, gp);
                }
            }
            else
            {
                ecb.AddComponent(entity, new GuardPoint 
                { 
                    Position = transform.ValueRO.Position,
                    Has = 1
                });
            }

            // Set target component for combat processing
            if (!em.HasComponent<Target>(entity))
            {
                ecb.AddComponent(entity, new Target { Value = target });
            }
            else
            {
                ecb.SetComponent(entity, new Target { Value = target });
            }

            // Clear any movement command - we're attacking now
            if (em.HasComponent<DesiredDestination>(entity))
            {
                ecb.SetComponent(entity, new DesiredDestination { Has = 0 });
            }
            
            // DON'T remove AttackCommand - keep it to indicate user intent
        }

        // =============================================================================
        // PHASE 2: Auto-acquire targets for idle units (no explicit attack command)
        // FIXED: Don't auto-acquire if unit is actively moving
        // =============================================================================
        var enemyQuery = SystemAPI.QueryBuilder()
            .WithAll<LocalTransform, FactionTag, Health>()
            .WithAll<UnitTag>()
            .Build();

        using var allEnemies = enemyQuery.ToEntityArray(Allocator.Temp);
        using var allEnemyTransforms = enemyQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
        using var allEnemyFactions = enemyQuery.ToComponentDataArray<FactionTag>(Allocator.Temp);
        using var allEnemyHealth = enemyQuery.ToComponentDataArray<Health>(Allocator.Temp);

        foreach (var (transform, faction, lineOfSight, entity) in SystemAPI
            .Query<RefRO<LocalTransform>, RefRO<FactionTag>, RefRO<LineOfSight>>()
            .WithAll<UnitTag>()
            .WithNone<AttackCommand>()
            .WithNone<Target>()
            .WithEntityAccess())
        {
            // FIXED: Don't auto-acquire if unit is actively moving
            if (em.HasComponent<DesiredDestination>(entity))
            {
                var dd = em.GetComponentData<DesiredDestination>(entity);
                if (dd.Has != 0)
                {
                    // Unit is moving - don't auto-acquire
                    continue;
                }
            }
            
            var myPos = transform.ValueRO.Position;
            var myFaction = faction.ValueRO.Value;
            var los = lineOfSight.ValueRO.Radius;
            
            // Check if we have a guard point, and if we've strayed too far
            if (em.HasComponent<GuardPoint>(entity))
            {
                var guardPoint = em.GetComponentData<GuardPoint>(entity);
                if (guardPoint.Has != 0)
                {
                    var distFromGuard = math.distance(myPos, guardPoint.Position);
                    if (distFromGuard > MaxGuardDistance)
                    {
                        // Return to guard point instead of auto-acquiring
                        if (!em.HasComponent<DesiredDestination>(entity))
                        {
                            ecb.AddComponent(entity, new DesiredDestination 
                            { 
                                Position = guardPoint.Position,
                                Has = 1
                            });
                        }
                        else
                        {
                            ecb.SetComponent(entity, new DesiredDestination 
                            { 
                                Position = guardPoint.Position,
                                Has = 1
                            });
                        }
                        continue;
                    }
                }
            }

            // Find nearest enemy in line of sight
            Entity bestTarget = Entity.Null;
            float bestDist = float.MaxValue;

            for (int i = 0; i < allEnemies.Length; i++)
            {
                if (allEnemyFactions[i].Value == myFaction) continue;
                if (allEnemyHealth[i].Value <= 0) continue;

                var enemyPos = allEnemyTransforms[i].Position;
                var dist = math.distance(myPos, enemyPos);

                if (dist <= los && dist < bestDist)
                {
                    bestTarget = allEnemies[i];
                    bestDist = dist;
                }
            }

            // Auto-acquire target if found
            if (bestTarget != Entity.Null)
            {
                if (!em.HasComponent<Target>(entity))
                {
                    ecb.AddComponent(entity, new Target { Value = bestTarget });
                }
                else
                {
                    ecb.SetComponent(entity, new Target { Value = bestTarget });
                }
            }
        }

        // =============================================================================
        // PHASE 3: Combat processing for units with targets
        // =============================================================================
        ProcessMeleeCombat(ref state, ref ecb, dt, time);
        ProcessRangedCombat(ref state, ref ecb, dt, time);

        // =============================================================================
        // PHASE 4: Return to guard point after combat ends
        // =============================================================================
        foreach (var (transform, guardPoint, entity) in SystemAPI
            .Query<RefRO<LocalTransform>, RefRO<GuardPoint>>()
            .WithAll<UnitTag>()
            .WithNone<Target>()
            .WithNone<AttackCommand>()
            .WithEntityAccess())
        {
            if (guardPoint.ValueRO.Has == 0) continue;

            var myPos = transform.ValueRO.Position;
            var gpPos = guardPoint.ValueRO.Position;
            var distToGuard = math.distance(myPos, gpPos);

            // If far from guard point and not currently moving there, start returning
            if (distToGuard > GuardReturnThreshold)
            {
                bool isMovingToGuard = false;
                if (em.HasComponent<DesiredDestination>(entity))
                {
                    var dest = em.GetComponentData<DesiredDestination>(entity);
                    if (dest.Has != 0)
                    {
                        var distToDest = math.distance(dest.Position, gpPos);
                        isMovingToGuard = distToDest < 1f; // Already moving to guard
                    }
                }

                if (!isMovingToGuard)
                {
                    if (!em.HasComponent<DesiredDestination>(entity))
                    {
                        ecb.AddComponent(entity, new DesiredDestination 
                        { 
                            Position = gpPos,
                            Has = 1
                        });
                    }
                    else
                    {
                        ecb.SetComponent(entity, new DesiredDestination 
                        { 
                            Position = gpPos,
                            Has = 1
                        });
                    }
                }
            }
        }

        // =============================================================================
        // PHASE 5: Clean up stale AttackCommand components
        // Remove AttackCommand from units that finished moving without a target
        // =============================================================================
        foreach (var (dd, entity) in SystemAPI
            .Query<RefRO<DesiredDestination>>()
            .WithAll<AttackCommand>()
            .WithNone<Target>()
            .WithEntityAccess())
        {
            // If unit has no active destination and no target, clear AttackCommand
            if (dd.ValueRO.Has == 0 && em.HasComponent<AttackCommand>(entity))
            {
                ecb.RemoveComponent<AttackCommand>(entity);
            }
        }
    }

    [BurstCompile]
    private void ProcessMeleeCombat(ref SystemState state, ref EntityCommandBuffer ecb, float dt, double time)
    {
        var em = state.EntityManager;

        foreach (var (transform, target, cooldown, damage, entity) in SystemAPI
            .Query<RefRO<LocalTransform>, RefRW<Target>, RefRW<AttackCooldown>, RefRO<Damage>>()
            .WithAll<UnitTag>()
            .WithNone<ArcherTag>()
            .WithEntityAccess())
        {
            ref var tgt = ref target.ValueRW;
            ref var cd = ref cooldown.ValueRW;
            
            // Update cooldown
            if (cd.Timer > 0)
            {
                cd.Timer -= dt;
            }

            // Validate target
            if (tgt.Value == Entity.Null || !em.Exists(tgt.Value))
            {
                tgt.Value = Entity.Null;
                ecb.RemoveComponent<Target>(entity);
                if (em.HasComponent<AttackCommand>(entity))
                {
                    ecb.RemoveComponent<AttackCommand>(entity);
                }
                continue;
            }

            var targetHealth = em.GetComponentData<Health>(tgt.Value);
            if (targetHealth.Value <= 0)
            {
                tgt.Value = Entity.Null;
                ecb.RemoveComponent<Target>(entity);
                if (em.HasComponent<AttackCommand>(entity))
                {
                    ecb.RemoveComponent<AttackCommand>(entity);
                }
                continue;
            }

            var myPos = transform.ValueRO.Position;
            var targetPos = em.GetComponentData<LocalTransform>(tgt.Value).Position;
            var dist = math.distance(myPos, targetPos);

            // Melee range: 0 to 1.5 units
            const float meleeRange = 1.5f;

            if (dist <= meleeRange)
            {
                // In range - stop moving and attack
                if (em.HasComponent<DesiredDestination>(entity))
                {
                    ecb.SetComponent(entity, new DesiredDestination { Has = 0 });
                }

                // Attack on cooldown
                if (cd.Timer <= 0)
                {
                    // Apply damage
                    var health = em.GetComponentData<Health>(tgt.Value);
                    health.Value -= damage.ValueRO.Value;
                    if (health.Value < 0) health.Value = 0;
                    ecb.SetComponent(tgt.Value, health);

                    // Reset cooldown
                    cd.Timer = cd.Cooldown;
                }
            }
            else
            {
                // Out of range - chase target
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
        }
    }

    [BurstCompile]
    private void ProcessRangedCombat(ref SystemState state, ref EntityCommandBuffer ecb, float dt, double time)
    {
        var em = state.EntityManager;

        foreach (var (transform, target, archerState, damage, faction, entity) in SystemAPI
            .Query<RefRO<LocalTransform>, RefRW<Target>, RefRW<ArcherState>, RefRO<Damage>, RefRO<FactionTag>>()
            .WithAll<ArcherTag>()
            .WithEntityAccess())
        {
            ref var tgt = ref target.ValueRW;
            ref var archer = ref archerState.ValueRW;

            // Update cooldown
            if (archer.CooldownTimer > 0)
            {
                archer.CooldownTimer -= dt;
            }

            // Validate target
            if (tgt.Value == Entity.Null || !em.Exists(tgt.Value))
            {
                tgt.Value = Entity.Null;
                archer.CurrentTarget = Entity.Null;
                ecb.RemoveComponent<Target>(entity);
                if (em.HasComponent<AttackCommand>(entity))
                {
                    ecb.RemoveComponent<AttackCommand>(entity);
                }
                continue;
            }

            var targetHealth = em.GetComponentData<Health>(tgt.Value);
            if (targetHealth.Value <= 0)
            {
                tgt.Value = Entity.Null;
                archer.CurrentTarget = Entity.Null;
                ecb.RemoveComponent<Target>(entity);
                if (em.HasComponent<AttackCommand>(entity))
                {
                    ecb.RemoveComponent<AttackCommand>(entity);
                }
                continue;
            }

            archer.CurrentTarget = tgt.Value;
            var myPos = transform.ValueRO.Position;
            var targetPos = em.GetComponentData<LocalTransform>(tgt.Value).Position;
            var dist = math.distance(myPos, targetPos);

            // Archer ranges: min 10, max 25
            const float minRange = 10f;
            const float maxRange = 25f;

            if (dist < minRange)
            {
                // Too close - kite away!
                archer.IsRetreating = 1;
                archer.AimTimer = 0;

                var retreatDir = math.normalize(myPos - targetPos);
                var retreatTarget = myPos + retreatDir * (minRange - dist + 3f);

                if (!em.HasComponent<DesiredDestination>(entity))
                {
                    ecb.AddComponent(entity, new DesiredDestination 
                    { 
                        Position = retreatTarget,
                        Has = 1
                    });
                }
                else
                {
                    ecb.SetComponent(entity, new DesiredDestination 
                    { 
                        Position = retreatTarget,
                        Has = 1
                    });
                }
            }
            else if (dist <= maxRange)
            {
                // In range - aim and fire
                archer.IsRetreating = 0;

                // Stop moving
                if (em.HasComponent<DesiredDestination>(entity))
                {
                    ecb.SetComponent(entity, new DesiredDestination { Has = 0 });
                }

                // Calculate aim time (longer for farther targets)
                var minAimTime = 0.3f;
                var maxAimTime = 1.2f;
                var aimRange = maxAimTime - minAimTime;
                var distRatio = (dist - minRange) / (maxRange - minRange);
                archer.AimTimeRequired = minAimTime + (aimRange * distRatio);

                archer.AimTimer += dt;

                // Fire when aimed and cooled down
                if (archer.AimTimer >= archer.AimTimeRequired && archer.CooldownTimer <= 0)
                {
                    archer.IsFiring = 1;
                    
                    // Create arrow
                    CreateArrow(ref ecb, myPos, targetPos, dist, entity, 
                        faction.ValueRO.Value, damage.ValueRO.Value, (float)time);

                    // Reset timers
                    archer.CooldownTimer = 1.5f;
                    archer.AimTimer = 0;
                    archer.IsFiring = 0;
                }
            }
            else
            {
                // Out of range - chase target
                archer.IsRetreating = 0;
                archer.AimTimer = 0;

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
        }
    }

    [BurstCompile]
    private void CreateArrow(ref EntityCommandBuffer ecb, float3 start, float3 targetPos, 
        float distance, Entity shooter, Faction faction, int damage, float time)
    {
        // Shoot straight for < 15 units, parabolic for >= 15 units
        const float parabolicThreshold = 15f;
        
        // Calculate horizontal distance (ignore height)
        float horizontalDist = math.length(new float2(targetPos.x - start.x, targetPos.z - start.z));
        bool isParabolic = horizontalDist >= parabolicThreshold;

        float3 velocity;
        float flightTime;

        if (isParabolic)
        {
            // Parabolic arc with height difference compensation
            var gravity = -9.8f;
            var heightDiff = targetPos.y - start.y; // Positive if target is higher
            
            var angle = math.radians(45f);
            
            float v0;
            if (math.abs(heightDiff) < 0.1f)
            {
                // Nearly level - use standard formula
                v0 = math.sqrt(math.abs(gravity) * horizontalDist / math.sin(2 * angle));
            }
            else
            {
                // Height difference - adjust initial velocity
                float denominator = horizontalDist * math.sin(2 * angle) - 2 * heightDiff * math.cos(angle) * math.cos(angle);
                
                if (denominator > 0)
                {
                    v0 = math.sqrt(math.abs(gravity) * horizontalDist * horizontalDist / denominator);
                }
                else
                {
                    // Fallback if target is too high or close
                    v0 = math.sqrt(math.abs(gravity) * horizontalDist / math.sin(2 * angle)) * 1.5f;
                }
            }
            
            // Calculate velocity components correctly
            var vx = v0 * math.cos(angle);  // Horizontal component
            var vy = v0 * math.sin(angle);  // Vertical component
            
            var horizontalDir = math.normalize(new float3(targetPos.x - start.x, 0, targetPos.z - start.z));
            velocity = horizontalDir * vx + new float3(0, vy, 0);
            
            // Calculate correct flight time with height
            float discriminant = vy * vy + 2 * math.abs(gravity) * heightDiff;
            if (discriminant >= 0)
            {
                flightTime = (vy + math.sqrt(discriminant)) / math.abs(gravity);
            }
            else
            {
                flightTime = horizontalDist / vx;
            }
        }
        else
        {
            // Straight shot with slight upward angle
            var shotSpeed = 35f;
            
            var direction = math.normalize(targetPos - start);
            
            // Ensure minimum upward angle of 5° for aesthetics
            float minPitch = math.radians(5f);
            float currentPitch = math.asin(direction.y);
            if (currentPitch < minPitch)
            {
                float3 horizontalDir = math.normalize(new float3(direction.x, 0, direction.z));
                direction = horizontalDir * math.cos(minPitch) + new float3(0, math.sin(minPitch), 0);
                direction = math.normalize(direction);
            }
            
            velocity = direction * shotSpeed;
            flightTime = distance / shotSpeed;
        }

        // Create arrow entity
        var arrow = ecb.CreateEntity();
        ecb.AddComponent(arrow, new LocalTransform 
        { 
            Position = start + new float3(0, 1.5f, 0),
            Rotation = quaternion.LookRotation(velocity, new float3(0, 1, 0)),
            Scale = 1f
        });

        ecb.AddComponent(arrow, new ArrowProjectile
        {
            Velocity = velocity,
            Gravity = isParabolic ? -9.8f : -1f,
            Shooter = shooter,
            IsParabolic = isParabolic
        });

        ecb.AddComponent(arrow, new Projectile
        {
            Start = start,
            End = targetPos,
            StartTime = time,
            FlightTime = flightTime,
            Damage = damage,
            Target = Entity.Null,
            Faction = faction
        });
    }
}