using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// A single pickable psycast surfaced by an <see cref="IPsycastSystemProvider"/>. Pure display
    /// data for the picker UI and the chosen-psycast list row; the actual grant is done by the
    /// provider via <see cref="IPsycastSystemProvider.GrantPsycast"/>.
    /// </summary>
    public class PsycastPickEntry
    {
        public string defName;
        public string label;
        public string description;
        public Texture2D icon;
        public int level;     // required psylink level (informational / sort)
        public double cost;    // silver cost contribution toward equipmentTotalCost
    }

    /// <summary>
    /// Pluggable psycast system for the unit designer's Psycasts tab. The base game and Vanilla
    /// Psycasts Expanded are distinct, non-overlapping systems (different AbilityDef types, different
    /// learning models), so each is wrapped in a provider. Exactly one provider is "active" at a
    /// time (see <see cref="PsycastSystemRegistry.Active"/>); saved psycasts carry their provider's
    /// <see cref="Key"/> so they apply through the right system even on load.
    ///
    /// Register implementations via <see cref="PsycastSystemRegistry.Register"/>. Providers are
    /// app-lifetime singletons (not per-game), so registration happens from a
    /// <c>[StaticConstructorOnStartup]</c>, not from world-component load.
    /// </summary>
    public interface IPsycastSystemProvider
    {
        /// <summary>Stable identifier persisted on every <c>SavedPsycast</c> this provider produces.</summary>
        string Key { get; }

        /// <summary>Human-readable name (for empty-state text / tooltips).</summary>
        string Label { get; }

        /// <summary>True when this system's mod(s) are loaded and the system is usable.</summary>
        bool IsActive { get; }

        /// <summary>Higher wins when multiple providers are active. Vanilla is lowest.</summary>
        int Priority { get; }

        /// <summary>Maximum psylink level this system supports (caps the tab's stepper).</summary>
        int MaxPsylinkLevel { get; }

        /// <summary>
        /// When true (VPE), the player explicitly chooses psycasts: the tab shows an "Edit Psycasts"
        /// button (<see cref="OpenEditor"/>) and a read-only list of the chosen psycasts. When
        /// false (base game), psycasts are granted randomly at spawn from the psylink level — no
        /// picker, no stored psycasts — exactly as base RimWorld does.
        /// </summary>
        bool SupportsExplicitSelection { get; }

        /// <summary>
        /// Opens the provider-owned editor (only when <see cref="SupportsExplicitSelection"/>). The
        /// editor writes the chosen psycasts back into <paramref name="unit"/>.psycasts; it should
        /// invoke <paramref name="onClosed"/> when done so the tab can refresh cost/preview.
        /// </summary>
        void OpenEditor(MilUnitFC unit, Action onClosed);

        /// <summary>
        /// Resolves a saved entry's display data (label/icon/cost) for list rendering. The entry is
        /// opaque to the core — the provider interprets its <c>kind</c>/<c>psycastDef</c>/<c>count</c>
        /// (e.g. VPE distinguishes psycasts, meditation foci, and stat upgrades).
        /// </summary>
        bool TryGetDisplay(SavedPsycast entry, out PsycastPickEntry display);

        /// <summary>Grants psylink of the given level to a freshly generated pawn (the neuroformer way).</summary>
        void ApplyPsylink(Pawn pawn, int level);

        /// <summary>
        /// Silver cost of raising a unit to the given psylink level, folded into the unit's
        /// <c>equipmentTotalCost</c>. Base game charges per psylink level (its psycasts are free random
        /// grants); VPE returns 0 — its balance lever is the per-psycast / focus / stat cost instead.
        /// </summary>
        double PsylinkCost(int level);

        /// <summary>
        /// Forces the end-state of one saved entry onto a freshly generated pawn. The provider owns
        /// interpretation of the entry's <c>kind</c> (psycast, meditation focus, stat upgrade, ...).
        /// </summary>
        void GrantPsycast(Pawn pawn, SavedPsycast entry);

        /// <summary>
        /// Returns the subset of <paramref name="selections"/> (in order) that fits this system's point
        /// budget for <paramref name="psylinkLevel"/> — used when a unit's psylink level drops. The base
        /// game stores no selections and returns the list unchanged; VPE trims from the end to its point
        /// budget. May mutate/return a new list; callers should assign the result back.
        /// </summary>
        List<SavedPsycast> ClampSelectionsToBudget(List<SavedPsycast> selections, int psylinkLevel);

        /// <summary>
        /// Point-economy summary for the unit's current selections, for display ("spent / budget").
        /// Returns false for systems without a point economy (base game), leaving the outs at 0.
        /// </summary>
        bool TryGetPointBudget(MilUnitFC unit, out int spent, out int budget);

        /// <summary>
        /// Brings a LIVE (already-spawned) pawn's psycasts in line with <paramref name="desired"/> for the
        /// in-place upgrade flow, preserving as much of the pawn's existing state as is appropriate. Base
        /// game adjusts the psylink level granularly — keeping current random psycasts, granting one for
        /// each newly-gained level, and stripping any now above the cap — because re-rolling would be a
        /// surprise. VPE wipes and re-applies (its grants are deterministic, so nothing is lost). Must
        /// tolerate a pawn with no existing psylink.
        /// </summary>
        void ReconcilePsycasts(Pawn pawn, MilUnitFC desired);
    }
}
