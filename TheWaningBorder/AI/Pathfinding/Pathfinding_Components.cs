using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

namespace TheWaningBorder.AI.Pathfinding
{
    public struct PathfindingRequest : IComponentData
    {
        public float3 StartPosition;
        public float3 EndPosition;
        public Entity RequestingEntity;
        public bool IsProcessed;
    }
    
    public struct PathComponent : IComponentData
    {
        public int CurrentWaypointIndex;
        public int TotalWaypoints;
        public bool HasPath;
        public bool IsMoving;
    }
}