// AIComponents.cs
// Components for AI systems - brain, managers, and state tracking
// Place in: Assets/Scripts/Core/Components/AI/

using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace TheWaningBorder.AI
{
    // ==================== AI Brain - Core Controller ====================

    public enum AIPersonality : byte
    {
        Balanced = 0,
        Aggressive = 1,
        Defensive = 2,
        Economic = 3,
        Rush = 4
    }

    public enum AIDifficulty : byte
    {
        Easy = 0,
        Normal = 1,
        Hard = 2,
        Expert = 3
    }

    /// <summary>
    /// Main AI controller for a faction. One per AI player.
    /// </summary>
    public struct AIBrain : IComponentData
    {
        public Faction Owner;
        public float UpdateInterval;    // How often AI thinks (seconds)
        public float NextUpdateTime;
        public byte IsActive;           // 0/1
        public AIPersonality Personality;
        public AIDifficulty Difficulty;
    }

    // ==================== Economy Manager ====================

    /// <summary>
    /// AI economic planning state.
    /// </summary>
    public struct AIEconomyState : IComponentData
    {
        public int AssignedMiners;
        public int DesiredMiners;
        public int ActiveGatherersHuts;
        public int DesiredGatherersHuts;

        public float LastMineAssignmentCheck;
        public float MineCheckInterval;

        public byte NeedsMoreSupplyIncome; // 0/1
        public byte NeedsMoreIronIncome;   // 0/1
    }

    /// <summary>
    /// Assignment of workers to a specific mine.
    /// </summary>
    public struct MineAssignment : IBufferElementData
    {
        public Entity MineEntity;
        public int AssignedWorkers;
        public int MaxWorkers;
        public float3 Position;
    }

    // ==================== Building Manager ====================

    /// <summary>
    /// AI building management state.
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
    /// Request for the AI to construct a building.
    /// </summary>
    public struct BuildRequest : IBufferElementData
    {
        public FixedString64Bytes BuildingType; // "GatherersHut", "Barracks", etc.
        public int Priority;                     // Higher = build first
        public byte Assigned;                    // 0 = pending, 1 = builder assigned
        public Entity AssignedBuilder;
        public float3 Position;                  // Target position
    }

    // ==================== Military Manager ====================

    /// <summary>
    /// AI military planning state.
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
    /// Request for the AI to train units.
    /// </summary>
    public struct RecruitmentRequest : IBufferElementData
    {
        public UnitClass UnitType;
        public int Quantity;
        public int Priority;
        public Entity RequestingManager;
    }

    // ==================== Scouting Manager ====================

    /// <summary>
    /// AI scouting state.
    /// </summary>
    public struct AIScoutingState : IComponentData
    {
        public int ActiveScouts;
        public int DesiredScouts;
        public float LastScoutUpdate;
        public float ScoutUpdateInterval;
        public float MapExplorationPercent;
    }

    /// <summary>
    /// Assignment of a scout to an area.
    /// </summary>
    public struct ScoutAssignment : IBufferElementData
    {
        public Entity Scout;
        public float3 TargetArea;
        public float AssignedTime;
    }

    /// <summary>
    /// Enemy sighting recorded by scouts.
    /// </summary>
    public struct EnemySighting : IBufferElementData
    {
        public float3 Position;
        public float TimeSpotted;
        public int EstimatedStrength;
        public byte IsBase; // 0/1
    }

    // ==================== Mission Manager ====================

    public enum MissionType : byte
    {
        None = 0,
        Scout = 1,
        Attack = 2,
        Defend = 3,
        Raid = 4,
        Expand = 5
    }

    public enum MissionStatus : byte
    {
        Pending = 0,
        Active = 1,
        Complete = 2,
        Failed = 3,
        Cancelled = 4
    }

    /// <summary>
    /// AI mission tracking state.
    /// </summary>
    public struct AIMissionState : IComponentData
    {
        public int ActiveMissions;
        public int PendingMissions;
        public float LastMissionUpdate;
        public float MissionUpdateInterval;
    }

    /// <summary>
    /// Individual mission data.
    /// </summary>
    public struct AIMission : IComponentData
    {
        public MissionType Type;
        public MissionStatus Status;
        public float3 TargetPosition;
        public Entity TargetEntity;
        public int Priority;
        public int RequiredStrength;
        public float CreatedTime;
        public Entity AssignedArmy;
    }

    // ==================== Tactical Manager ====================

    /// <summary>
    /// AI tactical operations state.
    /// </summary>
    public struct AITacticalState : IComponentData
    {
        public int ManagedArmies;
        public float LastTacticalUpdate;
        public float TacticalUpdateInterval;
    }

    /// <summary>
    /// Army group management.
    /// </summary>
    public struct AIArmy : IComponentData
    {
        public int ArmyId;
        public Faction Owner;
        public float3 Position;
        public int TotalStrength;
        public Entity MissionEntity;
        public byte IsInCombat;
        public float LastCombatTime;
        public int UnitCount;
    }

    /// <summary>
    /// Individual unit in an army.
    /// </summary>
    public struct ArmyUnit : IBufferElementData
    {
        public Entity Unit;
        public UnitClass Type;
        public int Strength; // HP + attack value
    }

    /// <summary>
    /// Potential attack target for tactical planning.
    /// </summary>
    public struct TacticalTarget : IBufferElementData
    {
        public Entity TargetEntity;
        public float3 Position;
        public int Priority;      // Higher = attack first
        public float ThreatLevel; // How dangerous
    }

    // ==================== Resource Requests ====================

    /// <summary>
    /// Request for resources from economy to other managers.
    /// </summary>
    public struct ResourceRequest : IBufferElementData
    {
        public int Supplies;
        public int Iron;
        public int Crystal;
        public int Veilsteel;
        public int Glow;

        public int Priority;
        public Entity Requester;
        public byte Approved; // 0 = pending, 1 = approved, 2 = denied
    }

    // ==================== Shared Knowledge ====================

    /// <summary>
    /// Shared intelligence across AI systems.
    /// </summary>
    public struct AISharedKnowledge : IComponentData
    {
        public float3 EnemyLastKnownPosition;
        public double EnemyLastSeenTime;
        public int EnemyEstimatedStrength;

        public int KnownEnemyBases;
        public int OwnMilitaryStrength;
        public int OwnEconomicStrength;

        public int EnemyBasesSpotted;
        public int EnemyArmiesSpotted;
    }

    // ==================== Resource Tags ====================

    /// <summary>Marker tag for iron mine entities.</summary>
    public struct IronMineTag : IComponentData { }
}