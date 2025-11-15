using Unity.Entities;
using Unity.Mathematics;
using TheWaningBorder.Core.GameManager;
using TheWaningBorder.Core.Utils;
using TheWaningBorder.Units.Base;
using TheWaningBorder.Resources.IronMining;

namespace TheWaningBorder.Units.Builder
{
    public static class Builder_Entities
    {
        public static Entity CreateBuilder(EntityManager entityManager, float3 position, int playerId)
        {
            var builderDef = TechTreeLoader.GetUnitDef("Builder");
            if (builderDef == null)
            {
                UnityEngine.Debug.LogError("[Builder] Definition not found in TechTree!");
                return Entity.Null;
            }
            
            var entity = entityManager.CreateEntity();
            
            // Unit component
            entityManager.AddComponentData(entity, new UnitComponent
            {
                UnitId = new Unity.Collections.FixedString64Bytes(builderDef.id),
                UnitClass = new Unity.Collections.FixedString64Bytes(builderDef.@class),
                Speed = builderDef.speed,
                AttackDamage = builderDef.damage,
                AttackSpeed = 1f,
                AttackRange = builderDef.attackRange,
                MinAttackRange = builderDef.minAttackRange,
                LineOfSight = builderDef.lineOfSight,
                DamageType = new Unity.Collections.FixedString64Bytes(builderDef.damageType),
                ArmorType = new Unity.Collections.FixedString64Bytes(builderDef.armorType),
                PopCost = builderDef.popCost
            });
            
            // Position
            entityManager.AddComponentData(entity, new PositionComponent
            {
                Position = position
            });
            
            // Health
            entityManager.AddComponentData(entity, new HealthComponent
            {
                CurrentHp = builderDef.hp,
                MaxHp = builderDef.hp,
                RegenRate = 0
            });
            
            // Movement
            entityManager.AddComponentData(entity, new MovementComponent
            {
                Destination = position,
                Speed = builderDef.speed,
                IsMoving = false,
                StoppingDistance = 1f
            });
            
            // Owner
            entityManager.AddComponentData(entity, new OwnerComponent
            {
                PlayerId = playerId
            });
            
            // Builder capability
            entityManager.AddComponentData(entity, new BuilderComponent
            {
                BuildSpeed = builderDef.buildSpeed,
                IsBuilding = false,
                BuildingTarget = Entity.Null,
                BuildProgress = 0f
            });
            
            // Selectable
            entityManager.AddComponentData(entity, new SelectableComponent
            {
                IsSelected = false,
                SelectionRadius = 1f
            });
            
            // Commandable
            entityManager.AddComponentData(entity, new CommandableComponent
            {
                CanMove = true,
                CanAttack = false,
                CanBuild = true,
                CanGather = false
            });
            
            return entity;
        }
    }
    
    public struct BuilderComponent : IComponentData
    {
        public float BuildSpeed;
        public bool IsBuilding;
        public Entity BuildingTarget;
        public float BuildProgress;
    }
}