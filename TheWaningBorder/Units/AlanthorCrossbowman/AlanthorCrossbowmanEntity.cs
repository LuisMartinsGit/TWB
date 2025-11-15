using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using TheWaningBorder.Core.Components;
using TheWaningBorder.Core.Systems;

namespace TheWaningBorder.Units.AlanthorCrossbowman
{
    /// <summary>
    /// Entity definition for Alanthor_Crossbowman unit
    /// All values MUST be loaded from TechTree.json - NO HARDCODED VALUES!
    /// </summary>
    public partial class AlanthorCrossbowmanEntity : DataLoaderSystem
    {
        private EntityArchetype alanthorcrossbowmanArchetype;

        protected override void OnCreate()
        {
            base.OnCreate();
            
            // Create archetype for Alanthor_Crossbowman
            alanthorcrossbowmanArchetype = EntityManager.CreateArchetype(
                typeof(UnitDataComponent),
                typeof(PositionComponent),
                typeof(HealthComponent),
                typeof(MovementComponent),
                typeof(AttackComponent),
                typeof(DefenseComponent),
                typeof(CostComponent),
                typeof(AlanthorCrossbowmanTag)
            );
        }

        public Entity CreateAlanthorCrossbowman(float3 spawnPosition)
        {
            // Load unit data from TechTree.json
            var unitData = GetUnitData("Alanthor_Crossbowman");
            
            if (unitData == null)
            {
                Debug.LogError("CRITICAL ERROR: Alanthor_Crossbowman data not found in TechTree.json!");
                throw new InvalidOperationException("Alanthor_Crossbowman configuration missing from TechTree.json!");
            }

            // Create entity with loaded data
            var entity = EntityManager.CreateEntity(alanthorcrossbowmanArchetype);
            
            // Set components from JSON data - NO HARDCODED VALUES
            EntityManager.SetComponentData(entity, new UnitDataComponent
            {
                Id = unitData.id,
                Class = unitData.@class,
                Hp = unitData.hp,
                Speed = unitData.speed,
                TrainingTime = unitData.trainingTime,
                ArmorType = unitData.armorType,
                Damage = unitData.damage,
                DamageType = unitData.damageType,
                LineOfSight = unitData.lineOfSight,
                AttackRange = unitData.attackRange,
                MinAttackRange = unitData.minAttackRange,
                PopCost = unitData.popCost
            });

            EntityManager.SetComponentData(entity, new PositionComponent
            {
                Position = spawnPosition
            });

            EntityManager.SetComponentData(entity, new HealthComponent
            {
                CurrentHp = unitData.hp,
                MaxHp = unitData.hp
            });

            EntityManager.SetComponentData(entity, new MovementComponent
            {
                Speed = unitData.speed,
                Destination = spawnPosition,
                IsMoving = false
            });

            EntityManager.SetComponentData(entity, new AttackComponent
            {
                Damage = unitData.damage,
                DamageType = unitData.damageType,
                AttackSpeed = 1.0f, // Should be loaded from JSON if available
                AttackRange = unitData.attackRange,
                MinAttackRange = unitData.minAttackRange,
                LastAttackTime = 0
            });

            EntityManager.SetComponentData(entity, new DefenseComponent
            {
                Melee = unitData.defense.melee,
                Ranged = unitData.defense.ranged,
                Siege = unitData.defense.siege,
                Magic = unitData.defense.magic
            });

            EntityManager.SetComponentData(entity, new CostComponent
            {
                Supplies = unitData.cost.ContainsKey("Supplies") ? unitData.cost["Supplies"] : 0,
                Iron = unitData.cost.ContainsKey("Iron") ? unitData.cost["Iron"] : 0,
                Crystal = unitData.cost.ContainsKey("Crystal") ? unitData.cost["Crystal"] : 0,
                Veilsteel = unitData.cost.ContainsKey("Veilsteel") ? unitData.cost["Veilsteel"] : 0,
                Glow = unitData.cost.ContainsKey("Glow") ? unitData.cost["Glow"] : 0
            });

            return entity;
        }

        protected override void OnUpdate()
        {
            throw new NotImplementedException();
        }

    }
}
