using System;
using Unity.Entities;
using UnityEngine;

namespace TheWaningBorder.Units.RunaiSkirmisher
{
    /// <summary>
    /// Component tag to identify Runai_Skirmisher units
    /// </summary>
    public struct RunaiSkirmisherTag : IComponentData { }

    /// <summary>
    /// Unit-specific components for Runai_Skirmisher
    /// All values must be loaded from TechTree.json
    /// </summary>
    [Serializable]
    public struct RunaiSkirmisherStateComponent
    {
        public bool IsIdle { get; set; }
        public bool IsAttacking { get; set; }
        public bool IsMoving { get; set; }
        public Entity CurrentTarget { get; set; }
    }


    /// <summary>
    /// Component for tracking Runai_Skirmisher abilities if any
    /// </summary>
    [Serializable]
    public struct RunaiSkirmisherAbilitiesComponent
    {
        public float AbilityCooldown { get; set; }
        public float LastAbilityUseTime { get; set; }
        public string ActiveAbility { get; set; }
    }
}
