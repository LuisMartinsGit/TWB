using System;
using Unity.Entities;
using UnityEngine;

namespace TheWaningBorder.Units.FeraldisHunter
{
    /// <summary>
    /// Component tag to identify Feraldis_Hunter units
    /// </summary>
    public struct FeraldisHunterTag : IComponentData { }

    /// <summary>
    /// Unit-specific components for Feraldis_Hunter
    /// All values must be loaded from TechTree.json
    /// </summary>
    [Serializable]
    public struct FeraldisHunterStateComponent
    {
        public bool IsIdle { get; set; }
        public bool IsAttacking { get; set; }
        public bool IsMoving { get; set; }
        public Entity CurrentTarget { get; set; }
    }


    /// <summary>
    /// Component for tracking Feraldis_Hunter abilities if any
    /// </summary>
    [Serializable]
    public struct FeraldisHunterAbilitiesComponent
    {
        public float AbilityCooldown { get; set; }
        public float LastAbilityUseTime { get; set; }
        public string ActiveAbility { get; set; }
    }
}
