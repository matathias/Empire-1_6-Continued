using System;
using System.Collections.Generic;
using System.Linq;
using FactionColonies.util;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace FactionColonies
{
    /*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*/
    /* EmpireDeathPenaltyUtil                                                      */
    /*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*/

    /// <summary>
    /// Central entry point for the consequences of losing Empire pawns and caravans. Every death path
    /// (mercs, civilian settlement defenders, trade-caravan pawns) funnels through here, which resolves a
    /// "home" settlement and schedules a gradual <see cref="DecayingStatPenalty"/> on it — bigger when the
    /// player caused the death. Slaughtering a caravan's pack animals applies a faction-wide penalty.
    ///
    /// <para>All magnitudes/durations/thresholds are internal constants. Happiness is hit
    /// by every death (it drives the Empire's goodwill anchor); unrest is an extra hit on player-caused
    /// deaths; loyalty is hit on caravan-wipe negligence.</para>
    /// </summary>
    public static class EmpireDeathPenaltyUtil
    {
        /* -*- Tunable constants -*- */
        // Happiness severity (total, pre-multiplier) delivered over PENALTY_DAYS for a single death.
        public const double DEATH_HAPPINESS_SMALL = 2.0;   // not player-caused
        public const double DEATH_HAPPINESS_LARGE = 12.0;   // player-caused (murder/collapse)
        public const double DEATH_UNREST_PLAYER = 4.0;     // extra unrest, player-caused only
        // Per-settlement severity for a faction-wide caravan-wipe (applied to happiness AND loyalty).
        public const double PACK_WIPE_SMALL = 16.0;         // enemy landed the last blow
        public const double PACK_WIPE_LARGE = 64.0;         // player landed the last blow
        public const int PENALTY_DAYS = 4;                 // drip duration
        public const double CASCADE_SPILL_RATE = 0.5;      // fraction of a saturated penalty spilled to others
        public const double THRESHOLD_LOW = 25.0;          // happiness/loyalty floor that trips cascade + caravan gate
        public const double THRESHOLD_HIGH_UNREST = 75.0;  // unrest ceiling that trips cascade + caravan gate

        private const string SrcPawn = "empirePawnDeath";
        private const string SrcCaravan = "empireCaravanDeath";
        private const string SrcWipe = "empireCaravanWipe";

        /* -*- Dedup between the Pawn.Kill prefix and Faction.Notify_MemberDied -*- */
        // The Kill prefix runs first in the same synchronous Kill call; when it handles a merc/caravan
        // pawn it records it here so Notify_MemberDied skips it (avoiding a second, faction-wide hit).
        // Keyed by pawn (not a single slot) so a nested Pawn.Kill — e.g. a death explosion killing a
        // second pawn mid-Kill — clears only its own entry in the postfix, never the outer pawn's.
        private static readonly HashSet<Pawn> handledByKillPrefix = new HashSet<Pawn>();
        // Per-caravan dedupe so a pack-animal wipe fires once, not once per dying animal. Transient.
        private static readonly HashSet<int> wipedCaravanLords = new HashSet<int>();

        public static void MarkHandledByKillPrefix(Pawn pawn)
        {
            if (pawn is object) handledByKillPrefix.Add(pawn);
        }
        public static bool WasHandledByKillPrefix(Pawn pawn) => pawn is object && handledByKillPrefix.Contains(pawn);
        public static void ClearHandledByKillPrefix(Pawn pawn)
        {
            if (pawn is object) handledByKillPrefix.Remove(pawn);
        }

        /// <summary>Clears all transient, per-session static state. Called on game init (new game and
        /// load) so nothing leaks across saves — notably the wiped-caravan set, whose Lord loadIDs
        /// collide across games (the loadID counter resets per game), which would otherwise suppress a
        /// pack-animal-wipe penalty in a save loaded after another in the same session.</summary>
        public static void ResetSessionState()
        {
            wipedCaravanLords.Clear();
            handledByKillPrefix.Clear();
        }

        /* Test support: the wiped-caravan set is otherwise only mutated through a full caravan death
         * path (a Lord + FactionFC), so the non-destructive regression test seeds/inspects it directly
         * (and gates itself on the set being empty so it never clobbers live entries). */
        internal static void MarkCaravanWipedForTest(int lordLoadID) => wipedCaravanLords.Add(lordLoadID);
        internal static bool IsCaravanWiped(int lordLoadID) => wipedCaravanLords.Contains(lordLoadID);
        internal static int WipedCaravanCountForTest => wipedCaravanLords.Count;

        /* -*-*-*-*-*-*-*-*-*-*-*-* Public death entry points *-*-*-*-*-*-*-*-*-*-*-*- */

        /// <summary>
        /// A mercenary died — from manual defense, auto-resolved combat (dinfo null), or offensive
        /// deployment, on or off map. Resolves the home settlement map-independently so even abstract
        /// losses are penalized.
        /// </summary>
        public static void HandleMercDeath(Pawn pawn, DamageInfo? dinfo)
        {
            MarkHandledByKillPrefix(pawn);
            MilitaryFC mfc = FindFC.Military;
            if (mfc is null) return;

            Mercenary merc = mfc.FindMercByPawn(pawn);
            WorldSettlementFC home = merc?.settlement ?? merc?.squad?.settlement;
            if (home is null) return;

            double extra = FindFC.FactionComp?.GetStatValue(FCStatDefOf.mercenaryDeathHappinessPenalty, home) ?? 0;
            ApplyDeathPenalty(home, dinfo, extra, SrcPawn, "FCPenaltyPawnDeath".Translate(FindFC.EmpireTitle.CapitalizeFirst()));
        }

        /// <summary>
        /// A non-merc, non-caravan Empire pawn (a generated settlement defender, or a visitor/lodger on
        /// the player's map) died. Home is the defended settlement when there is one, otherwise the
        /// highest-prosperity settlement so orphan deaths still carry a consequence.
        /// </summary>
        public static void HandleCivilianDefenderDeath(Pawn pawn, DamageInfo? dinfo, WorldSettlementFC home)
        {
            FactionFC faction = FindFC.FactionComp;
            if (faction is null) return;
            if (home is null) home = HighestProsperity(faction);
            if (home is null) return;
            ApplyDeathPenalty(home, dinfo, 0, SrcPawn, "FCPenaltyPawnDeath".Translate(FindFC.EmpireTitle.CapitalizeFirst()));
        }

        /// <summary>
        /// A pawn in an Empire trade caravan died. Home is the settlement the caravan was tagged to at
        /// spawn (fallback: highest-prosperity). Also checks whether this death wipes the caravan's pack
        /// animals — if so, a faction-wide penalty is applied.
        /// </summary>
        public static void HandleCaravanPawnDeath(Pawn pawn, DamageInfo? dinfo, Lord lord)
        {
            MarkHandledByKillPrefix(pawn);
            FactionFC faction = FindFC.FactionComp;
            if (faction is null || lord is null) return;

            WorldSettlementFC home = faction.TryGetCaravanHome(lord.loadID) ?? HighestProsperity(faction);
            if (home is object)
                ApplyDeathPenalty(home, dinfo, 0, SrcCaravan, "FCPenaltyCaravanDeath".Translate(FindFC.EmpireTitle.CapitalizeFirst()));

            // Pack-animal wipe: every animal in the caravan now dead (this pawn included).
            if (wipedCaravanLords.Contains(lord.loadID)) return;
            List<Pawn> owned = lord.ownedPawns;
            bool anyAnimal = false;
            bool allAnimalsDead = true;
            for (int i = 0; i < owned.Count; i++)
            {
                Pawn p = owned[i];
                if (p is null || !(p.RaceProps?.Animal ?? false)) continue;
                anyAnimal = true;
                if (p != pawn && !p.Dead && !p.Destroyed)
                {
                    allAnimalsDead = false;
                    break;
                }
            }
            if (anyAnimal && allAnimalsDead)
            {
                wipedCaravanLords.Add(lord.loadID);
                ApplyPackAnimalWipe(dinfo);
            }
        }

        /* -*-*-*-*-*-*-*-*-*-*-*-* Penalty scheduling *-*-*-*-*-*-*-*-*-*-*-*- */

        /// <summary>Core: schedule the happiness hit (always) + unrest hit (player-caused) on a home settlement.</summary>
        private static void ApplyDeathPenalty(WorldSettlementFC home, DamageInfo? dinfo, double extraHappiness, string sourceId, string label)
        {
            FactionFC faction = FindFC.FactionComp;
            if (faction is null || home is null) return;
            if (faction.IsMemberDeathPenaltySuppressed()) return;

            bool playerCaused = IsPlayerCaused(dinfo);
            double happiness = (playerCaused ? DEATH_HAPPINESS_LARGE : DEATH_HAPPINESS_SMALL)
                + (extraHappiness > 0 ? extraHappiness : 0);
            ScheduleSettlementPenalty(home, SettlementStat.Happiness, happiness, PENALTY_DAYS, sourceId, label);

            if (playerCaused)
                ScheduleSettlementPenalty(home, SettlementStat.Unrest, DEATH_UNREST_PLAYER, PENALTY_DAYS, sourceId, label);
        }

        /// <summary>
        /// Schedules one decaying penalty on <paramref name="home"/>. If the settlement is already past the
        /// threshold in that stat, also spills a reduced amount across the other settlements (cascade).
        /// </summary>
        public static void ScheduleSettlementPenalty(WorldSettlementFC home, SettlementStat which, double total, int days, string sourceId, string label)
        {
            if (home is null || total <= 0) return;
            FCStatDef lossStat = LossStatFor(which);
            if (lossStat is null) return;

            home.AddDecayingPenalty(lossStat, total, days, sourceId, label);

            FactionFC faction = FindFC.FactionComp;
            if (faction is null || !IsPastThreshold(home, which)) return;

            List<WorldSettlementFC> all = faction.settlements;
            if (all.Count <= 1) return;
            double spillEach = total * CASCADE_SPILL_RATE / (all.Count - 1);
            if (spillEach <= 0) return;
            foreach (WorldSettlementFC other in all)
            {
                if (other is null || other == home) continue;
                other.AddDecayingPenalty(lossStat, spillEach, days, sourceId + "_spill", label);
            }
        }

        /// <summary>Faction-wide happiness + loyalty penalty for letting a caravan's pack animals be wiped out.</summary>
        public static void ApplyPackAnimalWipe(DamageInfo? dinfo)
        {
            FactionFC faction = FindFC.FactionComp;
            if (faction is null || faction.IsMemberDeathPenaltySuppressed()) return;
            if (!faction.settlements.Any()) return;

            bool playerCaused = IsPlayerCaused(dinfo);
            double amount = playerCaused ? PACK_WIPE_LARGE : PACK_WIPE_SMALL;
            string label = "FCPenaltyCaravanWipe".Translate(FindFC.EmpireTitle.CapitalizeFirst());

            foreach (WorldSettlementFC s in faction.settlements)
            {
                if (s is null) continue;
                s.AddDecayingPenalty(FCStatDefOf.happinessLostBase, amount, PENALTY_DAYS, SrcWipe, label);
                s.AddDecayingPenalty(FCStatDefOf.loyaltyLostBase, amount, PENALTY_DAYS, SrcWipe, label);
            }

            Messages.Message("FCPenaltyCaravanWipeMessage".Translate(faction.name), MessageTypeDefOf.NegativeEvent);
        }

        /* -*-*-*-*-*-*-*-*-*-*-*-* Helpers *-*-*-*-*-*-*-*-*-*-*-*- */

        public static bool IsPlayerCaused(DamageInfo? dinfo)
        {
            if (dinfo is null) return false;
            if (dinfo.Value.Category == DamageInfo.SourceCategory.Collapse) return true;
            return dinfo.Value.Instigator?.Faction == Faction.OfPlayer;
        }

        private static FCStatDef LossStatFor(SettlementStat which)
        {
            switch (which)
            {
                case SettlementStat.Happiness: return FCStatDefOf.happinessLostBase;
                case SettlementStat.Loyalty: return FCStatDefOf.loyaltyLostBase;
                case SettlementStat.Unrest: return FCStatDefOf.unrestGainedBase;
                default: return null;
            }
        }

        private static bool IsPastThreshold(WorldSettlementFC s, SettlementStat which)
        {
            switch (which)
            {
                case SettlementStat.Happiness: return s.happiness < THRESHOLD_LOW;
                case SettlementStat.Loyalty: return s.loyalty < THRESHOLD_LOW;
                case SettlementStat.Unrest: return s.unrest > THRESHOLD_HIGH_UNREST;
                default: return false;
            }
        }

        /// <summary>Resolves a caravan's home settlement from its trader kind.</summary>
        public static WorldSettlementFC ResolveCaravanHome(TraderKindDef tk)
        {
            FactionFC faction = FindFC.FactionComp;
            if (faction is null || !faction.settlements.Any()) return null;

            if (tk is object && tk.defName is object)
            {
                if (tk.defName.StartsWith("FC_Caravan_Empire_"))
                {
                    string suffix = tk.defName.Substring("FC_Caravan_Empire_".Length);
                    ResourceTypeDef rtd = DefDatabase<ResourceTypeDef>.GetNamedSilentFail("RTD_" + suffix);
                    if (rtd is object)
                        return HighestBy(faction, s => s.GetResource(rtd)?.rawTotalProduction ?? 0);
                }
                else if (tk.category == "Slaver"
                    || tk.defName == "Caravan_Outlander_PirateMerchant"
                    || tk.defName == "Caravan_Neolithic_Slaver")
                {
                    return HighestBy(faction, s => s.settlementMilitaryLevel);
                }
                else if (tk.defName == "Caravan_Outlander_Exotic"
                    || tk.defName == "Caravan_Neolithic_ShamanMerchant")
                {
                    return HighestBy(faction, s => s.prosperity);
                }
            }

            return HighestProsperity(faction);
        }

        private static WorldSettlementFC HighestProsperity(FactionFC faction)
            => HighestBy(faction, s => s.prosperity);

        private static WorldSettlementFC HighestBy(FactionFC faction, Func<WorldSettlementFC, double> selector)
        {
            WorldSettlementFC best = null;
            double bestVal = 0;
            foreach (WorldSettlementFC s in faction.settlements)
            {
                if (s is null) continue;
                double v = selector(s);
                if (best is null || v > bestVal)
                {
                    best = s;
                    bestVal = v;
                }
            }
            return best;
        }
    }
}
