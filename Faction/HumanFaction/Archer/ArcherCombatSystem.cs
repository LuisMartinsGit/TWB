using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct ArcherCombatSystem : ISystem
{
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

        // Process all archers
        foreach (var (archerState, transform, faction, damage, lineOfSight, entity) 
                 in SystemAPI.Query<RefRW<ArcherState>, RefRO<LocalTransform>, RefRO<FactionTag>, RefRO<Damage>, RefRO<LineOfSight>>()
                 .WithAll<UnitTag>()
                 .WithEntityAccess())
        {
            ref var archer = ref archerState.ValueRW;
            var pos = transform.ValueRO.Position;

            // Update cooldown
            if (archer.CooldownTimer > 0)
            {
                archer.CooldownTimer -= dt;
            }

            // Find target if don't have one
            if (archer.CurrentTarget == Entity.Null)
            {
                archer.CurrentTarget = FindNearestEnemy(ref state, pos, faction.ValueRO.Value, lineOfSight.ValueRO.Radius);
                archer.AimTimer = 0;
                archer.IsFiring = 0;
                archer.IsRetreating = 0;
            }

            // If have target, check if still valid
            if (archer.CurrentTarget != Entity.Null)
            {
                if (!state.EntityManager.Exists(archer.CurrentTarget))
                {
                    archer.CurrentTarget = Entity.Null;
                    archer.AimTimer = 0;
                    archer.IsFiring = 0;
                    continue;
                }

                var targetPos = state.EntityManager.GetComponentData<LocalTransform>(archer.CurrentTarget).Position;
                var distance = math.distance(pos, targetPos);
                
                // Calculate effective range based on height
                var heightDiff = pos.y - targetPos.y;
                var effectiveMaxRange = archer.MaxRange + (heightDiff * archer.HeightRangeMod);
                
                // Check if target in range
                if (distance < archer.MinRange)
                {
                    // Too close - retreat!
                    archer.IsRetreating = 1;
                    archer.IsFiring = 0;
                    archer.AimTimer = 0;
                    
                    // Calculate retreat direction and add to desired destination
                    var retreatDir = math.normalize(pos - targetPos);
                    var retreatTarget = pos + retreatDir * (archer.MinRange + 2f);
                    
                    if (state.EntityManager.HasComponent<DesiredDestination>(entity))
                    {
                        ecb.SetComponent(entity, new DesiredDestination 
                        { 
                            Position = retreatTarget,
                            Has = 1
                        });
                    }
                    else
                    {
                        ecb.AddComponent(entity, new DesiredDestination 
                        { 
                            Position = retreatTarget,
                            Has = 1
                        });
                    }
                }
                else if (distance <= effectiveMaxRange)
                {
                    // In range - aim and fire
                    archer.IsRetreating = 0;
                    
                    // Clear movement
                    if (state.EntityManager.HasComponent<DesiredDestination>(entity))
                    {
                        ecb.SetComponent(entity, new DesiredDestination { Has = 0 });
                    }
                    
                    // Calculate aim time based on distance
                    var minAimTime = 0.3f;
                    var maxAimTime = 1.2f;
                    var aimTimeRange = maxAimTime - minAimTime;
                    var distanceRatio = (distance - archer.MinRange) / (effectiveMaxRange - archer.MinRange);
                    archer.AimTimeRequired = minAimTime + (aimTimeRange * distanceRatio);
                    
                    archer.AimTimer += dt;
                    
                    // Fire when aimed and cooled down
                    if (archer.AimTimer >= archer.AimTimeRequired && archer.CooldownTimer <= 0)
                    {
                        archer.IsFiring = 1;
                        
                        // Create arrow projectile
                        CreateArrow(ref ecb, pos, targetPos, distance, entity, 
                                  faction.ValueRO.Value, damage.ValueRO.Value, (float)time);
                        
                        // Reset timers
                        archer.CooldownTimer = 1.5f; // Cooldown between shots
                        archer.AimTimer = 0;
                        archer.IsFiring = 0;
                    }
                }
                else
                {
                    // Out of range
                    archer.CurrentTarget = Entity.Null;
                    archer.AimTimer = 0;
                    archer.IsFiring = 0;
                    archer.IsRetreating = 0;
                }
            }
        }
    }

    [BurstCompile]
    private Entity FindNearestEnemy(ref SystemState state, float3 pos, Faction myFaction, float sightRange)
    {
        Entity nearest = Entity.Null;
        float nearestDist = float.MaxValue;

        foreach (var (transform, faction, health, entity) 
                 in SystemAPI.Query<RefRO<LocalTransform>, RefRO<FactionTag>, RefRO<Health>>()
                 .WithAll<UnitTag>()
                 .WithEntityAccess())
        {
            if (faction.ValueRO.Value == myFaction) continue;
            if (health.ValueRO.Value <= 0) continue;

            var dist = math.distance(pos, transform.ValueRO.Position);
            if (dist < sightRange && dist < nearestDist)
            {
                nearestDist = dist;
                nearest = entity;
            }
        }

        return nearest;
    }

    [BurstCompile]
    private void CreateArrow(ref EntityCommandBuffer ecb, float3 start, float3 targetPos, 
                            float distance, Entity shooter, Faction faction, int damage, float time)
    {
        var parabolicThreshold = 11f; // Use arcing shots beyond this distance
        bool isParabolic = distance > parabolicThreshold;
        
        // Predict target movement (basic)
        var direction = math.normalize(targetPos - start);
        
        float3 velocity;
        float flightTime;
        
        if (isParabolic)
        {
            // Parabolic arc shot (for distant targets)
            var shotSpeed = 25f;
            var gravity = -9.8f;
            
            // Calculate velocity for 45-degree launch angle (maximum range)
            var horizontalDist = math.length(new float2(targetPos.x - start.x, targetPos.z - start.z));
            var verticalDist = targetPos.y - start.y;
            
            var angle = math.radians(45f);
            var vx = math.sqrt(math.abs(gravity) * horizontalDist / math.sin(2 * angle));
            var vy = vx * math.sin(angle);
            
            var horizontalDir = math.normalize(new float3(targetPos.x - start.x, 0, targetPos.z - start.z));
            velocity = horizontalDir * vx + new float3(0, vy, 0);
            
            flightTime = horizontalDist / vx;
        }
        else
        {
            // Straight shot (for close targets)
            var shotSpeed = 35f;
            velocity = direction * shotSpeed;
            flightTime = distance / shotSpeed;
        }
        
        // Create arrow entity
        var arrow = ecb.CreateEntity();
        ecb.AddComponent(arrow, new LocalTransform 
        { 
            Position = start,
            Rotation = quaternion.LookRotation(velocity, new float3(0, 1, 0)),
            Scale = 1f
        });
        
        ecb.AddComponent(arrow, new ArrowProjectile
        {
            Velocity = velocity,
            Gravity = isParabolic ? -9.8f : -2f, // Light gravity even for straight shots
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
        
        // Presentation ID for visual system
        ecb.AddComponent(arrow, new PresentationId { Id = 300 });
    }
}