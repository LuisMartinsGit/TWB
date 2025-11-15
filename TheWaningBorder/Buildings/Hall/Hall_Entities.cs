using Unity.Entities;
using Unity.Mathematics;
using TheWaningBorder.Core.GameManager;
using TheWaningBorder.Core.Utils;
using TheWaningBorder.Buildings.Base;

namespace TheWaningBorder.Buildings.Hall
{
    public static class Hall_Entities
    {
        public static Entity CreateHall(EntityManager entityManager, float3 position, int playerId)
        {
            var hallDef = TechTreeLoader.GetBuildingDef("Hall");
            if (hallDef == null)
            {
                UnityEngine.Debug.LogError("[Hall] Definition not found in TechTree!");
                return Entity.Null;
            }
            
            var entity = entityManager.CreateEntity();
            
            // Building component
            entityManager.AddComponentData(entity, new BuildingComponent
            {
                BuildingId = new Unity.Collections.FixedString64Bytes(hallDef.id),
                BuildingName = new Unity.Collections.FixedString64Bytes(hallDef.name),
                ConstructionProgress = 1f,
                IsConstructed = true,
                IsFoundation = false,
                LineOfSight = hallDef.lineOfSight,
                ArmorType = new Unity.Collections.FixedString64Bytes(hallDef.armorType)
            });
            
            // Position
            entityManager.AddComponentData(entity, new PositionComponent
            {
                Position = position
            });
            
            // Health
            entityManager.AddComponentData(entity, new HealthComponent
            {
                CurrentHp = hallDef.hp,
                MaxHp = hallDef.hp,
                RegenRate = 0
            });
            
            // Owner
            entityManager.AddComponentData(entity, new OwnerComponent
            {
                PlayerId = playerId,
                TeamId = playerId
            });
            
            // Production capability
            if (hallDef.trains != null && hallDef.trains.Count > 0)
            {
                entityManager.AddComponentData(entity, new ProductionComponent
                {
                    CanProduceUnits = true,
                    IsProducing = false,
                    ProductionProgress = 0f,
                    CurrentProductionId = new Unity.Collections.FixedString64Bytes(""),
                    ProductionTime = 0f
                });
                
                entityManager.AddComponentData(entity, new TrainingQueueComponent
                {
                    QueuedUnits = new Unity.Collections.FixedString512Bytes(""),
                    QueueLength = 0,
                    CurrentProgress = 0f,
                    IsTraining = false
                });
                
                entityManager.AddComponentData(entity, new GatheringPointComponent
                {
                    RallyPoint = position + new float3(5, 0, 5),
                    HasRallyPoint = true
                });
            }
            
            // Selectable
            entityManager.AddComponentData(entity, new SelectableComponent
            {
                IsSelected = false,
                SelectionRadius = 4f
            });
            
            return entity;
        }
    }
}