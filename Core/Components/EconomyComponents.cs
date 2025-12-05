// EconomyComponents.cs
// Components for resource management and faction economy
// Place in: Assets/Scripts/Core/Components/Economy/

using Unity.Entities;

// ==================== Faction Resources ====================

/// <summary>
/// All resource types for a faction.
/// Attached to the faction's bank entity.
/// </summary>
public struct FactionResources : IComponentData
{
    public int Supplies;
    public int Iron;
    public int Crystal;
    public int Veilsteel;
    public int Glow;
}

/// <summary>
/// Tracks integer-second ticks for resource income updates.
/// </summary>
public struct ResourceTickState : IComponentData
{
    public int LastWholeSecond; // floor(Time.ElapsedTime) applied last
}

// Legacy resource system (consider migrating to FactionResources)
public struct ResourceStock : IComponentData
{
    public int Value;
    public int IncomePerTick;
    public float TickTimer;
    public float TickInterval;
}

// ==================== Population System ====================

/// <summary>
/// Population tracking for a faction.
/// Attached to the faction's bank entity alongside FactionResources.
/// </summary>
public struct FactionPopulation : IComponentData
{
    /// <summary>How many population slots are currently used by units.</summary>
    public int Current;

    /// <summary>Maximum population available from buildings (capped at AbsoluteMax).</summary>
    public int Max;

    /// <summary>Hard cap on population - cannot exceed this value.</summary>
    public const int AbsoluteMax = 200;
}