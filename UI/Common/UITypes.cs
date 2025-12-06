using System.Collections.Generic;
using UnityEngine;
using TheWaningBorder.Core;

namespace TheWaningBorder.UI
{
/// <summary>
/// Display information for an entity in the UI.
/// </summary>

public struct EntityDisplayInfo
{
    // Identity
    public string Name;
    public string Type;
    public string Description;
    public Texture2D Portrait;
    public string Faction;
    
    // Health (nullable - not all entities have health)
    public int? CurrentHealth;
    public int? MaxHealth;
    
    // Combat stats (nullable)
    public bool HasCombatStats;
    public int? Attack;
    public int? Defense;
    public float? Speed;
    
    // Resource generation (for buildings)
    public bool HasResourceGeneration;
    public int? SuppliesPerMinute;
    public int? IronPerMinute;
}

    /// <summary>
    /// Action panel information for an entity.
    /// </summary>
    public struct EntityActionInfo
    {
        public ActionType Type;
        public List<ActionButton> Actions;
        public TrainingInfo? TrainingState;
    }

    /// <summary>
    /// Types of action panels.
    /// </summary>
    public enum ActionType
    {
        None,
        BuildingPlacement,
        UnitTraining
    }

    /// <summary>
    /// A button in the action panel.
    /// </summary>
    public struct ActionButton
    {
        public string Id;
        public string Label;
        public string Tooltip;
        public Cost Cost;
        public bool Enabled;
        public Texture2D Icon;
        public bool CanAfford;  // ADD THIS
    }

    /// <summary>
    /// Training queue information.
    /// </summary>
    public struct TrainingInfo
    {
        public string UnitId;
        public float Progress;
        public float Total;
        public int QueuePosition;
        public string CurrentUnitId;   // ADD THIS
        public float TimeRemaining;    // ADD THIS
    }
    
}