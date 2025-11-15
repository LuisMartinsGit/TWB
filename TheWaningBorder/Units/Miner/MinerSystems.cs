using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using TheWaningBorder.Core.Components;
using TheWaningBorder.Core.Systems;

namespace TheWaningBorder.Units.Miner
{
    /// <summary>
    /// System for handling Miner resource gathering
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public class MinerGatheringSystem : DataLoaderSystem
    {
        protected override void OnUpdate()
        {
            float deltaTime = Time.DeltaTime;
            
            Entities
                .WithAll<MinerTag, MinerGathererComponent>()
                .ForEach((Entity entity, ref MinerGathererComponent gatherer) =>
                {
                    if (!string.IsNullOrEmpty(gatherer.ResourceType))
                    {
                        // Gather resources using speed from JSON
                        var gatherAmount = gatherer.GatheringSpeed * deltaTime;
                        gatherer.CurrentCarryAmount = Mathf.Min(
                            gatherer.CurrentCarryAmount + (int)gatherAmount,
                            gatherer.CarryCapacity
                        );
                        
                        if (gatherer.CurrentCarryAmount >= gatherer.CarryCapacity)
                        {
                            // Return to drop-off point
                            ReturnResources(gatherer);
                        }
                    }
                }).Schedule();
        }

        private void ReturnResources(MinerGathererComponent gatherer)
        {
            // Handle resource return based on JSON data
            Debug.Log($"Returning {gatherer.CurrentCarryAmount} {gatherer.ResourceType}");
            gatherer.CurrentCarryAmount = 0;
        }
    }
}
