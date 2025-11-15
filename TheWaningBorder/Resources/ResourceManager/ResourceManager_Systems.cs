using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using TheWaningBorder.Core.GameManager;

namespace TheWaningBorder.Resources
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class ResourceManagerSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            // Update resource generation from buildings
            float deltaTime = SystemAPI.Time.DeltaTime;
            
            Entities
                .ForEach((Entity entity, ref ResourcesComponent resources, in PlayerComponent player) =>
                {
                    // Passive resource generation could go here
                    // For now, resources are only gained through mining and other explicit actions
                }).Schedule();
        }
        
        public static bool CanAfford(EntityManager entityManager, Entity playerEntity, 
                                     int supplies, int iron, int crystal = 0, int veilsteel = 0, int glow = 0)
        {
            if (!entityManager.HasComponent<ResourcesComponent>(playerEntity))
                return false;
            
            var resources = entityManager.GetComponentData<ResourcesComponent>(playerEntity);
            
            return resources.Supplies >= supplies &&
                   resources.Iron >= iron &&
                   resources.Crystal >= crystal &&
                   resources.Veilsteel >= veilsteel &&
                   resources.Glow >= glow;
        }
        
        public static bool SpendResources(EntityManager entityManager, Entity playerEntity, 
                                          int supplies, int iron, int crystal = 0, int veilsteel = 0, int glow = 0)
        {
            if (!CanAfford(entityManager, playerEntity, supplies, iron, crystal, veilsteel, glow))
                return false;
            
            var resources = entityManager.GetComponentData<ResourcesComponent>(playerEntity);
            resources.Supplies -= supplies;
            resources.Iron -= iron;
            resources.Crystal -= crystal;
            resources.Veilsteel -= veilsteel;
            resources.Glow -= glow;
            entityManager.SetComponentData(playerEntity, resources);
            
            return true;
        }
        
        public static void AddResources(EntityManager entityManager, Entity playerEntity, 
                                        int supplies = 0, int iron = 0, int crystal = 0, int veilsteel = 0, int glow = 0)
        {
            if (!entityManager.HasComponent<ResourcesComponent>(playerEntity))
                return;
            
            var resources = entityManager.GetComponentData<ResourcesComponent>(playerEntity);
            resources.Supplies += supplies;
            resources.Iron += iron;
            resources.Crystal += crystal;
            resources.Veilsteel += veilsteel;
            resources.Glow += glow;
            entityManager.SetComponentData(playerEntity, resources);
        }
    }
}