using System;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;
using UnityEngine;
using Random = Unity.Mathematics.Random;
using TheWaningBorder.Core.GameManager;
using TheWaningBorder.Core.Settings;
using TheWaningBorder.Units.Base;
using TheWaningBorder.Buildings.Base;
using TheWaningBorder.Map.Spawning;

namespace TheWaningBorder.Resources.IronMining
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class IronDepositGenerationSystem : SystemBase
    {
        private EntityQuery _spawnQuery;
        private Random _random;
        private int _nextPatchId = 0;
        
        protected override void OnCreate()
        {
            _random = new Random((uint)GameSettings.SpawnSeed);
            RequireForUpdate<GameStateComponent>();
        }
        
        public void GenerateInitialDeposits()
        {
            Debug.Log("[IronMining] Generating initial iron deposits...");
            
            var spawnPositions = GetPlayerSpawnPositions();
            
            // Generate guaranteed patches near spawn positions
            foreach (var spawnPos in spawnPositions)
            {
                CreateGuaranteedPatch(spawnPos);
            }
            
            // Generate additional random patches
            GenerateRandomPatches();
            
            Debug.Log($"[IronMining] Generated {_nextPatchId} iron patches");
        }
        
        private NativeArray<float3> GetPlayerSpawnPositions()
        {
            var positions = new NativeArray<float3>(GameSettings.TotalPlayers, Allocator.Temp);
            
            float mapRadius = GameSettings.MapHalfSize * 0.8f;
            
            switch (GameSettings.SpawnLayout)
            {
                case SpawnLayout.Circle:
                    for (int i = 0; i < GameSettings.TotalPlayers; i++)
                    {
                        float angle = (i * 2 * math.PI) / GameSettings.TotalPlayers;
                        positions[i] = new float3(
                            math.cos(angle) * mapRadius,
                            0,
                            math.sin(angle) * mapRadius
                        );
                    }
                    break;
                    
                case SpawnLayout.Grid:
                    int gridSize = (int)math.ceil(math.sqrt(GameSettings.TotalPlayers));
                    float spacing = (mapRadius * 2) / gridSize;
                    for (int i = 0; i < GameSettings.TotalPlayers; i++)
                    {
                        int x = i % gridSize;
                        int y = i / gridSize;
                        positions[i] = new float3(
                            -mapRadius + x * spacing + spacing/2,
                            0,
                            -mapRadius + y * spacing + spacing/2
                        );
                    }
                    break;
                    
                case SpawnLayout.TwoSides:
                    bool northSouth = (GameSettings.TwoSides == TwoSidesPreset.NorthVsSouth);
                    for (int i = 0; i < GameSettings.TotalPlayers; i++)
                    {
                        bool firstTeam = (i < GameSettings.TotalPlayers / 2);
                        float teamOffset = firstTeam ? -mapRadius * 0.7f : mapRadius * 0.7f;
                        float spread = ((i % (GameSettings.TotalPlayers/2)) - 1) * 20f;
                        
                        if (northSouth)
                            positions[i] = new float3(spread, 0, teamOffset);
                        else
                            positions[i] = new float3(teamOffset, 0, spread);
                    }
                    break;
                    
                default:
                    for (int i = 0; i < GameSettings.TotalPlayers; i++)
                    {
                        positions[i] = _random.NextFloat3(
                            new float3(-mapRadius, 0, -mapRadius),
                            new float3(mapRadius, 0, mapRadius)
                        );
                    }
                    break;
            }
            
            return positions;
        }
        
        private void CreateGuaranteedPatch(float3 spawnPosition)
        {
            // Place patch near spawn but not too close
            float angle = _random.NextFloat(0, 2 * math.PI);
            float distance = _random.NextFloat(15f, 25f);
            
            float3 patchCenter = spawnPosition + new float3(
                math.cos(angle) * distance,
                0,
                math.sin(angle) * distance
            );
            
            CreatePatch(patchCenter, GameSettings.DepositsPerPatch, true);
        }
        
        private void GenerateRandomPatches()
        {
            var existingPatches = new NativeList<float3>(Allocator.Temp);
            
            Entities.ForEach((in IronPatchComponent patch) =>
            {
                existingPatches.Add(patch.CenterPosition);
            }).Run();
            
            int patchesCreated = 0;
            int maxAttempts = 100;
            
            while (patchesCreated < GameSettings.AdditionalRandomPatches && maxAttempts > 0)
            {
                maxAttempts--;
                
                float3 position = new float3(
                    _random.NextFloat(-GameSettings.MapHalfSize, GameSettings.MapHalfSize),
                    0,
                    _random.NextFloat(-GameSettings.MapHalfSize, GameSettings.MapHalfSize)
                );
                
                // Check minimum distance from other patches
                bool tooClose = false;
                foreach (var existing in existingPatches)
                {
                    if (math.distance(position, existing) < GameSettings.MinPatchDistance)
                    {
                        tooClose = true;
                        break;
                    }
                }
                
                if (!tooClose)
                {
                    CreatePatch(position, GameSettings.DepositsPerPatch, false);
                    existingPatches.Add(position);
                    patchesCreated++;
                }
            }
            
            existingPatches.Dispose();
        }
        
        private void CreatePatch(float3 center, int depositCount, bool isGuaranteed)
        {
            int patchId = _nextPatchId++;
            
            // Create patch entity
            var patchEntity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(patchEntity, new IronPatchComponent
            {
                CenterPosition = center,
                Radius = GameSettings.PatchRadius,
                DepositCount = depositCount,
                PatchId = patchId,
                IsGuaranteedPatch = isGuaranteed
            });
            
            // Create individual deposits
            for (int i = 0; i < depositCount; i++)
            {
                float angle = (i * 2 * math.PI) / depositCount;
                float radius = _random.NextFloat(0.5f, GameSettings.PatchRadius);
                
                float3 depositPos = center + new float3(
                    math.cos(angle) * radius,
                    0,
                    math.sin(angle) * radius
                );
                
                CreateDeposit(depositPos, GameSettings.OrePerDeposit, patchId);
            }
        }
        
        private void CreateDeposit(float3 position, int oreAmount, int patchId)
        {
            var entity = EntityManager.CreateEntity();
            
            EntityManager.AddComponentData(entity, new IronDepositComponent
            {
                Position = position,
                RemainingOre = oreAmount,
                MaxOre = oreAmount,
                ClaimedByMiner = Entity.Null,
                PatchId = patchId,
                IsExhausted = false
            });
            
            EntityManager.AddComponentData(entity, new PositionComponent
            {
                Position = position
            });
            
            EntityManager.AddComponentData(entity, new ResourceDepositTag
            {
                Type = ResourceType.Iron
            });
        }
        
        protected override void OnUpdate() { }
    }
    
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class MinerTargetingSystem : SystemBase
    {
        private EntityQuery _depositQuery;
        private EntityQuery _dropOffQuery;
        
        protected override void OnCreate()
        {
            _depositQuery = GetEntityQuery(
                ComponentType.ReadOnly<IronDepositComponent>(),
                ComponentType.Exclude<MinerTag>()
            );
            
            _dropOffQuery = GetEntityQuery(
                ComponentType.ReadOnly<ResourceDropOffPointComponent>()
            );
        }
        
        protected override void OnUpdate()
        {
            var deposits = _depositQuery.ToEntityArray(Allocator.TempJob);
            var depositData = _depositQuery.ToComponentDataArray<IronDepositComponent>(Allocator.TempJob);
            
            var dropOffs = _dropOffQuery.ToEntityArray(Allocator.TempJob);
            var dropOffData = _dropOffQuery.ToComponentDataArray<ResourceDropOffPointComponent>(Allocator.TempJob);
            
            Entities
                .WithAll<MinerTag>()
                .ForEach((Entity entity, ref MiningStateComponent miningState, 
                         in PositionComponent position, in OwnerComponent owner) =>
                {
                    // Only retarget if idle
                    if (miningState.State != MiningState.Idle) return;
                    
                    // Find nearest unclaimed deposit
                    Entity nearestDeposit = Entity.Null;
                    float nearestDistance = float.MaxValue;
                    
                    for (int i = 0; i < deposits.Length; i++)
                    {
                        if (depositData[i].IsExhausted) continue;
                        if (depositData[i].ClaimedByMiner != Entity.Null) continue;
                        
                        float distance = math.distance(position.Position, depositData[i].Position);
                        if (distance < nearestDistance)
                        {
                            nearestDistance = distance;
                            nearestDeposit = deposits[i];
                        }
                    }
                    
                    if (nearestDeposit != Entity.Null)
                    {
                        // Find nearest drop-off point for return trip
                        Entity nearestDropOff = Entity.Null;
                        float nearestDropOffDistance = float.MaxValue;
                        
                        for (int i = 0; i < dropOffs.Length; i++)
                        {
                            if (dropOffData[i].OwnerId != owner.PlayerId) continue;
                            if (!dropOffData[i].CanReceiveIron) continue;
                            
                            float distance = math.distance(position.Position, dropOffData[i].DropOffPosition);
                            if (distance < nearestDropOffDistance)
                            {
                                nearestDropOffDistance = distance;
                                nearestDropOff = dropOffs[i];
                            }
                        }
                        
                        if (nearestDropOff != Entity.Null)
                        {
                            miningState.TargetDeposit = nearestDeposit;
                            miningState.ReturnBuilding = nearestDropOff;
                            miningState.State = MiningState.MovingToDeposit;
                        }
                    }
                }).Schedule();
            
            Dependency.Complete();
            deposits.Dispose();
            depositData.Dispose();
            dropOffs.Dispose();
            dropOffData.Dispose();
        }
    }
    
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(MinerTargetingSystem))]
    public partial class MiningSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            
            // Handle mining state
            Entities
                .WithAll<MinerTag>()
                .ForEach((Entity minerEntity, ref MiningStateComponent miningState, in PositionComponent position) =>
                {
                    if (miningState.State != MiningState.Mining) return;
                    if (miningState.TargetDeposit == Entity.Null) return;
                    
                    // Progress mining
                    miningState.MiningProgress += miningState.MiningSpeed * deltaTime;
                    
                    if (miningState.MiningProgress >= 1f)
                    {
                        // Mining complete
                        miningState.MiningProgress = 0f;
                        
                        // Check if deposit still has ore (access in main thread)
                        if (SystemAPI.HasComponent<IronDepositComponent>(miningState.TargetDeposit))
                        {
                            var deposit = SystemAPI.GetComponent<IronDepositComponent>(miningState.TargetDeposit);
                            
                            int oreToTake = math.min(deposit.RemainingOre, 
                                                     miningState.MaxCarryCapacity - miningState.CarriedOre);
                            
                            if (oreToTake > 0)
                            {
                                miningState.CarriedOre += oreToTake;
                                deposit.RemainingOre -= oreToTake;
                                
                                if (deposit.RemainingOre <= 0)
                                {
                                    deposit.IsExhausted = true;
                                    deposit.ClaimedByMiner = Entity.Null;
                                }
                                
                                SystemAPI.SetComponent(miningState.TargetDeposit, deposit);
                            }
                        }
                        
                        // Check if should return or continue mining
                        if (miningState.CarriedOre >= miningState.MaxCarryCapacity)
                        {
                            miningState.State = MiningState.Returning;
                            
                            // Release claim on deposit
                            if (SystemAPI.HasComponent<IronDepositComponent>(miningState.TargetDeposit))
                            {
                                var deposit = SystemAPI.GetComponent<IronDepositComponent>(miningState.TargetDeposit);
                                deposit.ClaimedByMiner = Entity.Null;
                                SystemAPI.SetComponent(miningState.TargetDeposit, deposit);
                            }
                        }
                    }
                }).Run();
        }
    }
    
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(MiningSystem))]
    public partial class OreDeliverySystem : SystemBase
    {
        protected override void OnUpdate()
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            
            Entities
                .WithAll<MinerTag>()
                .ForEach((Entity entity, ref MiningStateComponent miningState, in OwnerComponent owner) =>
                {
                    if (miningState.State != MiningState.Depositing) return;
                    if (miningState.CarriedOre <= 0) return;
                    
                    // Find player resources
                    bool delivered = false;
                    Entities
                        .WithAll<PlayerComponent>()
                        .ForEach((Entity playerEntity, ref ResourcesComponent resources, in PlayerComponent player) =>
                        {
                            if (player.PlayerId == owner.PlayerId)
                            {
                                resources.Iron += miningState.CarriedOre;
                                delivered = true;
                            }
                        }).Run();
                    
                    if (delivered)
                    {
                        Debug.Log($"[Mining] Player {owner.PlayerId} received {miningState.CarriedOre} iron");
                        miningState.CarriedOre = 0;
                        miningState.State = MiningState.Idle;
                        miningState.TargetDeposit = Entity.Null;
                    }
                }).Run();
        }
    }
}
