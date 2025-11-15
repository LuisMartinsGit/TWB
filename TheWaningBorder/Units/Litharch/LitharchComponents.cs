using System;
using Unity.Entities;
using UnityEngine;

namespace TheWaningBorder.Units.Litharch
{
    /// <summary>
    /// Component tag to identify Litharch units
    /// </summary>
    public struct LitharchTag : IComponentData { }

    /// <summary>
    /// Unit-specific components for Litharch
    /// All values must be loaded from TechTree.json
    /// </summary>
    [Serializable]
    public struct LitharchStateComponent
    {
        public bool IsIdle { get; set; }
        public bool IsAttacking { get; set; }
        public bool IsMoving { get; set; }
        public Entity CurrentTarget { get; set; }
    }

    /// <summary>
    /// Healer-specific component for Litharch
    /// </summary>
    [Serializable]
    public struct LitharchHealerComponent
    {
        public float HealsPerSecond { get; set; }
        public float HealRange { get; set; }
        public Entity CurrentHealTarget { get; set; }
    }

    /// <summary>
    /// Component for tracking Litharch abilities if any
    /// </summary>
    [Serializable]
    public struct LitharchAbilitiesComponent
    {
        public float AbilityCooldown { get; set; }
        public float LastAbilityUseTime { get; set; }
        public string ActiveAbility { get; set; }
    }
}
