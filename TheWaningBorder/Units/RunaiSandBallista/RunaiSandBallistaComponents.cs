using System;
using Unity.Entities;
using UnityEngine;

namespace TheWaningBorder.Units.RunaiSandBallista
{
    /// <summary>
    /// Component tag to identify Runai_SandBallista units
    /// </summary>
    public struct RunaiSandBallistaTag : IComponentData { }

    /// <summary>
    /// Unit-specific components for Runai_SandBallista
    /// All values must be loaded from TechTree.json
    /// </summary>
    [Serializable]
    public struct RunaiSandBallistaStateComponent
    {
        public bool IsIdle { get; set; }
        public bool IsAttacking { get; set; }
        public bool IsMoving { get; set; }
        public Entity CurrentTarget { get; set; }
    }


    /// <summary>
    /// Component for tracking Runai_SandBallista abilities if any
    /// </summary>
    [Serializable]
    public struct RunaiSandBallistaAbilitiesComponent
    {
        public float AbilityCooldown { get; set; }
        public float LastAbilityUseTime { get; set; }
        public string ActiveAbility { get; set; }
    }
}
