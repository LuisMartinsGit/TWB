using System.Collections.Generic;

namespace TheWaningBorder.Economy
{
    public static class BuildCosts
    {
        private static readonly Dictionary<string, Cost> _byId = new()
        {
            { "Hut",            Cost.Of(supplies: 80) },
            { "GatherersHut",   Cost.Of(supplies: 120, iron: 10) },
            { "Barracks",       Cost.Of(supplies: 220, iron: 40) },
            { "Shrine",         Cost.Of(supplies: 180, crystal: 20) },
            { "Vault",          Cost.Of(supplies: 260, iron: 60, crystal: 40) },
            { "Keep",           Cost.Of(supplies: 400, iron: 100, veilsteel: 20) },
        };

        public static bool TryGet(string id, out Cost cost) => _byId.TryGetValue(id, out cost);
    }
}
