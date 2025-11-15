using System;
using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Jobs;
using TheWaningBorder.Core.Components;
using TheWaningBorder.Core.Utilities;

namespace TheWaningBorder.Core.Systems
{
    /// <summary>
    /// Base class for systems that need to load data from TechTree.json
    /// Not an actual ECS system, just a helper base class
    /// </summary>
    public abstract class DataLoaderBase
    {
        protected TechTreeData TechTreeData { get; private set; }

        protected void LoadTechTreeData()
        {
            TechTreeData = TechTreeLoader.Data;
            
            if (TechTreeData == null)
            {
                Debug.LogError("CRITICAL ERROR: Failed to load TechTree.json. Game cannot continue without configuration data!");
                throw new InvalidOperationException("TechTree.json could not be loaded. Check JSON format.");
            }
        }

        protected UnitData GetUnitData(string unitId)
        {
            if (string.IsNullOrEmpty(unitId))
            {
                throw new ArgumentException("Unit ID cannot be null or empty!");
            }

            return TechTreeLoader.GetUnitData(unitId);
        }
    }

    /// <summary>
    /// System for handling unit movement
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct MovementSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            new MovementJob
            {
                DeltaTime = deltaTime
            }.ScheduleParallel();
        }

        [BurstCompile]
        partial struct MovementJob : IJobEntity
        {
            public float DeltaTime;

            void Execute(ref PositionComponent position, ref MovementComponent movement)
            {
                if (movement.IsMoving)
                {
                    float3 direction = math.normalize(movement.Destination - position.Position);
                    float moveDistance = movement.Speed * DeltaTime;
                    float distanceToTarget = math.distance(position.Position, movement.Destination);

                    if (distanceToTarget <= moveDistance)
                    {
                        position.Position = movement.Destination;
                        movement.IsMoving = false;
                    }
                    else
                    {
                        position.Position += direction * moveDistance;
                    }
                }
            }
        }
    }

    /// <summary>
    /// System for handling combat
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class CombatSystem : SystemBase
    {
        private EntityCommandBufferSystem _ecbSystem;
        private DataLoaderBase _dataLoader;

        protected override void OnCreate()
        {
            _ecbSystem = World.GetOrCreateSystemManaged<BeginSimulationEntityCommandBufferSystem>();
            _dataLoader = new CombatDataLoader();
            _dataLoader.LoadTechTreeData();
        }

        protected override void OnUpdate()
        {
            float currentTime = (float)SystemAPI.Time.ElapsedTime;
            var ecb = _ecbSystem.CreateCommandBuffer();

            Entities
                .WithName("UpdateAttacks")
                .ForEach((Entity entity, ref AttackComponent attack, in PositionComponent position) =>
                {
                    // Attack logic will use damage modifiers from TechTree.json
                    if (currentTime - attack.LastAttackTime >= 1f / attack.AttackSpeed)
                    {
                        attack.LastAttackTime = currentTime;
                        // Attack logic would go here
                    }
                }).Schedule();

            _ecbSystem.AddJobHandleForProducer(Dependency);
        }

        private class CombatDataLoader : DataLoaderBase { }
    }

    /// <summary>
    /// System for handling health and damage
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class HealthSystem : SystemBase
    {
        private EntityCommandBufferSystem _ecbSystem;

        protected override void OnCreate()
        {
            _ecbSystem = World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();
        }

        protected override void OnUpdate()
        {
            var ecb = _ecbSystem.CreateCommandBuffer().AsParallelWriter();

            Entities
                .WithName("ProcessHealth")
                .ForEach((Entity entity, int entityInQueryIndex, ref HealthComponent health) =>
                {
                    if (health.CurrentHp <= 0)
                    {
                        ecb.DestroyEntity(entityInQueryIndex, entity);
                    }
                    else if (health.CurrentHp > health.MaxHp)
                    {
                        health.CurrentHp = health.MaxHp;
                    }
                }).ScheduleParallel();

            _ecbSystem.AddJobHandleForProducer(Dependency);
        }
    }

    /// <summary>
    /// System for projectile movement
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct ProjectileSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            var ecb = new EntityCommandBuffer(Allocator.TempJob);

            new ProjectileJob
            {
                DeltaTime = deltaTime,
                ECB = ecb.AsParallelWriter()
            }.ScheduleParallel();

            state.Dependency.Complete();
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        [BurstCompile]
        partial struct ProjectileJob : IJobEntity
        {
            public float DeltaTime;
            public EntityCommandBuffer.ParallelWriter ECB;

            void Execute(Entity entity, [ChunkIndexInQuery] int chunkIndex, 
                        ref ProjectileComponent projectile, ref PositionComponent position)
            {
                float3 direction = math.normalize(projectile.TargetPosition - position.Position);
                float moveDistance = projectile.Speed * DeltaTime;
                float distanceToTarget = math.distance(position.Position, projectile.TargetPosition);

                if (distanceToTarget <= moveDistance)
                {
                    // Hit target - destroy projectile
                    ECB.DestroyEntity(chunkIndex, entity);
                }
                else
                {
                    position.Position += direction * moveDistance;
                }
            }
        }
    }
}
