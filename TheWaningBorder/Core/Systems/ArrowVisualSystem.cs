using System;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using TheWaningBorder.Core.Components;

namespace TheWaningBorder.Core.Systems
{
    /// <summary>
    /// System for managing arrow/projectile visuals
    /// All projectile parameters must be loaded from TechTree.json
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class ArrowVisualSystem : SystemBase
    {
        private GameObject arrowPrefab;
        private EntityQuery projectileQuery;

        protected override void OnCreate()
        {
            projectileQuery = GetEntityQuery(
                ComponentType.ReadOnly<ProjectileComponent>(),
                ComponentType.ReadOnly<PositionComponent>()
            );
        }

        protected override void OnUpdate()
        {
            // Ensure arrow prefab is loaded
            if (arrowPrefab == null)
            {
                LoadArrowPrefab();
                if (arrowPrefab == null)
                {
                    Debug.LogError("CRITICAL ERROR: Arrow prefab not found! Projectiles cannot be rendered without visual asset!");
                    return;
                }
            }

            // Update visual representations of projectiles
            Entities
                .WithoutBurst()
                .ForEach((Entity entity, in ProjectileComponent projectile, in PositionComponent position) =>
                {
                    UpdateArrowVisual(entity, projectile, position);
                }).Run();
        }

        private void LoadArrowPrefab()
        {
            var prefabPath = "Prefabs/Projectiles/Arrow";

            arrowPrefab = UnityEngine.Resources.Load<GameObject>(prefabPath);

            if (arrowPrefab == null)
            {
                Debug.LogWarning($"Arrow prefab not found at path: {prefabPath}. Using default.");
                arrowPrefab = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                arrowPrefab.transform.localScale = new Vector3(0.1f, 0.5f, 0.1f);
                arrowPrefab.SetActive(false);
            }
        }

        private void UpdateArrowVisual(Entity entity, ProjectileComponent projectile, PositionComponent position)
        {
            // Get or create visual GameObject for this arrow entity
            GameObject arrowVisual = GetOrCreateArrowVisual(entity);
            
            if (arrowVisual == null)
            {
                Debug.LogError($"Failed to create visual for projectile entity {entity}");
                return;
            }

            // Update position
            arrowVisual.transform.position = position.Position;

            // Calculate rotation to face target
            float3 direction = projectile.TargetPosition - position.Position;
            if (math.lengthsq(direction) > 0.001f)
            {
                quaternion targetRotation = quaternion.LookRotationSafe(direction, math.up());
                arrowVisual.transform.rotation = targetRotation;
            }

            // Apply any visual effects based on projectile type from JSON
            ApplyProjectileEffects(arrowVisual, projectile);
        }

        private GameObject GetOrCreateArrowVisual(Entity entity)
        {
            // This would typically use a pooling system
            // For now, create instance (should be pooled in production)
            if (!EntityManager.HasComponent<ArrowVisualData>(entity))
            {
                var visual = GameObject.Instantiate(arrowPrefab);
                visual.SetActive(true);
                visual.name = $"Arrow_{entity.Index}_{entity.Version}";
                
                // Add managed component to track visual
                EntityManager.AddComponentObject(entity, new ArrowVisualData { Visual = visual });
                return visual;
            }
            else
            {
                var managedVisual = EntityManager.GetComponentObject<ArrowVisualData>(entity);
                return managedVisual.Visual;
            }
        }

        private void ApplyProjectileEffects(GameObject visual, ProjectileComponent projectile)
        {
            // Apply effects based on damage type from JSON
            var particleSystem = visual.GetComponent<ParticleSystem>();
            if (particleSystem != null)
            {
                // Check damage type and apply appropriate effects
                // All parameters should come from TechTree.json
            }
        }

        protected override void OnDestroy()
        {
            // Clean up any remaining visuals
            Entities
                .WithoutBurst()
                .WithAll<ArrowVisualData>()
                .ForEach((Entity entity, ArrowVisualData visual) =>
                {
                    if (visual.Visual != null)
                    {
                        GameObject.Destroy(visual.Visual);
                    }
                }).Run();

            base.OnDestroy();
        }
    }

    // Managed component to track arrow visuals
    public class ArrowVisualData : IComponentData
    {
        public GameObject Visual;
    }
}
