using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using TheWaningBorder.Core.GameManager;

namespace TheWaningBorder.Resources.ResourceManager
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class ResourceManagerSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            
            // Update resource display
            Entities
                .ForEach((Entity entity, ref ResourcesComponent resources, in PlayerComponent player) =>
                {
                    if (player.IsHuman)
                    {
                        // This would typically update UI
                        // For now, just log periodically
                        if (UnityEngine.Random.Range(0f, 1f) < 0.01f) // Log occasionally
                        {
                            Debug.Log($"[Resources] Player {player.PlayerId}: Supplies={resources.Supplies}, Iron={resources.Iron}");
                        }
                    }
                })
                .WithoutBurst() // Required for Debug.Log
                .Run();
            
            // Check for resource overflow
            Entities
                .ForEach((Entity entity, ref ResourcesComponent resources) =>
                {
                    const int maxResources = 999999;
                    resources.Supplies = math.min(resources.Supplies, maxResources);
                    resources.Iron = math.min(resources.Iron, maxResources);
                    resources.Crystal = math.min(resources.Crystal, maxResources);
                    resources.Veilsteel = math.min(resources.Veilsteel, maxResources);
                    resources.Glow = math.min(resources.Glow, maxResources);
                })
                .Schedule();
        }
    }
}
