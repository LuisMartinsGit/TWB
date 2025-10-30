// File: Assets/Scripts/ECS/EconomyBootstrap.cs
using Unity.Entities;
using Unity.Mathematics;

public static class EconomyBootstrap
{
    /// <summary>
    /// Ensure one resource bank entity per participating faction with the desired starting amounts.
    /// supplies=400, iron=50, crystal=0, veilsteel=0, glow=0.
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

            var bank = em.CreateEntity(
                typeof(FactionTag),
                typeof(FactionResources),
                typeof(ResourceTickState)
            );

            em.SetComponentData(bank, new FactionTag { Value = fac });
            em.SetComponentData(bank, new FactionResources
            {
                Supplies = 400,
                Iron = 50,
                Crystal = 0,
                Veilsteel = 0,
                Glow = 0
            });

            // Initialize so the first add happens on the next whole second boundary
            em.SetComponentData(bank, new ResourceTickState
            {
                LastWholeSecond = (int)math.floor(world.Time.ElapsedTime)
            });
        }
    }
}
