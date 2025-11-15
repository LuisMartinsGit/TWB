using System;
using Unity.Entities;
using UnityEngine;

namespace TheWaningBorder.Units.AlanthorCataphract
{
    /// <summary>
    /// Component tag to identify Alanthor_Cataphract units
    /// </summary>
    public struct AlanthorCataphractTag : IComponentData { }

    /// <summary>
    /// Unit-specific components for Alanthor_Cataphract
    /// All values must be loaded from TechTree.json
    /// </summary>
    [Serializable]
    public struct AlanthorCataphractStateComponent
    {
        public bool IsIdle { get; set; }
        public bool IsAttacking { get; set; }
        public bool IsMoving { get; set; }
        public Entity CurrentTarget { get; set; }
    }


    /// <summary>
    /// Component for tracking Alanthor_Cataphract abilities if any
    /// </summary>
    [Serializable]
    public struct AlanthorCataphractAbilitiesComponent
    {
        public float AbilityCooldown { get; set; }
        public float LastAbilityUseTime { get; set; }
        public string ActiveAbility { get; set; }
    }
}
