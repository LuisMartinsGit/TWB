// EntityDisplayInfo.cs
// Data structures for unified UI display

using UnityEngine;

/// <summary>
/// Contains all information needed to display an entity in the EntityInfoPanel.
/// </summary>
public class EntityDisplayInfo
{
    public string Name = "Unknown";
    public string Type = "Unknown";
    public string Description = "";
    public Texture2D Portrait;
    
    // Combat stats
    public int? Attack;
    public int? Defense;
    public int? CurrentHealth;
    public int? MaxHealth;
    public float? Speed;
    
    // Building-specific resources
    public int? SuppliesPerMinute;
    public int? IronPerMinute;
    public int? CrystalPerMinute;
    public int? VeilsteelPerMinute;
    public int? GlowPerMinute;
    
    // Helper properties
    public bool HasCombatStats => Attack.HasValue || Defense.HasValue;
    public bool HasResourceGeneration => SuppliesPerMinute.HasValue || IronPerMinute.HasValue || 
                                         CrystalPerMinute.HasValue || VeilsteelPerMinute.HasValue || 
                                         GlowPerMinute.HasValue;
    public bool IsUnit => Type == "Unit";
    public bool IsBuilding => Type == "Building";
}

/// <summary>
/// Contains action button information.
/// </summary>
public class ActionButton
{
    public string Id;
    public string Label;
    public Texture2D Icon;
    public TheWaningBorder.Economy.Cost Cost;
    public bool CanAfford;
    public float TrainingTime;
    public string Tooltip;
}

/// <summary>
/// Contains training progress information.
/// </summary>
public class TrainingInfo
{
    public bool IsTraining;
    public string CurrentUnitId;
    public float Progress;        // 0 to 1
    public float TimeRemaining;
    public string[] Queue;
}

/// <summary>
/// Main action information for a selected entity.
/// </summary>
public class EntityActionInfo
{
    public ActionType Type = ActionType.None;
    public ActionButton[] Actions;
    public TrainingInfo TrainingState;
}

/// <summary>
/// Types of actions an entity can perform.
/// </summary>
public enum ActionType
{
    None,
    BuildingPlacement,  // Builder unit
    UnitTraining,       // Barracks, Hall, etc.
    Research,           // Future: Research buildings
    Upgrade             // Future: Building upgrades
}