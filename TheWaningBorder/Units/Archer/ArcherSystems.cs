using Unity.Entities;
using Unity.Mathematics;
using TheWaningBorder.Core.Components;
using TheWaningBorder.Core.Systems;

namespace TheWaningBorder.Units.Archer
{
    /// <summary>
    /// System for handling Archer projectile attacks
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class ArcherProjectileSystem : DataLoaderSystem
    {
        private EndSimulationEntityCommandBufferSystem _endSimEcbSystem;

        protected override void OnCreate()
        {
            base.OnCreate();
            _endSimEcbSystem = World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();
        }

        protected override void OnUpdate()
        {
            // Get data once on the main thread (this can use 'this' safely)
            var unitData = GetUnitData("Archer");
            float projectileSpeed = unitData.projectileSpeed > 0 ? unitData.projectileSpeed : 20f;

            // Command buffer for structural changes in a job
            var ecb = _endSimEcbSystem.CreateCommandBuffer().AsParallelWriter();

            Entities
                .WithAll<ArcherTag>()
                .ForEach((Entity entity,
                          int entityInQueryIndex,
                          in AttackComponent attack,
                          in PositionComponent position) =>
                {
                    // NOTE: DamageType should ideally be a FixedString, not a C# string, in AttackComponent
                    if (attack.DamageType != "ranged")
                        return;

                    // Create projectile via ECB (no EntityManager, no 'this')
                    var projectile = ecb.CreateEntity(entityInQueryIndex);

                    ecb.AddComponent(entityInQueryIndex, projectile, new ProjectileComponent
                    {
                        StartPosition  = position.Position,
                        TargetPosition = position.Position,      // TODO: set to real target
                        Speed          = projectileSpeed,
                        Damage         = attack.Damage,
                        DamageType     = attack.DamageType
                    });

                    // If your ProjectileComponent doesn't hold position, keep a PositionComponent too:
                    ecb.AddComponent(entityInQueryIndex, projectile, new PositionComponent
                    {
                        Position = position.Position
                    });
                })
                .ScheduleParallel();

            // Tell the ECB system about this job so it plays back safely
            _endSimEcbSystem.AddJobHandleForProducer(Dependency);
        }
    }
}
