using System;
using Unity.Entities;
using UnityEngine;

namespace TheWaningBorder.Units.AlanthorCrossbowman
{
    /// <summary>
    /// Component tag to identify Alanthor_Crossbowman units
    /// </summary>
    public struct AlanthorCrossbowmanTag : IComponentData { }

    /// <summary>
    /// Unit-specific components for Alanthor_Crossbowman
    /// All values must be loaded from TechTree.json
    /// </summary>
    [Serializable]
    public struct AlanthorCrossbowmanStateComponent
    {
        public bool IsIdle { get; set; }
        public bool IsAttacking { get; set; }
        public bool IsMoving { get; set; }
        public Entity CurrentTarget { get; set; }
    }


    /// <summary>
    /// Component for tracking Alanthor_Crossbowman abilities if any
    /// </summary>
    [Serializable]
    public struct AlanthorCrossbowmanAbilitiesComponent
    {
        public float AbilityCooldown { get; set; }
        public float LastAbilityUseTime { get; set; }
        public string ActiveAbility { get; set; }
    }
}
