using System;
using Unity.Entities;
using UnityEngine;

namespace TheWaningBorder.Units.FeraldisBerserker
{
    /// <summary>
    /// Component tag to identify Feraldis_Berserker units
    /// </summary>
    public struct FeraldisBerserkerTag : IComponentData { }

    /// <summary>
    /// Unit-specific components for Feraldis_Berserker
    /// All values must be loaded from TechTree.json
    /// </summary>
    [Serializable]
    public struct FeraldisBerserkerStateComponent
    {
        public bool IsIdle { get; set; }
        public bool IsAttacking { get; set; }
        public bool IsMoving { get; set; }
        public Entity CurrentTarget { get; set; }
    }


    /// <summary>
    /// Component for tracking Feraldis_Berserker abilities if any
    /// </summary>
    [Serializable]
    public struct FeraldisBerserkerAbilitiesComponent
    {
        public float AbilityCooldown { get; set; }
        public float LastAbilityUseTime { get; set; }
        public string ActiveAbility { get; set; }
    }
}
