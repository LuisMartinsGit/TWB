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
        public string Name;
        public string Type;
        public string Description;
        public Texture2D Portrait;
        public int CurrentHP;
        public int MaxHP;
        public string Faction;
    }

    /// <summary>
    /// Action panel information for an entity.
    /// </summary>
    public struct EntityActionInfo
    {
        public ActionType Type;
        public List<ActionButton> Actions;
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
    }
}