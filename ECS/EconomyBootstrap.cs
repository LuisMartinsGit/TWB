// EconomyBootstrap.cs (UPDATED VERSION)
// Replace your existing EconomyBootstrap.cs with this version
// Place in: Assets/Scripts/ECS/

using Unity.Entities;
using Unity.Mathematics;

public static class EconomyBootstrap
{
    /// <summary>
    /// Ensure one resource bank entity per participating faction with the desired starting amounts.
    /// Starting resources: supplies=400, iron=150, crystal=0, veilsteel=0, glow=0
    /// Starting population: current=0, max=0 (will increase as buildings are constructed)
    /// </summary>
    public static void EnsureFactionBanks(int totalPlayers)
    {
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null) return;

        var em = world.EntityManager;

        // For each faction index [0..totalPlayers-1], ensure a bank entity exists.
        for (int i = 0; i < totalPlayers; i++)
        {
            var fac = (Faction)i;

            // Check if a bank already exists for this faction
            var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<FactionTag>(),
                ComponentType.ReadOnly<FactionResources>()
            );
            using var banks = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            bool exists = false;
            for (int b = 0; b < banks.Length; b++)
            {
                var tag = em.GetComponentData<FactionTag>(banks[b]);
                if (tag.Value == fac) { exists = true; break; }
            }
            if (exists) continue;

            // Create faction resource bank with all resource tracking
            var bank = em.CreateEntity(
                typeof(FactionTag),
                typeof(FactionResources),
                typeof(ResourceTickState),
                typeof(FactionPopulation)  // NEW: Add population tracking
            );

            em.SetComponentData(bank, new FactionTag { Value = fac });
            
            // Initialize material resources
            em.SetComponentData(bank, new FactionResources
            {
                Supplies = 400,
                Iron = 150,
                Crystal = 0,
                Veilsteel = 0,
                Glow = 0
            });

            // NEW: Initialize population
            // Starts at 0/0 - will increase as Hall/Huts are built
            em.SetComponentData(bank, new FactionPopulation
            {
                Current = 0,  // No units yet
                Max = 0       // No population buildings yet
            });

            // Initialize resource tick tracking (for passive income)
            em.SetComponentData(bank, new ResourceTickState
            {
                LastWholeSecond = (int)math.floor(world.Time.ElapsedTime)
            });
        }
    }
}