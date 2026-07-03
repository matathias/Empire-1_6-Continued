using System;
using System.Collections.Generic;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Resolves a <see cref="MercenarySquadFC"/> to its projected combat power
    /// (<see cref="SquadPower"/>). Two squads at the same billet project distinct
    /// forces because the base computation reads each squad's actual loadout cost.
    /// <para>The registry first computes a base power from loadout cost + the
    /// home settlement's <see cref="FCStatDefOf.militaryCombatEfficiency"/>, then
    /// chains every registered <see cref="ISquadPowerModifier"/> in descending
    /// <see cref="ISquadPowerModifier.Priority"/> order. Each modifier receives
    /// the running power and returns the modified value, so multiple submods
    /// compose (veterancy + specialist + augmentation each contribute).</para>
    /// <para>Storage is a raw <see cref="List{T}"/> rather than <see cref="RegistryList{T}"/>
    /// because Register/Unregister/ClearAll must invalidate the cached priority-sorted
    /// view; the standard wrapper has no hook for that.</para>
    /// </summary>
    public static class SquadPowerRegistry
    {
        private static readonly List<ISquadPowerModifier> _modifiers = new List<ISquadPowerModifier>();
        private static List<ISquadPowerModifier> _sortedCache;

        internal static void Register(ISquadPowerModifier modifier)
        {
            if (modifier is null) return;
            if (!_modifiers.Contains(modifier))
            {
                _modifiers.Add(modifier);
                _sortedCache = null;
            }
        }

        internal static void Unregister(ISquadPowerModifier modifier)
        {
            if (_modifiers.Remove(modifier)) _sortedCache = null;
        }

        internal static void ClearAll()
        {
            _modifiers.Clear();
            _sortedCache = null;
        }

        public static IReadOnlyList<ISquadPowerModifier> Modifiers => _modifiers;

        /// <summary>Resolves <paramref name="squad"/> to its projected combat power.
        /// Returns a sensible default (level 1, efficiency 1) only for a null squad — callers
        /// don't need a null check on the result. An unassigned squad still reports its real
        /// cost-derived power level (efficiency falls back to 1 without a home settlement);
        /// <see cref="ComputeBasePower"/> already guards the settlement-dependent terms.</summary>
        public static SquadPower Resolve(MercenarySquadFC squad)
        {
            if (squad is null) return new SquadPower(1, 1);

            SquadPower power = ComputeBasePower(squad);
            SquadPower running = power;
            RegistryDispatch.Each(GetSortedModifiers(),
                m => running = m.ModifyPower(squad, running),
                nameof(ISquadPowerModifier.ModifyPower));
            return running;
        }

        /// <summary>Inverse of <see cref="CostFromLevel"/>:
        /// <c>cost = 1000 + 500·L + 600·L²  →  L = (-500 + √(250000 + 2400·(cost-1000))) / 1200</c>.
        /// Floored at 1 (matches the minimum settlement military level) so a near-zero-
        /// cost squad still has a projection. Result is a double so downstream can
        /// blend it before clamping.</summary>
        public static double LevelFromCost(double cost)
        {
            if (cost <= 1000) return 1.0;
            double disc = 250000.0 + 2400.0 * (cost - 1000.0);
            if (disc < 0) return 1.0;
            double level = (-500.0 + Math.Sqrt(disc)) / 1200.0;
            return Math.Max(1.0, level);
        }

        /// <summary>The loadout budget a squad of military level <paramref name="level"/> represents
        /// (cost = 1000 + 500·L + 600·L²), and the exact inverse of <see cref="LevelFromCost"/>.
        /// Canonical home of the level->cost curve: <see cref="MilitaryFC.CalculateSquadBudget"/>
        /// delegates here. Not clamped, so callers passing level 0 get the baseline 1000.</summary>
        public static double CostFromLevel(double level)
        {
            return 1000.0 + 500.0 * level + 600.0 * level * level;
        }

        /// <summary>Derives a military level from a set of LIVE pawns by summing each pawn's vanilla
        /// <see cref="Verse.Thing.MarketValue"/> (body + worn gear + implants), weighted by
        /// <see cref="SquadEffectivenessUtil.PawnEffectiveness"/> so downed/injured pawns contribute
        /// proportionally, then mapping the total through <see cref="LevelFromCost"/>. Lets external
        /// forces (e.g. defensive-outpost garrisons) be leveled on the same cost->level curve as squads
        /// instead of a bespoke formula. Returns the LevelFromCost floor (1.0) for a null/empty set.</summary>
        public static double LevelFromPawns(IEnumerable<Pawn> pawns)
        {
            if (pawns is null) return 1.0;
            double cost = 0;
            foreach (Pawn p in pawns)
            {
                if (p is null || p.Dead) continue;
                double eff = SquadEffectivenessUtil.PawnEffectiveness(p);
                if (eff <= 0) continue;
                cost += p.MarketValue * eff;
            }
            return LevelFromCost(cost);
        }

        private static SquadPower ComputeBasePower(MercenarySquadFC squad)
        {
            // Use effectiveness-weighted cost so squad combat power scales with pawn health
            // (downed pawns count as empty slots; injured pawns contribute reduced shares).
            // Cost displays (deployment / upgrade UI) still call GetCurrentLoadoutCost.
            double level = LevelFromCost(squad.GetEffectiveLoadoutCost());
            double efficiency = 1.0;
            FactionFC faction = FindFC.FactionComp;
            if (faction is object && squad.settlement is object)
            {
                // squad context lets per-squad (design + accolade) combat-efficiency modifiers fold in
                efficiency = faction.GetStatValue(FCStatDefOf.militaryCombatEfficiency, squad.settlement, squad);
                // squad-scoped militaryBaseLevel delta (this squad's own modifiers only — the faction/settlement
                // contribution stays in the settlement's military level, which squad forces don't otherwise use).
                level += faction.GetSquadStatValue(FCStatDefOf.militaryBaseLevel, squad);
            }
            return new SquadPower(level, efficiency);
        }

        private static List<ISquadPowerModifier> GetSortedModifiers()
        {
            if (_sortedCache != null) return _sortedCache;
            List<ISquadPowerModifier> copy = new List<ISquadPowerModifier>(_modifiers);
            copy.Sort((a, b) => b.Priority.CompareTo(a.Priority)); // highest first
            _sortedCache = copy;
            return _sortedCache;
        }
    }
}
