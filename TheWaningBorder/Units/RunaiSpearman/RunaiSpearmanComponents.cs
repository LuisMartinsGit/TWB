using System;
using Unity.Entities;
using UnityEngine;

namespace TheWaningBorder.Units.RunaiSpearman
{
    /// <summary>
    /// Component tag to identify Runai_Spearman units
    /// </summary>
    public struct RunaiSpearmanTag : IComponentData { }

    /// <summary>
    /// Unit-specific components for Runai_Spearman
    /// All values must be loaded from TechTree.json
    /// </summary>
    [Serializable]
    public struct RunaiSpearmanStateComponent
    {
        public bool IsIdle { get; set; }
        public bool IsAttacking { get; set; }
        public bool IsMoving { get; set; }
        public Entity CurrentTarget { get; set; }
    }


    /// <summary>
    /// Component for tracking Runai_Spearman abilities if any
    /// </summary>
    [Serializable]
    public struct RunaiSpearmanAbilitiesComponent
    {
        public float AbilityCooldown { get; set; }
        public float LastAbilityUseTime { get; set; }
        public string ActiveAbility { get; set; }
    }
}
