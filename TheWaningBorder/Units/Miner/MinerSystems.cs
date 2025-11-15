using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using TheWaningBorder.Core.Components;
using TheWaningBorder.Core.Systems;

namespace TheWaningBorder.Units.Miner
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class MinerGatheringSystem : DataLoaderSystem
    {
        protected override void OnUpdate()
        {
            float deltaTime = World.Time.DeltaTime;

            Entities
                .WithAll<MinerTag, MinerGathererComponent>()
                .ForEach((ref MinerGathererComponent gatherer) =>
                {
                    // assuming ResourceType is a FixedString, not a C# string (see below)
                    if (gatherer.ResourceType.Length == 0)
                        return;

                    float gatherAmount = gatherer.GatheringSpeed * deltaTime;

                    int newCarry = gatherer.CurrentCarryAmount + (int)gatherAmount;
                    if (newCarry >= gatherer.CarryCapacity)
                    {
                        // here you'd normally "bank" the resources on some player/base,
                        // but inside this job we just reset the carried amount
                        newCarry = 0;
                    }

                    // clamp
                    gatherer.CurrentCarryAmount = math.min(newCarry, gatherer.CarryCapacity);
                })
                .Schedule();
        }
    }
}
