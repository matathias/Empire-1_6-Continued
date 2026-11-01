using System.Collections.Generic;
using System.Linq;
using Verse;

namespace FactionColonies
{
    public static class PaymentTests
    {
        // ============================
        // SilverPaymentContext (pure)
        // ============================

        [EmpireTest("Payment")]
        public static void SilverPaymentContext_Constructor_SetsFields()
        {
            var ctx = new SilverPaymentContext(500, PaymentUtil.Reason_SquadDeployment);
            TestAssert.AreEqual(500, ctx.Amount, message: "Amount");
            TestAssert.AreEqual(PaymentUtil.Reason_SquadDeployment, ctx.Reason, message: "Reason");
            TestAssert.IsNull(ctx.Settlement, "Settlement should default to null");
        }

        [EmpireTest("Payment")]
        public static void SilverPaymentContext_ModifierReducesAmount()
        {
            var modifier = new TestPaymentZeroer();
            SilverPaymentRegistry.Register(modifier);
            try
            {
                var ctx = new SilverPaymentContext(200, "test");
                SilverPaymentRegistry.InvokeModifiers(ctx);
                TestAssert.AreEqual(0, ctx.Amount, "Modifier should zero the amount");
            }
            finally
            {
                SilverPaymentRegistry.Unregister(modifier);
            }
        }

        // ============================
        // PaySilver (game state)
        // ============================

        [EmpireTest("Payment")]
        public static void PaySilver_ZeroAmount_ReturnsTrue()
        {
            // Zero amount should succeed without consuming anything
            TestAssert.IsTrue(PaymentUtil.PaySilver(0, "test"),
                "PaySilver(0) should return true");
        }

        [EmpireTest("Payment")]
        public static void PaySilver_NegativeAmount_ReturnsTrue()
        {
            // Negative amount early-returns true
            TestAssert.IsTrue(PaymentUtil.PaySilver(-10, "test"),
                "PaySilver(-10) should return true");
        }

        [EmpireTest("Payment")]
        public static void PaySilver_RegistryReducesToZero_Succeeds()
        {
            var modifier = new TestPaymentZeroer();
            SilverPaymentRegistry.Register(modifier);
            try
            {
                // Even though we "pay" 9999, the modifier zeros it, so no silver consumed
                TestAssert.IsTrue(PaymentUtil.PaySilver(9999, "test"),
                    "PaySilver should succeed when modifier zeros the amount");
            }
            finally
            {
                SilverPaymentRegistry.Unregister(modifier);
            }
        }

        // ============================
        // TryPaySilver (atomic, game state)
        // ============================

        [EmpireTest("Payment")]
        public static void TryPaySilver_ZeroAmount_SucceedsNoDeduction()
        {
            int before = PaymentUtil.GetSilver();
            TestAssert.IsTrue(PaymentUtil.TryPaySilver(0, "test"), "TryPaySilver(0) should return true");
            TestAssert.AreEqual(before, PaymentUtil.GetSilver(), message: "TryPaySilver(0) must not deduct");
        }

        [EmpireTest("Payment")]
        public static void TryPaySilver_NegativeAmount_SucceedsNoDeduction()
        {
            int before = PaymentUtil.GetSilver();
            TestAssert.IsTrue(PaymentUtil.TryPaySilver(-50, "test"), "TryPaySilver(-50) should return true");
            TestAssert.AreEqual(before, PaymentUtil.GetSilver(), message: "TryPaySilver(negative) must not deduct");
        }

        [EmpireTest("Payment")]
        public static void TryPaySilver_Unaffordable_ReturnsFalseNoDeduction()
        {
            int before = PaymentUtil.GetSilver();
            // Ask for far more than any plausible balance: must refuse and deduct nothing (closes the
            // L1 footgun where the legacy PaySilver always returned true).
            TestAssert.IsFalse(PaymentUtil.TryPaySilver(before + 1_000_000, "test"),
                "TryPaySilver beyond the balance should return false");
            TestAssert.AreEqual(before, PaymentUtil.GetSilver(), message: "Failed TryPaySilver must deduct nothing");
        }

        [EmpireTest("Payment")]
        public static void TryPaySilver_ModifierZeros_SucceedsNoDeduction()
        {
            var modifier = new TestPaymentZeroer();
            SilverPaymentRegistry.Register(modifier);
            try
            {
                int before = PaymentUtil.GetSilver();
                TestAssert.IsTrue(PaymentUtil.TryPaySilver(9999, "test"),
                    "TryPaySilver should succeed when a modifier zeros the amount");
                TestAssert.AreEqual(before, PaymentUtil.GetSilver(), message: "Zeroed payment must not deduct");
            }
            finally
            {
                SilverPaymentRegistry.Unregister(modifier);
            }
        }

        [EmpireTest("Payment")]
        public static void TryPaySilver_ModifierInflates_ChecksEffectiveAmount()
        {
            var modifier = new TestPaymentInflator(1_000_000);
            SilverPaymentRegistry.Register(modifier);
            try
            {
                int before = PaymentUtil.GetSilver();
                // Nominal amount is 1, but the modifier inflates it past any balance. TryPaySilver must
                // check the modifier-adjusted (effective) amount, not the nominal one, so it refuses.
                TestAssert.IsFalse(PaymentUtil.TryPaySilver(1, "test"),
                    "TryPaySilver must check the modifier-adjusted (effective) amount");
                TestAssert.AreEqual(before, PaymentUtil.GetSilver(), message: "Refused inflated payment must deduct nothing");
            }
            finally
            {
                SilverPaymentRegistry.Unregister(modifier);
            }
        }

        // ============================
        // CanAfford + deferred commit (H1/H5)
        // ============================

        [EmpireTest("Payment")]
        public static void CanAfford_NoModifier_MatchesGetSilver()
        {
            int before = PaymentUtil.GetSilver();
            TestAssert.IsTrue(PaymentUtil.CanAfford(0), "CanAfford(0) should be true");
            TestAssert.IsTrue(PaymentUtil.CanAfford(before), "CanAfford(balance) should be true");
            TestAssert.IsFalse(PaymentUtil.CanAfford(before + 1_000_000),
                "CanAfford beyond the balance should be false with no modifiers");
        }

        [EmpireTest("Payment")]
        public static void CanAfford_DoesNotRunCommit()
        {
            // A financing modifier covers the amount, but CanAfford is a pure query: the commit
            // (the drain) must NOT run and the financer's pool must be untouched.
            var financer = new TestPaymentFinancer(500);
            SilverPaymentRegistry.Register(financer);
            try
            {
                TestAssert.IsTrue(PaymentUtil.CanAfford(200, "test"),
                    "CanAfford should be true when the financer fully covers it");
                TestAssert.IsFalse(financer.committed, "CanAfford must not run commit actions");
                TestAssert.AreEqual(500, financer.available, message: "CanAfford must not drain the financer");
            }
            finally
            {
                SilverPaymentRegistry.Unregister(financer);
            }
        }

        [EmpireTest("Payment")]
        public static void TryPaySilver_Unaffordable_DoesNotCommit()
        {
            // Financer covers only part; the residual exceeds home storage, so the payment fails.
            // The drain must NOT have run (H5: no eager consumption on a failed payment).
            int before = PaymentUtil.GetSilver();
            var financer = new TestPaymentFinancer(100);
            SilverPaymentRegistry.Register(financer);
            try
            {
                int amount = before + 100 + 1_000_000; // residual after cover = before + 1_000_000 > balance
                TestAssert.IsFalse(PaymentUtil.TryPaySilver(amount, "test"),
                    "Unaffordable payment should return false");
                TestAssert.IsFalse(financer.committed, "Unaffordable payment must not commit (H5)");
                TestAssert.AreEqual(100, financer.available, message: "Unaffordable payment must not drain the financer (H5)");
                TestAssert.AreEqual(before, PaymentUtil.GetSilver(), message: "Unaffordable payment must not deduct home silver");
            }
            finally
            {
                SilverPaymentRegistry.Unregister(financer);
            }
        }

        [EmpireTest("Payment")]
        public static void TryPaySilver_FullyFinanced_CommitsNoHomeDeduction()
        {
            // The financer fully covers the amount: the payment succeeds, the drain runs (commit),
            // and no home silver is touched. Proves the covered silver is really consumed -- not
            // phantom silver that lets the purchase go through for free (H1).
            int before = PaymentUtil.GetSilver();
            var financer = new TestPaymentFinancer(500);
            SilverPaymentRegistry.Register(financer);
            try
            {
                TestAssert.IsTrue(PaymentUtil.TryPaySilver(300, "test"),
                    "Fully-financed payment should succeed");
                TestAssert.IsTrue(financer.committed, "Fully-financed payment must commit the drain");
                TestAssert.AreEqual(200, financer.available, message: "Financer must be drained by the covered amount");
                TestAssert.AreEqual(before, PaymentUtil.GetSilver(), message: "Fully-financed payment must not touch home silver");
            }
            finally
            {
                SilverPaymentRegistry.Unregister(financer);
            }
        }

        // ============================
        // GetSilver (game state)
        // ============================

        [EmpireTest("Payment")]
        public static void GetSilver_IsNonNegative()
        {
            int silver = PaymentUtil.GetSilver();
            TestAssert.IsTrue(silver >= 0, $"GetSilver should be >= 0, got {silver}");
        }

        // ============================
        // ReturnValueOfTithe (pure)
        // ============================

        [EmpireTest("Payment")]
        public static void ReturnValueOfTithe_EmptyList_ReturnsZero()
        {
            double value = PaymentUtil.ReturnValueOfTithe(new List<Thing>());
            TestAssert.AreEqual(0.0, value, message: "Empty list should return 0");
        }

        // ============================
        // GenerateRewardThings (game state)
        // ============================

        [EmpireTest("Payment")]
        public static void GenerateRewardThings_NullRewardDef_ReturnsEmpty()
        {
            List<Thing> result = PaymentUtil.GenerateRewardThings(100, null);
            TestAssert.IsNotNull(result, "Should return non-null list");
            TestAssert.IsEmpty(result, "Null rewardDef should produce empty list");
        }

        [EmpireTest("Payment")]
        public static void GenerateRewardThings_ValidDef_ProducesThings()
        {
            ResourceEventRewardDef rewardDef = DefDatabase<ResourceEventRewardDef>.AllDefsListForReading
                .FirstOrDefault();
            if (rewardDef == null) TestAssert.Skip("No ResourceEventRewardDef found");

            List<Thing> result = PaymentUtil.GenerateRewardThings(500, rewardDef);

            TestAssert.IsNotNull(result, "Should return non-null list");
            TestAssert.IsNotEmpty(result, $"GenerateRewardThings should produce items for {rewardDef.defName}");
            foreach (Thing t in result)
            {
                TestAssert.IsNotNull(t, "Generated thing should not be null");
            }
        }

        // ============================
        // Test Double
        // ============================

        private class TestPaymentZeroer : ISilverPaymentModifier
        {
            public void ModifyPayment(SilverPaymentContext context)
            {
                context.Amount = 0;
            }
        }

        private class TestPaymentInflator : ISilverPaymentModifier
        {
            private readonly int add;
            public TestPaymentInflator(int add) { this.add = add; }
            public void ModifyPayment(SilverPaymentContext context)
            {
                context.Amount += add;
            }
        }

        // Mimics OutpostFinancer: covers part of the amount from an external pool, but defers the
        // actual drain to a commit action so a query or a failed payment never consumes it.
        private class TestPaymentFinancer : ISilverPaymentModifier
        {
            public int available;
            public bool committed;
            public TestPaymentFinancer(int available) { this.available = available; }
            public void ModifyPayment(SilverPaymentContext context)
            {
                int cover = context.Amount < available ? context.Amount : available;
                if (cover <= 0) return;
                context.Amount -= cover;
                context.Commit(delegate { available -= cover; committed = true; });
            }
        }
    }
}
