using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Arrow projectile system with realistic rotation
/// FIXED: Correct pitch angle direction (positive = nose up, negative = nose down)
/// </summary>
[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(UnifiedCombatSystem))]
public partial struct ArrowProjectileSystem : ISystem
{
    // Maximum pitch angle in radians (45 degrees)
    private const float MaxPitchAngle = 0.785398f; // 45° in radians

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

        // Move and update arrows
        foreach (var (transform, arrow, projectile, entity) 
                 in SystemAPI.Query<RefRW<LocalTransform>, RefRW<ArrowProjectile>, RefRO<Projectile>>()
                 .WithEntityAccess())
        {
            ref var trans = ref transform.ValueRW;
            ref var arr = ref arrow.ValueRW;
            
            // Apply gravity
            arr.Velocity.y += arr.Gravity * dt;
            
            // Move arrow
            var oldPos = trans.Position;
            var newPos = oldPos + arr.Velocity * dt;
            trans.Position = newPos;
            
            // Update rotation with realistic clamping
            if (math.lengthsq(arr.Velocity) > 0.001f)
            {
                trans.Rotation = CalculateRealisticArrowRotation(arr.Velocity);
            }
            
            // Check for hits or out of bounds
            var shouldDestroy = false;
            
            // Ground collision
            if (newPos.y < 0.5f)
            {
                shouldDestroy = true;
            }
            
            // Flight time exceeded
            var elapsed = (float)(time - projectile.ValueRO.StartTime);
            if (elapsed > projectile.ValueRO.FlightTime + 1f) // +1s grace period
            {
                shouldDestroy = true;
            }
            
            // Check collision with units
            foreach (var (targetTransform, targetHealth, targetFaction, targetEntity) 
                     in SystemAPI.Query<RefRO<LocalTransform>, RefRW<Health>, RefRO<FactionTag>>()
                     .WithAll<UnitTag>()
                     .WithEntityAccess())
            {
                // Don't hit own faction
                if (targetFaction.ValueRO.Value == projectile.ValueRO.Faction)
                    continue;
                
                // Don't hit dead units
                if (targetHealth.ValueRO.Value <= 0)
                    continue;
                
                var targetPos = targetTransform.ValueRO.Position;
                var dist = math.distance(newPos, targetPos);
                
                // Hit detection (simple sphere check)
                if (dist < 1.0f) // 1 unit hit radius
                {
                    // Apply damage
                    targetHealth.ValueRW.Value -= projectile.ValueRO.Damage;
                    
                    // Check for death
                    if (targetHealth.ValueRO.Value <= 0)
                    {
                        targetHealth.ValueRW.Value = 0;
                        // Death will be handled by DeathSystem
                    }
                    
                    shouldDestroy = true;
                    break;
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
    /// FIXED: Correct pitch direction - negative pitch = arrow points down, positive = arrow points up
    /// </summary>
    [BurstCompile]
    private static quaternion CalculateRealisticArrowRotation(float3 velocity)
    {
        // Get horizontal direction and speed
        float3 horizontalDir = math.normalize(new float3(velocity.x, 0, velocity.z));
        float horizontalSpeed = math.length(new float2(velocity.x, velocity.z));
        
        // Calculate pitch angle from velocity
        // Negative atan2 because we want: velocity.y > 0 = nose up (negative pitch in Unity's left-handed system)
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