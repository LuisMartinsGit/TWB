using System;
using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

namespace TheWaningBorder.Core.Components
{
    /// <summary>
    /// Base component for all units containing data from TechTree.json
    /// Using fields instead of properties for IComponentData compatibility
    /// </summary>
    [Serializable]
    public struct UnitDataComponent : IComponentData
    {
        public FixedString64Bytes Id;
        public FixedString64Bytes Class;
        public float Hp;
        public float Speed;
        public float TrainingTime;
        public FixedString64Bytes ArmorType;
        public float Damage;
        public FixedString64Bytes DamageType;
        public float LineOfSight;
        public float AttackRange;
        public float MinAttackRange;
        public int PopCost;
    }

    [Serializable]
    public struct DefenseComponent : IComponentData
    {
        public float Melee;
        public float Ranged;
        public float Siege;
        public float Magic;
    }

    [Serializable]
    public struct CostComponent : IComponentData
    {
        public int Supplies;
        public int Iron;
        public int Crystal;
        public int Veilsteel;
        public int Glow;
    }

    [Serializable]
    public struct PositionComponent : IComponentData
    {
        public float3 Position;
    }

    [Serializable]
    public struct HealthComponent : IComponentData
    {
        public float CurrentHp;
        public float MaxHp;

        public int RegenRate { get; internal set; }

    }

    [Serializable]
    public struct MovementComponent : IComponentData
    {
        public float Speed;
        public float3 Destination;
        public bool IsMoving;
        internal float StoppingDistance;

    }

    [Serializable]
    public struct AttackComponent : IComponentData
    {
        public float Damage;
        public FixedString64Bytes DamageType;
        public float AttackSpeed;
        public float AttackRange;
        public float MinAttackRange;
        public float LastAttackTime;
    }

    // Special components for specific unit types
    [Serializable]
    public struct BuilderComponent : IComponentData
    {
        public float BuildSpeed;
        public FixedString128Bytes CurrentBuildingId;
        public float BuildProgress;
    }

    [Serializable]
    public struct MinerComponent : IComponentData
    {
        public float GatheringSpeed;
        public int CarryCapacity;
        public int CurrentCarryAmount;
        public FixedString64Bytes ResourceType;
    }

    [Serializable]
    public struct HealerComponent : IComponentData
    {
        public float HealsPerSecond;
        public float HealRange;
    }

    [Serializable]
    public struct ProjectileComponent : IComponentData
    {
        public float3 StartPosition;
        public float3 TargetPosition;
        public float Speed;
        public float Damage;
        public FixedString64Bytes DamageType;
    }
}
