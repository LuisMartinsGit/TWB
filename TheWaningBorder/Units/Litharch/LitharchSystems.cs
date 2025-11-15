using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using TheWaningBorder.Core.Components;
using TheWaningBorder.Core.Systems;

namespace TheWaningBorder.Units.Litharch
{
    /// <summary>
    /// System for handling Litharch healing abilities
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public class LitharchHealingSystem : DataLoaderSystem
    {
        protected override void OnUpdate()
        {
            float deltaTime = Time.DeltaTime;
            
            Entities
                .WithAll<LitharchTag, LitharchHealerComponent>()
                .ForEach((Entity entity, ref LitharchHealerComponent healer, in PositionComponent position) =>
                {
                    // Find nearby wounded allies
                    var healAmount = healer.HealsPerSecond * deltaTime;
                    
                    // Apply healing to target based on JSON values
                    if (healer.CurrentHealTarget != Entity.Null)
                    {
                        ApplyHealing(healer.CurrentHealTarget, healAmount);
                    }
                }).Schedule();
        }

        private void ApplyHealing(Entity target, float amount)
        {
            if (EntityManager.HasComponent<HealthComponent>(target))
            {
                var health = EntityManager.GetComponentData<HealthComponent>(target);
                health.CurrentHp = Mathf.Min(health.CurrentHp + amount, health.MaxHp);
                EntityManager.SetComponentData(target, health);
            }
        }
    }
}
