using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

namespace TheWaningBorder.Units.Base
{
    public struct UnitComponent : IComponentData
    {
        public FixedString64Bytes UnitId;
        public FixedString64Bytes UnitClass;
        public float Speed;
        public float AttackDamage;
        public float AttackSpeed;
        public float AttackRange;
        public float MinAttackRange;
        public float LineOfSight;
        public FixedString64Bytes DamageType;
        public FixedString64Bytes ArmorType;
        public int PopCost;
    }
    
    public struct MovementComponent : IComponentData
    {
        public float3 Destination;
        public float Speed;
        public bool IsMoving;
        public float StoppingDistance;
    }
    
    public struct CombatComponent : IComponentData
    {
        public Entity Target;
        public float AttackCooldown;
        public float LastAttackTime;
        public bool IsAttacking;
        public float AttackDamage;
        public FixedString64Bytes DamageType;
    }
    
    public struct DefenseComponent : IComponentData
    {
        public float MeleeDefense;
        public float RangedDefense;
        public float SiegeDefense;
        public float MagicDefense;
    }
}