using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using TheWaningBorder.Core.Components;
using TheWaningBorder.Core.Systems;

namespace TheWaningBorder.Units.Builder
{
    /// <summary>
    /// System for handling Builder building abilities
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public class BuilderBuildingSystem : DataLoaderSystem
    {
        protected override void OnUpdate()
        {
            float deltaTime = Time.DeltaTime;
            
            Entities
                .WithAll<BuilderTag, BuilderBuilderComponent>()
                .ForEach((Entity entity, ref BuilderBuilderComponent builder) =>
                {
                    if (!string.IsNullOrEmpty(builder.CurrentBuildingId))
                    {
                        // Progress building construction using build speed from JSON
                        builder.BuildProgress += builder.BuildSpeed * deltaTime;
                        
                        if (builder.BuildProgress >= 100f)
                        {
                            CompleteBuildingConstruction(builder.CurrentBuildingId);
                            builder.CurrentBuildingId = "";
                            builder.BuildProgress = 0f;
                        }
                    }
                }).Schedule();
        }

        private void CompleteBuildingConstruction(string buildingId)
        {
            // Complete building construction based on TechTree.json data
            Debug.Log($"Building {buildingId} construction completed");
        }
    }
}
