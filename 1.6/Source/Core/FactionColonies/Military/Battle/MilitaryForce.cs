using RimWorld;
using System;
using Verse;

namespace FactionColonies
{
    public class MilitaryForce : IExposable
    {
        public double militaryLevel;
        public double militaryEfficiency;
        public double forceRemaining;
        public int random;
        public WorldSettlementFC homeSettlement;
        public Faction homeFaction;

        /// <summary>forceRemaining with defender advantage applied (for display).</summary>
        public double DefensivePower => Math.Round(forceRemaining * FCSettings.defenderAdvantage);

        public void ExposeData()
        {
            Scribe_Values.Look(ref militaryLevel, "militaryLevel");
            Scribe_Values.Look(ref militaryEfficiency, "militaryEfficiency");
            Scribe_Values.Look(ref forceRemaining, "forceRemaining");
            Scribe_Values.Look(ref random, "random");
            Scribe_References.Look(ref homeSettlement, "homeSettlement");
            Scribe_References.Look(ref homeFaction, "homeFaction");
        }

        public MilitaryForce()
        {
        }

        public MilitaryForce(double militaryLevel, double militaryEfficiency, WorldSettlementFC homeSettlement, Faction homeFaction)
        {
            this.militaryLevel = militaryLevel;
            this.militaryEfficiency = militaryEfficiency;
            this.homeSettlement = homeSettlement;
            this.homeFaction = homeFaction;
            forceRemaining = Math.Max(1, Math.Round(militaryLevel * militaryEfficiency));
        }

        /// <summary>Creates the force that this <paramref name="squad"/> projects. Reads the squad's
        /// power via <see cref="SquadPowerRegistry"/> (loadout-cost-derived militaryLevel +
        /// combat efficiency from the billet), then applies faction-level isAttacking /
        /// isDefending bonuses. The defending force is squad-only: the settlement being
        /// defended contributes nothing. Returns null if the squad is unassigned (no billet
        /// to anchor the force).</summary>
        public static MilitaryForce CreateMilitaryForceFromSquad(MercenarySquadFC squad, bool isAttacking = false)
        {
            if (squad?.settlement is null) return null;

            SquadPower power = SquadPowerRegistry.Resolve(squad);
            return CombineForce(power.militaryLevel, power.militaryEfficiency, squad.settlement, isAttacking, squad);
        }

        /// <summary>Half-power synthetic force for a settlement with squad capacity but no
        /// stationed squad. Lets empty billets still participate in the squad-driven battle
        /// pipeline at a reduced effectiveness, while <see cref="WorldSettlementFC.SquadCap"/>
        /// of 0 (structurally non-military) yields null. Power is the squad-equivalent of the
        /// settlement's military level — half of what a fully-kitted squad at that level would
        /// project.</summary>
        public static MilitaryForce CreateMilitaryForceFromUnstaffedBillet(WorldSettlementFC settlement, bool isAttacking = false)
        {
            if (settlement is null) return null;
            if (settlement.SquadCap <= 0) return null;

            double level = Math.Max(1, settlement.settlementMilitaryLevel) * 0.5;
            double efficiency = 1.0;
            FactionFC faction = FindFC.FactionComp;
            if (faction is object)
            {
                efficiency = faction.GetStatValue(FCStatDefOf.militaryCombatEfficiency, settlement);
            }
            return CombineForce(level, efficiency, settlement, isAttacking);
        }

        public static MilitaryForce CreateMilitaryForceFromSettlement(WorldSettlementFC settlement, bool isAttacking = false)
        {
            double reinforcerLevel = settlement.settlementMilitaryLevel;
            double reinforcerEff = settlement.GetStatValue(FCStatDefOf.militaryCombatEfficiency);
            return CombineForce(reinforcerLevel, reinforcerEff, settlement, isAttacking);
        }

        /* Shared force assembly: take the reinforcer's level/efficiency and apply the
         * faction's attacking/defending stat bonuses. Used by every public force factory
         * so the bonus chain stays in one place. */
        private static MilitaryForce CombineForce(double reinforcerLevel, double reinforcerEff,
            WorldSettlementFC anchorSettlement, bool isAttacking,
            MercenarySquadFC squad = null)
        {
            FactionFC faction = FindFC.FactionComp;

            double combinedLevel = reinforcerLevel;
            double blendedEff = reinforcerEff;

            if (faction is object)
            {
                // squad context folds per-squad (design + accolade) bonuses on top of the faction values.
                // For defending, the anchor settlement also folds in (biome + governor/specialist
                // defensive bonuses are settlement-scoped); GetStatValue no-ops a null settlement.
                if (isAttacking)
                {
                    combinedLevel += faction.GetStatValue(FCStatDefOf.militaryLevelBonusAttacking, null, squad);
                    blendedEff *= faction.GetStatValue(FCStatDefOf.militaryEfficiencyBonusAttacking, null, squad);
                }
                else
                {
                    combinedLevel += faction.GetStatValue(FCStatDefOf.militaryLevelBonusDefending, anchorSettlement, squad);
                    blendedEff *= faction.GetStatValue(FCStatDefOf.militaryEfficiencyBonusDefending, anchorSettlement, squad);
                }
            }

            return new MilitaryForce(combinedLevel, blendedEff, anchorSettlement, FindFC.EmpireFaction);
        }

    }
}