using System;
using Unity.Entities;
using UnityEngine;

namespace TheWaningBorder.Units.FeraldisSiegeRam
{
    /// <summary>
    /// Component tag to identify Feraldis_SiegeRam units
    /// </summary>
    public struct FeraldisSiegeRamTag : IComponentData { }

    /// <summary>
    /// Unit-specific components for Feraldis_SiegeRam
    /// All values must be loaded from TechTree.json
    /// </summary>
    [Serializable]
    public struct FeraldisSiegeRamStateComponent
    {
        public bool IsIdle { get; set; }
        public bool IsAttacking { get; set; }
        public bool IsMoving { get; set; }
        public Entity CurrentTarget { get; set; }
    }


    /// <summary>
    /// Component for tracking Feraldis_SiegeRam abilities if any
    /// </summary>
    [Serializable]
    public struct FeraldisSiegeRamAbilitiesComponent
    {
        public float AbilityCooldown { get; set; }
        public float LastAbilityUseTime { get; set; }
        public string ActiveAbility { get; set; }
    }
}
