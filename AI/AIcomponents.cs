using Unity.Entities;
using Unity.Mathematics;


namespace TheWaningBorder.AI
{
    /// <summary>
    /// Represents a zone on the map for exploration tracking.
    /// Each AI brain maintains a buffer of these to track map exploration.
    /// </summary>
    public struct ExplorationZone : IBufferElementData
    {
        public float3 CenterPosition;    // Center of this exploration zone
        public double LastVisitedTime;   // When this zone was last explored (ElapsedTime)
        public byte IsExplored;          // 0 = never visited, 1 = has been visited
        public int VisitCount;           // How many times this zone has been visited
    }

    /// <summary>
    /// Updated ScoutAssignment with better target tracking
    /// Replace the existing ScoutAssignment in your Components.cs with this
    /// </summary>
    public struct ScoutAssignment : IBufferElementData
    {
        public Entity ScoutUnit;
        public float3 TargetArea;
        public byte IsActive;             // 0 = inactive/dead, 1 = active scout
        public float LastReportTime;      
        public int AssignedZoneIndex;     // Which exploration zone is this scout heading to (-1 = none)
        public double AssignmentTime;     // When was this scout assigned to current zone
        public float DistanceToTarget;    // Distance remaining to target (for completion check)
    }
}