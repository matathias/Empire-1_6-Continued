using System.Linq;
using Verse;

namespace FactionColonies
{
    /* DESTRUCTIVE: runs real tax cycles against the live ledger. Creates bills, spends/accrues
       silver, applies penalties, and may revoke edicts on unpaid upkeep. Not reverted. */
    public static class TaxEconomyDestructiveTests
    {
        [EmpireDestructiveTest("Destructive.Tax")]
        public static void AddTax_GeneratesBills()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();
            if (f.settlements.Count == 0) TestAssert.Skip("No settlements to tax");

            int before = FindFC.TaxLedger.Bills.Count;
            TestAssert.DoesNotThrow(() => FindFC.TaxLedger.AddTax(f), "AddTax threw");
            TestAssert.IsTrue(FindFC.TaxLedger.Bills.Count > before,
                "AddTax should add at least one bill when settlements exist");
            DestructiveTestUtil.AssertEmpireInvariants(f, "AddTax_GeneratesBills");
        }

        [EmpireDestructiveTest("Destructive.Tax")]
        public static void ProcessBills_AndAutoresolve_NoDangling()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();
            if (f.settlements.Count == 0) TestAssert.Skip("No settlements to tax");

            TestAssert.DoesNotThrow(() => FindFC.TaxLedger.AddTax(f), "AddTax threw");
            TestAssert.DoesNotThrow(() => FindFC.TaxLedger.AutoresolveBills(), "AutoresolveBills threw");
            TestAssert.DoesNotThrow(() => FindFC.TaxLedger.ProcessBills(), "ProcessBills threw");
            // AssertEmpireInvariants verifies no surviving bill references a removed settlement.
            DestructiveTestUtil.AssertEmpireInvariants(f, "ProcessBills_AndAutoresolve");
        }

        [EmpireDestructiveTest("Destructive.Tax")]
        public static void TaxTick_AfterReschedule_Advances()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();
            if (FindFC.EmpireFaction is null) TestAssert.Skip("No Empire faction");

            FindFC.TaxLedger.Reschedule(0); // due now
            int now = Find.TickManager.TicksGame;
            TestAssert.DoesNotThrow(() => FindFC.TaxLedger.TaxTick(f, FindFC.EmpireFaction), "TaxTick threw");
            TestAssert.GreaterThan(FindFC.TaxLedger.nextTaxDueTick, now,
                "TaxTick should reschedule nextTaxDueTick into the future");
            DestructiveTestUtil.AssertEmpireInvariants(f, "TaxTick_AfterReschedule");
        }

        [EmpireDestructiveTest("Destructive.Tax")]
        public static void AddTax_WithTransientSettlement_ThenRemove_BillCleared()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();

            WorldSettlementFC s = DestructiveTestUtil.CreateTransientSettlement();
            if (s is null) TestAssert.Skip("No valid tile found for a new settlement");

            TestAssert.DoesNotThrow(() => FindFC.TaxLedger.AddTax(f), "AddTax threw");
            // The new settlement should now have at least one bill.
            TestAssert.IsTrue(FindFC.TaxLedger.Bills.Any(b => b?.settlement == s),
                "New settlement should have a tax bill after AddTax");

            DestructiveTestUtil.SafeRemoveSettlement(s);
            TestAssert.IsFalse(FindFC.TaxLedger.Bills.Any(b => b?.settlement == s),
                "Removing the settlement should sweep its orphaned bills");
            DestructiveTestUtil.AssertEmpireInvariants(f, "AddTax_WithTransientSettlement_ThenRemove");
        }

        /* -*-*-*-  Random-tithe escrow accounting  -*-*-*- */

        /// <summary>
        /// Configures a fresh transient settlement so exactly one tithe-able resource drives the
        /// random-tithe path deterministically: all other resources are paused (their production
        /// becomes plain silver, no RNG goods), the target's specified tithes are cleared, and the
        /// target has workers so it actually produces. Returns null if no positive-production
        /// tithe-able resource could be prepared (caller skips).
        /// </summary>
        private static ResourceFC PrepareEscrowTarget(WorldSettlementFC s)
        {
            // Pick the tithe-able resource that produces the most per worker; pause all others.
            ResourceFC target = null;
            foreach (ResourceFC r in s.Resources)
            {
                if (!r.canTithe) continue;
                if (target is null || r.production > target.production) target = r;
            }
            if (target is null) return null;

            foreach (ResourceFC r in s.Resources)
                r.tithesPaused = !ReferenceEquals(r, target);

            for (int i = target.Tithes.Count - 1; i >= 0; i--) target.RemoveTitheAt(i);

            if (target.assignedWorkers == 0) s.IncreaseWorkers(target, 3);
            target.SetDirtyCache();
            s.DirtyProfitCache();
            return target.taxableProductionMarketValue > 0 ? target : null;
        }

        /// <summary>Zeroes one-time silver (for determinism), accrues 'days' identical daily cycles, then taxes.</summary>
        private static int AccrueAndTax(WorldSettlementFC s, int days)
        {
            s.oneTimeSilverIncome = 0;
            for (int d = 0; d < days; d++) s.AccumulateDailyProduction();
            s.CreateTax(out int silver);
            return silver;
        }

        [EmpireDestructiveTest("Destructive.Tax")]
        public static void RandomTitheRollover_WithholdsFromSilver_NotStoreAndPay()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();
            WorldSettlementFC s = DestructiveTestUtil.CreateTransientSettlement();
            if (s is null) TestAssert.Skip("No valid tile for a settlement");
            try
            {
                ResourceFC target = PrepareEscrowTarget(s);
                if (target is null) TestAssert.Skip("No positive-production tithe-able resource");

                const int days = 3;

                // Baseline: no random tithe, so the target's production is paid entirely as silver.
                target.hasRandomTithe = false;
                target.randomTitheStock = 0;
                target.disburseTitheStock = false;
                target.SetDirtyCache();
                int baselineSilver = AccrueAndTax(s, days);

                // Same production, now with a random tithe whose filter allows nothing -> the budget
                // deterministically rolls into escrow (no goods, no RNG, no ThingSetMaker).
                target.hasRandomTithe = true;
                target.autoMaxRandomTithe = true;
                target.randomTitheFilter.SetDisallowAll();
                target.randomTitheStock = 0;
                target.disburseTitheStock = false;
                target.SetDirtyCache();
                int rolloverSilver = AccrueAndTax(s, days);

                double escrow = target.randomTitheStock;
                TestAssert.GreaterThan(escrow, 0,
                    "Empty-filter random tithe should escrow its budget into randomTitheStock");

                // The escrowed value must be WITHHELD from silver exactly once: paid silver plus the
                // value now held in escrow reconciles to the baseline. The pre-fix bug both paid the
                // value as silver AND stored it, so rolloverSilver would equal baselineSilver and this
                // sum would overshoot the baseline by 'escrow'.
                TestAssert.AreEqual(baselineSilver, rolloverSilver + escrow, tolerance: 3,
                    message: $"rolloverSilver({rolloverSilver}) + escrow({escrow:F0}) should equal baseline({baselineSilver})");

                DestructiveTestUtil.AssertEmpireInvariants(f, "RandomTitheRollover_Withholds");
            }
            finally
            {
                DestructiveTestUtil.SafeRemoveSettlement(s);
            }
        }

        [EmpireDestructiveTest("Destructive.Tax")]
        public static void RandomTitheDisburse_PaysEscrowExactlyOnce()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();
            WorldSettlementFC s = DestructiveTestUtil.CreateTransientSettlement();
            if (s is null) TestAssert.Skip("No valid tile for a settlement");
            try
            {
                ResourceFC target = PrepareEscrowTarget(s);
                if (target is null) TestAssert.Skip("No positive-production tithe-able resource");

                const int days = 2;
                const double stock = 500;

                // No new random tithe; a fixed escrow already sits in stock (as if a prior rollover
                // withheld it). Disburse off -> stock is untouched and excluded from the baseline silver.
                target.hasRandomTithe = false;
                target.disburseTitheStock = false;
                target.randomTitheStock = stock;
                target.SetDirtyCache();
                int noDisburse = AccrueAndTax(s, days);
                TestAssert.AreEqual(stock, target.randomTitheStock, tolerance: 0.001,
                    message: "With disburse off and no random tithe, escrow stock should be untouched");

                // Same state, disburse on -> the escrow is paid out once and cleared.
                target.randomTitheStock = stock;
                target.disburseTitheStock = true;
                target.SetDirtyCache();
                int disbursed = AccrueAndTax(s, days);

                TestAssert.AreEqual(stock, disbursed - noDisburse, tolerance: 3,
                    message: $"Disburse should add exactly the escrow to silver (disbursed={disbursed}, noDisburse={noDisburse}, stock={stock})");
                TestAssert.AreEqual(0, (int)target.randomTitheStock,
                    message: "Disburse should clear the escrow stock");

                DestructiveTestUtil.AssertEmpireInvariants(f, "RandomTitheDisburse_PaysOnce");
            }
            finally
            {
                DestructiveTestUtil.SafeRemoveSettlement(s);
            }
        }

        [EmpireDestructiveTest("Destructive.Tax")]
        public static void RandomTitheBudget_ScalesWithAccruedDays()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();
            WorldSettlementFC s = DestructiveTestUtil.CreateTransientSettlement();
            if (s is null) TestAssert.Skip("No valid tile for a settlement");
            try
            {
                ResourceFC target = PrepareEscrowTarget(s);
                if (target is null) TestAssert.Skip("No positive-production tithe-able resource");

                target.hasRandomTithe = true;
                target.autoMaxRandomTithe = true;
                target.randomTitheFilter.SetDisallowAll();
                target.disburseTitheStock = false;

                // One-day accrual escrow.
                target.randomTitheStock = 0;
                target.SetDirtyCache();
                AccrueAndTax(s, 1);
                double oneDay = target.randomTitheStock;

                // Three-day accrual escrow.
                target.randomTitheStock = 0;
                target.SetDirtyCache();
                AccrueAndTax(s, 3);
                double threeDay = target.randomTitheStock;

                TestAssert.GreaterThan(oneDay, 0, "Single-day accrual should escrow a positive budget");
                // The per-day random budget is scaled by accrued days, so three days should escrow
                // clearly more than one, rather than being capped at ~one day's budget regardless.
                TestAssert.GreaterThan(threeDay, oneDay * 2,
                    $"Three-day escrow ({threeDay:F0}) should exceed 2x the one-day escrow ({oneDay:F0})");

                DestructiveTestUtil.AssertEmpireInvariants(f, "RandomTitheBudget_ScalesWithDays");
            }
            finally
            {
                DestructiveTestUtil.SafeRemoveSettlement(s);
            }
        }

        [EmpireDestructiveTest("Destructive.Tax")]
        public static void RandomTitheEscrow_AccumulatesAcrossCycles()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();
            WorldSettlementFC s = DestructiveTestUtil.CreateTransientSettlement();
            if (s is null) TestAssert.Skip("No valid tile for a settlement");
            try
            {
                ResourceFC target = PrepareEscrowTarget(s);
                if (target is null) TestAssert.Skip("No positive-production tithe-able resource");

                target.hasRandomTithe = true;
                target.autoMaxRandomTithe = true;
                target.randomTitheFilter.SetDisallowAll();
                target.disburseTitheStock = false;
                target.randomTitheStock = 0;
                target.SetDirtyCache();

                const int days = 2;
                AccrueAndTax(s, days);
                double afterOne = target.randomTitheStock;

                // Second cycle WITHOUT resetting the stock: carried escrow must ride on top of the new
                // cycle's budget instead of being re-capped at one cycle's worth (which would freeze the
                // stock and make items above one cycle's budget forever unaffordable).
                AccrueAndTax(s, days);
                double afterTwo = target.randomTitheStock;

                TestAssert.GreaterThan(afterOne, 0, "First cycle should escrow a positive budget");
                TestAssert.GreaterThan(afterTwo, afterOne * 1.5,
                    $"Escrow must accumulate across cycles (after one: {afterOne:F0}, after two: {afterTwo:F0})");

                DestructiveTestUtil.AssertEmpireInvariants(f, "RandomTitheEscrow_AccumulatesAcrossCycles");
            }
            finally
            {
                DestructiveTestUtil.SafeRemoveSettlement(s);
            }
        }
    }
}
