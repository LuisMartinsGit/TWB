// AIManagerComponents.cs
// Components for AI manager systems (Economy, Building, Military, etc.)
// Location: Assets/Scripts/AI/Components/AIManagerComponents.cs

using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace TheWaningBorder.AI
{
    // ═══════════════════════════════════════════════════════════════════════
    // ECONOMY MANAGER
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// State tracking for AI economy management.
    /// </summary>
    public struct AIEconomyState : IComponentData
    {
        public int AssignedMiners;
        public int DesiredMiners;
        public int ActiveGatherersHuts;
        public int DesiredGatherersHuts;
        public float LastMineAssignmentCheck;
        public float MineCheckInterval;
        public byte NeedsMoreSupplyIncome;
        public byte NeedsMoreIronIncome;
    }

    /// <summary>
    /// Tracks miner assignments to iron deposits.
    /// </summary>
    public struct MineAssignment : IBufferElementData
    {
        public Entity Miner;
        public Entity Mine;
        public float AssignedTime;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // BUILDING MANAGER
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// State tracking for AI building/construction management.
    /// </summary>
    public struct AIBuildingState : IComponentData
    {
        public int ActiveBuilders;
        public int DesiredBuilders;
        public int QueuedConstructions;
        public float LastBuildCheck;
        public float BuildCheckInterval;
    }

    /// <summary>
    /// A queued building construction request.
    /// </summary>
    public struct BuildRequest : IBufferElementData
    {
        public FixedString64Bytes BuildingType;
        public float3 DesiredPosition;
        public int Priority;
        public byte Assigned;           // 0 = not assigned, 1 = assigned to builder
        public Entity AssignedBuilder;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // MILITARY MANAGER
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// State tracking for AI military management.
    /// </summary>
    public struct AIMilitaryState : IComponentData
    {
        public int TotalSoldiers;
        public int TotalArchers;
        public int TotalSiegeUnits;
        public int ActiveBarracks;
        public int DesiredBarracks;
        public int ArmiesCount;
        public int ScoutsCount;
        public int QueuedSoldiers;
        public int QueuedArchers;
        public int QueuedSiegeUnits;
        public float LastRecruitmentCheck;
        public float RecruitmentCheckInterval;
    }

    /// <summary>
    /// A queued unit recruitment request.
    /// </summary>
    public struct RecruitmentRequest : IBufferElementData
    {
        public UnitClass UnitType;
        public int Quantity;
        public int Priority;
        public Entity RequestingManager;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // MISSION MANAGER
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// State tracking for AI mission management.
    /// </summary>
    public struct AIMissionState : IComponentData
    {
        public int ActiveMissions;
        public int PendingMissions;
        public float LastMissionUpdate;
        public float MissionUpdateInterval;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // TACTICAL MANAGER
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// State tracking for AI tactical decisions.
    /// </summary>
    public struct AITacticalState : IComponentData
    {
        public int ManagedArmies;
        public float LastTacticalUpdate;
        public float TacticalUpdateInterval;
    }
// ═══════════════════════════════════════════════════════════════════════
// MISSION SYSTEM
// ═══════════════════════════════════════════════════════════════════════

/// <summary>
/// Mission type enumeration.
/// </summary>
public enum MissionType : byte
{
    None = 0,
    Attack = 1,
    Defend = 2,
    Scout = 3,
    Raid = 4,
    Reinforce = 5
}

/// <summary>
/// Mission status enumeration.
/// </summary>
public enum MissionStatus : byte
{
    Pending = 0,
    InProgress = 1,
    Completed = 2,
    Failed = 3,
    Cancelled = 4
}

/// <summary>
/// Represents an AI mission (attack, defend, scout, etc.)
/// </summary>
public struct AIMission : IComponentData
{
    public MissionType Type;
    public MissionStatus Status;
    public Faction OwnerFaction;
    public float3 TargetPosition;
    public Entity TargetEntity;
    public Entity AssignedArmy;
    public int Priority;
    public float CreatedTime;
    public float CompletedTime;
}

// ═══════════════════════════════════════════════════════════════════════
// ARMY SYSTEM
// ═══════════════════════════════════════════════════════════════════════

/// <summary>
/// Army status enumeration.
/// </summary>
public enum ArmyStatus : byte
{
    Idle = 0,
    Moving = 1,
    Attacking = 2,
    Defending = 3,
    Retreating = 4,
    Regrouping = 5
}

/// <summary>
/// Represents an AI-controlled army group.
/// </summary>
public struct AIArmy : IComponentData
{
    public int ArmyId;
    public Faction OwnerFaction;
    public ArmyStatus Status;
    public float3 Position;
    public float3 TargetPosition;
    public Entity CurrentMission;
    public int UnitCount;
    public int TotalStrength;
    public float LastUpdateTime;
}
}