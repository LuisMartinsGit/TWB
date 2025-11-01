using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Prevents units from occupying the same space by pushing them apart.
/// Units maintain separation based on their Radius components.
/// FIXED: Uses EntityCommandBuffer for structural changes
/// </summary>
[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(SimplifiedMoveSystem))]
public partial struct UnitSeparationSystem : ISystem
{
    private const float PushForce = 8f; // How strongly units push each other
    private const float MinSeparation = 0.1f; // Minimum distance to maintain
    
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float dt = SystemAPI.Time.DeltaTime;
        var em = state.EntityManager;
        
        // Get ECB for structural changes
        var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

        // Initialize Radius for units that don't have it - USE ECB
        foreach (var (tag, entity) in SystemAPI
            .Query<RefRO<UnitTag>>()
            .WithNone<Radius>()
            .WithEntityAccess())
        {
            ecb.AddComponent(entity, new Radius { Value = 0.5f }); // Default radius
        }

        // Get all unit positions and radii
        var unitQuery = SystemAPI.QueryBuilder()
            .WithAll<LocalTransform, Radius, UnitTag>()
            .Build();

        var allUnits = unitQuery.ToEntityArray(Allocator.Temp);
        var allPositions = unitQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
        var allRadii = unitQuery.ToComponentDataArray<Radius>(Allocator.Temp);

        // Process each unit
        for (int i = 0; i < allUnits.Length; i++)
        {
            if (!em.Exists(allUnits[i])) continue;
            
            var myPos = allPositions[i].Position;
            var myRadius = allRadii[i].Value;
            float3 pushDirection = float3.zero;
            int pushCount = 0;

            // Check against all other units
            for (int j = 0; j < allUnits.Length; j++)
            {
                if (i == j) continue; // Skip self
                if (!em.Exists(allUnits[j])) continue;

                var otherPos = allPositions[j].Position;
                var otherRadius = allRadii[j].Value;

                // Calculate distance (only horizontal, ignore height)
                float3 diff = myPos - otherPos;
                diff.y = 0; // Ignore vertical separation
                
                float distSq = math.lengthsq(diff);
                float minDist = myRadius + otherRadius + MinSeparation;
                float minDistSq = minDist * minDist;

                // Units are overlapping or too close
                if (distSq < minDistSq && distSq > 0.0001f)
                {
                    float dist = math.sqrt(distSq);
                    float3 pushDir = diff / dist; // Normalized direction away from other
                    float overlap = minDist - dist;
                    
                    // Stronger push for more overlap
                    pushDirection += pushDir * overlap;
                    pushCount++;
                }
            }

            // Apply separation push
            if (pushCount > 0)
            {
                pushDirection /= pushCount; // Average push direction
                
                // Only push if not actively moving to a specific destination
                bool isMoving = false;
                if (em.HasComponent<DesiredDestination>(allUnits[i]))
                {
                    var dd = em.GetComponentData<DesiredDestination>(allUnits[i]);
                    isMoving = dd.Has != 0;
                }

                // Apply push (reduced if unit is moving)
                float pushMultiplier = isMoving ? 0.3f : 1.0f;
                float3 newPos = myPos + pushDirection * PushForce * dt * pushMultiplier;
                
                // Update position
                var transform = em.GetComponentData<LocalTransform>(allUnits[i]);
                transform.Position = newPos;
                em.SetComponentData(allUnits[i], transform);
            }
        }

        allUnits.Dispose();
        allPositions.Dispose();
        allRadii.Dispose();
    }
}