using Unity.Entities;
using Unity.Mathematics;
using TheWaningBorder.Core.Components;
using TheWaningBorder.Core.Systems;

namespace TheWaningBorder.Units.FeraldisHunter
{
    /// <summary>
    /// System for handling Feraldis_Hunter projectile attacks
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class FeraldisHunterProjectileSystem : DataLoaderSystem
    {
        private EndSimulationEntityCommandBufferSystem _endSimEcbSystem;

        protected override void OnCreate()
        {
            base.OnCreate();
            _endSimEcbSystem = World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();
        }

        protected override void OnUpdate()
        {
            // 1. Get data from JSON on the main thread (safe to use 'this' here)
            var unitData = GetUnitData("Feraldis_Hunter");
            float projectileSpeed = unitData.projectileSpeed > 0 ? unitData.projectileSpeed : 20f;

            // 2. Create ECB for structural changes inside the job
            var ecb = _endSimEcbSystem.CreateCommandBuffer().AsParallelWriter();

            // 3. Burst/job-friendly ForEach (no EntityManager, no instance methods)
            Entities
                .WithAll<FeraldisHunterTag>()
                .ForEach((Entity entity,
                          int entityInQueryIndex,
                          in AttackComponent attack,
                          in PositionComponent position) =>
                {
                    // DamageType should ideally be a FixedString, not a C# string
                    if (attack.DamageType != "ranged")
                        return;

                    // Create projectile via ECB instead of EntityManager
                    var projectile = ecb.CreateEntity(entityInQueryIndex);

                    ecb.AddComponent(entityInQueryIndex, projectile, new ProjectileComponent
                    {
                        StartPosition  = position.Position,
                        TargetPosition = position.Position,    // TODO: set to actual target later
                        Speed          = projectileSpeed,
                        Damage         = attack.Damage,
                        DamageType     = attack.DamageType
                    });

                    // If your projectile also needs PositionComponent:
                    ecb.AddComponent(entityInQueryIndex, projectile, new PositionComponent
                    {
                        Position = position.Position
                    });
                })
                .ScheduleParallel();

            // 4. Make sure ECB playback waits for this job
            _endSimEcbSystem.AddJobHandleForProducer(Dependency);
        }
    }
}
