using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

namespace TheWaningBorder.Buildings.Base
{
    public struct BuildingComponent : IComponentData
    {
        public FixedString64Bytes BuildingId;
        public FixedString64Bytes BuildingName;
        public float ConstructionProgress;
        public bool IsConstructed;
        public bool IsFoundation;
        public float LineOfSight;
        public FixedString64Bytes ArmorType;
    }
    
    public struct ProductionComponent : IComponentData
    {
        public bool CanProduceUnits;
        public bool IsProducing;
        public float ProductionProgress;
        public FixedString64Bytes CurrentProductionId;
        public float ProductionTime;
    }
    
    public struct TrainingQueueComponent : IComponentData
    {
        public FixedString512Bytes QueuedUnits; // Comma-separated list
        public int QueueLength;
        public float CurrentProgress;
        public bool IsTraining;
    }
    
    public struct GatheringPointComponent : IComponentData
    {
        public float3 RallyPoint;
        public bool HasRallyPoint;
    }
}