using System;
using Unity.Entities;
using UnityEngine;

namespace TheWaningBorder.Units.AlanthorBallista
{
    /// <summary>
    /// Component tag to identify Alanthor_Ballista units
    /// </summary>
    public struct AlanthorBallistaTag : IComponentData { }

    /// <summary>
    /// Unit-specific components for Alanthor_Ballista
    /// All values must be loaded from TechTree.json
    /// </summary>
    [Serializable]
    public struct AlanthorBallistaStateComponent
    {
        public bool IsIdle { get; set; }
        public bool IsAttacking { get; set; }
        public bool IsMoving { get; set; }
        public Entity CurrentTarget { get; set; }
    }


    /// <summary>
    /// Component for tracking Alanthor_Ballista abilities if any
    /// </summary>
    [Serializable]
    public struct AlanthorBallistaAbilitiesComponent
    {
        public float AbilityCooldown { get; set; }
        public float LastAbilityUseTime { get; set; }
        public string ActiveAbility { get; set; }
    }
}
