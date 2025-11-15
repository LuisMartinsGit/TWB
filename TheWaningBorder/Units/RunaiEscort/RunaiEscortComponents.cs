using System;
using Unity.Entities;
using UnityEngine;

namespace TheWaningBorder.Units.RunaiEscort
{
    /// <summary>
    /// Component tag to identify Runai_Escort units
    /// </summary>
    public struct RunaiEscortTag : IComponentData { }

    /// <summary>
    /// Unit-specific components for Runai_Escort
    /// All values must be loaded from TechTree.json
    /// </summary>
    [Serializable]
    public struct RunaiEscortStateComponent
    {
        public bool IsIdle { get; set; }
        public bool IsAttacking { get; set; }
        public bool IsMoving { get; set; }
        public Entity CurrentTarget { get; set; }
    }


    /// <summary>
    /// Component for tracking Runai_Escort abilities if any
    /// </summary>
    [Serializable]
    public struct RunaiEscortAbilitiesComponent
    {
        public float AbilityCooldown { get; set; }
        public float LastAbilityUseTime { get; set; }
        public string ActiveAbility { get; set; }
    }
}
