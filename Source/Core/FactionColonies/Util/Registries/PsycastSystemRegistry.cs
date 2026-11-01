using System.Collections.Generic;
using System.Linq;

namespace FactionColonies
{
    /// <summary>
    /// App-lifetime registry of <see cref="IPsycastSystemProvider"/>s for the unit designer's
    /// Psycasts tab. The base-game (<see cref="VanillaPsycastProvider"/>) is built in; external
    /// systems such as Vanilla Psycasts Expanded register a higher-priority provider from their
    /// compat assembly's <c>[StaticConstructorOnStartup]</c>.
    ///
    /// Unlike the per-game domain registries (see <see cref="EmpireRegistry"/>), this holds
    /// app-lifetime singletons and is therefore NOT facade-managed and NOT cleared on game dispose —
    /// the same lifetime model as <see cref="MilitaryWindowRegistry"/>.
    /// </summary>
    public static class PsycastSystemRegistry
    {
        private static readonly VanillaPsycastProvider _vanilla = new VanillaPsycastProvider();
        private static readonly List<IPsycastSystemProvider> _providers =
            new List<IPsycastSystemProvider> { _vanilla };

        public static void Register(IPsycastSystemProvider provider)
        {
            if (provider is object && !_providers.Contains(provider)) _providers.Add(provider);
        }

        public static void Unregister(IPsycastSystemProvider provider)
        {
            if (provider is object) _providers.Remove(provider);
        }

        public static IReadOnlyList<IPsycastSystemProvider> Providers => _providers;

        /// <summary>
        /// The highest-priority active provider, or null when no psycast system is available
        /// (e.g. no Royalty and no VPE). The Psycasts tab keys its UI off this.
        /// </summary>
        public static IPsycastSystemProvider Active =>
            _providers.Where(p => p.IsActive).OrderByDescending(p => p.Priority).FirstOrDefault();

        /// <summary>
        /// Resolves the provider that produced a saved psycast (by its persisted <c>systemKey</c>),
        /// regardless of which provider is currently <see cref="Active"/>. Returns null if the
        /// originating system is not loaded — the caller skips that entry.
        /// </summary>
        public static IPsycastSystemProvider ByKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            return _providers.FirstOrDefault(p => p.Key == key);
        }
    }
}
