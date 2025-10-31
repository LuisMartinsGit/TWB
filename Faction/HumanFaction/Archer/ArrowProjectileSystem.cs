using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(UnifiedCombatSystem))]
public partial struct ArrowProjectileSystem : ISystem
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
            
            // Update rotation to face movement direction
            if (math.lengthsq(arr.Velocity) > 0.001f)
            {
                trans.Rotation = quaternion.LookRotation(arr.Velocity, new float3(0, 1, 0));
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
}