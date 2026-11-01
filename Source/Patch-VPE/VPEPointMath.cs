using System.Collections.Generic;
using VanillaPsycastsExpanded;
using Verse;
using AbilityDef = VEF.Abilities.AbilityDef;

namespace FactionColonies.VPE
{
    /// <summary>
    /// Authoritative VPE point math for the unit designer. We deliberately do NOT rely on VPE's mutable
    /// <c>Hediff_PsycastAbilities.points</c> counter — it desyncs across our generate/replay cycles
    /// (the source of the "phantom points" bug). Instead the budget is derived purely from the unit's
    /// psylink level, and the spent count purely from the stored selection list.
    ///
    /// A psylink level L grants <c>L + 1</c> points (VPE seeds 2 at level 1, then +1 per level), matching
    /// a naturally-levelled VPE caster. Costs: unlock path = 1, learn ability = 1, unlock meditation
    /// focus = 1, stat point = 1. Paths are not stored on the unit (re-derived from each ability's
    /// <c>Psycast().path</c>) but still cost a point — the first ability of a path pays for it.
    /// </summary>
    public static class VPEPointMath
    {
        public static int Budget(int psylinkLevel) => psylinkLevel <= 0 ? 0 : psylinkLevel + 1;

        public static int SpentPoints(IEnumerable<SavedPsycast> selections)
        {
            if (selections is null) return 0;
            int spent = 0;
            HashSet<PsycasterPathDef> paths = new HashSet<PsycasterPathDef>();
            foreach (SavedPsycast s in selections)
            {
                if (s.systemKey != VPEPsycastProvider.ProviderKey) continue;

                if (s.kind == VPEPsycastProvider.KindStatUpgrade)
                {
                    spent += s.count > 0 ? s.count : 1;
                }
                else if (s.kind == VPEPsycastProvider.KindMeditationFocus)
                {
                    spent += 1;
                }
                else
                {
                    PsycasterPathDef path = PathOf(s.psycastDef);
                    if (path != null && paths.Add(path)) spent += 1; // first ability of a path pays the path
                    spent += 1; // the ability itself
                }
            }
            return spent;
        }

        /// <summary>
        /// Walks <paramref name="selections"/> in order and keeps the longest prefix that fits the point
        /// budget for <paramref name="psylinkLevel"/>; everything after the first entry that doesn't fit
        /// is dropped. A stat-upgrade entry may be partially kept (its <c>count</c> reduced) to consume
        /// the last remaining points.
        /// </summary>
        public static List<SavedPsycast> Trim(List<SavedPsycast> selections, int psylinkLevel)
        {
            List<SavedPsycast> result = new List<SavedPsycast>();
            if (selections is null) return result;

            int budget = Budget(psylinkLevel);
            int spent = 0;
            HashSet<PsycasterPathDef> paths = new HashSet<PsycasterPathDef>();

            foreach (SavedPsycast s in selections)
            {
                if (s.systemKey != VPEPsycastProvider.ProviderKey)
                {
                    result.Add(s); // foreign entries (none in practice) pass through untouched
                    continue;
                }

                if (s.kind == VPEPsycastProvider.KindStatUpgrade)
                {
                    int want = s.count > 0 ? s.count : 1;
                    int room = budget - spent;
                    if (room <= 0) break;
                    int take = want <= room ? want : room;
                    SavedPsycast kept = s;
                    kept.count = take;
                    result.Add(kept);
                    spent += take;
                    if (take < want) break; // budget exhausted mid-entry
                    continue;
                }

                int cost = 1;
                PsycasterPathDef pathToAdd = null;
                if (s.kind != VPEPsycastProvider.KindMeditationFocus)
                {
                    PsycasterPathDef path = PathOf(s.psycastDef);
                    if (path != null && !paths.Contains(path)) { cost += 1; pathToAdd = path; }
                }

                if (spent + cost > budget) break;
                spent += cost;
                if (pathToAdd != null) paths.Add(pathToAdd);
                result.Add(s);
            }

            return result;
        }

        private static PsycasterPathDef PathOf(string psycastDefName)
        {
            if (string.IsNullOrEmpty(psycastDefName)) return null;
            AbilityDef def = DefDatabase<AbilityDef>.GetNamedSilentFail(psycastDefName);
            if (def is null) return null;
            AbilityExtension_Psycast psycast = def.Psycast();
            return psycast != null ? psycast.path : null;
        }
    }
}
