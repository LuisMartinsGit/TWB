// BuildingComponents.cs
// Components specific to building entities
// Place in: Assets/Scripts/Core/Components/Building/

using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

// ==================== Building Identity ====================

/// <summary>
/// Identifies an entity as a building.
/// IsBase = 1 for main bases/outposts.
/// </summary>
public struct BuildingTag : IComponentData
{
    public byte IsBase; // 1 for Hall/main base/outpost
}

// ==================== Era 1 Building Tags ====================

/// <summary>Resource collection building.</summary>
public struct GathererHutTag : IComponentData { }

/// <summary>Population housing building.</summary>
public struct HutTag : IComponentData { }

/// <summary>Military training building.</summary>
public struct BarracksTag : IComponentData { }

/// <summary>Siege/advanced unit training building.</summary>
public struct WorkshopTag : IComponentData { }

/// <summary>Resource storage building.</summary>
public struct DepotTag : IComponentData { }

/// <summary>Religious/support building.</summary>
public struct TempleTag : IComponentData { }

/// <summary>Defensive wall segment.</summary>
public struct WallTag : IComponentData { }

/// <summary>Resource vault building.</summary>
public struct VaultTag : IComponentData { }

// ==================== Era 2 - Runai Culture Buildings ====================

/// <summary>Runai expansion base.</summary>
public struct OutpostTag : IComponentData { }

/// <summary>Runai trade building.</summary>
public struct TradeHubTag : IComponentData { }

// ==================== Era 2 - Alanthor Culture Buildings ====================

/// <summary>Alanthor metal processing building.</summary>
public struct SmelterTag : IComponentData { }

/// <summary>Alanthor advanced construction building.</summary>
public struct CrucibleTag : IComponentData { }

// ==================== Era 2 - Feraldis Culture Buildings ====================

/// <summary>Feraldis hunting building.</summary>
public struct HuntingLodgeTag : IComponentData { }

/// <summary>Feraldis lumber building.</summary>
public struct LoggingStationTag : IComponentData { }

/// <summary>Feraldis weapon forge building.</summary>
public struct WarbrandFoundryTag : IComponentData { }

// ==================== Sect Buildings ====================

/// <summary>Small religious building for sects.</summary>
public struct ChapelSmallTag : IComponentData { }

/// <summary>Large religious building for sects.</summary>
public struct ChapelLargeTag : IComponentData { }

/// <summary>Unique sect-specific building.</summary>
public struct SectUniqueBuildingTag : IComponentData { }

/// <summary>Unique sect-specific unit type.</summary>
public struct SectUniqueUnitTag : IComponentData { }

// ==================== Construction System ====================

/// <summary>
/// Building construction parameters.
/// </summary>
public struct Buildable : IComponentData
{
    public float BuildTimeSeconds; // Total construction time
}

/// <summary>
/// Active construction progress tracking.
/// </summary>
public struct UnderConstruction : IComponentData
{
    public float Progress; // Current progress (0 to Total)
    public float Total;    // Total required construction work
}

/// <summary>
/// Build order assigned to a builder unit.
/// </summary>
public struct BuildOrder : IComponentData
{
    public Entity Site; // Building entity being constructed
}

/// <summary>
/// Stores defense values to apply when construction completes.
/// </summary>
public struct DeferredDefense : IComponentData
{
    public float Melee;
    public float Ranged;
    public float Siege;
    public float Magic;
}

/// <summary>
/// Defensive stats for completed buildings.
/// </summary>
public struct Defense : IComponentData
{
    public float Melee;
    public float Ranged;
    public float Siege;
    public float Magic;
}

// ==================== Training System ====================

/// <summary>
/// Current training state of a building.
/// </summary>
public struct TrainingState : IComponentData
{
    public byte Busy;       // 0 = idle, 1 = training
    public float Remaining; // Seconds until current unit completes
}

/// <summary>
/// Queue item for unit training.
/// </summary>
public struct TrainQueueItem : IBufferElementData
{
    public FixedString64Bytes UnitId;
}

// Legacy production system (consider deprecating in favor of TrainingState)
public struct ProductionQueue : IBufferElementData
{
    public UnitClass Class;
}

public struct ProductionState : IComponentData
{
    public float Timer;        // Time left to finish current item (<=0 means idle)
    public float BaseTime;     // Base production time per unit
    public UnitClass CurrentClass;
}

// ==================== Economy Components ====================

/// <summary>
/// Attached to buildings that provide population capacity.
/// </summary>
public struct PopulationProvider : IComponentData
{
    public int Amount;
}

/// <summary>
/// Attached to buildings that provide supply income.
/// </summary>
public struct SuppliesIncome : IComponentData
{
    public int PerMinute; // e.g., 180 supplies per minute
}