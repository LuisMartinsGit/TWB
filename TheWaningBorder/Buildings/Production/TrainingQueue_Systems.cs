using System;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using UnityEngine;
using TheWaningBorder.Core.GameManager;
using TheWaningBorder.Core.Utils;
using TheWaningBorder.Units.Base;
using TheWaningBorder.Buildings.Base;
using TheWaningBorder.Core.Components;

namespace TheWaningBorder.Buildings.Production
{
    // Training Queue System - The ONLY system that uses EntityCommandBuffer
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class TrainingQueueSystem : SystemBase
    {
        private EndSimulationEntityCommandBufferSystem _ecbSystem;

        protected override void OnCreate()
        {
            _ecbSystem = World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();
        }

        protected override void OnUpdate()
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            var ecb = _ecbSystem.CreateCommandBuffer();

            // Lookups instead of lambda params or WithAll<> (avoids DC0005 / SGQC001)
            var ownerLookup    = GetComponentLookup<Core.GameManager.OwnerComponent>(isReadOnly: true);
            var positionLookup = GetComponentLookup<PositionComponent>(isReadOnly: true);
            var buildingLookup = GetComponentLookup<BuildingComponent>(isReadOnly: true);

            Entities
                .WithName("ProcessTrainingQueues")
                .WithReadOnly(ownerLookup)
                .WithReadOnly(positionLookup)
                .WithReadOnly(buildingLookup)
                .WithoutBurst() // string ops + Debug.Log are managed
                .ForEach((Entity buildingEntity, ref TrainingQueueComponent queue) =>
                {
                    if (!queue.IsTraining || queue.QueueLength == 0)
                        return;

                    // Ensure required components exist on this entity
                    if (!buildingLookup.HasComponent(buildingEntity) ||
                        !positionLookup.HasComponent(buildingEntity) ||
                        !ownerLookup.HasComponent(buildingEntity))
                        return;

                    var position = positionLookup[buildingEntity];
                    var owner    = ownerLookup[buildingEntity];

                    // Parse the first unit in queue
                    var queuedUnits = queue.QueuedUnits.ToString();
                    if (string.IsNullOrEmpty(queuedUnits))
                        return;

                    var units = queuedUnits.Split(',');
                    if (units.Length == 0)
                        return;

                    string currentUnitId = units[0];
                    var unitDef = TechTreeLoader.GetUnitDef(currentUnitId);

                    if (unitDef == null)
                    {
                        Debug.LogError($"[Training] Unit definition not found for: {currentUnitId}");
                        queue.IsTraining = false;
                        return;
                    }

                    // Update training progress
                    queue.CurrentProgress += deltaTime / unitDef.trainingTime;

                    if (queue.CurrentProgress < 1.0f)
                        return;

                    // Training complete - spawn unit
                    float3 spawnPosition = position.Position + new float3(5, 0, 5); // Offset from building

                    // Create unit entity
                    var unitEntity = ecb.CreateEntity();

                    // Add components based on unit definition
                    ecb.AddComponent(unitEntity, new UnitComponent
                    {
                        UnitId       = new FixedString64Bytes(unitDef.id),
                        UnitClass    = new FixedString64Bytes(unitDef.@class),
                        Speed        = unitDef.speed,
                        AttackDamage = unitDef.damage,
                        AttackSpeed  = 1f,
                        AttackRange  = unitDef.attackRange,
                        MinAttackRange = unitDef.minAttackRange,
                        LineOfSight    = unitDef.lineOfSight,
                        DamageType     = new FixedString64Bytes(unitDef.damageType),
                        ArmorType      = new FixedString64Bytes(unitDef.armorType),
                        PopCost        = unitDef.popCost
                    });

                    ecb.AddComponent(unitEntity, new PositionComponent
                    {
                        Position = spawnPosition
                    });

                    ecb.AddComponent(unitEntity, new HealthComponent
                    {
                        CurrentHp = unitDef.hp,
                        MaxHp     = unitDef.hp,
                        RegenRate = 0
                    });

                    ecb.AddComponent(unitEntity, new Core.Components.MovementComponent
                    {
                        Destination      = spawnPosition,
                        Speed            = unitDef.speed,
                        IsMoving         = false,
                        StoppingDistance = 1f
                    });

                    ecb.AddComponent(unitEntity, new Core.GameManager.OwnerComponent
                    {
                        PlayerId = owner.PlayerId,
                        TeamId   = owner.TeamId
                    });

                    ecb.AddComponent(unitEntity, new Core.GameManager.SelectableComponent
                    {
                        IsSelected      = false,
                        SelectionRadius = 1f
                    });

                    ecb.AddComponent(unitEntity, new Core.GameManager.CommandableComponent
                    {
                        CanMove   = true,
                        CanAttack = unitDef.damage > 0,
                        CanBuild  = unitDef.buildSpeed > 0,
                        CanGather = unitDef.gatheringSpeed > 0
                    });

                    Debug.Log($"[Training] Spawned {unitDef.id} for player {owner.PlayerId}");

                    // Remove from queue
                    if (units.Length > 1)
                    {
                        var remainingQueue = string.Join(",", units, 1, units.Length - 1);
                        queue.QueuedUnits   = new FixedString512Bytes(remainingQueue);
                        queue.QueueLength  -= 1;
                        queue.CurrentProgress = 0f;
                    }
                    else
                    {
                        queue.QueuedUnits    = new FixedString512Bytes("");
                        queue.QueueLength    = 0;
                        queue.CurrentProgress = 0f;
                        queue.IsTraining      = false;
                    }
                })
                .Run();

            _ecbSystem.AddJobHandleForProducer(Dependency);
        }
    }

    // Helper class for managing training queues
    public static class TrainingQueueHelper
    {
        public static void AddToQueue(EntityManager entityManager, Entity buildingEntity, string unitId)
        {
            if (!entityManager.HasComponent<TrainingQueueComponent>(buildingEntity))
            {
                Debug.LogError("[Training] Building doesn't have training queue component!");
                return;
            }

            var queue = entityManager.GetComponentData<TrainingQueueComponent>(buildingEntity);

            // Check if unit can be trained
            var unitDef = TechTreeLoader.GetUnitDef(unitId);
            if (unitDef == null)
            {
                Debug.LogError($"[Training] Unit definition not found: {unitId}");
                return;
            }

            // Add to queue
            string currentQueue = queue.QueuedUnits.ToString();
            if (string.IsNullOrEmpty(currentQueue))
            {
                queue.QueuedUnits = new FixedString512Bytes(unitId);
                queue.IsTraining  = true;
            }
            else
            {
                queue.QueuedUnits = new FixedString512Bytes(currentQueue + "," + unitId);
            }

            queue.QueueLength++;
            entityManager.SetComponentData(buildingEntity, queue);

            Debug.Log($"[Training] Added {unitId} to training queue");
        }

        public static void CancelFromQueue(EntityManager entityManager, Entity buildingEntity, int index)
        {
            if (!entityManager.HasComponent<TrainingQueueComponent>(buildingEntity))
                return;

            var queue = entityManager.GetComponentData<TrainingQueueComponent>(buildingEntity);
            var units = queue.QueuedUnits.ToString().Split(',');

            if (index < 0 || index >= units.Length)
                return;

            var newQueue = new System.Collections.Generic.List<string>(units);
            newQueue.RemoveAt(index);

            queue.QueuedUnits = new FixedString512Bytes(string.Join(",", newQueue));
            queue.QueueLength = newQueue.Count;

            if (index == 0)
            {
                queue.CurrentProgress = 0f;
                queue.IsTraining      = newQueue.Count > 0;
            }

            entityManager.SetComponentData(buildingEntity, queue);
        }
    }
}
