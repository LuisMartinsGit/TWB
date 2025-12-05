// UnitComponents.cs
// Components specific to unit entities (soldiers, workers, etc.)
// Place in: Assets/Scripts/Core/Components/Unit/

using Unity.Entities;
using Unity.Mathematics;

// ==================== Unit Classification ====================

public enum UnitClass : byte
{
    Melee = 0,
    Ranged = 1,
    Siege = 2,
    Support = 3,
    Magic = 4,
    Economy = 5,
    Miner = 6,
    Scout = 7
}

/// <summary>
/// Identifies an entity as a unit with a specific class.
/// </summary>
public struct UnitTag : IComponentData
{
    public UnitClass Class;
}

// ==================== Unit Type Tags ====================

/// <summary>Marks a unit as capable of constructing buildings.</summary>
public struct CanBuild : IComponentData
{
    public bool Value;
}

/// <summary>Marker tag for Archer units.</summary>
public struct ArcherTag : IComponentData { }

// ==================== Archer State ====================

/// <summary>
/// Archer-specific combat state tracking.
/// </summary>
public struct ArcherState : IComponentData
{
    public Entity CurrentTarget;
    public float AimTimer;           // Time spent aiming at current target
    public float AimTimeRequired;    // How long to aim before firing
    public float CooldownTimer;      // Time until can fire again
    public float MinRange;           // Minimum attack range
    public float MaxRange;           // Maximum attack range
    public float HeightRangeMod;     // Range bonus/penalty per unit height difference
    public byte IsRetreating;        // 1 if backing away from too-close enemy
    public byte IsFiring;            // 1 when actively firing
}

/// <summary>
/// Arrow projectile physics data.
/// </summary>
public struct ArrowProjectile : IComponentData
{
    public float3 Velocity;      // Current velocity vector
    public float Gravity;        // Gravity constant (typically -9.81)
    public Entity Shooter;       // Who shot it (for friendly fire checking)
    public bool IsParabolic;     // false = horizontal, true = parabolic arc
}

/// <summary>
/// Visual cleanup timer for landed arrows.
/// </summary>
public struct ArrowLanded : IComponentData
{
    public float TimeLeft; // Seconds until arrow visual is removed
}

// ==================== Miner Components ====================

/// <summary>Marker tag for Miner units.</summary>
public struct MinerTag : IComponentData { }

/// <summary>
/// Miner work state enumeration.
/// </summary>
public enum MinerWorkState : byte
{
    Idle = 0,
    MovingToDeposit = 1,
    Gathering = 2,
    ReturningToBase = 3
}

/// <summary>
/// Miner behavior and state tracking.
/// </summary>
public struct MinerState : IComponentData
{
    public Entity AssignedDeposit;   // Which deposit to mine
    public int CurrentLoad;          // Resources currently carrying
    public float GatherTimer;        // Time accumulator for gathering
    public MinerWorkState State;     // Current work state
}

/// <summary>
/// Target mine for a miner unit.
/// </summary>
public struct MiningTarget : IComponentData
{
    public Entity Mine;
    public float3 TargetPosition;
}

// ==================== Population System ====================

/// <summary>
/// Attached to units that consume population slots.
/// Most basic units consume 1 slot, larger units may consume more.
/// </summary>
public struct PopulationCost : IComponentData
{
    public int Amount;
}

// ==================== Army System ====================

/// <summary>
/// Tags a unit as belonging to an army group.
/// ArmyId of -1 indicates a scout (unassigned).
/// </summary>
public struct ArmyTag : IComponentData
{
    public int ArmyId;
}