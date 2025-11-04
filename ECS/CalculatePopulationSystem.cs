// CalculatePopulationSystem.cs
// Place in: Assets/Scripts/ECS/Systems/

using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Recalculates max and current population for each faction every frame.
/// 
/// Max Population = Sum of all completed PopulationProvider buildings (capped at 200)
/// Current Population = Sum of all living units with PopulationCost
/// 
/// This system runs after BuilderConstructionSystem so it picks up newly completed buildings.
/// </summary>
[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(BuilderConstructionSystem))]
public partial struct CalculatePopulationSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        // Require at least one faction with population tracking
        state.RequireForUpdate(SystemAPI.QueryBuilder()
            .WithAll<FactionTag, FactionPopulation>()
            .Build());
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // For each faction, calculate max and current population
        foreach (var (factionTag, pop) in 
            SystemAPI.Query<RefRO<FactionTag>, RefRW<FactionPopulation>>())
        {
            var faction = factionTag.ValueRO.Value;
            
            // Calculate max population from completed buildings
            int maxPop = 0;
            foreach (var (providerFactionTag, provider) in 
                SystemAPI.Query<RefRO<FactionTag>, RefRO<PopulationProvider>>()
                    .WithNone<UnderConstruction>())  // Only count completed buildings
            {
                if (providerFactionTag.ValueRO.Value == faction)
                {
                    maxPop += provider.ValueRO.Amount;
                }
            }
            
            // Cap at absolute maximum (200)
            maxPop = math.min(maxPop, FactionPopulation.AbsoluteMax);
            
            // Calculate current population from living units
            int currentPop = 0;
            foreach (var (unitFactionTag, cost) in 
                SystemAPI.Query<RefRO<FactionTag>, RefRO<PopulationCost>>())
            {
                if (unitFactionTag.ValueRO.Value == faction)
                {
                    currentPop += cost.ValueRO.Amount;
                }
            }
            
            // Update population values
            pop.ValueRW.Max = maxPop;
            pop.ValueRW.Current = currentPop;
        }
    }
}