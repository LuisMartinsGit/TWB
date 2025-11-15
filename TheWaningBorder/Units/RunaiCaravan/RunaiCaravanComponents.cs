using System;
using Unity.Entities;
using UnityEngine;

namespace TheWaningBorder.Units.RunaiCaravan
{
    /// <summary>
    /// Component tag to identify Runai_Caravan units
    /// </summary>
    public struct RunaiCaravanTag : IComponentData { }

    /// <summary>
    /// Unit-specific components for Runai_Caravan
    /// All values must be loaded from TechTree.json
    /// </summary>
    [Serializable]
    public struct RunaiCaravanStateComponent
    {
        public bool IsIdle { get; set; }
        public bool IsAttacking { get; set; }
        public bool IsMoving { get; set; }
        public Entity CurrentTarget { get; set; }
    }


    /// <summary>
    /// Component for tracking Runai_Caravan abilities if any
    /// </summary>
    [Serializable]
    public struct RunaiCaravanAbilitiesComponent
    {
        public float AbilityCooldown { get; set; }
        public float LastAbilityUseTime { get; set; }
        public string ActiveAbility { get; set; }
    }
}
