using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace FactionColonies.util
{
    /// <summary>
    /// Manages cloning of Empire's template PawnKindDefs for each enabled race and tech level.
    /// Templates are defined in XML with race=Human and cloned at runtime with the target race set.
    /// </summary>
    static class PawnKindTemplateUtil
    {
        private static Dictionary<RaceTechKey, List<PawnKindDef>> cloneCache = new Dictionary<RaceTechKey, List<PawnKindDef>>();

        // Def-derived gear data per race, bucketed by tech level. Populated once per race, never cleared.
        private static Dictionary<ThingDef, PerRaceGearIndex> raceGearIndex = new Dictionary<ThingDef, PerRaceGearIndex>();

        // Apparel tags that at least one loaded apparel ThingDef actually carries. A pawnkind can declare an
        // apparelTag that no apparel uses (e.g. a race's vestigial "pilgrim" tag whose kinds equip via
        // apparelRequired instead). Harvesting such a dead tag makes PawnApparelGenerator's strict tag filter
        // reject every candidate -> naked pawns, so EnsureRaceGearIndex filters them out via this set.
        private static HashSet<string> apparelTagsInUse;

        private static HashSet<string> ApparelTagsInUse
        {
            get
            {
                if (apparelTagsInUse is null)
                {
                    apparelTagsInUse = new HashSet<string>();
                    foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
                    {
                        if (def.apparel?.tags is null) continue;
                        foreach (string tag in def.apparel.tags)
                            apparelTagsInUse.Add(tag);
                    }
                }
                return apparelTagsInUse;
            }
        }

        private struct RaceGearData
        {
            public List<string> apparelTags;
            public List<string> weaponTags;
            public FloatRange apparelMoney;
            public FloatRange weaponMoney;
        }

        private struct PerRaceGearIndex
        {
            public Dictionary<TechLevel, RaceGearData> tiers;
        }

        private struct RaceTechKey : IEquatable<RaceTechKey>
        {
            public ThingDef race;
            public TechLevel techLevel;

            public RaceTechKey(ThingDef race, TechLevel techLevel)
            {
                this.race = race;
                this.techLevel = techLevel;
            }

            public override int GetHashCode()
            {
                return Gen.HashCombineInt(race.GetHashCode(), (int)techLevel);
            }

            public override bool Equals(object obj)
            {
                if (!(obj is RaceTechKey other)) return false;
                return Equals(other);
            }

            public bool Equals(RaceTechKey other) => race == other.race && techLevel == other.techLevel;

            public static bool operator ==(RaceTechKey rtk1, RaceTechKey rtk2)
            {
                return rtk1.Equals(rtk2);
            }

            public static bool operator !=(RaceTechKey rtk1, RaceTechKey rtk2)
            {
                return !rtk1.Equals(rtk2);
            }
        }

        private static PawnKindDef[] Templates
        {
            get
            {
                return new PawnKindDef[]
                {
                    PColonyPawnKindDefOf.PColony_Fighter,
                    PColonyPawnKindDefOf.PColony_Elite,
                    PColonyPawnKindDefOf.PColony_Leader,
                    PColonyPawnKindDefOf.PColony_Trader,
                    PColonyPawnKindDefOf.PColony_Guard,
                    PColonyPawnKindDefOf.PColony_Villager
                };
            }
        }

        /// <summary>
        /// Gets or creates cloned template PawnKindDefs for the given race and tech level.
        /// Clones are cached and reused until <see cref="InvalidateCache"/> is called.
        /// </summary>
        public static List<PawnKindDef> GetOrCreateClonesForRace(ThingDef race, TechLevel techLevel)
        {
            RaceTechKey key = new RaceTechKey(race, techLevel);
            if (cloneCache.TryGetValue(key, out List<PawnKindDef> cached))
            {
                return cached;
            }

            List<PawnKindDef> clones = new List<PawnKindDef>();
            foreach (PawnKindDef template in Templates)
            {
                PawnKindDef clone = template.ShallowClone();
                clone.race = race;
                clone.defName = template.defName + "_" + race.defName;
                clone.xenotypeSet = null;
                clone.useFactionXenotypes = false;

                // Copy tag lists so we can mutate them without affecting the template
                if (template.weaponTags != null)
                    clone.weaponTags = new List<string>(template.weaponTags);
                if (template.apparelTags != null)
                    clone.apparelTags = new List<string>(template.apparelTags);

                ApplyTechLevelScaling(clone, techLevel);

                if (race != ThingDefOf.Human)
                {
                    ApplyRaceGearOverrides(clone, race, techLevel);
                }

                clones.Add(clone);
            }

            cloneCache[key] = clones;
            return clones;
        }

        /// <summary>
        /// Returns the Fighter template clone for a given race at the current Empire tech level.
        /// </summary>
        public static PawnKindDef GetFighterForRace(ThingDef race)
        {
            TechLevel techLevel = FindFC.FactionComp?.techLevel ?? TechLevel.Industrial;
            List<PawnKindDef> clones = GetOrCreateClonesForRace(race, techLevel);
            // Fighter is the first template in the array
            return clones.Count > 0 ? clones[0] : PColonyPawnKindDefOf.PColony_Fighter;
        }

        /// <summary>
        /// Returns the Villager template clone for a given race at the current Empire tech level.
        /// </summary>
        public static PawnKindDef GetVillagerForRace(ThingDef race)
        {
            TechLevel techLevel = FindFC.FactionComp?.techLevel ?? TechLevel.Industrial;
            List<PawnKindDef> clones = GetOrCreateClonesForRace(race, techLevel);
            // Villager is the last template in the array (index 5)
            return clones.Count > 5 ? clones[5] : PColonyPawnKindDefOf.PColony_Villager;
        }

        /// <summary>
        /// Clears the clone cache. Must be called when race weights or tech level change.
        /// </summary>
        public static void InvalidateCache()
        {
            cloneCache.Clear();
            // raceGearIndex is not cleared; it's derived from defs which don't change at runtime.
            //   Which SHOULDN'T change, anyways. I know there's a debug action to hot-reload defs,
            //   but if you use that, then the cache errors on you, buddy.
        }

        /// <summary>
        /// Adjusts weapon/apparel money and tags on a cloned PawnKindDef based on tech level.
        /// Templates are defined at Industrial level; this scales relative to that baseline.
        /// </summary>
        private static void ApplyTechLevelScaling(PawnKindDef clone, TechLevel techLevel)
        {
            switch (techLevel)
            {
                case TechLevel.Neolithic:
                    ScaleMoneyRanges(clone, 0.3f, 0.4f);
                    ReplaceWeaponTags(clone, new List<string> { "NeolithicMeleeBasic", "NeolithicRangedBasic" });
                    ReplaceApparelTags(clone, new List<string> { "Neolithic" });
                    break;

                case TechLevel.Medieval:
                    ScaleMoneyRanges(clone, 0.6f, 0.6f);
                    ReplaceWeaponTags(clone, new List<string> { "MedievalMeleeBasic", "MedievalMeleeDecent" });
                    ReplaceApparelTags(clone, new List<string> { "IndustrialBasic" });
                    break;

                case TechLevel.Industrial:
                    // Base level — no changes needed
                    break;

                case TechLevel.Spacer:
                    ScaleMoneyRanges(clone, 1.5f, 1.4f);
                    AddTagIfAbsent(clone.weaponTags, "SpacerGun");
                    AddTagIfAbsent(clone.apparelTags, "SpacerMilitary");
                    break;

                case TechLevel.Ultra:
                case TechLevel.Archotech:
                    ScaleMoneyRanges(clone, 2.0f, 1.8f);
                    AddTagIfAbsent(clone.weaponTags, "SpacerGun");
                    AddTagIfAbsent(clone.weaponTags, "UltratechMelee");
                    AddTagIfAbsent(clone.apparelTags, "SpacerMilitary");
                    break;
            }
        }

        private static void ScaleMoneyRanges(PawnKindDef clone, float weaponMult, float apparelMult)
        {
            clone.weaponMoney = new FloatRange(clone.weaponMoney.min * weaponMult, clone.weaponMoney.max * weaponMult);
            clone.apparelMoney = new FloatRange(clone.apparelMoney.min * apparelMult, clone.apparelMoney.max * apparelMult);
        }

        private static void ReplaceWeaponTags(PawnKindDef clone, List<string> newTags)
        {
            if (clone.weaponTags == null)
                clone.weaponTags = new List<string>();
            else
                clone.weaponTags.Clear();
            clone.weaponTags.AddRange(newTags);
        }

        private static void ReplaceApparelTags(PawnKindDef clone, List<string> newTags)
        {
            if (clone.apparelTags == null)
                clone.apparelTags = new List<string>();
            else
                clone.apparelTags.Clear();
            clone.apparelTags.AddRange(newTags);
        }

        private static void AddTagIfAbsent(List<string> tags, string tag)
        {
            if (tags != null && !tags.Contains(tag))
            {
                tags.Add(tag);
            }
        }

        /// <summary>
        /// For non-Human HAR races, replaces the clone's apparel/weapon tags with tags harvested
        /// from the race's own PawnKindDefs, and floor-clamps budgets to ensure the race's gear is affordable.
        /// </summary>
        private static void ApplyRaceGearOverrides(PawnKindDef clone, ThingDef race, TechLevel techLevel)
        {
            RaceGearData gearData = GetRaceGearData(race, techLevel);

            // Replace apparel tags with the race's own tags so PawnApparelGenerator can find matching apparel.
            // If no tags were found even after fallback, set to null so the generator skips tag filtering
            // entirely and lets HAR's race restrictions handle it.
            if (gearData.apparelTags != null && gearData.apparelTags.Count > 0)
            {
                clone.apparelTags = new List<string>(gearData.apparelTags);
            }
            else
            {
                clone.apparelTags = null;
            }

            // Replace weapon tags only if the race defines its own; otherwise keep Empire's tags
            if (gearData.weaponTags != null && gearData.weaponTags.Count > 0)
            {
                clone.weaponTags = new List<string>(gearData.weaponTags);
            }

            // Floor-clamp budgets: take the max of Empire's (possibly tech-scaled) budget and the race's average.
            // This ensures alien gear (often more expensive) is affordable while preserving tech-level scaling boosts.
            clone.apparelMoney = new FloatRange(
                Math.Max(clone.apparelMoney.min, gearData.apparelMoney.min),
                Math.Max(clone.apparelMoney.max, gearData.apparelMoney.max));
            clone.weaponMoney = new FloatRange(
                Math.Max(clone.weaponMoney.min, gearData.weaponMoney.min),
                Math.Max(clone.weaponMoney.max, gearData.weaponMoney.max));
        }

        /// <summary>
        /// Returns gear data for a race at a single tech tier matching <paramref name="techLevel"/>,
        /// plus any factionless PKDs (Undefined bucket). Falls back to the closest available tier
        /// if no exact match exists.
        /// </summary>
        private static RaceGearData GetRaceGearData(ThingDef race, TechLevel techLevel)
        {
            PerRaceGearIndex index = EnsureRaceGearIndex(race);
            if (index.tiers.Count == 0)
                return default;

            // Pick a single tier: exact match, or closest available
            TechLevel chosen = techLevel;
            if (!index.tiers.ContainsKey(chosen) || chosen == TechLevel.Undefined)
            {
                chosen = GetClosestRaceTechLevel(index, techLevel);
            }

            RaceGearData result = chosen != TechLevel.Undefined && index.tiers.TryGetValue(chosen, out RaceGearData tier)
                ? tier
                : default;

            // Always fold in the Undefined bucket (factionless/generic PKDs)
            if (index.tiers.TryGetValue(TechLevel.Undefined, out RaceGearData undefined))
            {
                result = MergeGearData(result, undefined);
            }

            // If the tech-appropriate tier has no usable apparel tags (e.g. a low-tech tier whose only
            // tags were dead-filtered), borrow apparel tags from the NEAREST tier that has some. This keeps
            // generation restricted to the race's OWN apparel instead of falling back to any vanilla apparel
            // (a HAR race wearing vanilla clothes). The borrowed tier's apparel budget is folded into the
            // floor so its apparel is affordable; borrowing the *nearest* tier (not a union) keeps a low-tech
            // Empire on the closest civilian tier (e.g. Medieval RK_Worker) rather than pulling higher-tier gear.
            if (result.apparelTags is null || result.apparelTags.Count == 0)
            {
                RaceGearData borrowed = NearestTierWithApparelTags(index, techLevel);
                if (borrowed.apparelTags != null && borrowed.apparelTags.Count > 0)
                {
                    result.apparelTags = borrowed.apparelTags;
                    result.apparelMoney = new FloatRange(
                        Math.Max(result.apparelMoney.min, borrowed.apparelMoney.min),
                        Math.Max(result.apparelMoney.max, borrowed.apparelMoney.max));
                }
            }

            return result;
        }

        /// <summary>
        /// Returns the gear data of the nearest tier (by tech-level distance to <paramref name="target"/>)
        /// whose apparel tags are non-empty, preferring a tier at or below the target on ties. Used as a
        /// fallback when the tech-appropriate tier itself has no usable apparel tags, so generation still
        /// restricts to the race's own apparel. Returns default when no tier has usable apparel tags.
        /// </summary>
        private static RaceGearData NearestTierWithApparelTags(PerRaceGearIndex index, TechLevel target)
        {
            RaceGearData best = default;
            int bestDist = int.MaxValue;
            bool bestAtOrBelow = false;
            foreach (KeyValuePair<TechLevel, RaceGearData> kvp in index.tiers)
            {
                if (kvp.Key == TechLevel.Undefined) continue;
                if (kvp.Value.apparelTags is null || kvp.Value.apparelTags.Count == 0) continue;

                int dist = Math.Abs((int)kvp.Key - (int)target);
                bool atOrBelow = kvp.Key <= target;
                if (dist < bestDist || (dist == bestDist && atOrBelow && !bestAtOrBelow))
                {
                    bestDist = dist;
                    bestAtOrBelow = atOrBelow;
                    best = kvp.Value;
                }
            }
            return best;
        }

        /// <summary>
        /// Combines two <see cref="RaceGearData"/>: unions tags, averages budgets.
        /// </summary>
        private static RaceGearData MergeGearData(RaceGearData a, RaceGearData b)
        {
            List<string> apparelTags = null;
            if (a.apparelTags != null || b.apparelTags != null)
            {
                HashSet<string> set = new HashSet<string>();
                if (a.apparelTags != null) set.AddRange(a.apparelTags);
                if (b.apparelTags != null) set.AddRange(b.apparelTags);
                apparelTags = set.ToList();
            }

            List<string> weaponTags = null;
            if (a.weaponTags != null || b.weaponTags != null)
            {
                HashSet<string> set = new HashSet<string>();
                if (a.weaponTags != null) set.AddRange(a.weaponTags);
                if (b.weaponTags != null) set.AddRange(b.weaponTags);
                weaponTags = set.ToList();
            }

            int count = 0;
            float amMin = 0f, amMax = 0f, wmMin = 0f, wmMax = 0f;
            if (a.apparelMoney.max > 0 || a.weaponMoney.max > 0)
            {
                amMin += a.apparelMoney.min; amMax += a.apparelMoney.max;
                wmMin += a.weaponMoney.min; wmMax += a.weaponMoney.max;
                count++;
            }
            if (b.apparelMoney.max > 0 || b.weaponMoney.max > 0)
            {
                amMin += b.apparelMoney.min; amMax += b.apparelMoney.max;
                wmMin += b.weaponMoney.min; wmMax += b.weaponMoney.max;
                count++;
            }

            return new RaceGearData
            {
                apparelTags = apparelTags,
                weaponTags = weaponTags,
                apparelMoney = count > 0 ? new FloatRange(amMin / count, amMax / count) : new FloatRange(0, 0),
                weaponMoney = count > 0 ? new FloatRange(wmMin / count, wmMax / count) : new FloatRange(0, 0)
            };
        }

        /// <summary>
        /// Returns the closest tech level to <paramref name="target"/> among the index's tiers.
        /// Prefers the highest tier &lt;= target; if none exist, returns the lowest above target.
        /// Skips the <see cref="TechLevel.Undefined"/> bucket.
        /// </summary>
        private static TechLevel GetClosestRaceTechLevel(PerRaceGearIndex index, TechLevel target)
        {
            TechLevel bestBelow = TechLevel.Undefined;
            TechLevel bestAbove = TechLevel.Undefined;
            foreach (TechLevel tier in index.tiers.Keys)
            {
                if (tier == TechLevel.Undefined) continue;
                if (tier <= target)
                {
                    if (bestBelow == TechLevel.Undefined || tier > bestBelow)
                        bestBelow = tier;
                }
                else
                {
                    if (bestAbove == TechLevel.Undefined || tier < bestAbove)
                        bestAbove = tier;
                }
            }
            return bestBelow != TechLevel.Undefined ? bestBelow : bestAbove;
        }

        /// <summary>
        /// Ensures the gear index for <paramref name="race"/> is populated. Scans all PawnKindDefs
        /// once per race and buckets gear data by <see cref="PawnKindDef.defaultFactionDef"/> tech level.
        /// PKDs with no defaultFactionDef are bucketed under <see cref="TechLevel.Undefined"/>.
        /// </summary>
        private static PerRaceGearIndex EnsureRaceGearIndex(ThingDef race)
        {
            if (raceGearIndex.TryGetValue(race, out PerRaceGearIndex existing))
                return existing;

            // Group PKDs by their faction's tech level
            Dictionary<TechLevel, List<PawnKindDef>> groups = new Dictionary<TechLevel, List<PawnKindDef>>();
            foreach (PawnKindDef def in FactionCache.AllPawnKindDefs)
            {
                if (def.race != race) continue;
                if (def.defName is null) continue;
                if (def.defName.StartsWith("PColony_")) continue;

                TechLevel tier = def.defaultFactionDef?.techLevel ?? TechLevel.Undefined;
                if (!groups.TryGetValue(tier, out List<PawnKindDef> list))
                {
                    list = new List<PawnKindDef>();
                    groups[tier] = list;
                }
                list.Add(def);
            }

            // Build per-tier RaceGearData
            Dictionary<TechLevel, RaceGearData> tiers = new Dictionary<TechLevel, RaceGearData>();
            foreach (var kvp in groups)
            {
                HashSet<string> apparelTags = new HashSet<string>();
                HashSet<string> weaponTags = new HashSet<string>();
                float apparelMoneyMinSum = 0f, apparelMoneyMaxSum = 0f;
                float weaponMoneyMinSum = 0f, weaponMoneyMaxSum = 0f;
                int apparelBudgetCount = 0;
                int weaponBudgetCount = 0;

                foreach (PawnKindDef def in kvp.Value)
                {
                    if (def.apparelTags != null)
                    {
                        // Skip dead tags (no loaded apparel carries them) — harvesting one would make
                        // PawnApparelGenerator's tag filter reject every candidate and spawn the pawn naked.
                        foreach (string tag in def.apparelTags)
                            if (ApparelTagsInUse.Contains(tag))
                                apparelTags.Add(tag);
                    }
                    if (def.weaponTags != null)
                    {
                        weaponTags.AddRange(def.weaponTags);
                    }
                    if (def.apparelMoney.max > 0)
                    {
                        apparelMoneyMinSum += def.apparelMoney.min;
                        apparelMoneyMaxSum += def.apparelMoney.max;
                        apparelBudgetCount++;
                    }
                    if (def.weaponMoney.max > 0)
                    {
                        weaponMoneyMinSum += def.weaponMoney.min;
                        weaponMoneyMaxSum += def.weaponMoney.max;
                        weaponBudgetCount++;
                    }
                }

                tiers[kvp.Key] = new RaceGearData
                {
                    apparelTags = apparelTags.Count > 0 ? apparelTags.ToList() : null,
                    weaponTags = weaponTags.Count > 0 ? weaponTags.ToList() : null,
                    apparelMoney = apparelBudgetCount > 0
                        ? new FloatRange(apparelMoneyMinSum / apparelBudgetCount, apparelMoneyMaxSum / apparelBudgetCount)
                        : new FloatRange(0, 0),
                    weaponMoney = weaponBudgetCount > 0
                        ? new FloatRange(weaponMoneyMinSum / weaponBudgetCount, weaponMoneyMaxSum / weaponBudgetCount)
                        : new FloatRange(0, 0)
                };
            }

            PerRaceGearIndex index = new PerRaceGearIndex { tiers = tiers };
            raceGearIndex[race] = index;
            return index;
        }

        /// <summary>
        /// If <paramref name="pawnKind"/> is one of Empire's base template PawnKindDefs
        /// (e.g. PColony_Fighter), reassigns it to the race-specific clone for
        /// <paramref name="race"/> at the current Empire tech level. Returns true if a
        /// reassignment was made. Used by <see cref="FactionColonies.MilUnitFC"/> to recover
        /// from BackCompatibility remapping clone-defName saves back to the base template.
        /// </summary>
        public static bool TryRestoreRaceSpecificClone(ref PawnKindDef pawnKind, ThingDef race)
        {
            if (pawnKind is null || race is null) return false;
            int templateIndex = GetTemplateIndex(pawnKind);
            if (templateIndex < 0) return false;

            TechLevel techLevel = FindFC.FactionComp != null
                ? FindFC.FactionComp.techLevel
                : TechLevel.Industrial;

            List<PawnKindDef> clones = GetOrCreateClonesForRace(race, techLevel);
            if (templateIndex >= clones.Count) return false;

            pawnKind = clones[templateIndex];
            return true;
        }

        /// <summary>
        /// After loading, pawn kindDefs were remapped to base templates by BackCompatibility.
        /// This restores the correct race-specific clone, preserving the pawn.def == kindDef.race
        /// invariant that PawnGenerator.IsValidCandidateToRedress and other systems rely on.
        /// Also handles stale clones after tech level changes.
        /// </summary>
        public static void FixupPawnKindDefs(FactionFC factionFc)
        {
            Faction empireFaction = FindFC.EmpireFaction;
            if (empireFaction == null) return;

            TechLevel techLevel = factionFc.techLevel;
            int fixedCount = 0;

            // Mercenaries (direct access — most important)
            if (factionFc.military?.mercenarySquads != null)
            {
                foreach (MercenarySquadFC squad in factionFc.military.mercenarySquads)
                {
                    if (squad?.mercenaries == null) continue;
                    foreach (Mercenary merc in squad.mercenaries)
                    {
                        if (FixupPawn(merc?.pawn, techLevel)) fixedCount++;
                    }
                }
            }

            // Faction leader
            if (FixupPawn(empireFaction.leader, techLevel)) fixedCount++;

            // World pawns
            if (Find.WorldPawns != null)
            {
                foreach (Pawn pawn in Find.WorldPawns.AllPawnsAliveOrDead)
                {
                    if (pawn != null && pawn.Faction == empireFaction)
                    {
                        if (FixupPawn(pawn, techLevel)) fixedCount++;
                    }
                }
            }

            // Map pawns
            if (Find.Maps != null)
            {
                foreach (Map map in Find.Maps)
                {
                    if (map?.mapPawns == null) continue;
                    foreach (Pawn pawn in map.mapPawns.AllPawns)
                    {
                        if (pawn != null && pawn.Faction == empireFaction)
                        {
                            if (FixupPawn(pawn, techLevel)) fixedCount++;
                        }
                    }
                }
            }

            if (fixedCount > 0)
                LogUtil.Message($"Fixed kindDef on {fixedCount} Empire pawns");
        }

        private static bool FixupPawn(Pawn pawn, TechLevel techLevel)
        {
            if (pawn?.kindDef == null || pawn.def == null) return false;
            if (!pawn.kindDef.defName.StartsWith("PColony_")) return false;

            int templateIndex = GetTemplateIndex(pawn.kindDef);
            if (templateIndex < 0) return false;

            List<PawnKindDef> clones = GetOrCreateClonesForRace(pawn.def, techLevel);
            if (templateIndex >= clones.Count) return false;

            PawnKindDef correctClone = clones[templateIndex];
            if (pawn.kindDef == correctClone) return false;

            pawn.kindDef = correctClone;
            return true;
        }

        private static int GetTemplateIndex(PawnKindDef kindDef)
        {
            PawnKindDef[] templates = Templates;
            for (int i = 0; i < templates.Length; i++)
            {
                if (kindDef == templates[i]) return i;
                if (kindDef.defName.StartsWith(templates[i].defName + "_")) return i;
            }
            return -1;
        }
    }
}
