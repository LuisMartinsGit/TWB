using Unity.Entities;

namespace TheWaningBorder.Economy
{
    public struct Cost
    {
        public int Supplies, Iron, Crystal, Veilsteel, Glow;

        public static Cost Of(int supplies=0, int iron=0, int crystal=0, int veilsteel=0, int glow=0)
            => new Cost { Supplies = supplies, Iron = iron, Crystal = crystal, Veilsteel = veilsteel, Glow = glow };

        public bool IsZero => Supplies==0 && Iron==0 && Crystal==0 && Veilsteel==0 && Glow==0;
    }

    public static class FactionEconomy
    {
        public static bool TryGetBank(EntityManager em, Faction fac, out Entity bank)
        {
            var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<FactionTag>(),
                ComponentType.ReadWrite<FactionResources>()
            );

            using var ents = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            using var tags = query.ToComponentDataArray<FactionTag>(Unity.Collections.Allocator.Temp);

            for (int i = 0; i < ents.Length; i++)
            {
                if (tags[i].Value == fac) { bank = ents[i]; return true; }
            }
            bank = Entity.Null;
            return false;
        }

        public static bool CanAfford(EntityManager em, Faction fac, in Cost c)
        {
            if (c.IsZero) return true;
            if (!TryGetBank(em, fac, out var bank)) return false;

            var r = em.GetComponentData<FactionResources>(bank);
            return r.Supplies >= c.Supplies
                && r.Iron >= c.Iron
                && r.Crystal >= c.Crystal
                && r.Veilsteel >= c.Veilsteel
                && r.Glow >= c.Glow;
        }

        public static bool Spend(EntityManager em, Faction fac, in Cost c)
        {
            if (c.IsZero) return true;
            if (!TryGetBank(em, fac, out var bank)) return false;

            var r = em.GetComponentData<FactionResources>(bank);
            if (r.Supplies < c.Supplies || r.Iron < c.Iron || r.Crystal < c.Crystal ||
                r.Veilsteel < c.Veilsteel || r.Glow < c.Glow)
                return false;

            r.Supplies -= c.Supplies;
            r.Iron -= c.Iron;
            r.Crystal -= c.Crystal;
            r.Veilsteel -= c.Veilsteel;
            r.Glow -= c.Glow;

            em.SetComponentData(bank, r);
            return true;
        }
    }
}
