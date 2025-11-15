using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using TheWaningBorder.Core.GameManager;

namespace TheWaningBorder.Resources.IronMining
{
    public static class IronMining_Entities
    {
        public static Entity CreateIronDeposit(EntityManager entityManager, float3 position, int oreAmount, int patchId = 0)
        {
            var entity = entityManager.CreateEntity();
            
            entityManager.AddComponentData(entity, new IronDepositComponent
            {
                Position = position,
                RemainingOre = oreAmount,
                MaxOre = oreAmount,
                ClaimedByMiner = Entity.Null,
                PatchId = patchId,
                IsExhausted = false
            });
            
            entityManager.AddComponentData(entity, new PositionComponent
            {
                Position = position
            });
            
            entityManager.AddComponentData(entity, new ResourceDepositTag
            {
                Type = ResourceType.Iron
            });
            
            Debug.Log($"[IronMining] Created iron deposit at {position} with {oreAmount} ore");
            
            return entity;
        }
        
        public static Entity CreateIronPatch(EntityManager entityManager, float3 center, int depositCount, float radius)
        {
            var patchEntity = entityManager.CreateEntity();
            
            entityManager.AddComponentData(patchEntity, new IronPatchComponent
            {
                CenterPosition = center,
                Radius = radius,
                DepositCount = depositCount,
                PatchId = patchEntity.Index,
                IsGuaranteedPatch = false
            });
            
            // Create individual deposits in the patch
            var random = new Unity.Mathematics.Random((uint)(center.x * 1000 + center.z));
            
            for (int i = 0; i < depositCount; i++)
            {
                float angle = (i * 2 * math.PI) / depositCount;
                float depositRadius = random.NextFloat(1f, radius);
                
                float3 depositPos = center + new float3(
                    math.cos(angle) * depositRadius,
                    0,
                    math.sin(angle) * depositRadius
                );
                
                CreateIronDeposit(entityManager, depositPos, 500, patchEntity.Index);
            }
            
            Debug.Log($"[IronMining] Created iron patch at {center} with {depositCount} deposits");
            
            return patchEntity;
        }
        
        public static void AddMiningCapability(EntityManager entityManager, Entity unitEntity, float miningSpeed, int carryCapacity)
        {
            if (!entityManager.HasComponent<MinerTag>(unitEntity))
            {
                entityManager.AddComponentData(unitEntity, new MinerTag
                {
                    PlayerId = entityManager.GetComponentData<OwnerComponent>(unitEntity).PlayerId
                });
            }
            
            if (!entityManager.HasComponent<MiningStateComponent>(unitEntity))
            {
                entityManager.AddComponentData(unitEntity, new MiningStateComponent
                {
                    TargetDeposit = Entity.Null,
                    ReturnBuilding = Entity.Null,
                    CarriedOre = 0,
                    MiningProgress = 0f,
                    State = MiningState.Idle,
                    MiningSpeed = miningSpeed,
                    MaxCarryCapacity = carryCapacity,
                    TimeAtDeposit = 0f
                });
            }
        }
    }
}
