using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Helper methods for finding empty spawn positions.
/// Searches in a spiral pattern around the desired position to find space.
/// </summary>
[BurstCompile]
public static class SpawnPlacementHelper
{
    /// <summary>
    /// Find an empty position near the desired spawn point.
    /// Searches in a spiral pattern to avoid existing units.
    /// </summary>
    public static float3 FindEmptyPosition(
        float3 desiredPos, 
        float spawnRadius,
        EntityManager em,
        int maxAttempts = 16)
    {
        // Check if desired position is already clear
        if (IsPositionClear(desiredPos, spawnRadius, em))
        {
            return desiredPos;
        }

        // Search in a spiral pattern
        float angleStep = 45f * (math.PI / 180f); // 45 degrees in radians
        float radiusStep = spawnRadius * 2.5f; // Distance between rings
        
        for (int ring = 1; ring <= 4; ring++) // Up to 4 rings
        {
            float ringRadius = ring * radiusStep;
            int pointsInRing = ring * 8; // More points in outer rings
            
            for (int i = 0; i < pointsInRing; i++)
            {
                float angle = (i * 2f * math.PI) / pointsInRing;
                float3 offset = new float3(
                    math.cos(angle) * ringRadius,
                    0,
                    math.sin(angle) * ringRadius
                );
                
                float3 testPos = desiredPos + offset;
                
                if (IsPositionClear(testPos, spawnRadius, em))
                {
                    return testPos;
                }
            }
        }

        // If all else fails, return position offset to the side
        return desiredPos + new float3(spawnRadius * 3f, 0, 0);
    }

    /// <summary>
    /// Check if a position is clear of other units
    /// </summary>
    private static bool IsPositionClear(
        float3 position,
        float radius,
        EntityManager em)
    {
        // Get all units with positions
        var unitQuery = em.CreateEntityQuery(
            ComponentType.ReadOnly<LocalTransform>(),
            ComponentType.ReadOnly<UnitTag>()
        );

        if (unitQuery.CalculateEntityCount() == 0)
        {
            unitQuery.Dispose();
            return true; // No units, position is clear
        }

        var units = unitQuery.ToEntityArray(Allocator.Temp);
        var transforms = unitQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

        bool isClear = true;
        float minClearDist = radius * 2.2f; // Need this much space
        float minClearDistSq = minClearDist * minClearDist;

        for (int i = 0; i < units.Length; i++)
        {
            if (!em.Exists(units[i])) continue;

            var otherPos = transforms[i].Position;
            
            // Calculate horizontal distance only
            float3 diff = position - otherPos;
            diff.y = 0;
            float distSq = math.lengthsq(diff);

            if (distSq < minClearDistSq)
            {
                isClear = false;
                break;
            }
        }

        units.Dispose();
        transforms.Dispose();
        unitQuery.Dispose();

        return isClear;
    }

    /// <summary>
    /// Get the radius of a unit, with fallback to default
    /// </summary>
    public static float GetUnitRadius(Entity entity, EntityManager em)
    {
        if (em.HasComponent<Radius>(entity))
        {
            return em.GetComponentData<Radius>(entity).Value;
        }
        return 0.5f; // Default radius
    }
}