using System.Collections.Generic;
using RimWorld;
using Verse;

namespace FactionColonies
{
    /* Tests for SquadRestockUtil: the pure consumption-diff seam (ComputeConsumption) plus guard
       no-ops on the pawn-IO orchestrator (ReconcileAndBill). The live diff/refill/bill behavior
       needs real merc pawns and is exercised in-game; the value math is unit-tested here.
       Category "SquadRestock" — run via Debug menu -> Empire -> Run Tests by Category. */
    public static class SquadRestockUtilTests
    {
        // Synthetic item with a fixed market value; no quality/stuff so SavedThing.MarketValue
        // resolves to BaseMarketValue (CraftUtil.ThingValue falls through to BaseMarketValue).
        private static ThingDef Item(float marketValue)
        {
            return new ThingDef
            {
                category = ThingCategory.Item,
                statBases = new List<StatModifier> { new StatModifier { stat = StatDefOf.MarketValue, value = marketValue } }
            };
        }

        private static List<SavedThing> Design(ThingDef thing, int count) =>
            new List<SavedThing> { new SavedThing(thing, null, count, null) };

        // -*- ComputeConsumption: shortfall billed + queued for refill -*-

        [EmpireTest("SquadRestock")]
        public static void ComputeConsumption_PartialUse_BillsAndRefillsDifference()
        {
            ThingDef med = Item(100f);
            var refill = new List<SquadRestockUtil.ConsumedItem>();
            // Design wants 2; pawn still holds 1 -> 1 consumed @100.
            double value = SquadRestockUtil.ComputeConsumption(Design(med, 2), key => 1, refill);

            TestAssert.AreEqual(100.0, value);
            TestAssert.AreEqual(1, refill.Count, "one shortfall queued");
            TestAssert.AreEqual(1, refill[0].count, "refill exactly the consumed count");
            TestAssert.IsTrue(ReferenceEquals(med, refill[0].thing), "refill targets the design item");
        }

        [EmpireTest("SquadRestock")]
        public static void ComputeConsumption_FullStock_NothingConsumed()
        {
            ThingDef med = Item(100f);
            var refill = new List<SquadRestockUtil.ConsumedItem>();
            double value = SquadRestockUtil.ComputeConsumption(Design(med, 2), key => 2, refill);

            TestAssert.AreEqual(0.0, value);
            TestAssert.AreEqual(0, refill.Count);
        }

        [EmpireTest("SquadRestock")]
        public static void ComputeConsumption_Overstocked_ClampsToZero()
        {
            // Looted extras: pawn holds more than the design wants -> no negative bill, no refill.
            ThingDef med = Item(100f);
            var refill = new List<SquadRestockUtil.ConsumedItem>();
            double value = SquadRestockUtil.ComputeConsumption(Design(med, 2), key => 5, refill);

            TestAssert.AreEqual(0.0, value);
            TestAssert.AreEqual(0, refill.Count);
        }

        [EmpireTest("SquadRestock")]
        public static void ComputeConsumption_DuplicateDesignRows_Aggregate()
        {
            ThingDef med = Item(100f);
            var design = new List<SavedThing>
            {
                new SavedThing(med, null, 1, null),
                new SavedThing(med, null, 1, null)
            };
            var refill = new List<SquadRestockUtil.ConsumedItem>();
            // Wanted 2 total, pawn holds 0 -> 2 consumed @100, aggregated to one refill row.
            double value = SquadRestockUtil.ComputeConsumption(design, key => 0, refill);

            TestAssert.AreEqual(200.0, value);
            TestAssert.AreEqual(1, refill.Count, "aggregated to a single refill entry");
            TestAssert.AreEqual(2, refill[0].count);
        }

        [EmpireTest("SquadRestock")]
        public static void ComputeConsumption_NullArgs_ReturnsZero()
        {
            TestAssert.AreEqual(0.0, SquadRestockUtil.ComputeConsumption(null, key => 0, null));
            TestAssert.AreEqual(0.0, SquadRestockUtil.ComputeConsumption(Design(Item(100f), 2), null, null));
        }

        // -*- ReconcileAndBill: guard no-ops (live diff/refill exercised in-game) -*-

        [EmpireTest("SquadRestock")]
        public static void ReconcileAndBill_NullSquad_ReturnsZero()
        {
            TestAssert.AreEqual(0, SquadRestockUtil.ReconcileAndBill(null));
        }

        [EmpireTest("SquadRestock")]
        public static void ReconcileAndBill_EmptySquad_ReturnsZero()
        {
            TestAssert.AreEqual(0, SquadRestockUtil.ReconcileAndBill(new MercenarySquadFC()));
        }

        [EmpireTest("SquadRestock")]
        public static void ReconcileAndBill_GodMode_ReturnsZero()
        {
            bool original = DebugSettings.godMode;
            DebugSettings.godMode = true;
            try
            {
                TestAssert.AreEqual(0, SquadRestockUtil.ReconcileAndBill(new MercenarySquadFC()));
            }
            finally
            {
                DebugSettings.godMode = original;
            }
        }

        // -*- ComputeEquipmentRestock: repair damaged, replace destroyed -*-

        private static SquadRestockUtil.PresentItem Present(ThingDef thing, int hp, int maxHp, object handle = null) =>
            new SquadRestockUtil.PresentItem
            {
                thing = thing, stuff = null, quality = null,
                hitPoints = hp, maxHitPoints = maxHp, handle = handle ?? new object()
            };

        [EmpireTest("SquadRestock")]
        public static void ComputeEquipmentRestock_FullHp_NoChargeNoAction()
        {
            ThingDef armor = Item(100f);
            var present = new List<SquadRestockUtil.PresentItem> { Present(armor, 100, 100) };
            var r = SquadRestockUtil.ComputeEquipmentRestock(Design(armor, 1), present);

            TestAssert.AreEqual(0.0, r.value);
            TestAssert.AreEqual(0, r.repairHandles.Count);
            TestAssert.AreEqual(0, r.replacements.Count);
        }

        [EmpireTest("SquadRestock")]
        public static void ComputeEquipmentRestock_Damaged_BillsDurabilityLostAndQueuesRepair()
        {
            ThingDef armor = Item(100f);
            object handle = new object();
            // 60/100 HP -> 40% durability lost -> 100 * 0.4 = 40.
            var present = new List<SquadRestockUtil.PresentItem> { Present(armor, 60, 100, handle) };
            var r = SquadRestockUtil.ComputeEquipmentRestock(Design(armor, 1), present);

            TestAssert.AreEqual(40.0, r.value);
            TestAssert.AreEqual(1, r.repairHandles.Count, "damaged item queued for repair");
            TestAssert.IsTrue(ReferenceEquals(handle, r.repairHandles[0]), "repair targets the live item handle");
            TestAssert.AreEqual(0, r.replacements.Count);
        }

        [EmpireTest("SquadRestock")]
        public static void ComputeEquipmentRestock_Missing_BillsFullAndQueuesReplacement()
        {
            ThingDef armor = Item(100f);
            // Design wants the item; pawn no longer has it -> destroyed/lost -> full charge + replace.
            var r = SquadRestockUtil.ComputeEquipmentRestock(Design(armor, 1), new List<SquadRestockUtil.PresentItem>());

            TestAssert.AreEqual(100.0, r.value);
            TestAssert.AreEqual(1, r.replacements.Count, "missing item queued for replacement");
            TestAssert.AreEqual(0, r.repairHandles.Count);
        }

        [EmpireTest("SquadRestock")]
        public static void ComputeEquipmentRestock_LootedExtra_Ignored()
        {
            ThingDef armor = Item(100f);
            ThingDef loot = Item(50f);
            // Design item present at full HP + an unrelated looted item -> nothing billed, loot ignored.
            var present = new List<SquadRestockUtil.PresentItem>
            {
                Present(armor, 100, 100),
                Present(loot, 50, 100)
            };
            var r = SquadRestockUtil.ComputeEquipmentRestock(Design(armor, 1), present);

            TestAssert.AreEqual(0.0, r.value);
            TestAssert.AreEqual(0, r.repairHandles.Count);
            TestAssert.AreEqual(0, r.replacements.Count);
        }

        [EmpireTest("SquadRestock")]
        public static void ComputeEquipmentRestock_NoDurability_NeverRepaired()
        {
            // maxHitPoints == 0 (item without durability): matched, but never billed for repair.
            ThingDef armor = Item(100f);
            var present = new List<SquadRestockUtil.PresentItem> { Present(armor, 0, 0) };
            var r = SquadRestockUtil.ComputeEquipmentRestock(Design(armor, 1), present);

            TestAssert.AreEqual(0.0, r.value);
            TestAssert.AreEqual(0, r.repairHandles.Count);
            TestAssert.AreEqual(0, r.replacements.Count);
        }

        [EmpireTest("SquadRestock")]
        public static void ComputeEquipmentRestock_MixedRepairAndReplace_Sums()
        {
            ThingDef a = Item(100f);
            ThingDef b = Item(200f);
            var design = new List<SavedThing>
            {
                new SavedThing(a, null, 1, null),
                new SavedThing(b, null, 1, null)
            };
            // a present at 50% (repair 50), b absent (replace 200) -> 250.
            var present = new List<SquadRestockUtil.PresentItem> { Present(a, 50, 100) };
            var r = SquadRestockUtil.ComputeEquipmentRestock(design, present);

            TestAssert.AreEqual(250.0, r.value);
            TestAssert.AreEqual(1, r.repairHandles.Count);
            TestAssert.AreEqual(1, r.replacements.Count);
        }

        [EmpireTest("SquadRestock")]
        public static void ComputeEquipmentRestock_NullDesign_ReturnsEmpty()
        {
            var r = SquadRestockUtil.ComputeEquipmentRestock(null, new List<SquadRestockUtil.PresentItem>());
            TestAssert.AreEqual(0.0, r.value);
            TestAssert.AreEqual(0, r.repairHandles.Count);
            TestAssert.AreEqual(0, r.replacements.Count);
        }

        // -*- Quality is part of the match key: worn gear is matched to its design by
        //     (def, stuff, quality). A present item whose quality differs from the design no longer
        //     matches, so it is billed at full value and re-equipped. This is the failure the removed
        //     combat-efficiency gear-quality shift caused every manual battle (the shift raised worn
        //     gear above the design quality, so surviving gear was rebilled as destroyed). -*-

        private static List<SavedThing> DesignQ(ThingDef thing, QualityCategory quality) =>
            new List<SavedThing> { new SavedThing(thing, null, 1, quality) };

        private static SquadRestockUtil.PresentItem PresentQ(ThingDef thing, QualityCategory quality) =>
            new SquadRestockUtil.PresentItem
            {
                thing = thing, stuff = null, quality = quality,
                hitPoints = 100, maxHitPoints = 100, handle = new object()
            };

        [EmpireTest("SquadRestock")]
        public static void ComputeEquipmentRestock_MatchingQuality_NoCharge()
        {
            ThingDef armor = Item(100f);
            var present = new List<SquadRestockUtil.PresentItem> { PresentQ(armor, QualityCategory.Normal) };
            var r = SquadRestockUtil.ComputeEquipmentRestock(DesignQ(armor, QualityCategory.Normal), present);

            TestAssert.AreEqual(0.0, r.value, "gear matching the design quality is neither repaired nor rebilled");
            TestAssert.AreEqual(0, r.replacements.Count);
            TestAssert.AreEqual(0, r.repairHandles.Count);
        }

        [EmpireTest("SquadRestock")]
        public static void ComputeEquipmentRestock_QualityShiftedAboveDesign_RebilledAsDestroyed()
        {
            // Regression guard for the removed efficiency gear-quality shift: a surviving item whose
            // quality was bumped above the design fails the (def, stuff, quality) match, so it bills
            // full value and re-equips instead of being recognized as the same item.
            ThingDef armor = Item(100f);
            var present = new List<SquadRestockUtil.PresentItem> { PresentQ(armor, QualityCategory.Excellent) };
            var r = SquadRestockUtil.ComputeEquipmentRestock(DesignQ(armor, QualityCategory.Normal), present);

            TestAssert.AreEqual(100.0, r.value, "a quality-mismatched item bills the full design value");
            TestAssert.AreEqual(1, r.replacements.Count, "mismatched item is treated as destroyed/lost");
            TestAssert.AreEqual(0, r.repairHandles.Count, "the mismatched present item is ignored, not repaired");
        }
    }
}
