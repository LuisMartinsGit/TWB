using System;
using Unity.Entities;
using UnityEngine;

namespace TheWaningBorder.Units.FeraldisWarboarRider
{
    /// <summary>
    /// Component tag to identify Feraldis_WarboarRider units
    /// </summary>
    public struct FeraldisWarboarRiderTag : IComponentData { }

    /// <summary>
    /// Unit-specific components for Feraldis_WarboarRider
    /// All values must be loaded from TechTree.json
    /// </summary>
    [Serializable]
    public struct FeraldisWarboarRiderStateComponent
    {
        public bool IsIdle { get; set; }
        public bool IsAttacking { get; set; }
        public bool IsMoving { get; set; }
        public Entity CurrentTarget { get; set; }
    }


    /// <summary>
    /// Component for tracking Feraldis_WarboarRider abilities if any
    /// </summary>
    [Serializable]
    public struct FeraldisWarboarRiderAbilitiesComponent
    {
        public float AbilityCooldown { get; set; }
        public float LastAbilityUseTime { get; set; }
        public string ActiveAbility { get; set; }
    }
}
