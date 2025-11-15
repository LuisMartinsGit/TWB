using System;
using Unity.Entities;
using UnityEngine;

namespace TheWaningBorder.Units.RunaiRaider
{
    /// <summary>
    /// Component tag to identify Runai_Raider units
    /// </summary>
    public struct RunaiRaiderTag : IComponentData { }

    /// <summary>
    /// Unit-specific components for Runai_Raider
    /// All values must be loaded from TechTree.json
    /// </summary>
    [Serializable]
    public struct RunaiRaiderStateComponent
    {
        public bool IsIdle { get; set; }
        public bool IsAttacking { get; set; }
        public bool IsMoving { get; set; }
        public Entity CurrentTarget { get; set; }
    }


    /// <summary>
    /// Component for tracking Runai_Raider abilities if any
    /// </summary>
    [Serializable]
    public struct RunaiRaiderAbilitiesComponent
    {
        public float AbilityCooldown { get; set; }
        public float LastAbilityUseTime { get; set; }
        public string ActiveAbility { get; set; }
    }
}
