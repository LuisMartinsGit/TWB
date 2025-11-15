using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using TheWaningBorder.Core.GameManager;
using TheWaningBorder.Resources.IronMining;
using TheWaningBorder.Units.Base;
using Unity.Collections;
using Unity.Burst;

namespace TheWaningBorder.Resources.IronMining
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [BurstCompile]
    public partial class IronMiningSystem : SystemBase
    {
        private EntityCommandBufferSystem _ecbSystem;
        private EntityQuery _depositQuery;
        private EntityQuery _buildingQuery;
        private EntityQuery _playerQuery;
        
        protected override void OnCreate()
        {
            _ecbSystem = World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();
            
            // Create queries for deposits, buildings, and players
            _depositQuery = GetEntityQuery(
                ComponentType.ReadWrite<IronDepositComponent>(),
                ComponentType.ReadOnly<PositionComponent>()
            );
            
            _buildingQuery = GetEntityQuery(
                ComponentType.ReadOnly<BuildingComponent>(),
                ComponentType.ReadOnly<PositionComponent>(),
                ComponentType.ReadOnly<OwnerComponent>()
            );
            
            _playerQuery = GetEntityQuery(
                ComponentType.ReadWrite<ResourcesComponent>(),
                ComponentType.ReadOnly<PlayerComponent>()
            );
        }
        
        protected override void OnUpdate()
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            var ecb = _ecbSystem.CreateCommandBuffer().AsParallelWriter();
            
            // Get component lookups for efficient access
            var depositLookup = GetComponentLookup<IronDepositComponent>(false);
            var positionLookup = GetComponentLookup<PositionComponent>(true);
            var ownerLookup = GetComponentLookup<OwnerComponent>(true);
            var buildingLookup = GetComponentLookup<BuildingComponent>(true);
            
            // Get arrays of deposits and buildings for searching
            var deposits = _depositQuery.ToEntityArray(Allocator.TempJob);
            var buildings = _buildingQuery.ToEntityArray(Allocator.TempJob);
            
            // Find idle miners and assign them to deposits
            Entities
                .WithAll<MinerTag>()
                .WithReadOnly(positionLookup)
                //.WithReadOnly(ownerLookup)
                .WithDisposeOnCompletion(deposits)
                .ForEach((Entity entity, int entityInQueryIndex, 
                         ref MiningStateComponent miningState, 
                         ref MovementComponent movement,
                         in PositionComponent position, 
                         in OwnerComponent owner) =>
                {
                    if (miningState.State == MiningState.Idle)
                    {
                        // Find nearest unclaimed deposit
                        Entity nearestDeposit = Entity.Null;
                        float nearestDistance = float.MaxValue;
                        
                        for (int i = 0; i < deposits.Length; i++)
                        {
                            var depositEntity = deposits[i];
                            if (!depositLookup.HasComponent(depositEntity)) continue;
                            
                            var deposit = depositLookup[depositEntity];
                            if (deposit.RemainingOre <= 0 || deposit.ClaimedByMiner != Entity.Null) continue;
                            
                            if (!positionLookup.HasComponent(depositEntity)) continue;
                            var depositPos = positionLookup[depositEntity];
                            
                            float distance = math.distance(position.Position, depositPos.Position);
                            if (distance < nearestDistance)
                            {
                                nearestDistance = distance;
                                nearestDeposit = depositEntity;
                            }
                        }
                        
                        if (nearestDeposit != Entity.Null)
                        {
                            miningState.State = MiningState.MovingToDeposit;
                            miningState.TargetDeposit = nearestDeposit;
                            
                            // Claim the deposit - need to use ECB since we're in a job
                            var depositPos = positionLookup[nearestDeposit];
                            movement.Destination = depositPos.Position;
                            movement.IsMoving = true;
                            
                            // Mark deposit as claimed (will be applied via ECB after job completes)
                            var deposit = depositLookup[nearestDeposit];
                            deposit.ClaimedByMiner = entity;
                            depositLookup[nearestDeposit] = deposit;
                        }
                    }
                })
                .ScheduleParallel();
            
            // Handle mining when miners reach deposits
            Entities
                .WithAll<MinerTag>()
                .WithReadOnly(positionLookup)
                .ForEach((Entity entity, ref MiningStateComponent miningState, in PositionComponent position) =>
                {
                    if (miningState.State == MiningState.MovingToDeposit && miningState.TargetDeposit != Entity.Null)
                    {
                        if (depositLookup.HasComponent(miningState.TargetDeposit) && 
                            positionLookup.HasComponent(miningState.TargetDeposit))
                        {
                            var deposit = depositLookup[miningState.TargetDeposit];
                            var depositPos = positionLookup[miningState.TargetDeposit];
                            
                            float distance = math.distance(position.Position, depositPos.Position);
                            if (distance <= 2f) // Mining range
                            {
                                miningState.State = MiningState.Mining;
                                miningState.MiningProgress = 0f;
                            }
                        }
                    }
                })
                .ScheduleParallel();
            
            // Mine ore
            Entities
                .WithAll<MinerTag>()
                .ForEach((Entity entity, ref MiningStateComponent miningState) =>
                {
                    if (miningState.State == MiningState.Mining)
                    {
                        miningState.MiningProgress += deltaTime * 20f; // Mining speed
                        
                        if (miningState.MiningProgress >= 100f)
                        {
                            if (depositLookup.HasComponent(miningState.TargetDeposit))
                            {
                                var deposit = depositLookup[miningState.TargetDeposit];
                                
                                int oreToMine = math.min(5, deposit.RemainingOre); // Mine 5 ore at a time
                                deposit.RemainingOre -= oreToMine;
                                miningState.CarriedOre = oreToMine;
                                
                                depositLookup[miningState.TargetDeposit] = deposit;
                                
                                miningState.State = MiningState.Returning;
                                miningState.MiningProgress = 0f;
                                
                                // Release claim on deposit
                                deposit.ClaimedByMiner = Entity.Null;
                                depositLookup[miningState.TargetDeposit] = deposit;
                            }
                        }
                    }
                })
                .ScheduleParallel();
            
            // Return to base - find dropoff points
            var buildingsArray = _buildingQuery.ToEntityArray(Allocator.TempJob);
            
            Entities
                .WithAll<MinerTag>()
                .WithReadOnly(positionLookup)
                .WithReadOnly(ownerLookup)
                //.WithReadOnly(buildingLookup)
                .WithDisposeOnCompletion(buildingsArray)
                .ForEach((Entity entity, ref MiningStateComponent miningState, ref MovementComponent movement,
                         in OwnerComponent owner) =>
                {
                    if (miningState.State == MiningState.Returning)
                    {
                        // Find nearest dropoff point (Town Center, etc.)
                        Entity nearestDropoff = Entity.Null;
                        float3 dropoffPos = float3.zero;
                        float nearestDistance = float.MaxValue;
                        
                        for (int i = 0; i < buildingsArray.Length; i++)
                        {
                            var buildingEntity = buildingsArray[i];
                            if (!ownerLookup.HasComponent(buildingEntity)) continue;
                            
                            var buildingOwner = ownerLookup[buildingEntity];
                            if (buildingOwner.PlayerId != owner.PlayerId) continue;
                            
                            if (!positionLookup.HasComponent(buildingEntity)) continue;
                            var buildingPos = positionLookup[buildingEntity];
                            
                            float distance = math.distance(movement.Destination, buildingPos.Position);
                            if (distance < nearestDistance)
                            {
                                nearestDistance = distance;
                                nearestDropoff = buildingEntity;
                                dropoffPos = buildingPos.Position;
                            }
                        }
                        
                        if (nearestDropoff != Entity.Null)
                        {
                            miningState.TargetDropoff = nearestDropoff;
                            movement.Destination = dropoffPos;
                            movement.IsMoving = true;
                            miningState.State = MiningState.Depositing;
                        }
                    }
                })
                .ScheduleParallel();
        }
    }
    
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(IronMiningSystem))]
    [BurstCompile]
    public partial class OreDeliverySystem : SystemBase
    {
        private EntityQuery _playerQuery;
        
        protected override void OnCreate()
        {
            _playerQuery = GetEntityQuery(
                ComponentType.ReadWrite<ResourcesComponent>(),
                ComponentType.ReadOnly<PlayerComponent>()
            );
        }
        
        protected override void OnUpdate()
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            
            // Get component lookups
            var positionLookup = GetComponentLookup<PositionComponent>(true);
            var resourcesLookup = GetComponentLookup<ResourcesComponent>(false);
            var playerLookup = GetComponentLookup<PlayerComponent>(true);
            
            // Get player entities
            var playerEntities = _playerQuery.ToEntityArray(Allocator.TempJob);
            
            Entities
                .WithAll<MinerTag>()
                .WithReadOnly(positionLookup)
                .WithReadOnly(playerLookup)
                .WithDisposeOnCompletion(playerEntities)
                .ForEach((Entity entity, ref MiningStateComponent miningState, 
                         in OwnerComponent owner, in PositionComponent position) =>
                {
                    if (miningState.State != MiningState.Depositing) return;
                    if (miningState.CarriedOre <= 0) return;
                    
                    // Check if near dropoff
                    if (miningState.TargetDropoff != Entity.Null && 
                        positionLookup.HasComponent(miningState.TargetDropoff))
                    {
                        var dropoffPos = positionLookup[miningState.TargetDropoff];
                        float distance = math.distance(position.Position, dropoffPos.Position);
                        
                        if (distance <= 3f) // Dropoff range
                        {
                            // Find player resources and deliver ore
                            bool delivered = false;
                            
                            for (int i = 0; i < playerEntities.Length; i++)
                            {
                                var playerEntity = playerEntities[i];
                                if (!playerLookup.HasComponent(playerEntity)) continue;
                                
                                var player = playerLookup[playerEntity];
                                if (player.PlayerId == owner.PlayerId)
                                {
                                    if (resourcesLookup.HasComponent(playerEntity))
                                    {
                                        var resources = resourcesLookup[playerEntity];
                                        resources.Iron += miningState.CarriedOre;
                                        resourcesLookup[playerEntity] = resources;
                                        delivered = true;
                                        break;
                                    }
                                }
                            }
                            
                            if (delivered)
                            {
                                miningState.CarriedOre = 0;
                                miningState.State = MiningState.Idle;
                                miningState.TargetDeposit = Entity.Null;
                                miningState.TargetDropoff = Entity.Null;
                            }
                        }
                    }
                })
                .ScheduleParallel();
        }
    }
    
    // BuildingComponent definition (should be in Buildings namespace but included here for compilation)
    public struct BuildingComponent : IComponentData
    {
        public FixedString64Bytes BuildingId;
        public bool IsConstructed;
    }
}