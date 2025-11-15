using Unity.Entities;
using Unity.Mathematics;
using TheWaningBorder.Core.Components;
using TheWaningBorder.Core.Systems;

namespace TheWaningBorder.Units.RunaiSkirmisher
{
    /// <summary>
    /// System for handling Runai_Skirmisher projectile attacks
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class RunaiSkirmisherProjectileSystem : DataLoaderSystem
    {
        private EndSimulationEntityCommandBufferSystem _endSimEcbSystem;

        protected override void OnCreate()
        {
            base.OnCreate();
            _endSimEcbSystem = World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();
        }

        protected override void OnUpdate()
        {
            // 1) Get JSON / config once on the main thread (safe to use 'this' here)
            var unitData = GetUnitData("Runai_Skirmisher");
            float projectileSpeed = unitData.projectileSpeed > 0 ? unitData.projectileSpeed : 20f;

            // 2) Command buffer for structural changes from a job
            var ecb = _endSimEcbSystem.CreateCommandBuffer().AsParallelWriter();

            // 3) Burst-friendly job, no 'this', no EntityManager, no instance methods
            Entities
                .WithAll<RunaiSkirmisherTag>()
                .ForEach((Entity entity,
                          int entityInQueryIndex,
                          in AttackComponent attack,
                          in PositionComponent position) =>
                {
                    // DamageType should ideally be a FixedString, not a C# string
                    if (attack.DamageType != "ranged")
                        return;

                    // Create projectile via ECB, not EntityManager
                    var projectile = ecb.CreateEntity(entityInQueryIndex);

                    ecb.AddComponent(entityInQueryIndex, projectile, new ProjectileComponent
                    {
                        StartPosition  = position.Position,
                        TargetPosition = position.Position,  // TODO: set to actual target when you have it
                        Speed          = projectileSpeed,
                        Damage         = attack.Damage,
                        DamageType     = attack.DamageType
                    });

                    // If you want a separate PositionComponent on the projectile:
                    ecb.AddComponent(entityInQueryIndex, projectile, new PositionComponent
                    {
                        Position = position.Position
                    });
                })
                .ScheduleParallel();

            // 4) Let the ECB system know about this job
            _endSimEcbSystem.AddJobHandleForProducer(Dependency);
        }
    }
}
