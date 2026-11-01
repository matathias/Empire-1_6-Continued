using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using VanillaPsycastsExpanded;
using VanillaPsycastsExpanded.UI;
using VEF.Abilities;
using Verse;
using AbilityDef = VEF.Abilities.AbilityDef;

namespace FactionColonies.VPE
{
    /// <summary>
    /// Vanilla Psycasts Expanded provider for the unit designer's Psycasts tab. VPE uses its own
    /// ability framework (<c>VEF.Abilities.AbilityDef</c>, a separate DefDatabase from base game) and a
    /// path/points learning model, so it brings its own editor (<see cref="FCWindow_VPEPsycastEditor"/>)
    /// rather than the generic picker. Psylink is granted the "neuroformer way" (base <c>PsychicAmplifier</c>
    /// hediff); VPE's own Harmony patches attach its psycast tracker and sync level/points.
    /// </summary>
    public class VPEPsycastProvider : IPsycastSystemProvider
    {
        public const string ProviderKey = "VPE";

        // SavedPsycast.kind values this provider understands (null/"" == a psycast).
        // VPE psylink points can be spent on three things; each maps to one of these entry kinds.
        public const string KindMeditationFocus = "MeditationFocus";
        public const string KindStatUpgrade = "StatUpgrade";

        public string Key => ProviderKey;
        public string Label => "Vanilla Psycasts Expanded";
        public bool IsActive => FactionCompat.VPEActive;
        public int Priority => 100;
        public int MaxPsylinkLevel => PsycastsMod.Settings != null ? PsycastsMod.Settings.maxLevel : 30;
        public bool SupportsExplicitSelection => true;

        public void OpenEditor(MilUnitFC unit, Action onClosed)
        {
            if (unit is null) return;
            Find.WindowStack.Add(new FCWindow_VPEPsycastEditor(unit, onClosed));
        }

        public bool TryGetDisplay(SavedPsycast entry, out PsycastPickEntry display)
        {
            display = null;

            if (entry.kind == KindMeditationFocus)
            {
                MeditationFocusDef focus = DefDatabase<MeditationFocusDef>.GetNamedSilentFail(entry.psycastDef);
                if (focus is null) return false;
                display = new PsycastPickEntry
                {
                    defName = focus.defName,
                    label = focus.LabelCap,
                    description = focus.description,
                    icon = focus.Icon(),
                    level = 0, // foci are point-gated, not psylink-level-gated — never stripped on level drop
                    cost = FCSettings.vpeFocusCost * FCSettings.militaryPsycastCostMultiplier
                };
                return true;
            }

            if (entry.kind == KindStatUpgrade)
            {
                int n = entry.count > 0 ? entry.count : 1;
                display = new PsycastPickEntry
                {
                    defName = "",
                    label = "VPE.PsycasterStats".Translate() + (n > 1 ? " x" + n : ""),
                    description = null,
                    icon = null,
                    level = 0,
                    cost = FCSettings.vpeStatPointCost * n * FCSettings.militaryPsycastCostMultiplier
                };
                return true;
            }

            // Default: a psycast.
            AbilityDef def = DefDatabase<AbilityDef>.GetNamedSilentFail(entry.psycastDef);
            if (def is null) return false;
            display = ToEntry(def);
            return true;
        }

        internal static PsycastPickEntry ToEntry(AbilityDef def)
        {
            AbilityExtension_Psycast psycast = def.Psycast();
            int level = psycast != null ? psycast.level : 0;
            return new PsycastPickEntry
            {
                defName = def.defName,
                label = def.LabelCap,
                description = def.description,
                icon = def.icon,
                level = level,
                cost = (FCSettings.vpePsycastBaseCost + FCSettings.vpePsycastPerLevelCost * level) * FCSettings.militaryPsycastCostMultiplier
            };
        }

        /// <summary>
        /// Raises the pawn's psylink to <paramref name="level"/> via the base-game psylink hediff.
        /// VPE's Harmony patches (<c>Hediff_Psylink.PostAdd</c> / <c>ChangeLevel</c>) attach the VPE
        /// psycast tracker, sync its level, grant points, and suppress the base random-psycast grant.
        /// </summary>
        public void ApplyPsylink(Pawn pawn, int level)
        {
            if (pawn?.health is null || level < 1) return;

            Hediff_Psylink psylink = pawn.GetMainPsylinkSource();
            if (psylink is null)
            {
                // Creates the psylink hediff at level 1; VPE's PostAdd patch attaches the tracker.
                pawn.ChangePsylinkLevel(1, false);
                psylink = pawn.GetMainPsylinkSource();
            }

            int current = psylink != null ? psylink.level : 0;
            int target = Math.Min(level, MaxPsylinkLevel);
            if (target > current)
                pawn.ChangePsylinkLevel(target - current, false);
        }

        /// <summary>
        /// VPE charges nothing for psylink levels themselves — the balance lever is the per-psycast,
        /// per-focus, and per-stat-point cost (see <see cref="TryGetDisplay"/>), configured in the
        /// Compatibility settings tab. So this is always 0.
        /// </summary>
        public double PsylinkCost(int level) => 0;

        public void GrantPsycast(Pawn pawn, SavedPsycast entry)
        {
            if (pawn is null) return;

            // Stat upgrade: add the designed number of psycaster-stat points (forces the end state,
            // independent of the point economy, like the ability grant below).
            if (entry.kind == KindStatUpgrade)
            {
                int n = entry.count > 0 ? entry.count : 1;
                Hediff_PsycastAbilities statTracker = pawn.Psycasts();
                if (statTracker != null) statTracker.ImproveStats(n);
                return;
            }

            // Meditation focus: unlock the chosen focus on the pawn's psycast tracker.
            if (entry.kind == KindMeditationFocus)
            {
                MeditationFocusDef focus = DefDatabase<MeditationFocusDef>.GetNamedSilentFail(entry.psycastDef);
                if (focus is null) return;
                Hediff_PsycastAbilities fociTracker = pawn.Psycasts();
                if (fociTracker != null && !fociTracker.unlockedMeditationFoci.Contains(focus))
                    fociTracker.UnlockMeditationFocus(focus);
                return;
            }

            // Default: a psycast.
            AbilityDef def = DefDatabase<AbilityDef>.GetNamedSilentFail(entry.psycastDef);
            if (def is null) return;

            CompAbilities comp = pawn.GetComp<CompAbilities>();
            if (comp is null) return;
            if (comp.HasAbility(def)) return;

            // Unlock the owning path (so the merc's psycaster UI reflects it), then grant the ability
            // directly — the same primitive the editor uses (CompAbilities.GiveAbility). NOT
            // AbilityExtension_Psycast.UnlockWithPrereqs: that pulls in prerequisite abilities we didn't
            // choose, and the chosen set already includes every prereq in order.
            AbilityExtension_Psycast psycast = def.Psycast();
            Hediff_PsycastAbilities tracker = pawn.Psycasts();
            if (psycast != null && tracker != null && psycast.path != null && !tracker.unlockedPaths.Contains(psycast.path))
                tracker.UnlockPath(psycast.path);
            comp.GiveAbility(def);
        }

        public List<SavedPsycast> ClampSelectionsToBudget(List<SavedPsycast> selections, int psylinkLevel)
            => VPEPointMath.Trim(selections, psylinkLevel);

        public bool TryGetPointBudget(MilUnitFC unit, out int spent, out int budget)
        {
            spent = 0;
            budget = 0;
            if (unit is null) return false;
            budget = VPEPointMath.Budget(unit.psylinkLevel);
            spent = VPEPointMath.SpentPoints(unit.psycasts);
            return true;
        }

        /// <summary>
        /// VPE reconcile = full wipe + deterministic re-apply. VPE's grants are exact (no randomness),
        /// so nothing is lost by wiping and rebuilding to the desired psylink level + chosen psycasts.
        /// </summary>
        public void ReconcilePsycasts(Pawn pawn, MilUnitFC desired)
        {
            if (pawn?.health is null) return;

            Wipe(pawn);

            if (desired is null || desired.psylinkLevel <= 0) return;
            ApplyPsylink(pawn, desired.psylinkLevel);
            if (desired.psycasts is null) return;
            foreach (SavedPsycast a in desired.psycasts)
            {
                if (a.systemKey == ProviderKey)
                    GrantPsycast(pawn, a);
            }
        }

        /// <summary>
        /// Wipes the pawn's VPE psycast state: VPE's own <see cref="Hediff_PsycastAbilities.Reset"/>
        /// clears learned psycasts/paths/foci from the <c>CompAbilities</c>, then the VPE implant and
        /// base psylink hediffs are removed. Also strips any stray base-game psycast abilities.
        /// </summary>
        private static void Wipe(Pawn pawn)
        {
            Hediff_PsycastAbilities tracker = pawn.Psycasts();
            if (tracker != null)
            {
                tracker.Reset();
                pawn.health.RemoveHediff(tracker);
            }

            if (pawn.abilities != null)
            {
                foreach (RimWorld.Ability a in pawn.abilities.abilities.Where(a => a.def.IsPsycast).ToList())
                    pawn.abilities.RemoveAbility(a.def);
            }

            Hediff_Psylink ps = pawn.GetMainPsylinkSource();
            if (ps != null) pawn.health.RemoveHediff(ps);
        }
    }
}
