using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace FactionColonies
{
    public class FCPolicyBehavior_Egalitarian : FCPolicyBehavior
    {
        private Dictionary<PlanetTile, TaxBreakData> taxBreaks = new Dictionary<PlanetTile, TaxBreakData>();

        public override double ModifyStat(FCStatDef stat, double currentValue, WorldSettlementFC settlement)
        {
            if (settlement == null) return currentValue;
            if (!IsOnTaxBreak(settlement.Tile)) return currentValue;

            var ext = Ext<FCPolicyBehaviorExt_Egalitarian>();

            if (stat == FCStatDefOf.taxBonusFlat)
                return currentValue - ext.taxBreakPenalty;
            if (stat == FCStatDefOf.happinessGainedBase)
                return currentValue + ext.taxBreakHappinessBonus;
            if (stat == FCStatDefOf.prosperityGainedBase)
                return currentValue + ext.taxBreakProsperityBonus;

            return currentValue;
        }

        public override string GetStatDescription(FCStatDef stat, WorldSettlementFC settlement)
        {
            if (settlement == null) return null;
            if (!IsOnTaxBreak(settlement.Tile)) return null;

            var ext = Ext<FCPolicyBehaviorExt_Egalitarian>();

            if (stat == FCStatDefOf.taxBonusFlat)
                return TextUtil.AdditiveBonusLine(-ext.taxBreakPenalty, policy.def.LabelCap) + "\n";
            if (stat == FCStatDefOf.happinessGainedBase)
                return TextUtil.AdditiveBonusLine(ext.taxBreakHappinessBonus, policy.def.LabelCap) + "\n";
            if (stat == FCStatDefOf.prosperityGainedBase)
                return TextUtil.AdditiveBonusLine(ext.taxBreakProsperityBonus, policy.def.LabelCap) + "\n";

            return null;
        }

        public override IEnumerable<FloatMenuOption> GetSettlementActions(FactionFC faction, WorldSettlementFC settlement)
        {
            var ext = Ext<FCPolicyBehaviorExt_Egalitarian>();
            yield return new FloatMenuOption("FCGiveTaxBreak".Translate(), delegate
            {
                if (!IsOnTaxBreak(settlement.Tile))
                {
                    Find.WindowStack.Add(new FCWindow_Confirm(
                        "FCConfirmTaxBreak".Translate(),
                        () =>
                        {
                            var data = GetOrCreate(settlement.Tile);
                            data.startTick = Find.TickManager.TicksGame;
                            data.enabled = true;
                            settlement.InvalidateStatCache();
                            Messages.Message("FCGivingTaxBreak".Translate(settlement.Name), MessageTypeDefOf.NeutralEvent);
                        }));
                }
                else
                {
                    var data = GetOrCreate(settlement.Tile);
                    Messages.Message(
                        "FCAlreadyGivingTaxBreak".Translate(Math.Round(
                            (data.startTick + GenDate.TicksPerDay * ext.taxBreakDurationDays -
                                Find.TickManager.TicksGame) / (double)GenDate.TicksPerDay, 1)),
                        MessageTypeDefOf.RejectInput);
                }
            });
        }

        public override void Tick(FactionFC faction)
        {
            if (Find.TickManager.TicksGame % 250 != 0) return;
            int currentTick = Find.TickManager.TicksGame;
            int durationTicks = GenDate.TicksPerDay * Ext<FCPolicyBehaviorExt_Egalitarian>().taxBreakDurationDays;
            foreach (var kvp in taxBreaks)
            {
                if (kvp.Value.enabled && (kvp.Value.startTick + durationTicks) <= currentTick)
                {
                    kvp.Value.enabled = false;
                    faction.ReturnSettlementByLocation(kvp.Key)?.InvalidateStatCache();
                }
            }
        }

        private bool IsOnTaxBreak(PlanetTile tile) => taxBreaks.TryGetValue(tile, out var d) && d.enabled;

        private TaxBreakData GetOrCreate(PlanetTile tile)
        {
            if (!taxBreaks.TryGetValue(tile, out var data))
            {
                data = new TaxBreakData();
                taxBreaks[tile] = data;
            }
            return data;
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref taxBreaks, "taxBreaks", LookMode.Value, LookMode.Deep);
            taxBreaks = taxBreaks ?? new Dictionary<PlanetTile, TaxBreakData>();
        }

        // Debug accessors
        public int DebugTaxBreakCount() => taxBreaks.Count;
        public int DebugActiveTaxBreakCount() => taxBreaks.Count(kvp => kvp.Value.enabled);
    }
}
