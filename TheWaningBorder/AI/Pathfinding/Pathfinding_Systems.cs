using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Burst;
using TheWaningBorder.Units.Base;

namespace TheWaningBorder.AI.Pathfinding
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class PathfindingSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var movementLookup = GetComponentLookup<MovementComponent>(false);

            Entities
                .ForEach((Entity entity, ref PathfindingRequest request) =>
                {
                    if (!request.IsProcessed && movementLookup.HasComponent(request.RequestingEntity))
                    {
                        var movement = movementLookup[request.RequestingEntity];
                        movement.Destination = request.EndPosition;
                        movement.IsMoving = true;
                        movementLookup[request.RequestingEntity] = movement;

                        request.IsProcessed = true;
                    }
                })
                .Run();
        }

    }
}