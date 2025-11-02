// PopulationHelper.cs
// Utility functions for checking and managing population
// Place in: Assets/Scripts/ECS/

using Unity.Collections;
using Unity.Entities;

public static class PopulationHelper
{
    /// <summary>
    /// Get the population stats for a specific faction
    /// </summary>
    public static bool TryGetFactionPopulation(Faction faction, out int current, out int max)
    {
        current = 0;
        max = 0;
        
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null) return false;

        var em = world.EntityManager;
        var query = em.CreateEntityQuery(
            ComponentType.ReadOnly<FactionTag>(),
            ComponentType.ReadOnly<FactionPopulation>()
        );

        using var entities = query.ToEntityArray(Allocator.Temp);
        using var tags = query.ToComponentDataArray<FactionTag>(Allocator.Temp);
        using var populations = query.ToComponentDataArray<FactionPopulation>(Allocator.Temp);

        for (int i = 0; i < tags.Length; i++)
        {
            if (tags[i].Value == faction)
            {
                current = populations[i].Current;
                max = populations[i].Max;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Check if a faction has enough population capacity to create a unit
    /// </summary>
    public static bool HasPopulationCapacity(Faction faction, int requiredPopulation)
    {
        if (TryGetFactionPopulation(faction, out int current, out int max))
        {
            return (current + requiredPopulation) <= max;
        }
        return false;
    }

    /// <summary>
    /// Get the population cost for a unit type.
    /// Override this with tech tree lookup in the future.
    /// </summary>
    public static int GetUnitPopulationCost(string unitId)
    {
        return unitId switch
        {
            // Basic units - 1 population each
            "Builder" => 1,
            "Archer" => 1,
            "Swordsman" => 1,
            "Miner" => 1,
            
            // You can add more expensive units later
            // "Knight" => 2,
            // "Colossus" => 3,
            
            // Default for unknown units
            _ => 1
        };
    }

    /// <summary>
    /// Get the population provided by a building type.
    /// Override this with tech tree lookup in the future.
    /// </summary>
    public static int GetBuildingPopulationProvided(string buildingId)
    {
        return buildingId switch
        {
            "Hall" => 20,
            "Hut" => 10,
            _ => 0  // Most buildings don't provide population
        };
    }

    /// <summary>
    /// Check if a faction is at the absolute population cap (200)
    /// </summary>
    public static bool IsAtPopulationCap(Faction faction)
    {
        if (TryGetFactionPopulation(faction, out _, out int max))
        {
            return max >= FactionPopulation.AbsoluteMax;
        }
        return false;
    }

    /// <summary>
    /// Get a formatted string for population display in UI
    /// Example: "15/50" or "150/200 (MAX)"
    /// </summary>
    public static string GetPopulationDisplayString(Faction faction)
    {
        if (TryGetFactionPopulation(faction, out int current, out int max))
        {
            if (max >= FactionPopulation.AbsoluteMax)
            {
                return $"{current}/{max} (MAX)";
            }
            return $"{current}/{max}";
        }
        return "0/0";
    }
}