using System;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace TheWaningBorder.Units.Builder
{
    /// <summary>
    /// Component tag to identify Builder units
    /// </summary>
    public struct BuilderTag : IComponentData { }

    /// <summary>
    /// Unit-specific components for Builder
    /// All values must be loaded from TechTree.json
    /// </summary>
    [Serializable]
    public struct BuilderStateComponent
    {
        public bool IsIdle { get; set; }
        public bool IsAttacking { get; set; }
        public bool IsMoving { get; set; }
        public Entity CurrentTarget { get; set; }
    }

    /// <summary>
    /// Builder-specific component for Builder
    /// </summary>
    [Serializable]
    public struct BuilderBuilderComponent : IComponentData
    {
        public float BuildSpeed { get; set; }
        public FixedString64Bytes CurrentBuildingId { get; set; }
        public float BuildProgress { get; set; }
    }

    /// <summary>
    /// Component for tracking Builder abilities if any
    /// </summary>
    [Serializable]
    public struct BuilderAbilitiesComponent
    {
        public float AbilityCooldown { get; set; }
        public float LastAbilityUseTime { get; set; }
        public FixedString64Bytes  ActiveAbility { get; set; }
    }
}
