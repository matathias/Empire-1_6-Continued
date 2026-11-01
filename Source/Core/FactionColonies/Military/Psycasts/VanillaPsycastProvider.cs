using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Base-game (Royalty) psycast provider. Mirrors vanilla exactly: there is no selection UI — the
    /// player only sets a psylink level, and the base game grants one <em>random</em> psycast per level
    /// when the pawn gains psylink (<see cref="Hediff_Psylink.TryGiveAbilityOfLevel"/>). Stores no
    /// psycasts, so <see cref="SupportsExplicitSelection"/> is false and the psycast-grant/display
    /// methods are no-ops.
    /// </summary>
    public class VanillaPsycastProvider : IPsycastSystemProvider
    {
        public const string ProviderKey = "Vanilla";

        public string Key => ProviderKey;
        public string Label => "Royalty";
        public bool IsActive => ModLister.RoyaltyInstalled;
        public int Priority => 0;
        public int MaxPsylinkLevel => 6;
        public bool SupportsExplicitSelection => false;

        // No custom editor / no stored psycasts.
        public void OpenEditor(MilUnitFC unit, Action onClosed) { }

        public bool TryGetDisplay(SavedPsycast entry, out PsycastPickEntry display)
        {
            display = null;
            return false;
        }

        /// <summary>
        /// Raises the pawn's psylink to <paramref name="level"/> the neuroformer way. As in vanilla,
        /// the base game grants one random psycast per level gained (letters suppressed). The first
        /// <see cref="PawnUtility.ChangePsylinkLevel"/> creates the hediff at level 1 and returns, so
        /// a second call advances to the target level.
        /// </summary>
        public void ApplyPsylink(Pawn pawn, int level)
        {
            if (pawn?.health is null || level < 1) return;

            Hediff_Psylink psylink = pawn.GetMainPsylinkSource();
            if (psylink is null)
            {
                pawn.ChangePsylinkLevel(1, false);
                psylink = pawn.GetMainPsylinkSource();
            }

            int current = psylink != null ? psylink.level : 0;
            int target = Math.Min(level, MaxPsylinkLevel);
            if (target > current)
                pawn.ChangePsylinkLevel(target - current, false);
        }

        // Base game stores no chosen psycasts; psycasts are the random grants from ApplyPsylink.
        public void GrantPsycast(Pawn pawn, SavedPsycast entry) { }

        // No point economy and no stored selections — nothing to trim or summarize.
        public List<SavedPsycast> ClampSelectionsToBudget(List<SavedPsycast> selections, int psylinkLevel) => selections;

        public bool TryGetPointBudget(MilUnitFC unit, out int spent, out int budget)
        {
            spent = 0;
            budget = 0;
            return false;
        }

        /* Cost of the unit's psylink levels, mirroring the psylink neuroformer item's value
         * (def "PsychicAmplifier", ~2600 silver) scaled per level and by the settings multiplier.
         * This is the base-game balance lever: vanilla psycasts are free random grants, so the
         * psylink levels themselves carry the cost. */
        public double PsylinkCost(int level)
        {
            if (level <= 0) return 0;
            ThingDef neuroformer = DefDatabase<ThingDef>.GetNamedSilentFail("PsychicAmplifier");
            double perLevel = neuroformer is object ? neuroformer.BaseMarketValue : 2600.0;
            return Math.Floor(perLevel * FCSettings.militaryPsylinkCostMultiplier * level);
        }

        /// <summary>
        /// Granular psylink reconcile: keeps the pawn's existing random psycasts and only adjusts for the
        /// level delta. Raising calls <see cref="PawnUtility.ChangePsylinkLevel"/>, which grants one random
        /// psycast per newly-gained level (and skips levels the pawn already has a psycast for). Lowering
        /// drops the level and strips only the psycasts now above the cap. Going to 0 removes everything.
        /// </summary>
        public void ReconcilePsycasts(Pawn pawn, MilUnitFC desired)
        {
            if (pawn?.health is null) return;
            int target = desired != null ? Math.Min(desired.psylinkLevel, MaxPsylinkLevel) : 0;

            Hediff_Psylink ps = pawn.GetMainPsylinkSource();
            int current = ps != null ? ps.level : 0;
            if (target == current) return;

            if (target <= 0)
            {
                StripPsycastsAboveLevel(pawn, 0);
                if (ps != null) pawn.health.RemoveHediff(ps);
                return;
            }

            if (ps is null)
            {
                ApplyPsylink(pawn, target); // no existing psylink — grant fresh
                return;
            }

            // ChangePsylinkLevel grants randoms only for newly-gained levels (positive offset) and
            // never double-grants a level the pawn already has, so raising preserves the existing set.
            pawn.ChangePsylinkLevel(target - current, false);
            if (target < current)
                StripPsycastsAboveLevel(pawn, target);
        }

        private static void StripPsycastsAboveLevel(Pawn pawn, int cap)
        {
            if (pawn.abilities is null) return;
            foreach (Ability a in pawn.abilities.abilities.Where(a => a.def.IsPsycast && a.def.level > cap).ToList())
                pawn.abilities.RemoveAbility(a.def);
        }
    }
}
