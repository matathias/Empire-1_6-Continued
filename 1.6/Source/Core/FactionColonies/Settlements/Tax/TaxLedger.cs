using System;
using System.Collections.Generic;
using System.Linq;
using FactionColonies.util;
using RimWorld;
using Verse;

namespace FactionColonies
{
    /* Owns the faction's tax/billing state and the tax-cycle orchestration.
     * Lives on FactionFC.taxLedger.
     *
     * State persisted via nested <taxLedger> Scribe element. Legacy flat fields
     * (Bills, OldBills, taxTimeDue, autoResolveBills, allowLatePayments, nextTaxID,
     * nextBillID) on old saves are migrated by FactionFC.ExposeData's migration
     * shim, which calls SeedFromLegacy() once during ResolvingCrossRefs. */
    public class TaxLedger : IExposable
    {
        /* Owned state. UI checkbox widgets pass these by ref (autoResolve,
         * allowLatePayments) — that's why they're public fields, not properties. */
        internal List<BillFC> bills = new List<BillFC>();
        internal List<BillFC> oldBills = new List<BillFC>();
        public bool autoResolve;
        public bool allowLatePayments = true;
        public int nextTaxDueTick = Find.TickManager.TicksGame;
        internal int nextTaxId = 1;
        internal int nextBillId = 1;

        /* Read-only facade for external callers. */
        public IReadOnlyList<BillFC> Bills => bills;
        public IReadOnlyList<BillFC> OldBills => oldBills;

        /* Mutation */
        public void AddBill(BillFC bill)
        {
            if (bill is null) return;
            bills.Add(bill);
        }

        public bool RemoveBill(BillFC bill)
        {
            if (bill is null) return false;
            return bills.Remove(bill);
        }

        public int RemoveBillsWhere(Predicate<BillFC> match)
        {
            if (match is null) return 0;
            int removed = 0;
            for (int i = bills.Count - 1; i >= 0; i--)
            {
                if (!match(bills[i])) continue;
                bills.RemoveAt(i);
                removed++;
            }
            return removed;
        }

        public void ArchiveResolved(BillFC bill)
        {
            if (bill is null) return;
            bills.Remove(bill);
            oldBills.Add(bill);
        }

        public void ClearAllBills() => bills.Clear();
        public void ClearOldBills() => oldBills.Clear();

        /* ID generators */
        public int NextTaxId() => ++nextTaxId;
        public int NextBillId() => ++nextBillId;

        public void Reschedule(int ticksFromNow) =>
            nextTaxDueTick = Find.TickManager.TicksGame + ticksFromNow;

        /* Tax cycle. Called from FactionFC.WorldComponentTick. */
        public void TaxTick(FactionFC faction, Faction colonyFaction)
        {
            if (faction is null || colonyFaction is null) return;
            if (Find.TickManager.TicksGame < nextTaxDueTick) return;

            AddTax(faction);
            nextTaxDueTick = Find.TickManager.TicksGame + FCSettings.timeBetweenTaxes;

            if (autoResolve)
                AutoresolveBills();

            /* Auto-replace fallen squad mercs from empire silver (opt-in; no-op when off). */
            faction.military?.TryAutoReplaceAllSquads();

            /* Rebuild caravan trader kinds to reflect current worker assignments. */
            faction.RebuildCaravanTraderKinds();
        }

        public void AddTax(FactionFC faction)
        {
            if (faction is null) return;

            LogUtil.Message($"AddTax at tick {Find.TickManager.TicksGame}: settlements={faction.settlements.Count}, timeBetweenTaxes={FCSettings.timeBetweenTaxes}");
            TaxTickRegistry.InvokePreTaxResolution(faction);

            foreach (ResourcePool pool in faction.resourcePools)
            {
                if (pool.resource.PoolResourceResetsAtTaxTime())
                    pool.pool = 0;
            }

            if (faction.settlements.Count != 0)
            {
                foreach (WorldSettlementFC settlement in faction.settlements)
                {
                    faction.AddExperienceToFactionLevel(2f);

                    List<Thing> list = settlement.CreateTax(out int silverAmount);
                    List<ResourcePool> billResourcePools = settlement.CreateResourcePools();

                    BillFC bill = new BillFC(settlement);
                    bill.label = "FCBillKindTax".Translate();
                    bill.taxes.resourcePools = billResourcePools;
                    bill.taxes.itemTithes.AddRange(list);
                    bill.taxes.silverAmount = silverAmount;
                    bill.AddUnpaidPenalty(BillPenaltyStat.Unrest, 10);
                    bill.AddUnpaidPenalty(BillPenaltyStat.Happiness, 10);
                    bill.AddLatePaidPenalty(BillPenaltyStat.Unrest, 4);
                    bill.AddLatePaidPenalty(BillPenaltyStat.Happiness, 4);

                    bills.Add(bill);

                    TextUtil.GetTownTitle(settlement);
                    FindFC.PolicyManager.ForEachBehavior(b => b.OnTaxCollected(faction, settlement));
                }

                Find.LetterStack.ReceiveLetter("FCTaxesBilledShort".Translate(), "FCTaxesBilledDesc".Translate(),
                    LetterDefOf.PositiveEvent);
                faction.DirtyFactionProfitCache();
            }
            else
            {
                Messages.Message("FCNoSettlementsToTax".Translate(), MessageTypeDefOf.NeutralEvent);
            }

            /* Deduct edict upkeep */
            int edictUpkeep = FindFC.PolicyManager.GetEdictUpkeep();
            if (edictUpkeep > 0)
            {
                if (!PaymentUtil.TryPaySilver(edictUpkeep, "EdictUpkeep"))
                {
                    FindFC.PolicyManager.RevokeAllEdicts();
                    Messages.Message("FCEdictUpkeepUnpaid".Translate(), MessageTypeDefOf.NegativeEvent);
                }
            }

            TaxTickRegistry.InvokePostTaxResolution(faction);
        }

        /*-*-*- Bill processing -*-*-*/

        /// <summary>Rare-tick scan: resolves overdue bills, applies late-paid penalties
        /// (with a batched letter) or unpaid penalties + message, and removes resolved bills.
        /// Called from FactionFC.WorldComponentTick.</summary>
        public void ProcessBills()
        {
            List<WorldSettlementFC> latePaidSettlements = new List<WorldSettlementFC>();
            int currentTick = Find.TickManager.TicksGame;

            for (int i = bills.Count - 1; i >= 0; i--)
            {
                BillFC bill = bills[i];
                if (bill.dueTick >= currentTick) continue;

                /* taxes is null on a default-constructed BillFC; a bill with no taxes owes nothing. */
                bool owedMoney = bill.taxes is object && bill.taxes.silverAmount < 0;
                WorldSettlementFC settlement = bill.settlement;

                /* When the player has disabled late payments, owed-silver bills skip
                 * the resolve attempt entirely and go straight to the unpaid penalty
                 * path. Positive-silver bills (tax tributes the player would gain
                 * from) always auto-resolve at expiry -- there's nothing to defer. */
                bool skipAttempt = !allowLatePayments && owedMoney;

                if (!skipAttempt && bill.AttemptResolve())
                {
                    if (owedMoney && settlement is object)
                    {
                        bill.ApplyLatePaidPenalties();
                        latePaidSettlements.Add(settlement);
                    }
                }
                else
                {
                    bill.ApplyUnpaidPenalties();
                    RemoveBill(bill);
                }
            }

            if (latePaidSettlements.Count > 0)
            {
                string settlementList = string.Join("\n", latePaidSettlements.Select(s => "  - " + s.Name));
                Find.LetterStack.ReceiveLetter(
                    "FCLateBillAutoPaidLabel".Translate(),
                    "FCLateBillAutoPaidDesc".Translate(latePaidSettlements.Count, settlementList),
                    LetterDefOf.NegativeEvent);
            }
        }

        /// <summary>Net positive-silver bills against negative-silver bills, then resolve
        /// any remainder. Called from <see cref="TaxTick"/> when <see cref="autoResolve"/>
        /// is on, and from the manual auto-resolve toggle in MainTabWindow_Colony.</summary>
        public void AutoresolveBills()
        {
            int resolvedBills = 0;
            (List<BillFC> negativeBills, List<BillFC> positiveBills) = ReturnBillTypes();

            int i = 0;
            int maxOuterIterations = bills.Count * bills.Count + 1;
            int outerIterations = 0;
            while (i < negativeBills.Count)
            {
                if (++outerIterations > maxOuterIterations)
                {
                    LogUtil.Error($"AutoresolveBills: exceeded {maxOuterIterations} outer iterations. Bailing out to prevent freeze.");
                    break;
                }
                BillFC negativeBill = negativeBills[i];
                bool matched = false;
                int j = 0;
                int maxInnerIterations = bills.Count * 2 + 1;
                int innerIterations = 0;
                while (j < positiveBills.Count)
                {
                    if (++innerIterations > maxInnerIterations)
                    {
                        LogUtil.Error("AutoresolveBills: exceeded max inner iterations. Bailing out to prevent freeze.");
                        break;
                    }
                    BillFC positiveBill = positiveBills[j];
                    float result = positiveBill.taxes.silverAmount + negativeBill.taxes.silverAmount;
                    if (result == 0)
                    {
                        /* Bills cancel each other out — resolve both, restart outer */
                        positiveBill.taxes.silverAmount = 0;
                        negativeBill.taxes.silverAmount = 0;
                        positiveBill.Resolve();
                        negativeBill.Resolve();
                        resolvedBills += 2;
                        (negativeBills, positiveBills) = ReturnBillTypes();
                        i = 0;
                        matched = true;
                        break;
                    }
                    else if (result > 0)
                    {
                        /* Positive bill covers the negative — resolve negative, restart outer */
                        positiveBill.taxes.silverAmount = result;
                        negativeBill.taxes.silverAmount = 0;
                        negativeBill.Resolve();
                        resolvedBills++;
                        (negativeBills, positiveBills) = ReturnBillTypes();
                        i = 0;
                        matched = true;
                        break;
                    }
                    else /* result < 0 */
                    {
                        /* Negative exceeds positive — resolve positive, continue inner */
                        positiveBill.taxes.silverAmount = 0;
                        negativeBill.taxes.silverAmount = result;
                        positiveBill.Resolve();
                        resolvedBills++;
                        (negativeBills, positiveBills) = ReturnBillTypes();
                        j = 0;
                        continue;
                    }
                }

                if (!matched)
                {
                    if (negativeBill.AttemptResolve())
                    {
                        (negativeBills, positiveBills) = ReturnBillTypes();
                        resolvedBills++;
                    }
                    else
                    {
                        i++;  /* Only skip bills that genuinely can't be resolved */
                    }
                }
            }

            /* Resolve remaining positive bills */
            foreach (BillFC positiveBill in new List<BillFC>(positiveBills))
            {
                positiveBill.Resolve();
                resolvedBills++;
            }

            Messages.Message("FCNumberTaxesHasBeenSolved".Translate(resolvedBills), MessageTypeDefOf.NeutralEvent);
        }

        private (List<BillFC>, List<BillFC>) ReturnBillTypes()
        {
            List<BillFC> positive = new List<BillFC>();
            List<BillFC> negative = new List<BillFC>();
            foreach (BillFC b in bills)
            {
                if (b.taxes.silverAmount >= 0) positive.Add(b);
                else negative.Add(b);
            }
            return (negative, positive);
        }

        /*-*-*- Bill factories -*-*-*/

        /// <summary>Creates a deployment-cost bill against the squad's home settlement
        /// and adds it to this ledger. No-op when cost is zero (slider at 0%), godMode is
        /// on, or the squad has no home settlement.</summary>
        public BillFC CreateDeploymentCostBill(MercenarySquadFC squad)
        {
            if (squad is null) return null;
            int cost = squad.DeploymentCost();
            if (cost <= 0 || DebugSettings.godMode) return null;

            WorldSettlementFC home = squad.settlement;
            if (home is null)
            {
                LogUtil.Warning($"TaxLedger.CreateDeploymentCostBill: squad {squad.GetUniqueLoadID()} has no home settlement; skipping bill.");
                return null;
            }

            int lifespanTicks = Math.Max(1, FCSettings.deploymentBillLifespan_days) * GenDate.TicksPerDay;
            BillFC bill = new BillFC(home, lifespanTicks);
            bill.label = "FCBillKindSquadDeployment".Translate();
            bill.taxes.silverAmount = -cost;
            /* silverAmount must be set BEFORE the Scaled helpers — they read it for
             * the linear scaling computation. */
            bill.AddUnpaidPenaltyScaled(BillPenaltyStat.Unrest, 25);
            bill.AddUnpaidPenaltyScaled(BillPenaltyStat.Happiness, 25);
            bill.AddLatePaidPenaltyScaled(BillPenaltyStat.Unrest, 5);
            bill.AddLatePaidPenaltyScaled(BillPenaltyStat.Happiness, 5);
            AddBill(bill);
            return bill;
        }

        /// <summary>Creates a restock-cost bill against the squad's home settlement for carried
        /// inventory consumed in a manual battle (see <see cref="SquadRestockUtil"/>) and adds it to
        /// this ledger. No-op when cost is non-positive, godMode is on, or the squad has no home
        /// settlement. Cost is computed by the caller; this just authors the bill.</summary>
        public BillFC CreateRestockCostBill(MercenarySquadFC squad, int cost)
        {
            if (squad is null || cost <= 0 || DebugSettings.godMode) return null;

            WorldSettlementFC home = squad.settlement;
            if (home is null)
            {
                LogUtil.Warning($"TaxLedger.CreateRestockCostBill: squad {squad.GetUniqueLoadID()} has no home settlement; skipping bill.");
                return null;
            }

            int lifespanTicks = Math.Max(1, FCSettings.deploymentBillLifespan_days) * GenDate.TicksPerDay;
            BillFC bill = new BillFC(home, lifespanTicks);
            bill.label = "FCBillKindSquadRestock".Translate();
            bill.taxes.silverAmount = -cost;
            /* silverAmount must be set BEFORE the Scaled helpers — they read it for
             * the linear scaling computation. */
            bill.AddUnpaidPenaltyScaled(BillPenaltyStat.Unrest, 15);
            bill.AddUnpaidPenaltyScaled(BillPenaltyStat.Happiness, 15);
            bill.AddLatePaidPenaltyScaled(BillPenaltyStat.Unrest, 3);
            bill.AddLatePaidPenaltyScaled(BillPenaltyStat.Happiness, 3);
            AddBill(bill);
            return bill;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref nextTaxDueTick, "nextTaxDueTick", Find.TickManager.TicksGame);
            Scribe_Values.Look(ref autoResolve, "autoResolve");
            Scribe_Values.Look(ref allowLatePayments, "allowLatePayments", true);
            Scribe_Values.Look(ref nextTaxId, "nextTaxId", 1);
            Scribe_Values.Look(ref nextBillId, "nextBillId", 1);
            Scribe_Collections.Look(ref bills, "bills", LookMode.Deep);
            Scribe_Collections.Look(ref oldBills, "oldBills", LookMode.Deep);
            if (bills is null) bills = new List<BillFC>();
            if (oldBills is null) oldBills = new List<BillFC>();
        }

        /* True iff the ledger is at first-construction defaults. Used by the
         * FactionFC migration shim to decide whether to seed from legacy flat fields. */
        public bool IsEmpty =>
            bills.Count == 0 && oldBills.Count == 0
            && nextTaxId == 1 && nextBillId == 1;

        /* One-shot seed from FactionFC's legacy flat scribe fields. Called from
         * FactionFC.ExposeData during ResolvingCrossRefs when an old-format save
         * is loaded. Any field whose legacy value is at its sentinel (-1 for ints,
         * default for bools) is left at the ledger's default. */
        public void SeedFromLegacy(List<BillFC> legacyBills, List<BillFC> legacyOldBills,
            bool legacyAutoResolve, bool legacyAllowLate,
            int legacyTaxTimeDue, int legacyNextTaxId, int legacyNextBillId)
        {
            if (legacyBills != null) bills = legacyBills;
            if (legacyOldBills != null) oldBills = legacyOldBills;
            autoResolve = legacyAutoResolve;
            allowLatePayments = legacyAllowLate;
            if (legacyTaxTimeDue != -1) nextTaxDueTick = legacyTaxTimeDue;
            if (legacyNextTaxId != -1) nextTaxId = legacyNextTaxId;
            if (legacyNextBillId != -1) nextBillId = legacyNextBillId;
        }
    }
}
