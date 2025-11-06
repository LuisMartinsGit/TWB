
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

public enum UnitClass : byte { Melee = 0, Ranged = 1, Siege = 2, Support = 3, Magic = 4, Economy = 5 }
public enum Faction : byte
{
    Blue = 0,
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
public struct Radius : IComponentData
{
    public float Value; // collision/spacing radius in world units
}

// Siege projectile flight data (ECS time/impact handled in systems).
public struct Projectile : IComponentData
{
    public float3 Start;
    public float3 End;        // impact point (or target position at fire time)
    public double StartTime;  // SystemAPI.Time.ElapsedTime when launched
    public float FlightTime; // seconds to reach End
    public int Damage;
    public Entity Target;     // optional: if still valid at impact, apply to this
    public Faction Faction;   // who fired (for friendly-fire rules if needed)
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

public struct BarracksTag : IComponentData { }

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

public struct FactionPopulation : IComponentData
{
    /// <summary>How many population slots are currently used by units</summary>
    public int Current;

    /// <summary>Maximum population available from buildings (capped at AbsoluteMax)</summary>
    public int Max;

    /// <summary>Hard cap on population - cannot exceed this value</summary>
    public const int AbsoluteMax = 200;
}

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

public struct FogVisibleTag : IComponentData {} // singleton tag

public struct VisibleUnitElement : IBufferElementData
{
    public Entity Value;
}
