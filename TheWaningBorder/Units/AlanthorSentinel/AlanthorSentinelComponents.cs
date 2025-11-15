using System;
using Unity.Entities;
using UnityEngine;

namespace TheWaningBorder.Units.AlanthorSentinel
{
    /// <summary>
    /// Component tag to identify Alanthor_Sentinel units
    /// </summary>
    public struct AlanthorSentinelTag : IComponentData { }

    /// <summary>
    /// Unit-specific components for Alanthor_Sentinel
    /// All values must be loaded from TechTree.json
    /// </summary>
    [Serializable]
    public struct AlanthorSentinelStateComponent
    {
        public bool IsIdle { get; set; }
        public bool IsAttacking { get; set; }
        public bool IsMoving { get; set; }
        public Entity CurrentTarget { get; set; }
    }


    /// <summary>
    /// Component for tracking Alanthor_Sentinel abilities if any
    /// </summary>
    [Serializable]
    public struct AlanthorSentinelAbilitiesComponent
    {
        public float AbilityCooldown { get; set; }
        public float LastAbilityUseTime { get; set; }
        public string ActiveAbility { get; set; }
    }
}
