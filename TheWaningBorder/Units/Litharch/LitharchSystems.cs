using Unity.Entities;
using Unity.Mathematics;
using TheWaningBorder.Core.Components;
using TheWaningBorder.Core.Systems;

namespace TheWaningBorder.Units.Litharch
{
    /// <summary>
    /// System for handling Litharch healing abilities
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class LitharchHealingSystem : DataLoaderSystem
    {
        protected override void OnUpdate()
        {
            float deltaTime = World.Time.DeltaTime;

            // Lookup for HealthComponent (read/write = false)
            var healthLookup = GetComponentLookup<HealthComponent>(isReadOnly: false);

            Entities
                .WithAll<LitharchTag, LitharchHealerComponent>()
                // We're writing through the lookup, so disable safety restriction for this job copy
                .WithNativeDisableContainerSafetyRestriction(healthLookup)
                .ForEach((ref LitharchHealerComponent healer, in PositionComponent position) =>
                {
                    if (healer.CurrentHealTarget == Entity.Null)
                        return;

                    if (!healthLookup.HasComponent(healer.CurrentHealTarget))
                        return;

                    var health = healthLookup[healer.CurrentHealTarget];

                    float healAmount = healer.HealsPerSecond * deltaTime;
                    health.CurrentHp = math.min(health.CurrentHp + healAmount, health.MaxHp);

                    healthLookup[healer.CurrentHealTarget] = health;
                })
                .Schedule();
        }
    }
}
