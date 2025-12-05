// AIScoutingComponents.cs
// Components for AI scouting and exploration systems
// Location: Assets/Scripts/AI/Components/AIScoutingComponents.cs

using Unity.Entities;
using Unity.Mathematics;

namespace TheWaningBorder.AI
{
    // ═══════════════════════════════════════════════════════════════════════
    // EXPLORATION ZONE
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Represents a zone on the map for AI exploration tracking.
    /// Used by AIScoutingBehavior to track explored/unexplored areas.
    /// </summary>
    public struct ExplorationZone : IComponentData
    {
        /// <summary>Center position of this exploration zone</summary>
        public float3 Position;
        
        /// <summary>Radius of the zone</summary>
        public float Radius;
        
        /// <summary>Last time this zone was scouted (game time)</summary>
        public float LastExploredTime;
        
        /// <summary>Priority for exploration (higher = more important)</summary>
        public int Priority;
        
        /// <summary>Whether this zone has been explored at least once</summary>
        public byte IsExplored; // 0 = unexplored, 1 = explored
        
        /// <summary>Whether enemy presence was detected in this zone</summary>
        public byte HasEnemyPresence; // 0 = no, 1 = yes
        
        /// <summary>Faction that owns this exploration zone data</summary>
        public Faction Owner;
    }

    /// <summary>
    /// Buffer element for storing multiple exploration zones per AI brain.
    /// </summary>
    public struct ExplorationZoneBuffer : IBufferElementData
    {
        public float3 Position;
        public float Radius;
        public float LastExploredTime;
        public int Priority;
        public byte IsExplored;
        public byte HasEnemyPresence;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COMBAT POWER
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Represents the combat strength of a unit or army.
    /// Used for AI tactical decisions and army composition.
    /// </summary>
    public struct CombatPower : IComponentData
    {
        /// <summary>Base combat power value</summary>
        public int Value;
        
        /// <summary>Offensive strength (attack capability)</summary>
        public int OffensivePower;
        
        /// <summary>Defensive strength (survivability)</summary>
        public int DefensivePower;
        
        /// <summary>Threat level this unit poses (for targeting priority)</summary>
        public float ThreatLevel;
    }

    /// <summary>
    /// Buffer element for tracking combat power of units in an army.
    /// </summary>
    public struct CombatPowerEntry : IBufferElementData
    {
        public Entity Unit;
        public int Power;
        public UnitClass UnitType;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SCOUTING STATE
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// State tracking for AI scouting behavior.
    /// </summary>
    public struct AIScoutingState : IComponentData
    {
        /// <summary>Number of active scouts</summary>
        public int ActiveScouts;
        
        /// <summary>Target number of scouts to maintain</summary>
        public int DesiredScouts;
        
        /// <summary>Last time scouting priorities were updated</summary>
        public float LastPriorityUpdate;
        
        /// <summary>Update interval for scouting priorities</summary>
        public float PriorityUpdateInterval;
        
        /// <summary>Zones that need exploration</summary>
        public int UnexploredZoneCount;
    }

    /// <summary>
    /// Tag component marking a unit as assigned to scouting duty.
    /// </summary>
    public struct ScoutAssignment : IComponentData
    {
        /// <summary>Target zone to scout</summary>
        public float3 TargetZone;
        
        /// <summary>Time when this assignment was given</summary>
        public float AssignedTime;
        
        /// <summary>Priority of this scouting mission</summary>
        public int Priority;
    }
}