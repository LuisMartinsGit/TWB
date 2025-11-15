using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using TheWaningBorder.Core.GameManager;

namespace TheWaningBorder.Units.Base
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    //[UpdateAfter(typeof(UnitMovementSystem))]
    public partial class UnitSeparationSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            float separationRadius = 1.5f;
            float separationForce = 5f;

            // Query all units that participate in separation
            var query = GetEntityQuery(
                ComponentType.ReadOnly<PositionComponent>(),
                ComponentType.ReadOnly<MovementComponent>());

            // Take a snapshot of entities & positions
            var allEntities = query.ToEntityArray(Allocator.TempJob);
            var allPositions = query.ToComponentDataArray<PositionComponent>(Allocator.TempJob);

            float jobDeltaTime = deltaTime;
            float jobSeparationRadius = separationRadius;
            float jobSeparationForce = separationForce;

            Entities
                .WithReadOnly(allEntities)
                .WithReadOnly(allPositions)
                .WithDisposeOnCompletion(allEntities)
                .WithDisposeOnCompletion(allPositions)
                .ForEach((Entity entity, ref PositionComponent position, in MovementComponent movement) =>
                {
                    float3 separation = float3.zero;
                    int neighborCount = 0;

                    float3 pos = position.Position;

                    for (int i = 0; i < allEntities.Length; i++)
                    {
                        var otherEntity = allEntities[i];
                        if (otherEntity == entity)
                            continue;

                        float3 otherPos = allPositions[i].Position;
                        float distance = math.distance(pos, otherPos);

                        if (distance < jobSeparationRadius && distance > 0.001f)
                        {
                            float3 diff = pos - otherPos;
                            separation += math.normalize(diff) * (1f - distance / jobSeparationRadius);
                            neighborCount++;
                        }
                    }

                    if (neighborCount > 0)
                    {
                        separation = math.normalize(separation) * jobSeparationForce * jobDeltaTime;
                        position.Position += separation;
                    }
                })
                .Schedule();
        }
    }
}
