using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Unity.Collections;
using TheWaningBorder.Core.Components;
using TheWaningBorder.Core.Systems;

namespace TheWaningBorder.Units.Builder
{
    /// <summary>
    /// System for handling Builder building abilities
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class BuilderBuildingSystem : DataLoaderSystem
    {
        protected override void OnUpdate()
        {
            float deltaTime = World.Time.DeltaTime;

            Entities
                .WithAll<BuilderTag, BuilderBuilderComponent>()
                .ForEach((ref BuilderBuilderComponent builder) =>
                {
                    // CurrentBuildingId is a FixedString64Bytes now
                    // Check if it's empty by using Length
                    if (builder.CurrentBuildingId.Length == 0)
                        return;

                    // Progress building construction using build speed from JSON
                    builder.BuildProgress += builder.BuildSpeed * deltaTime;

                    if (builder.BuildProgress >= 100f)
                    {
                        CompleteBuildingConstruction(builder.CurrentBuildingId);

                        // Clear the building id and reset progress
                        builder.CurrentBuildingId = default; // clears FixedString64Bytes
                        builder.BuildProgress = 0f;
                    }
                })
                .Schedule();
        }

        // static so the job does NOT capture 'this'
        private static void CompleteBuildingConstruction(FixedString64Bytes buildingId)
        {
            // Complete building construction based on TechTree.json data
            Debug.Log($"Building {buildingId} construction completed");
        }
    }
}
