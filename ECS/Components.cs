
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public enum UnitClass : byte { Melee = 0, Ranged = 1, Siege = 2 , Support = 3, Magic = 4,Economy = 5, Miner = 6, Scout = 7}
public enum Faction : byte
{
    Blue = 0,  // human
    Red = 1,
    Green = 2,
    Yellow = 3,
    Purple = 4,
    Orange = 5,
    Teal = 6,
    White = 7
}
public struct FactionProgress : IComponentData
{
    public byte Culture;

}
public static class Cultures
{
    public const byte None = 0;
    public const byte Runai = 1;
    public const byte Alanthor = 2;
    public const byte Feraldis = 3;
}

public struct FactionTag : IComponentData
{
    public Faction Value;
}

public struct UnitTag : IComponentData
{
    public UnitClass Class;
}

public struct BuildingTag : IComponentData
{
    public byte IsBase; // 1 for main base/outpost
}

public struct Health : IComponentData
{
    public int Value;
    public int Max;
}

public struct Damage : IComponentData
{
    public int Value;
}

public struct MoveSpeed : IComponentData
{
    public float Value;
}

public struct CanBuild : IComponentData
{
    public bool Value;
}

public struct AttackRange : IComponentData
{
    public float Value;
}

public struct AttackCooldown : IComponentData
{
    public float Cooldown;  // seconds between attacks
    public float Timer;     // countdown
}

public struct Target : IComponentData
{
    public Entity Value; // Entity.Null if none
}

public struct DesiredDestination : IComponentData
{
    public float3 Position;
    public byte Has; // 0/1
}

public struct ResourceStock : IComponentData
{
    public int Value;
    public int IncomePerTick;
    public float TickTimer;
    public float TickInterval;
}

public struct ProductionQueue : IBufferElementData
{
    public UnitClass Class;
}

public struct ProductionState : IComponentData
{
    public float Timer;     // time left to finish current item; <= 0 means idle
    public float BaseTime;  // base production time per unit
    public UnitClass CurrentClass; // <-- add this line
}

public struct RallyPoint : IComponentData
{
    public float3 Position;
    public byte Has;
}

// Marker to clean up orphan visuals, not part of ECS core
public struct PresentationId : IComponentData
{
    public int Id;
}
// Waypoints buffer for patrol paths
public struct PatrolWaypoint : IBufferElementData
{
    public float3 Position;
    public float  WaitSeconds; // optional pause at this waypoint
}

// Per-unit patrol state
public struct PatrolAgent : IComponentData
{
    public int Index;   // current waypoint index
    public byte Loop;    // 1 = loop, 0 = stop at end (or ping-pong if you add it later)
    public float WaitTimer; // countdown while waiting at a waypoint
}
public struct Radius : IComponentData
{
    public float Value; // collision/spacing radius in world units
}
public struct UserMoveOrder : IComponentData { } // tag = presence only

// Flash-beam visual event(s) for ranged shots.
// Attach as a DynamicBuffer<LaserFx> on the shooter (created on demand).
public struct LaserFx : IBufferElementData
{
    public Entity Target;     // who we zapped
    public float  Lifetime;   // seconds left to draw the beam (e.g., 0.08f)
}

// Siege projectile flight data (ECS time/impact handled in systems).
public struct Projectile : IComponentData
{
    public float3 Start;
    public float3 End;        // impact point (or target position at fire time)
    public double StartTime;  // SystemAPI.Time.ElapsedTime when launched
    public float  FlightTime; // seconds to reach End
    public int    Damage;
    public Entity Target;     // optional: if still valid at impact, apply to this
    public Faction Faction;   // who fired (for friendly-fire rules if needed)
}
// Axis-aligned impassable box (hard no-go)
public struct ObstacleAABB : IComponentData
{
    public float3 Min;  // inclusive
    public float3 Max;  // inclusive
}

// Optional: visual cue (no behavior)
public struct ObstacleTag : IComponentData {} // marker only

public struct TurnSpeed : IComponentData
{
    /// <summary>Radians per second. ~6.28 = 360°/s; ~12.56 = 720°/s.</summary>
    public float RadiansPerSecond;
}
public struct ArmorRating : IComponentData
{
    public float Value; // e.g., 0..100+
}

// ===== Strategy layer =====
public struct AIStrategyBudget : IComponentData {
    // Fractions sum ~1.0 (soft)
    public float Economy;   // workers/outposts
    public float Research;  // tech (stub if not yet)
    public float Military;  // army production
    public float Defense;   // towers/garrisons
    public float Expansion; // new bases/outposts
}

// Rolling perception snapshot (lightweight, per brain)
public struct AIStrategyPerception : IComponentData {
    public int MyArmyPower;
    public int EnemyArmyPowerNearby;
    public int KnownEnemyBases;    // from scout memory
    public int MapControlPercent;  // derived from revealed/visible
}

// ===== Mini-AI requests =====
public enum AIRequestType : byte { QueueMelee, QueueRanged, QueueSiege, BuildOutpost, BuildDefense, ResearchX }
public struct AIRequest : IBufferElementData {
    public AIRequestType Type;
    public int Quantity;
    public float3 Position; // for builds/waypoints
}

// ===== Military layer & armies =====
public enum MissionType : byte { Attack, Defend, Raid, Harass, Escort, Rally }
public struct ArmyTag : IComponentData { public int ArmyId;

    public Entity ArmyEntity { get; internal set; }
}
public struct ArmyDefinition : IComponentData {
    public int ArmyId;
    public Faction Owner;
    public MissionType CurrentMission;
    public Entity MissionEntity; // link to mission data
}

public struct MissionAttack : IComponentData {
    public float3 TargetPos;
    public Entity PrimaryTarget; // base/unit if known
}
public struct MissionDefend : IComponentData { public float3 AreaCenter; public float Radius; }
public struct MissionScout : IComponentData { public float3 TargetPos; }

// ===== Scout memory =====
public struct ScoutIntel : IBufferElementData {
    public float3 Pos;
    public float LastSeenTime;
    public byte  IsEnemyBase; // 0/1
}

// Optional: heatmap over FoW cells (kept sparse)
// Instead of a big grid, store “frontier” cells to visit next:
public struct ExploreFrontier : IBufferElementData
{
    public float3 WorldPos;
    public float Score; // higher = more attractive
}

public enum CrystalSubNodeType : byte
{
    Resource, Enforcer, Repair, Turret, Nexus, Ward, Storm, Obelisk
}

public struct CrystalTag : IComponentData {}                // mark crystal units/buildings
public struct CursedGroundTag : IComponentData {}           // attach to ground/tiles if you instantiate blockers
public struct CrystalMainNodeTag : IComponentData {}        // identify main hives
public struct CrystalSubNodeTag : IComponentData { public CrystalSubNodeType Type; }

/// <summary>Attached to any Crystal node (main or sub) that spreads the curse.</summary>
public struct CrystalNode : IComponentData
{
    public byte IsMain;             // 1 main hive / bud, 0 sub-node
    public float SpreadPerTick;     // cells per tick (logical units)
    public float SpreadRadius;      // world radius
    public float IncomePerTick;     // resource trickle for crystals
    public float TickInterval;      // seconds between ticks
    public float TickTimer;         // accum
    public byte Enabled;
}

/// <summary>Tracks the Crystal faction global level/XP.</summary>
public struct CrystalLevelState : IComponentData
{
    public int Level;           // 1..5
    public int Xp;              // current
    public int XpToNext;        // needed
    public float HarassTimer;   // next harassment wave
}

/// <summary>Noise built by player mining actions near cursed ground.</summary>
public struct CrystalMiningNoise : IComponentData
{
    public int LocalNoise;      // per region; if you segment the map, replicate this per area
}

/// <summary>Rally ability per Main Node (cooldown tracked locally).</summary>
public struct CrystalRally : IComponentData
{
    public float Cooldown;      // seconds
    public float Timer;         // accum
}

/// <summary>Simple aura definition for Main Node @ L2+.</summary>
public struct CrystalMainAuras : IComponentData
{
    public float AllyAspdPct;
    public float AllyMovePct;
    public float EnemyArmorPct;
    public float EnemyAccuracyPct;
    public float Radius;
    public byte Enabled;
}

/// <summary>Teleport link marker for Nexus nodes.</summary>
public struct CrystalNexus : IComponentData
{
    public Entity Linked;       // another Nexus (bidirectional)
    public byte Streams;        // concurrent streams allowed
}

/// <summary>Siege lattice linking (Lv4 feature): set when a node is in a cluster.</summary>
public struct CrystalLatticeMember : IComponentData
{
    public byte InCluster;
    public byte BonusProjectiles; // +1 beams/bolts when clustered
}

/// <summary>Ultimate toggles at L5.</summary>
public struct CrystalUltimateState : IComponentData
{
    public byte EmbraceActive;  // 1 if Crystal’s Embrace currently running
    public float EmbraceTimer;
    public byte TamingProtocolReady;
}
public struct MinAttackRange : IComponentData
{
    public float Value;
}
public struct LineOfSight : IComponentData
{
    public float Radius;
}

public struct GathererHutTag : IComponentData { }
public struct HutTag : IComponentData { }

public struct TempleTag : IComponentData { }

public struct VaultTag : IComponentData { }

public struct FactionResources : IComponentData
{
    public int Supplies;
    public int Iron;
    public int Crystal;
    public int Veilsteel;
    public int Glow;
}

/// <summary>Tracks integer-second ticks for resource updates.</summary>
public struct ResourceTickState : IComponentData
{
    public int LastWholeSecond; // floor(Time.ElapsedTime) applied last
}

/// <summary>Attach to any building that provides Supplies income (e.g., Hall).</summary>
public struct SuppliesIncome : IComponentData
{
    public int PerMinute; // e.g., 180
}

public struct BarracksTag : IComponentData {}

public struct TrainingState : IComponentData
{
    public byte Busy;       // 0/1
    public float Remaining; // seconds
}

public struct TrainQueueItem : IBufferElementData
{
    public FixedString64Bytes UnitId;
}

public struct GuardPoint : IComponentData
{
    public float3 Position;
    public byte Has; // 0/1
}
public struct ArrowLanded : IComponentData
{
    public float TimeLeft; // seconds
}

/// Population tracking for a faction. Current = used slots, Max = available slots (capped at 200).
/// Add this to the faction's resource bank entity alongside FactionResources.
/// </summary>
public struct FactionPopulation : IComponentData
{
    /// <summary>How many population slots are currently used by units</summary>
    public int Current;
    
    /// <summary>Maximum population available from buildings (capped at AbsoluteMax)</summary>
    public int Max;
    
    /// <summary>Hard cap on population - cannot exceed this value</summary>
    public const int AbsoluteMax = 200;
}

/// <summary>
/// Attached to buildings that provide population capacity.
/// Examples: Hut (10), Hall (20)
/// </summary>
public struct PopulationProvider : IComponentData
{
    /// <summary>How much population capacity this building provides when completed</summary>
    public int Amount;
}

/// <summary>
/// Attached to units that consume population slots.
/// Most basic units consume 1 slot, larger units may consume more.
/// </summary>
public struct PopulationCost : IComponentData
{
    /// <summary>How many population slots this unit consumes</summary>
    public int Amount;
}
// AIComponents.cs
// Core components for AI system

namespace TheWaningBorder.AI
{
    // ==================== AI Brain ====================

    /// <summary>
    /// Main AI controller for a faction. One per AI player.
    /// </summary>
    public struct AIBrain : IComponentData
    {
        public Faction Owner;
        public float UpdateInterval; // How often AI thinks (seconds)
        public float NextUpdateTime;
        public byte IsActive; // 0/1
        public AIPersonality Personality;
        public AIDifficulty Difficulty;
    }

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

    // ==================== Economy Manager ====================

    public struct AIEconomyState : IComponentData
    {
        public int AssignedMiners;
        public int DesiredMiners;
        public int ActiveGatherersHuts;
        public int DesiredGatherersHuts;

        public float LastMineAssignmentCheck;
        public float MineCheckInterval; // seconds

        public byte NeedsMoreSupplyIncome; // 0/1
        public byte NeedsMoreIronIncome; // 0/1
    }

    public struct MineAssignment : IBufferElementData
    {
        public Entity MineEntity;
        public int AssignedWorkers;
        public int MaxWorkers; // Capacity of this mine
        public float3 Position;
    }

    // ==================== Building Manager ====================

    public struct AIBuildingState : IComponentData
    {
        public int ActiveBuilders;
        public int DesiredBuilders;
        public int QueuedConstructions;

        public float LastBuildCheck;
        public float BuildCheckInterval;
    }

    public struct BuildRequest : IBufferElementData
    {
        public FixedString64Bytes BuildingType; // "GatherersHut", "Barracks", etc.
        public float3 DesiredPosition;
        public int Priority; // Higher = more urgent
        public byte Assigned; // 0 = pending, 1 = builder assigned
        public Entity AssignedBuilder;
    }

    // ==================== Military Manager ====================

    public struct AIMilitaryState : IComponentData
    {
        public int TotalSoldiers;
        public int TotalArchers;
        public int TotalSiegeUnits;
        public int QueuedSoldiers;
        public int QueuedArchers;
        public int QueuedSiegeUnits;

        public int ActiveBarracks;
        public int DesiredBarracks;

        public int ArmiesCount;
        public int ScoutsCount;

        public float LastRecruitmentCheck;
        public float RecruitmentCheckInterval;
    }

    public struct RecruitmentRequest : IBufferElementData
    {
        public UnitClass UnitType;
        public int Quantity;
        public int Priority;
        public Entity RequestingManager; // Which manager requested this
    }

    // ==================== Scouting Manager ====================

    public struct AIScoutingState : IComponentData
    {
        public int ActiveScouts;
        public int DesiredScouts;

        public float LastScoutUpdate;
        public float ScoutUpdateInterval;

        public float MapExplorationPercent; // 0-100
    }

    // public struct ScoutAssignment : IBufferElementData
    // {
    //     public Entity ScoutUnit;
    //     public float3 TargetArea;
    //     public byte IsActive; // 0/1
    //     public float LastReportTime;
    // }

    public struct EnemySighting : IBufferElementData
    {
        public Faction EnemyFaction;
        public float3 Position;
        public double TimeStamp; // SystemAPI.Time.ElapsedTime
        public int EstimatedStrength; // Rough power estimate
        public byte IsBase; // 0 = army, 1 = base/building
    }

    // ==================== Mission Manager ====================

    public struct AIMissionState : IComponentData
    {
        public int ActiveMissions;
        public int PendingMissions;

        public float LastMissionUpdate;
        public float MissionUpdateInterval;
    }

    public enum MissionType : byte
    {
        Attack = 0,
        Defend = 1,
        Raid = 2,
        Scout = 3,
        Expand = 4
    }

    public enum MissionStatus : byte
    {
        Pending = 0,
        Active = 1,
        Completed = 2,
        Failed = 3,
        Cancelled = 4
    }

    public struct AIMission : IComponentData
    {
        public int MissionId;
        public MissionType Type;
        public MissionStatus Status;
        public float3 TargetPosition;
        public Faction TargetFaction;

        public int RequiredStrength; // How much army power needed
        public int AssignedStrength; // Current army power assigned

        public double CreatedTime;
        public double LastUpdateTime;

        public int Priority; // Higher = more important
    }

    public struct AssignedArmy : IBufferElementData
    {
        public Entity ArmyEntity;
        public int Strength;
    }

    // ==================== Tactical Manager ====================

    public struct AITacticalState : IComponentData
    {
        public int ManagedArmies;

        public float LastTacticalUpdate;
        public float TacticalUpdateInterval;
    }

    public struct AIArmy : IComponentData
    {
        public int ArmyId;
        public Faction Owner;
        public Entity MissionEntity; // Current mission

        public float3 Position;
        public int TotalStrength;

        public byte IsInCombat; // 0/1
        public float LastCombatTime;

        public int UnitCount { get; internal set; }
    }

    public struct ArmyUnit : IBufferElementData
    {
        public Entity Unit;
        public UnitClass Type;
        public int Strength; // HP + attack value
    }

    public struct TacticalTarget : IBufferElementData
    {
        public Entity TargetEntity;
        public float3 Position;
        public int Priority; // Higher = attack first
        public float ThreatLevel; // How dangerous
    }

    // ==================== Resource Requests ====================

    public struct ResourceRequest : IBufferElementData
    {
        public int Supplies;
        public int Iron;
        public int Crystal;
        public int Veilsteel;
        public int Glow;

        public int Priority;
        public Entity Requester; // Which entity needs resources
        public byte Approved; // 0 = pending, 1 = approved, 2 = denied
    }

    // ==================== Shared Utilities ====================

    public struct AISharedKnowledge : IComponentData
    {
        public float3 EnemyLastKnownPosition;
        public double EnemyLastSeenTime;
        public int EnemyEstimatedStrength;

        public int KnownEnemyBases;
        public int OwnMilitaryStrength;
        public int OwnEconomicStrength; // Resources per minute

        public int EnemyBasesSpotted { get; internal set; }
        public int EnemyArmiesSpotted { get; internal set; }
    }
}


public struct MiningTarget : IComponentData
{
    public Entity Mine;
    public float3 TargetPosition;
}

namespace TheWaningBorder.AI
{
    public struct IronMineTag : IComponentData {}   // tag component
}
