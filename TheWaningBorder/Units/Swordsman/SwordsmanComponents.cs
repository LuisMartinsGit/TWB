using System;
using Unity.Entities;
using UnityEngine;

namespace TheWaningBorder.Units.Swordsman
{
    /// <summary>
    /// Component tag to identify Swordsman units
    /// </summary>
    public struct SwordsmanTag : IComponentData { }

    /// <summary>
    /// Unit-specific components for Swordsman
    /// All values must be loaded from TechTree.json
    /// </summary>
    [Serializable]
    public struct SwordsmanStateComponent
    {
        public bool IsIdle { get; set; }
        public bool IsAttacking { get; set; }
        public bool IsMoving { get; set; }
        public Entity CurrentTarget { get; set; }
    }


    /// <summary>
    /// Component for tracking Swordsman abilities if any
    /// </summary>
    [Serializable]
    public struct SwordsmanAbilitiesComponent
    {
        public float AbilityCooldown { get; set; }
        public float LastAbilityUseTime { get; set; }
        public string ActiveAbility { get; set; }
    }
}
