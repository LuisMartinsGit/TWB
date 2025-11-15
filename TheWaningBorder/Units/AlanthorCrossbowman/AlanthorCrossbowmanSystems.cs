using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Unity.Collections;
using TheWaningBorder.Core.Components;
using TheWaningBorder.Core.Systems;

namespace TheWaningBorder.Units.AlanthorCrossbowman
{
    /// <summary>
    /// System for handling Alanthor_Crossbowman projectile attacks
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class AlanthorCrossbowmanProjectileSystem : DataLoaderSystem
    {
        private EndSimulationEntityCommandBufferSystem _endSimEcbSystem;

        protected override void OnCreate()
        {
            base.OnCreate();
            _endSimEcbSystem = World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();
        }

        protected override void OnUpdate()
        {
            // Get data ONCE on main thread (this can freely use 'this')
            var unitData = GetUnitData("Alanthor_Crossbowman");
            float projectileSpeed = unitData.projectileSpeed > 0 ? unitData.projectileSpeed : 20f;

            // Create ECB writer (struct) – safe to capture in Burst job
            var ecb = _endSimEcbSystem.CreateCommandBuffer().AsParallelWriter();

            Entities
                .WithAll<AlanthorCrossbowmanTag>()
                .ForEach((Entity entity,
                          int entityInQueryIndex,
                          in AttackComponent attack,
                          in PositionComponent position) =>
                {
                    // NOTE: DamageType should be a FixedString, not a C# string, in AttackComponent
                    if (attack.DamageType == "ranged")
                    {
                        // Create projectile via ECB – no EntityManager, no 'this'
                        var projectile = ecb.CreateEntity(entityInQueryIndex);

                        ecb.AddComponent(entityInQueryIndex, projectile, new ProjectileComponent
                        {
                            StartPosition  = position.Position,
                            TargetPosition = position.Position,   // TODO: set actual target when you have it
                            Speed          = projectileSpeed,
                            Damage         = attack.Damage,
                            DamageType     = attack.DamageType
                        });

                        ecb.AddComponent(entityInQueryIndex, projectile, new PositionComponent
                        {
                            Position = position.Position
                        });
                    }
                })
                .ScheduleParallel();

            // Make sure ECB system knows about this job
            _endSimEcbSystem.AddJobHandleForProducer(Dependency);
        }
    }
}
