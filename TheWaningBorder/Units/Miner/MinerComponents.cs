using System;
using Unity.Entities;
using UnityEngine;

namespace TheWaningBorder.Units.Miner
{
    /// <summary>
    /// Component tag to identify Miner units
    /// </summary>
    public struct MinerTag : IComponentData { }

    /// <summary>
    /// Unit-specific components for Miner
    /// All values must be loaded from TechTree.json
    /// </summary>
    [Serializable]
    public struct MinerStateComponent
    {
        public bool IsIdle { get; set; }
        public bool IsAttacking { get; set; }
        public bool IsMoving { get; set; }
        public Entity CurrentTarget { get; set; }
    }

    /// <summary>
    /// Gatherer-specific component for Miner
    /// </summary>
    [Serializable]
    public struct MinerGathererComponent
    {
        public float GatheringSpeed { get; set; }
        public int CarryCapacity { get; set; }
        public int CurrentCarryAmount { get; set; }
        public string ResourceType { get; set; }
    }

    /// <summary>
    /// Component for tracking Miner abilities if any
    /// </summary>
    [Serializable]
    public struct MinerAbilitiesComponent
    {
        public float AbilityCooldown { get; set; }
        public float LastAbilityUseTime { get; set; }
        public string ActiveAbility { get; set; }
    }
}
