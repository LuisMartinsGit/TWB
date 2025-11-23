using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Simple movement system that handles unit movement and rotation.
/// Combat logic is handled by UnifiedCombatSystem.
/// FIXED: Does NOT remove AttackCommand - lets UnifiedCombatSystem handle it
/// </summary>
[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(UnifiedCombatSystem))]  // CHANGED: Run BEFORE combat system
public partial struct SimplifiedMoveSystem : ISystem
{
    private const float StopDistance = 0.5f;
    private const float DefaultMoveSpeed = 3.5f;

    [BurstCompile]
    public void OnCreate(ref SystemState state) 
    {
        state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var em = state.EntityManager;
        float dt = SystemAPI.Time.DeltaTime;
        
        // Get ECB for structural changes
        var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

        // Process MoveCommand -> DesiredDestination conversion
        foreach (var (mc, entity) in SystemAPI.Query<RefRO<MoveCommand>>().WithEntityAccess())
        {
            // Buildings don't move
            if (SystemAPI.HasComponent<BuildingTag>(entity))
            {
                ecb.RemoveComponent<MoveCommand>(entity);
                
                if (em.HasComponent<DesiredDestination>(entity))
                {
                    var dd = em.GetComponentData<DesiredDestination>(entity);
                    dd.Has = 0;
                    ecb.SetComponent(entity, dd);
                }
                continue;
            }

            // Update guard point when explicitly commanded to move
            if (em.HasComponent<GuardPoint>(entity))
            {
                ecb.SetComponent(entity, new GuardPoint 
                { 
                    Position = mc.ValueRO.Destination,
                    Has = 1
                });
            }

            // Set destination
            if (!em.HasComponent<DesiredDestination>(entity))
            {
                ecb.AddComponent(entity, new DesiredDestination 
                { 
                    Position = mc.ValueRO.Destination,
                    Has = 1
                });
            }
            else
            {
                ecb.SetComponent(entity, new DesiredDestination 
                { 
                    Position = mc.ValueRO.Destination,
                    Has = 1
                });
            }

            // Clear Target - this is safe because we only set data
            if (em.HasComponent<Target>(entity))
            {
                ecb.SetComponent(entity, new Target { Value = Entity.Null });
            }
            if (!em.HasComponent<UserMoveOrder>(entity))
            {
                ecb.AddComponent<UserMoveOrder>(entity);
            }
            // DON'T remove AttackCommand here - let UnifiedCombatSystem see the MoveCommand
            // and skip attack processing for this entity
            
            // Remove MoveCommand itself - it's been processed
            ecb.RemoveComponent<MoveCommand>(entity);
        }

        // Move units toward their destinations
        foreach (var (xf, dd, entity) in SystemAPI
            .Query<RefRW<LocalTransform>, RefRW<DesiredDestination>>()
            .WithAll<UnitTag>()
            .WithEntityAccess())
        {
            if (dd.ValueRO.Has == 0) continue;
            if (SystemAPI.HasComponent<BuildingTag>(entity)) 
            { 
                dd.ValueRW.Has = 0; 
                continue; 
            }

            // Get move speed
            float speed = DefaultMoveSpeed;
            if (em.HasComponent<MoveSpeed>(entity))
            {
                var ms = em.GetComponentData<MoveSpeed>(entity).Value;
                if (ms > 0) speed = ms;
            }

            float3 pos = xf.ValueRO.Position;
            float3 goal = dd.ValueRO.Position;
            
            // Calculate direction (ignore Y for horizontal movement)
            float3 to = goal - pos;
            to.y = 0f;
            
            float distSqr = math.lengthsq(to);
            
            // Check if arrived
            if (distSqr <= (StopDistance * StopDistance))
            {
                dd.ValueRW.Has = 0;
                
                // ============================================================
                // NEW: Remove UserMoveOrder tag when destination reached
                // This allows auto-targeting to resume
                // ============================================================
                if (em.HasComponent<UserMoveOrder>(entity))
                {
                    ecb.RemoveComponent<UserMoveOrder>(entity);
                }
                
                continue;
            }

            // Move toward goal
            float dist = math.sqrt(distSqr);
            float3 dir = to / math.max(1e-5f, dist);
            
            float step = math.min(speed * dt, dist);
            
            var t = xf.ValueRO;
            t.Position = pos + dir * step;

            // Rotate to face movement direction
            if (math.lengthsq(dir) > 1e-8f)
            {
                float3 fwd = math.normalize(new float3(dir.x, 0f, dir.z));
                t.Rotation = quaternion.RotateY(math.atan2(fwd.x, fwd.z));
            }

            xf.ValueRW = t;
        }
    }
}