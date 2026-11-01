using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace FactionColonies.util
{
    /*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*
     * Aggregate-then-score xenotype cost valuator. Used by MilUnitFC.UpdateEquipmentTotalCost
     * and FCWindow_XenoPicker.
     *
     * All gene-level effects (stat offsets, stat factors, capacity mods, aptitudes,
     * marketValueFactor, biostats, painFactor, damageFactors) are aggregated across the
     * whole xenotype into a single profile, then scored once. Combat stats split into
     * Shooting / Melee buckets; combat term = max(shooting, melee) + shared + nonCombat,
     * with a symmetric synergy bonus on min(shooting, melee) when both share a sign
     * (extra cost when both are good, extra discount when both are bad). This captures
     * the fact that a player can route a xenotype to a melee-only or shooting-only
     * loadout, so a weakness on one side is not a real cost when the other side is strong.
     *
     * No clamping happens during aggregation or scoring. The final factor is clamped
     * only by ApplyCap (floor at MIN_FACTOR = 0.1, optional ceiling from FCSettings).
     *
     * Scored profiles cache per-xenotype (immutable defs) and per custom xenotype name
     * (invalidated via FactionCache.InvalidateCustomXenotypeCache). FCSettings weights
     * apply at read time, so slider edits do not require cache invalidation.
     *-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*/
    public static class GeneValuationUtil
    {
        public struct GeneValueComponents
        {
            public float MvfBonus;
            public float MetBonus;
            public float ArcBonus;
            public float EffectShooting;
            public float EffectMelee;
            public float EffectShared;
            public float EffectNonCombat;
            public float PainBonus;
            public float DmgResistBonus;

            public float FlattenEffect()
            {
                float combat = Mathf.Max(EffectShooting, EffectMelee) + EffectShared + EffectNonCombat;
                bool bothPositive = EffectShooting > 0f && EffectMelee > 0f;
                bool bothNegative = EffectShooting < 0f && EffectMelee < 0f;
                if (bothPositive || bothNegative)
                    combat += Mathf.Min(EffectShooting, EffectMelee) * BothCombatBonus;
                return combat;
            }

            public string DescribeCombatBranch()
            {
                if (EffectShooting > 0f && EffectMelee > 0f) return "max+synergy";
                if (EffectShooting < 0f && EffectMelee < 0f) return "max+anti-synergy";
                return "max only";
            }

            public float ApplyWeights()
            {
                return MvfBonus        * FCSettings.geneValueWeightMvf
                     + MetBonus        * FCSettings.geneValueWeightMet
                     + ArcBonus        * FCSettings.geneValueWeightArc
                     + FlattenEffect() * FCSettings.geneValueWeightEffects
                     + PainBonus       * FCSettings.geneValueWeightPain
                     + DmgResistBonus  * FCSettings.geneValueWeightDmgResist;
            }
        }

        public enum StatBucket { Shooting, Melee, Shared, NonCombat, Skip }

        /* Aggregated raw profile for a xenotype. Built by BuildProfile from a gene list,
         * then scored by ScoreProfile into GeneValueComponents. Public so debug code can
         * inspect raw aggregated values. */
        public class XenotypeProfile
        {
            public readonly Dictionary<StatDef, float> StatOffsetSum = new Dictionary<StatDef, float>();
            public readonly Dictionary<StatDef, float> StatFactorProduct = new Dictionary<StatDef, float>();
            public readonly Dictionary<PawnCapacityDef, float> CapOffsetSum = new Dictionary<PawnCapacityDef, float>();
            public readonly Dictionary<PawnCapacityDef, float> CapFactorProduct = new Dictionary<PawnCapacityDef, float>();
            public readonly Dictionary<SkillDef, int> AptitudeSum = new Dictionary<SkillDef, int>();
            public readonly Dictionary<DamageDef, float> DamageFactorProduct = new Dictionary<DamageDef, float>();
            public float MvfProduct = 1f;
            public float MetSum;
            public float ArcSum;
            public float PainFactorProduct = 1f;
        }

        /* Floor on the final xenotype factor (post quarter-rounding). Prevents free mercs. */
        private const float MIN_FACTOR = 0.25f;
        /* Synergy multiplier applied to min(shooting, melee) when both share a sign. */
        private const float BothCombatBonus = 0.5f;

        /* Piecewise-linear curve applied to per-stat / per-aptitude / per-cap raw contributions
         * (and to DmgResistBonus). Identity in [-1, +1]; slope 1/3 outside up to plus/minus 2 at
         * plus/minus 4; SimpleCurve.Evaluate clamps to endpoint y-values past that. Mirrors the
         * shape of vanilla's PriceUtility.AverageSkillCurve. */
        private static readonly SimpleCurve _statContributionCurve = new SimpleCurve
        {
            new CurvePoint(-4f, -2f),
            new CurvePoint(-1f, -1f),
            new CurvePoint( 0f,  0f),
            new CurvePoint( 1f,  1f),
            new CurvePoint( 4f,  2f),
        };

        private static readonly Dictionary<XenotypeDef, GeneValueComponents> _xenotypeComponentsCache
            = new Dictionary<XenotypeDef, GeneValueComponents>();
        private static readonly Dictionary<string, GeneValueComponents> _customXenotypeComponentsCache
            = new Dictionary<string, GeneValueComponents>();

        /* Called by FactionCache.InvalidateCustomXenotypeCache so player-edited xenotypes
         * pick up gene list changes. */
        public static void InvalidateCustomXenotypeCache()
        {
            _customXenotypeComponentsCache.Clear();
        }

        /*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*/
        /* Public API                                                                 */
        /*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*/

        public static float XenotypeFactor(XenotypeDef xenotype)
        {
            if (xenotype is null) return 1f;
            GeneValueComponents components;
            if (!_xenotypeComponentsCache.TryGetValue(xenotype, out components))
            {
                components = ScoreProfile(BuildProfile(xenotype.genes));
                _xenotypeComponentsCache[xenotype] = components;
            }
            return ApplyCap(1f + components.ApplyWeights());
        }

        public static float XenotypeFactor(CustomXenotype custom)
        {
            if (custom is null) return 1f;
            string key = custom.name;
            if (key is null) return ApplyCap(1f + ScoreProfile(BuildProfile(custom.genes)).ApplyWeights());
            GeneValueComponents components;
            if (!_customXenotypeComponentsCache.TryGetValue(key, out components))
            {
                components = ScoreProfile(BuildProfile(custom.genes));
                _customXenotypeComponentsCache[key] = components;
            }
            return ApplyCap(1f + components.ApplyWeights());
        }

        public static float XenotypeFactor(IEnumerable<GeneDef> genes)
        {
            if (genes is null) return 1f;
            return ApplyCap(1f + ScoreProfile(BuildProfile(genes)).ApplyWeights());
        }

        /* Unclamped factor (1 + weighted sum). Lets debug code show what got floored/capped. */
        public static float RawXenotypeFactor(XenotypeDef xenotype)
        {
            return 1f + GetXenotypeComponents(xenotype).ApplyWeights();
        }

        public static float RawXenotypeFactor(CustomXenotype custom)
        {
            return 1f + GetXenotypeComponents(custom).ApplyWeights();
        }

        public static GeneValueComponents GetXenotypeComponents(XenotypeDef xenotype)
        {
            if (xenotype is null) return default(GeneValueComponents);
            GeneValueComponents components;
            if (!_xenotypeComponentsCache.TryGetValue(xenotype, out components))
            {
                components = ScoreProfile(BuildProfile(xenotype.genes));
                _xenotypeComponentsCache[xenotype] = components;
            }
            return components;
        }

        public static GeneValueComponents GetXenotypeComponents(CustomXenotype custom)
        {
            if (custom is null) return default(GeneValueComponents);
            string key = custom.name;
            if (key is null) return ScoreProfile(BuildProfile(custom.genes));
            GeneValueComponents components;
            if (!_customXenotypeComponentsCache.TryGetValue(key, out components))
            {
                components = ScoreProfile(BuildProfile(custom.genes));
                _customXenotypeComponentsCache[key] = components;
            }
            return components;
        }

        /* Raw aggregated profiles for debug inspection. Not cached — debug paths only. */
        public static XenotypeProfile GetXenotypeProfile(XenotypeDef xenotype)
        {
            return BuildProfile(xenotype is null ? null : (IEnumerable<GeneDef>)xenotype.genes);
        }

        public static XenotypeProfile GetXenotypeProfile(CustomXenotype custom)
        {
            return BuildProfile(custom is null ? null : (IEnumerable<GeneDef>)custom.genes);
        }

        /* Single-gene raw contribution. Useful for debug breakdowns of "what does this gene do." */
        public static XenotypeProfile GetGeneProfile(GeneDef gene)
        {
            if (gene is null) return new XenotypeProfile();
            return BuildProfile(new GeneDef[] { gene });
        }

        private static float ApplyCap(float factor)
        {
            if (!FCSettings.geneValueFactorUnlimited)
            {
                float cap = FCSettings.geneValueMaxFactor;
                if (factor > cap) factor = cap;
            }
            /* Round to nearest 0.25 so xenotype prices snap to clean quarter steps. */
            factor = (float)(Math.Round((double)factor * 4.0, MidpointRounding.AwayFromZero) / 4.0);
            if (factor < MIN_FACTOR) factor = MIN_FACTOR;
            return factor;
        }

        /*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*/
        /* Aggregation: gene list -> XenotypeProfile                                  */
        /*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*/

        private static XenotypeProfile BuildProfile(IEnumerable<GeneDef> genes)
        {
            XenotypeProfile p = new XenotypeProfile();
            if (genes is null) return p;
            foreach (GeneDef gene in genes)
            {
                if (gene is null) continue;
                AccumulateGene(p, gene);
            }
            return p;
        }

        private static void AccumulateGene(XenotypeProfile p, GeneDef gene)
        {
            /* statOffsets sum per stat */
            if (gene.statOffsets is object)
            {
                foreach (StatModifier so in gene.statOffsets)
                {
                    if (so.stat is null) continue;
                    float cur;
                    p.StatOffsetSum.TryGetValue(so.stat, out cur);
                    p.StatOffsetSum[so.stat] = cur + so.value;
                }
            }

            /* statFactors multiply per stat */
            if (gene.statFactors is object)
            {
                foreach (StatModifier sf in gene.statFactors)
                {
                    if (sf.stat is null) continue;
                    float cur;
                    if (!p.StatFactorProduct.TryGetValue(sf.stat, out cur)) cur = 1f;
                    p.StatFactorProduct[sf.stat] = cur * sf.value;
                }
            }

            /* capMods: offset sums, postFactor multiplies, per capacity */
            if (gene.capMods is object)
            {
                foreach (PawnCapacityModifier cm in gene.capMods)
                {
                    if (cm.capacity is null) continue;
                    float curO;
                    p.CapOffsetSum.TryGetValue(cm.capacity, out curO);
                    p.CapOffsetSum[cm.capacity] = curO + cm.offset;
                    float curF;
                    if (!p.CapFactorProduct.TryGetValue(cm.capacity, out curF)) curF = 1f;
                    p.CapFactorProduct[cm.capacity] = curF * cm.postFactor;
                }
            }

            /* aptitudes sum levels per skill */
            if (gene.aptitudes is object)
            {
                foreach (Aptitude apt in gene.aptitudes)
                {
                    if (apt.skill is null) continue;
                    int cur;
                    p.AptitudeSum.TryGetValue(apt.skill, out cur);
                    p.AptitudeSum[apt.skill] = cur + apt.level;
                }
            }

            /* damageFactors multiply per damage type */
            if (gene.damageFactors is object)
            {
                foreach (DamageFactor df in gene.damageFactors)
                {
                    if (df.damageDef is null) continue;
                    float cur;
                    if (!p.DamageFactorProduct.TryGetValue(df.damageDef, out cur)) cur = 1f;
                    p.DamageFactorProduct[df.damageDef] = cur * df.factor;
                }
            }

            /* Scalars */
            p.MvfProduct *= gene.marketValueFactor;
            p.MetSum     += gene.biostatMet;
            p.ArcSum     += gene.biostatArc;
            p.PainFactorProduct *= gene.painFactor;
        }

        /*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*/
        /* Scoring: XenotypeProfile -> GeneValueComponents                            */
        /*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*/

        public static GeneValueComponents ScoreProfile(XenotypeProfile p)
        {
            GeneValueComponents c = default(GeneValueComponents);
            if (p is null) return c;

            /* statOffsets: normalize against baseline, apply inverse-direction, curve, weight, classify, bucket.
             * The curve compresses extreme normalized values (e.g. ComfyTemperatureMin offsets normalized
             * against a 0-baseline stat can run to +/-20+) into a bounded contribution. */
            foreach (KeyValuePair<StatDef, float> kvp in p.StatOffsetSum)
            {
                StatDef stat = kvp.Key;
                float weight = StatWeight(stat);
                if (weight == 0f) continue;
                float denom = Mathf.Max(1f, Mathf.Abs(stat.defaultBaseValue));
                float raw = kvp.Value / denom;
                if (InverseDirectionStats.Contains(stat)) raw = -raw;
                float curved = _statContributionCurve.Evaluate(raw);
                AddToBucket(ref c, ClassifyStat(stat), curved * weight);
            }

            /* statFactors: bonus = (product - 1), apply inverse-direction, curve, weight, classify, bucket.
             * The curve caps a single large factor (e.g. InjuryHealingFactor x4 -> raw +3) at the curve
             * endpoint without flat clamping. */
            foreach (KeyValuePair<StatDef, float> kvp in p.StatFactorProduct)
            {
                StatDef stat = kvp.Key;
                float weight = StatWeight(stat);
                if (weight == 0f) continue;
                float raw = kvp.Value - 1f;
                if (InverseDirectionStats.Contains(stat)) raw = -raw;
                float curved = _statContributionCurve.Evaluate(raw);
                AddToBucket(ref c, ClassifyStat(stat), curved * weight);
            }

            /* capMods: combined contribution per capacity, curve, then route to Shared (sight/manipulation/
             * moving/consciousness affect both combat styles). */
            HashSet<PawnCapacityDef> seenCaps = new HashSet<PawnCapacityDef>();
            foreach (KeyValuePair<PawnCapacityDef, float> kvp in p.CapOffsetSum)
            {
                seenCaps.Add(kvp.Key);
                float factorProd;
                if (!p.CapFactorProduct.TryGetValue(kvp.Key, out factorProd)) factorProd = 1f;
                float raw = kvp.Value + (factorProd - 1f);
                c.EffectShared += _statContributionCurve.Evaluate(raw) * 0.5f;
            }
            foreach (KeyValuePair<PawnCapacityDef, float> kvp in p.CapFactorProduct)
            {
                if (seenCaps.Contains(kvp.Key)) continue;
                float raw = kvp.Value - 1f;
                c.EffectShared += _statContributionCurve.Evaluate(raw) * 0.5f;
            }

            /* Aptitudes: Shooting/Melee route to their buckets, Medicine to NonCombat. Curve the
             * level*0.05 raw before weighting so a single huge aptitude stack still saturates cleanly. */
            foreach (KeyValuePair<SkillDef, int> kvp in p.AptitudeSum)
            {
                float weight = SkillWeight(kvp.Key);
                if (weight == 0f) continue;
                float raw = kvp.Value * 0.05f;
                float contribution = _statContributionCurve.Evaluate(raw) * weight;
                if (kvp.Key == SkillDefOf.Shooting)   c.EffectShooting += contribution;
                else if (kvp.Key == SkillDefOf.Melee) c.EffectMelee    += contribution;
                else                                   c.EffectNonCombat += contribution;
            }

            /* Scalars. Met stays capped at 0 since positive metabolism is a design budget benefit. */
            c.MvfBonus  = p.MvfProduct - 1f;
            c.MetBonus  = Mathf.Max(0f, -p.MetSum);
            c.ArcBonus  = p.ArcSum;
            c.PainBonus = 1f - p.PainFactorProduct;

            /* DmgResistBonus: sum of per-type (1 - product), then curved. Sanguophage's Flame x4 alone
             * gives -3; without the curve it'd swamp a 0.3 weight to -0.9. */
            float dmgR = 0f;
            foreach (KeyValuePair<DamageDef, float> kvp in p.DamageFactorProduct)
                dmgR += 1f - kvp.Value;
            c.DmgResistBonus = _statContributionCurve.Evaluate(dmgR);

            return c;
        }

        private static void AddToBucket(ref GeneValueComponents c, StatBucket bucket, float contribution)
        {
            switch (bucket)
            {
                case StatBucket.Shooting:  c.EffectShooting  += contribution; break;
                case StatBucket.Melee:     c.EffectMelee     += contribution; break;
                case StatBucket.Shared:    c.EffectShared    += contribution; break;
                case StatBucket.NonCombat: c.EffectNonCombat += contribution; break;
                case StatBucket.Skip:
                default:
                    break;
            }
        }

        /*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*/
        /* Stat classification and weights                                            */
        /*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*/

        public static StatBucket ClassifyStat(StatDef stat)
        {
            if (stat is null) return StatBucket.Skip;
            StatBucket bucket;
            if (StatBuckets.TryGetValue(stat, out bucket)) return bucket;
            if (stat.category == StatCategoryDefOf.PawnCombat) return StatBucket.Shared;
            if (ExplicitStatWeights.ContainsKey(stat)) return StatBucket.NonCombat;
            return StatBucket.Skip;
        }

        private static Dictionary<StatDef, StatBucket> _statBuckets;
        private static Dictionary<StatDef, StatBucket> StatBuckets
        {
            get
            {
                if (_statBuckets is null)
                {
                    _statBuckets = new Dictionary<StatDef, StatBucket>();
                    /* Pure shooting */
                    AddBucket("ShootingAccuracyPawn",          StatBucket.Shooting);
                    AddBucket("ShootingAccuracyFactor_Touch",  StatBucket.Shooting);
                    AddBucket("ShootingAccuracyFactor_Short",  StatBucket.Shooting);
                    AddBucket("ShootingAccuracyFactor_Medium", StatBucket.Shooting);
                    AddBucket("ShootingAccuracyFactor_Long",   StatBucket.Shooting);
                    AddBucket("AimingDelayFactor",             StatBucket.Shooting);
                    AddBucket("AimingTime",                    StatBucket.Shooting);
                    AddBucket("RangedCooldownFactor",          StatBucket.Shooting);
                    AddBucket("MortarMissRadiusFactor",        StatBucket.Shooting);
                    /* Pure melee */
                    AddBucket("MeleeHitChance",                StatBucket.Melee);
                    AddBucket("MeleeDPS",                      StatBucket.Melee);
                    AddBucket("MeleeDamageFactor",             StatBucket.Melee);
                    AddBucket("MeleeCooldownFactor",           StatBucket.Melee);
                    AddBucket("MeleeArmorPenetration",         StatBucket.Melee);
                    /* Defensive / combat-agnostic */
                    AddBucket("MeleeDodgeChance",              StatBucket.Shared);
                    AddBucket("IncomingDamageFactor",          StatBucket.Shared);
                    AddBucket("StaggerDurationFactor",         StatBucket.Shared);
                    AddBucket("ArmorRating_Sharp",             StatBucket.Shared);
                    AddBucket("ArmorRating_Blunt",             StatBucket.Shared);
                    AddBucket("ArmorRating_Heat",              StatBucket.Shared);
                    AddBucket("PainShockThreshold",            StatBucket.Shared);
                    AddBucket("MentalBreakThreshold",          StatBucket.Shared);
                }
                return _statBuckets;
            }
        }

        private static void AddBucket(string defName, StatBucket bucket)
        {
            StatDef d = DefDatabase<StatDef>.GetNamedSilentFail(defName);
            if (d is object) _statBuckets[d] = bucket;
        }

        /* Stats where lower = better. Used in scoring to invert sign so genes that
         * reduce these stats (Flammability=0.1, IncomingDamageFactor=0.75, etc.) score
         * as positive contributions. */
        private static HashSet<StatDef> _inverseDirectionStats;
        private static HashSet<StatDef> InverseDirectionStats
        {
            get
            {
                if (_inverseDirectionStats is null)
                {
                    _inverseDirectionStats = new HashSet<StatDef>();
                    AddIfPresent("ComfyTemperatureMin");    // lower = more cold tolerance
                    AddIfPresent("StaggerDurationFactor");  // lower = recovers faster
                    AddIfPresent("Flammability");           // lower = doesn't catch fire
                    AddIfPresent("IncomingDamageFactor");   // lower = tougher
                    AddIfPresent("AimingDelayFactor");      // lower = faster aim
                    AddIfPresent("AimingTime");             // lower = faster aim
                    AddIfPresent("RangedCooldownFactor");   // lower = faster ranged cooldown
                    AddIfPresent("MeleeCooldownFactor");    // lower = faster melee cooldown
                    AddIfPresent("MentalBreakThreshold");   // lower = mentally tougher
                    AddIfPresent("HungerRateMultiplier");   // lower = eats less
                    AddIfPresent("MortarMissRadiusFactor"); // lower = better mortar accuracy
                }
                return _inverseDirectionStats;
            }
        }

        private static void AddIfPresent(string defName)
        {
            StatDef d = DefDatabase<StatDef>.GetNamedSilentFail(defName);
            if (d is object) _inverseDirectionStats.Add(d);
        }

        private static float StatWeight(StatDef stat)
        {
            if (stat is null) return 0f;
            /* Explicitly bucketed stats always get full weight, regardless of category. */
            if (StatBuckets.ContainsKey(stat)) return 1.0f;
            if (stat.category == StatCategoryDefOf.PawnCombat) return 1.0f;
            float w;
            return ExplicitStatWeights.TryGetValue(stat, out w) ? w : 0f;
        }

        private static Dictionary<StatDef, float> _explicitStatWeights;
        private static Dictionary<StatDef, float> ExplicitStatWeights
        {
            get
            {
                if (_explicitStatWeights is null)
                {
                    _explicitStatWeights = new Dictionary<StatDef, float>();
                    AddStat("MoveSpeed",                  0.6f);
                    AddStat("ImmunityGainSpeed",          0.4f);
                    AddStat("InjuryHealingFactor",        0.4f);
                    AddStat("ToxicResistance",            0.3f);
                    AddStat("ToxicEnvironmentResistance", 0.3f);
                    AddStat("VacuumResistance",           0.3f);
                    AddStat("ComfyTemperatureMax",        0.2f);
                    AddStat("ComfyTemperatureMin",        0.2f);
                    AddStat("PsychicSensitivity",         0.1f);
                }
                return _explicitStatWeights;
            }
        }

        private static void AddStat(string defName, float weight)
        {
            StatDef d = DefDatabase<StatDef>.GetNamedSilentFail(defName);
            if (d is object) _explicitStatWeights[d] = weight;
        }

        private static float SkillWeight(SkillDef skill)
        {
            if (skill is null) return 0f;
            if (skill == SkillDefOf.Shooting || skill == SkillDefOf.Melee) return 1.0f;
            if (skill == SkillDefOf.Medicine) return 0.5f;
            return 0f;
        }
    }
}
