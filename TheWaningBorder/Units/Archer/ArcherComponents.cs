using System;
using Unity.Entities;
using UnityEngine;

namespace TheWaningBorder.Units.Archer
{
    /// <summary>
    /// Component tag to identify Archer units
    /// </summary>
    public struct ArcherTag : IComponentData { }

    /// <summary>
    /// Unit-specific components for Archer
    /// All values must be loaded from TechTree.json
    /// </summary>
    [Serializable]
    public struct ArcherStateComponent
    {
        public bool IsIdle { get; set; }
        public bool IsAttacking { get; set; }
        public bool IsMoving { get; set; }
        public Entity CurrentTarget { get; set; }
    }


    /// <summary>
    /// Component for tracking Archer abilities if any
    /// </summary>
    [Serializable]
    public struct ArcherAbilitiesComponent
    {
        public float AbilityCooldown { get; set; }
        public float LastAbilityUseTime { get; set; }
        public string ActiveAbility { get; set; }
    }
}
