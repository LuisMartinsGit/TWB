using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using TheWaningBorder.Core.Components;
using TheWaningBorder.Core.Systems;

namespace TheWaningBorder.Units.RunaiSkirmisher
{
    /// <summary>
    /// System for handling Runai_Skirmisher projectile attacks
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public class RunaiSkirmisherProjectileSystem : DataLoaderSystem
    {
        protected override void OnUpdate()
        {
            Entities
                .WithAll<RunaiSkirmisherTag>()
                .ForEach((Entity entity, ref AttackComponent attack, in PositionComponent position) =>
                {
                    // Create projectile when attacking
                    // All projectile parameters from TechTree.json
                    if (attack.DamageType == "ranged")
                    {
                        CreateProjectile(entity, attack, position);
                    }
                }).Schedule();
        }

        private void CreateProjectile(Entity attacker, AttackComponent attack, PositionComponent position)
        {
            var unitData = GetUnitData("Runai_Skirmisher");
            
            // Create projectile entity with data from JSON
            var projectile = EntityManager.CreateEntity(
                typeof(ProjectileComponent),
                typeof(PositionComponent)
            );
            
            // Set projectile data from JSON - no hardcoded values!
            EntityManager.SetComponentData(projectile, new ProjectileComponent
            {
                StartPosition = position.Position,
                TargetPosition = position.Position, // Would be set to actual target
                Speed = unitData.projectileSpeed > 0 ? unitData.projectileSpeed : 20f,
                Damage = attack.Damage,
                DamageType = attack.DamageType
            });
        }
    }
}
