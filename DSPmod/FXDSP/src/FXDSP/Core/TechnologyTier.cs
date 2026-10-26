using System;
using System.Collections.Generic;

namespace FXDSP.Core
{
    public sealed class TechnologyRequirements
    {
        public int[] Items = Array.Empty<int>();
        public int[] Prerequisites = Array.Empty<int>();
    }

    // Ingredient production chains do not determine a building's research tier.
    public sealed class TechnologyTier
    {
        private readonly Func<int, TechnologyRequirements> _lookup;
        private readonly Dictionary<int, int> _cache = new Dictionary<int, int>();
        public TechnologyTier(Func<int, TechnologyRequirements> lookup) { _lookup = lookup ?? throw new ArgumentNullException(nameof(lookup)); }
        public int Resolve(int technologyId)
        {
            if (technologyId <= 0) return 1;
            int cached;
            if (_cache.TryGetValue(technologyId, out cached)) return cached;
            var visited = new HashSet<int>();
            var pending = new Stack<int>();
            pending.Push(technologyId);
            int tier = 1;
            while (pending.Count > 0)
            {
                int id = pending.Pop();
                if (id <= 0 || !visited.Add(id)) continue;
                var tech = _lookup(id);
                if (tech == null) continue;
                foreach (int item in tech.Items ?? Array.Empty<int>())
                    if (item >= 6001 && item <= 6006) tier = Math.Max(tier, Math.Min(5, item - 6000));
                foreach (int prerequisite in tech.Prerequisites ?? Array.Empty<int>()) pending.Push(prerequisite);
            }
            _cache[technologyId] = tier;
            return tier;
        }
    }
}
