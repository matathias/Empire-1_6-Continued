using System;

namespace FactionColonies
{
    /* Cost calculations for a mercenary squad. */
    public static class SquadCostExtensions
    {
        /// <summary>Sum of equipment market values across all currently-equipped mercenaries,
        /// read from each merc's <see cref="Mercenary.EffectiveLoadout"/> . Changes to the unit
        /// template after a hire/fill/upgrade are not reflected here — only what was applied to
        /// the pawn.</summary>
        public static double GetCurrentLoadoutCost(this MercenarySquadFC squad)
        {
            double total = 0;
            if (squad?.mercenaries is null) return total;
            foreach (Mercenary merc in squad.mercenaries)
            {
                if (merc is null || merc.IsEmptySlot) continue;
                if (merc.pawn is object && merc.pawn.Dead) continue;
                MilUnitFC current = merc.EffectiveLoadout;
                if (current is null) continue;
                total += current.getTotalCost;
            }
            return total;
        }

        /// <summary>Loadout cost weighted by per-pawn combat effectiveness — a downed pawn
        /// contributes 0 (effectively an empty slot for power-projection), an injured pawn
        /// contributes a fraction (worst of consciousness/manipulation/moving), and a healthy
        /// pawn contributes their full loadout value. Used by
        /// <see cref="SquadPowerRegistry.ComputeBasePower"/> so squad combat power scales with
        /// pawn health. Cost displays (deployment / upgrade / inspection) keep using
        /// <see cref="GetCurrentLoadoutCost"/>; only the power projection cares about health.</summary>
        public static double GetEffectiveLoadoutCost(this MercenarySquadFC squad)
        {
            double total = 0;
            if (squad?.mercenaries is null) return total;
            foreach (Mercenary merc in squad.mercenaries)
            {
                if (merc is null || merc.IsEmptySlot) continue;
                MilUnitFC current = merc.EffectiveLoadout;
                if (current is null) continue;
                double effectiveness = SquadEffectivenessUtil.PawnEffectiveness(merc.pawn);
                if (effectiveness <= 0) continue;
                total += current.getTotalCost * effectiveness;
            }
            return total;
        }

        /// <summary>Silver cost to deploy this squad on an offensive op or to a player map.
        /// Computed as <c>FCSettings.squadDeploymentCostPercentage * GetCurrentLoadoutCost</c>,
        /// rounded to int.</summary>
        public static int DeploymentCost(this MercenarySquadFC squad)
        {
            return MilitaryDeploymentUtil.CalculateDeploymentCost(squad.GetCurrentLoadoutCost());
        }

        /// <summary>Total silver to refill all fillable empty slots, summed over each slot's
        /// blueprint cost * <see cref="FCSettings.squadHireCostMultiplier"/>. The blueprint
        /// is the merc's <see cref="Mercenary.BlueprintLoadout"/> (personalization snapshot
        /// or pool reference). Slots whose blueprint is null or blank contribute zero — they
        /// are pure placeholders kept around to keep slot indices aligned with the template.</summary>
        public static int FillEmptySlotsCost(this MercenarySquadFC squad)
        {
            int total = 0;
            if (squad?.mercenaries is null) return total;
            foreach (Mercenary m in squad.mercenaries)
            {
                if (m is null || !m.IsEmptySlot) continue;
                MilUnitFC blueprint = m.BlueprintLoadout;
                if (blueprint is null || blueprint.isBlank) continue;
                total += (int)Math.Round(blueprint.getTotalCost * FCSettings.squadHireCostMultiplier);
            }
            // Folded-in: every Missing sub-pawn of a live merc is restored by the same Fill action.
            foreach (Mercenary sub in squad.MissingSubPawns())
                total += SubPawnReplaceCost(sub);
            return total;
        }

        /// <summary>Silver to recreate one dead sub-pawn (animal or mech), mirroring the design's
        /// per-unit cost: market value * the matching cost multiplier * <see cref="FCSettings.squadHireCostMultiplier"/>.
        /// The flat mechlink surcharge is excluded — the link persists on the (still-present) mechanitor.</summary>
        public static int SubPawnReplaceCost(Mercenary sub)
        {
            if (sub?.subPawnKind?.race is null) return 0;
            double mult = sub.subPawnType == Mercenary.SubPawnType.Mech
                ? FCSettings.militaryMechCostMultiplier
                : FCSettings.militaryAnimalCostMultiplier;
            double raw = Math.Floor(sub.subPawnKind.race.BaseMarketValue * mult);
            return (int)Math.Round(raw * FCSettings.squadHireCostMultiplier);
        }
    }
}
