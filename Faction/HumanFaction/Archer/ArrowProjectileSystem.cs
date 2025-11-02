using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Arrow projectile system with HOMING behavior - arrows track and always hit their targets
/// Arrows follow targets and guarantee hits (no accuracy system)
/// </summary>
[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(UnifiedCombatSystem))]
public partial struct ArrowProjectileSystem : ISystem
{
    // Maximum lifetime before despawn (2 seconds)
    private const float MaxArrowLifetime = 2.0f;
    
    // Homing parameters
    private const float ArrowSpeed = 30f;          // Arrow travel speed
    private const float HomingStrength = 8f;       // How aggressively arrows track (radians/sec)
    private const float HitRadius = 0.8f;          // Distance to register a hit
    
    // Maximum pitch angle in radians (45 degrees) - for visual only
    private const float MaxPitchAngle = 0.785398f;

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

        // Move and update arrows with HOMING behavior
        foreach (var (transform, arrow, projectile, entity) 
                 in SystemAPI.Query<RefRW<LocalTransform>, RefRW<ArrowProjectile>, RefRW<Projectile>>()
                 .WithEntityAccess())
        {
            ref var trans = ref transform.ValueRW;
            ref var arr = ref arrow.ValueRW;
            ref var proj = ref projectile.ValueRW;
            
            var arrowPos = trans.Position;
            var shouldDestroy = false;
            
            // Calculate elapsed time since spawn
            var elapsed = (float)(time - projectile.ValueRO.StartTime);
            
            // DESPAWN AFTER 2 SECONDS (safety check)
            if (elapsed > MaxArrowLifetime)
            {
                shouldDestroy = true;
            }
            else
            {
                // HOMING BEHAVIOR - Track the target
                float3 targetPos = proj.End; // Default to original target position
                Entity targetEntity = proj.Target;
                bool targetIsAlive = false;
                
                // Check if target still exists and is alive
                if (targetEntity != Entity.Null && em.Exists(targetEntity))
                {
                    if (em.HasComponent<Health>(targetEntity))
                    {
                        var targetHealth = em.GetComponentData<Health>(targetEntity);
                        if (targetHealth.Value > 0)
                        {
                            targetIsAlive = true;
                            // Update target position to current location
                            if (em.HasComponent<LocalTransform>(targetEntity))
                            {
                                var targetTransform = em.GetComponentData<LocalTransform>(targetEntity);
                                targetPos = targetTransform.Position;
                                proj.End = targetPos; // Update stored target position
                            }
                        }
                    }
                }
                
                // Calculate direction to target
                float3 toTarget = targetPos - arrowPos;
                float distToTarget = math.length(toTarget);
                
                // Check if we hit the target
                if (distToTarget < HitRadius)
                {
                    // GUARANTEED HIT - Apply damage
                    if (targetIsAlive && targetEntity != Entity.Null && em.Exists(targetEntity))
                    {
                        if (em.HasComponent<Health>(targetEntity))
                        {
                            var targetHealth = em.GetComponentData<Health>(targetEntity);
                            targetHealth.Value -= proj.Damage;
                            
                            if (targetHealth.Value <= 0)
                            {
                                targetHealth.Value = 0;
                            }
                            
                            em.SetComponentData(targetEntity, targetHealth);
                        }
                    }
                    shouldDestroy = true;
                }
                else
                {
                    // HOMING - Steer towards target
                    float3 desiredDirection = math.normalize(toTarget);
                    float3 currentDirection = math.normalize(arr.Velocity);
                    
                    // Smoothly interpolate direction (homing behavior)
                    float3 newDirection = math.normalize(math.lerp(currentDirection, desiredDirection, HomingStrength * dt));
                    
                    // Set velocity with constant speed
                    arr.Velocity = newDirection * ArrowSpeed;
                    
                    // Move arrow
                    trans.Position = arrowPos + arr.Velocity * dt;
                    
                    // Update rotation to point in direction of travel
                    if (math.lengthsq(arr.Velocity) > 0.001f)
                    {
                        trans.Rotation = CalculateRealisticArrowRotation(arr.Velocity);
                    }
                }
            }
            
            // Destroy arrow if needed
            if (shouldDestroy)
            {
                ecb.DestroyEntity(entity);
            }
        }
    }

    /// <summary>
    /// Calculate realistic arrow rotation with clamped pitch angle
    /// </summary>
    [BurstCompile]
    private static quaternion CalculateRealisticArrowRotation(float3 velocity)
    {
        // Get horizontal direction and speed
        float3 horizontalDir = math.normalize(new float3(velocity.x, 0, velocity.z));
        float horizontalSpeed = math.length(new float2(velocity.x, velocity.z));
        
        // Calculate pitch angle from velocity
        float pitchAngle = -math.atan2(velocity.y, horizontalSpeed);
        
        // Clamp pitch to realistic range (-45° to +45°)
        pitchAngle = math.clamp(pitchAngle, -MaxPitchAngle, MaxPitchAngle);
        
        // Get yaw from horizontal direction
        float yaw = math.atan2(horizontalDir.x, horizontalDir.z);
        
        // Construct rotation: First rotate around Y (yaw), then around local X (pitch)
        quaternion yawRotation = quaternion.RotateY(yaw);
        quaternion pitchRotation = quaternion.RotateX(pitchAngle);
        
        return math.mul(yawRotation, pitchRotation);
    }
}