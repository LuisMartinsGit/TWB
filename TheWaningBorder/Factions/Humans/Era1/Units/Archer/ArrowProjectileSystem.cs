using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using TheWaningBorder.Factions.Humans.Era1.Units;

/// <summary>
/// Arrow projectile system with ARCHED trajectories that GUARANTEE hits
/// Uses parametric curves - arrows follow a beautiful arc but always reach their target
/// </summary>
[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(UnifiedCombatSystem))]
public partial struct ArrowProjectileSystem : ISystem
{
    // Flight parameters
    private const float FlightDuration = 0.8f;     // How long arrows take to reach target (seconds)
    private const float ArcHeight = 3f;            // Height of arc above midpoint (adjustable for visual effect)
    private const float HitRadius = 0.8f;          // Distance to register a hit
    
    // Maximum pitch angle in radians (60 degrees) - arrows can point more steeply now
    private const float MaxPitchAngle = 1.047f;

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

        // Move and update arrows with ARCHED trajectory
        foreach (var (transform, arrow, projectile, entity) 
                 in SystemAPI.Query<RefRW<LocalTransform>, RefRW<ArrowProjectile>, RefRW<Projectile>>()
                 .WithEntityAccess())
        {
            ref var trans = ref transform.ValueRW;
            ref var arr = ref arrow.ValueRW;
            ref readonly var proj = ref projectile.ValueRO;
            
            var arrowPos = trans.Position;
            var shouldDestroy = false;
            
            // Calculate elapsed time since spawn
            var elapsed = (float)(time - proj.StartTime);
            
            // Calculate progress through flight (0 to 1)
            float t = elapsed / FlightDuration;
            
            // SAFETY: Despawn if arrow takes too long
            if (t > 1.5f) // 150% of expected flight time
            {
                shouldDestroy = true;
            }
            else
            {
                // Get target position (update if target moved)
                float3 targetPos = proj.End;
                Entity targetEntity = proj.Target;
                bool targetIsAlive = false;
                
                // Track moving targets
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
                            }
                        }
                    }
                }
                
                // Calculate distance to target
                float distToTarget = math.length(targetPos - arrowPos);
                
                // Check if we hit the target
                if (t >= 0.95f || distToTarget < HitRadius) // Hit if very close OR near end of flight
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
                    // ARCHED TRAJECTORY - Calculate position along parabolic curve
                    float3 startPos = proj.Start + new float3(0, 1.5f, 0); // Archer height
                    
                    // Calculate control point for parabolic arc
                    float3 midpoint = (startPos + targetPos) * 0.5f;
                    
                    // Arc height scales with distance (longer shots = higher arc)
                    float horizontalDist = math.length(targetPos - startPos);
                    float arcHeight = ArcHeight * math.min(1f, horizontalDist / 15f); // Cap at 15 units
                    
                    float3 controlPoint = midpoint + new float3(0, arcHeight, 0);
                    
                    // Quadratic Bezier curve: B(t) = (1-t)²P0 + 2(1-t)tP1 + t²P2
                    // P0 = start, P1 = control point (above midpoint), P2 = target
                    float oneMinusT = 1f - t;
                    float3 newPosition = 
                        oneMinusT * oneMinusT * startPos +
                        2f * oneMinusT * t * controlPoint +
                        t * t * targetPos;
                    
                    // Calculate velocity direction from derivative of bezier curve
                    // B'(t) = 2(1-t)(P1-P0) + 2t(P2-P1)
                    float3 velocity = 
                        2f * oneMinusT * (controlPoint - startPos) +
                        2f * t * (targetPos - controlPoint);
                    
                    // Normalize and scale velocity for smooth motion
                    velocity = math.normalize(velocity) * (horizontalDist / FlightDuration);
                    
                    // Update arrow position
                    trans.Position = newPosition;
                    
                    // Update rotation to point along velocity vector
                    if (math.lengthsq(velocity) > 0.001f)
                    {
                        trans.Rotation = CalculateArrowRotation(velocity);
                    }
                    
                    // Store velocity in arrow component (for visual effects if needed)
                    arr.Velocity = velocity;
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
    /// Calculate arrow rotation pointing along velocity vector
    /// Allows full range of pitch angles for realistic arcing
    /// </summary>
    [BurstCompile]
    private static quaternion CalculateArrowRotation(float3 velocity)
    {
        // Get horizontal direction and speed
        float3 horizontalDir = math.normalize(new float3(velocity.x, 0, velocity.z));
        float horizontalSpeed = math.length(new float2(velocity.x, velocity.z));
        
        // Prevent division by zero
        if (horizontalSpeed < 0.001f)
        {
            // Pointing straight up or down
            return quaternion.LookRotation(new float3(0, 0, 1), math.up());
        }
        
        // Calculate pitch angle from velocity
        float pitchAngle = math.atan2(velocity.y, horizontalSpeed);
        
        // Clamp pitch to realistic range for arrows
        pitchAngle = math.clamp(pitchAngle, -MaxPitchAngle, MaxPitchAngle);
        
        // Get yaw from horizontal direction
        float yaw = math.atan2(horizontalDir.x, horizontalDir.z);
        
        // Construct rotation: First rotate around Y (yaw), then around local X (pitch)
        quaternion yawRotation = quaternion.RotateY(yaw);
        quaternion pitchRotation = quaternion.RotateX(-pitchAngle); // Negative for correct orientation
        
        return math.mul(yawRotation, pitchRotation);
    }
}