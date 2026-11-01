using RimWorld;
using System;
using System.Collections.Generic;
using Verse;

namespace FactionColonies
{
    public enum BillPenaltyStat
    {
        Happiness = 0,
        Unrest = 1,
        Loyalty = 2,
        Prosperity = 3,
    }

    public class BillStatPenalty : IExposable
    {
        public BillPenaltyStat stat;
        public double amount;

        public BillStatPenalty() { }

        public BillStatPenalty(BillPenaltyStat stat, double amount)
        {
            this.stat = stat;
            this.amount = amount;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref stat, "stat");
            Scribe_Values.Look(ref amount, "amount", 0.0);
        }
    }

    public class BillFC : ILoadReferenceable, IExposable
    {
        /* Default lifespan for tax bills (5 in-game days). Surfaced as a const so
         * squad deployment bill creation can override it via FCSettings.deploymentBillLifespan_days. */
        public const int DefaultLifespanTicks = GenDate.TicksPerDay * 5;

        //internal variables
        public int loadID;
        public int dueTick;
        public string label;
        public List<BillStatPenalty> unpaidPenalties = new List<BillStatPenalty>();
        public List<BillStatPenalty> latePaidPenalties = new List<BillStatPenalty>();

        //ref
        public WorldSettlementFC settlement;
        public TaxesFC taxes;


        public void ExposeData()
        {
            Scribe_Values.Look(ref loadID, "loadID", -1);
            Scribe_Values.Look(ref dueTick, "dueTick", -1);
            Scribe_Values.Look(ref label, "label");
            Scribe_Collections.Look(ref unpaidPenalties, "unpaidPenalties", LookMode.Deep);
            Scribe_Collections.Look(ref latePaidPenalties, "latePaidPenalties", LookMode.Deep);

            Scribe_References.Look(ref settlement, "settlement");
            Scribe_Deep.Look(ref taxes, "taxes");

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (unpaidPenalties is null) unpaidPenalties = new List<BillStatPenalty>();
                if (latePaidPenalties is null) latePaidPenalties = new List<BillStatPenalty>();
            }
        }

        public string GetUniqueLoadID()
        {
            return "Bill_" + loadID;
        }

        public BillFC()
        {

        }

        public BillFC(WorldSettlementFC settlement)
            : this(settlement, DefaultLifespanTicks) { }

        public BillFC(WorldSettlementFC settlement, int lifespanTicks)
        {
            SetUniqueLoadID();
            this.settlement = settlement;
            dueTick = Find.TickManager.TicksGame + lifespanTicks;
            taxes = new TaxesFC(this);
        }

        public void SetUniqueLoadID()
        {
            FactionFC comp = FindFC.FactionComp;
            if (comp is object)
            {
                loadID = comp.taxLedger.NextBillId();
            }
            else
            {
                loadID = Rand.Int;
                LogUtil.Error($"BillFC.SetUniqueLoadID: FactionComp is null. Assigned fallback loadID {loadID}.");
            }
        }

        /*-*-*- Penalty helpers -*-*-*/

        /* Direct: store the raw amount as a positive magnitude. Polarity is applied
         * at penalty time by ApplyUnpaidPenalties / ApplyLatePaidPenalties below. */
        public void AddUnpaidPenalty(BillPenaltyStat stat, double amount)
        {
            unpaidPenalties.Add(new BillStatPenalty(stat, amount));
        }

        public void AddLatePaidPenalty(BillPenaltyStat stat, double amount)
        {
            latePaidPenalties.Add(new BillStatPenalty(stat, amount));
        }

        /* Scaled: linear in (silver / settlement income), computed at construction time
         * and frozen on the bill. Caller MUST set taxes.silverAmount before calling. */
        public void AddUnpaidPenaltyScaled(BillPenaltyStat stat, double k)
        {
            unpaidPenalties.Add(new BillStatPenalty(stat, ScalePenalty(k)));
        }

        public void AddLatePaidPenaltyScaled(BillPenaltyStat stat, double k)
        {
            latePaidPenalties.Add(new BillStatPenalty(stat, ScalePenalty(k)));
        }

        private double ScalePenalty(double k)
        {
            double income = settlement?.totalIncome ?? 0;
            return k * Math.Abs(taxes.silverAmount) / Math.Max(1.0, income);
        }

        /*-*-*- Penalty application -*-*-*/

        /// <summary>Applies this bill's unpaid penalties to its settlement and shows the
        /// "not enough silver" message. Called when the bill expires without being resolved.</summary>
        public void ApplyUnpaidPenalties()
        {
            if (settlement is null)
            {
                LogUtil.Warning("BillFC.ApplyUnpaidPenalties: bill has null settlement (loadID=" + loadID + "). Skipping penalty.");
                return;
            }
            string penaltyDesc = ApplyAndDescribe(unpaidPenalties);
            string message = "FCNotEnoughSilverForBill".Translate(settlement.Name);
            if ((taxes?.itemTithes?.Count ?? 0) > 0)
                message += $" {"FCConfiscatedTithes".Translate()}";
            message += $" {penaltyDesc}.";
            Messages.Message(new Message(message, MessageTypeDefOf.NegativeEvent));
        }

        /// <summary>Applies this bill's late-paid penalties to its settlement. No message
        /// (callers batch a per-cycle letter — see TaxLedger.ProcessBills).</summary>
        public void ApplyLatePaidPenalties()
        {
            if (settlement is null) return;
            ApplyAndDescribe(latePaidPenalties);
        }

        private string ApplyAndDescribe(List<BillStatPenalty> penalties)
        {
            if (penalties is null || penalties.Count == 0) return "";
            string desc = "";
            foreach (BillStatPenalty p in penalties)
            {
                if (desc.Length > 0) desc += ", ";
                desc += ApplyOne(p);
            }
            return desc;
        }

        private string ApplyOne(BillStatPenalty p)
        {
            double gain = 0;
            string penalty = "";
            switch (p.stat)
            {
                case BillPenaltyStat.Unrest:
                    gain = settlement.GainUnrest(p.amount);
                    penalty = $"{TextUtil.ColorizeAdditiveBonus(gain, true)} {"FCUnrest".Translate()}";
                    break;
                case BillPenaltyStat.Happiness:
                    gain = settlement.GainHappiness(-p.amount);
                    penalty = $"{TextUtil.ColorizeAdditiveBonus(gain)} {"FCHappiness".Translate()}";
                    break;
                case BillPenaltyStat.Loyalty:
                    gain = settlement.GainLoyalty(-p.amount);
                    penalty = $"{TextUtil.ColorizeAdditiveBonus(gain)} {"FCLoyality".Translate()}";
                    break;
                case BillPenaltyStat.Prosperity:
                    gain = settlement.GainProsperity(-p.amount);
                    penalty = $"{TextUtil.ColorizeAdditiveBonus(gain)} {"FCProsperity".Translate()}";
                    break;
            }
            return penalty;
        }

        /*-*-*- Resolution -*-*-*/

        public bool Resolve()
        {
            if (AttemptResolve()) return true;

            ApplyUnpaidPenalties();
            FindFC.TaxLedger?.RemoveBill(this);
            return false;
        }

        public bool AttemptResolve()
        {
            FactionFC factionfc = FindFC.FactionComp;
            if (taxes.silverAmount >= 0 || PaymentUtil.CanAfford((int)(-1 * taxes.silverAmount), PaymentUtil.Reason_TaxPayment, settlement))
            { //if the payment can be afforded (crediting this settlement's financing, if any) & map belongs to player

                FCEventMaker.CreateTaxEvent(this);
                if (taxes.resourcePools.Count > 0)
                {
                    factionfc.AddResourcePools(taxes.resourcePools);
                }

                return true;

            }

            return false;
        }
    }
}
