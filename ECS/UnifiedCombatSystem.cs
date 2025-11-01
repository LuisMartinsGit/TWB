using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Unified combat system with HEIGHT-BASED DAMAGE MODIFIERS.
/// Height advantage: +20% damage (max)
/// Height disadvantage: -20% damage (max)
/// Minimum damage: 1 (never 0)
/// </summary>
[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct UnifiedCombatSystem : ISystem
{
    private const float MaxGuardDistance = 20f;
    private const float GuardReturnThreshold = 2f;
    
    // Height damage modifier settings
    private const float HeightDamageScale = 0.04f; // 4% per unit height diff
    private const float MaxHeightBonus = 0.20f;    // Cap at +20%
    private const float MaxHeightPenalty = -0.20f; // Cap at -20%
    
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
        // PHASE 1: Handle user attack commands
        // =============================================================================
        foreach (var (attackCmd, transform, entity) in SystemAPI
            .Query<RefRO<AttackCommand>, RefRO<LocalTransform>>()
            .WithAll<UnitTag>()
            .WithEntityAccess())
        {
            // Check if unit is actively moving by player command
            if (em.HasComponent<DesiredDestination>(entity))
            {
                var dd = em.GetComponentData<DesiredDestination>(entity);
                if (dd.Has != 0)
                {
                    bool isReturningToGuard = false;
                    if (em.HasComponent<GuardPoint>(entity))
                    {
                        var gp = em.GetComponentData<GuardPoint>(entity);
                        if (gp.Has != 0)
                        {
                            var distToGuard = math.distance(dd.Position, gp.Position);
                            isReturningToGuard = distToGuard < 1f;
                        }
                    }
                    
                    if (!isReturningToGuard && !em.HasComponent<Target>(entity))
                    {
                        ecb.RemoveComponent<AttackCommand>(entity);
                        continue;
                    }
                }
            }
            
            var target = attackCmd.ValueRO.Target;
            
            if (target == Entity.Null || !em.Exists(target))
            {
                ecb.RemoveComponent<AttackCommand>(entity);
                continue;
            }

            if (em.HasComponent<Health>(target))
            {
                var targetHealth = em.GetComponentData<Health>(target);
                if (targetHealth.Value <= 0)
                {
                    ecb.RemoveComponent<AttackCommand>(entity);
                    continue;
                }
            }

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

            if (!em.HasComponent<Target>(entity))
            {
                ecb.AddComponent(entity, new Target { Value = target });
            }
            else
            {
                ecb.SetComponent(entity, new Target { Value = target });
            }

            if (em.HasComponent<DesiredDestination>(entity))
            {
                ecb.SetComponent(entity, new DesiredDestination { Has = 0 });
            }
        }

        // =============================================================================
        // PHASE 2: Auto-acquire targets for idle units
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
            if (em.HasComponent<DesiredDestination>(entity))
            {
                var dd = em.GetComponentData<DesiredDestination>(entity);
                if (dd.Has != 0)
                {
                    continue;
                }
            }
            
            var myPos = transform.ValueRO.Position;
            var myFaction = faction.ValueRO.Value;
            var los = lineOfSight.ValueRO.Radius;
            
            if (em.HasComponent<GuardPoint>(entity))
            {
                var guardPoint = em.GetComponentData<GuardPoint>(entity);
                if (guardPoint.Has != 0)
                {
                    var distFromGuard = math.distance(myPos, guardPoint.Position);
                    if (distFromGuard > MaxGuardDistance)
                    {
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
        // PHASE 3: Combat processing
        // =============================================================================
        ProcessMeleeCombat(ref state, ref ecb, dt, time);
        ProcessRangedCombat(ref state, ref ecb, dt, time);

        // =============================================================================
        // PHASE 4: Return to guard point
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

            if (distToGuard > GuardReturnThreshold)
            {
                bool isMovingToGuard = false;
                if (em.HasComponent<DesiredDestination>(entity))
                {
                    var dest = em.GetComponentData<DesiredDestination>(entity);
                    if (dest.Has != 0)
                    {
                        var distToDest = math.distance(dest.Position, gpPos);
                        isMovingToGuard = distToDest < 1f;
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
        // =============================================================================
        foreach (var (dd, entity) in SystemAPI
            .Query<RefRO<DesiredDestination>>()
            .WithAll<AttackCommand>()
            .WithNone<Target>()
            .WithEntityAccess())
        {
            if (dd.ValueRO.Has == 0 && em.HasComponent<AttackCommand>(entity))
            {
                ecb.RemoveComponent<AttackCommand>(entity);
            }
        }
    }

    /// <summary>
    /// Calculate height-based damage modifier.
    /// Returns multiplier: 0.8 to 1.2 (±20% cap)
    /// </summary>
    [BurstCompile]
    private static float CalculateHeightDamageModifier(float attackerHeight, float targetHeight)
    {
        float heightDiff = attackerHeight - targetHeight;
        float modifier = heightDiff * HeightDamageScale;
        
        // Clamp to ±20%
        modifier = math.clamp(modifier, MaxHeightPenalty, MaxHeightBonus);
        
        return 1.0f + modifier;
    }

    /// <summary>
    /// Apply damage with minimum guarantee and height modifier.
    /// Ensures damage is never less than 1.
    /// </summary>
    [BurstCompile]
    private static int CalculateFinalDamage(int baseDamage, float heightModifier)
    {
        float modifiedDamage = baseDamage * heightModifier;
        int finalDamage = (int)math.round(modifiedDamage);
        
        // Ensure minimum 1 damage
        return math.max(1, finalDamage);
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
            
            if (cd.Timer > 0)
            {
                cd.Timer -= dt;
            }

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

            const float meleeRange = 1.5f;

            if (dist <= meleeRange)
            {
                if (em.HasComponent<DesiredDestination>(entity))
                {
                    ecb.SetComponent(entity, new DesiredDestination { Has = 0 });
                }

                if (cd.Timer <= 0)
                {
                    // Calculate height-based damage modifier
                    float heightModifier = CalculateHeightDamageModifier(myPos.y, targetPos.y);
                    int finalDamage = CalculateFinalDamage(damage.ValueRO.Value, heightModifier);
                    
                    // Apply damage
                    var health = em.GetComponentData<Health>(tgt.Value);
                    health.Value -= finalDamage;
                    if (health.Value < 0) health.Value = 0;
                    ecb.SetComponent(tgt.Value, health);

                    cd.Timer = cd.Cooldown;
                }
            }
            else
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

            if (archer.CooldownTimer > 0)
            {
                archer.CooldownTimer -= dt;
            }

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

            const float minRange = 10f;
            const float maxRange = 25f;

            if (dist < minRange)
            {
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
                archer.IsRetreating = 0;

                if (em.HasComponent<DesiredDestination>(entity))
                {
                    ecb.SetComponent(entity, new DesiredDestination { Has = 0 });
                }

                var minAimTime = 0.3f;
                var maxAimTime = 1.2f;
                var aimRange = maxAimTime - minAimTime;
                var distRatio = (dist - minRange) / (maxRange - minRange);
                archer.AimTimeRequired = minAimTime + (aimRange * distRatio);

                archer.AimTimer += dt;

                if (archer.AimTimer >= archer.AimTimeRequired && archer.CooldownTimer <= 0)
                {
                    archer.IsFiring = 1;
                    
                    // Calculate height-based damage modifier for arrow
                    float heightModifier = CalculateHeightDamageModifier(myPos.y, targetPos.y);
                    int finalDamage = CalculateFinalDamage(damage.ValueRO.Value, heightModifier);
                    
                    // Create arrow with modified damage
                    CreateArrow(ref ecb, myPos, targetPos, dist, entity, 
                        faction.ValueRO.Value, finalDamage, (float)time);

                    archer.CooldownTimer = 1.5f;
                    archer.AimTimer = 0;
                    archer.IsFiring = 0;
                }
            }
            else
            {
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
        const float parabolicThreshold = 15f;
        
        float horizontalDist = math.length(new float2(targetPos.x - start.x, targetPos.z - start.z));
        bool isParabolic = horizontalDist >= parabolicThreshold;

        float3 velocity;
        float flightTime;

        if (isParabolic)
        {
            var gravity = -9.8f;
            var heightDiff = targetPos.y - start.y;
            
            var angle = math.radians(45f);
            
            float v0;
            if (math.abs(heightDiff) < 0.1f)
            {
                v0 = math.sqrt(math.abs(gravity) * horizontalDist / math.sin(2 * angle));
            }
            else
            {
                float denominator = horizontalDist * math.sin(2 * angle) - 2 * heightDiff * math.cos(angle) * math.cos(angle);
                
                if (denominator > 0)
                {
                    v0 = math.sqrt(math.abs(gravity) * horizontalDist * horizontalDist / denominator);
                }
                else
                {
                    v0 = math.sqrt(math.abs(gravity) * horizontalDist / math.sin(2 * angle)) * 1.5f;
                }
            }
            
            var vx = v0 * math.cos(angle);
            var vy = v0 * math.sin(angle);
            
            var horizontalDir = math.normalize(new float3(targetPos.x - start.x, 0, targetPos.z - start.z));
            velocity = horizontalDir * vx + new float3(0, vy, 0);
            
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
            var shotSpeed = 35f;
            
            var direction = math.normalize(targetPos - start);
            
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
            Damage = damage, // Already modified by height
            Target = Entity.Null,
            Faction = faction
        });
    }
}