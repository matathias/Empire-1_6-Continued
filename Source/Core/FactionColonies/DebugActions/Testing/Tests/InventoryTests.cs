using System.Collections.Generic;
using RimWorld;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Tests for the carry-inventory blocklist pool.
    /// Category "Inventory" — run via Debug menu -> Empire -> Run Tests by Category.
    /// </summary>
    public static class InventoryTests
    {
        /*-*-*-*-* Pure set-algebra tests (ComputeAllowed) *-*-*-*-*/

        [EmpireTest("Inventory")]
        public static void ComputeAllowed_SubtractsExcludedFromBase()
        {
            ThingDef a = new ThingDef { defName = "EmpireTest_A" };
            ThingDef b = new ThingDef { defName = "EmpireTest_B" };
            ThingDef c = new ThingDef { defName = "EmpireTest_C" };

            HashSet<ThingDef> result = MilitaryInventoryUtil.ComputeAllowed(
                new[] { a, b, c }, new[] { b }, new ThingDef[0]);

            TestAssert.Contains(result, a, "non-excluded base item kept");
            TestAssert.Contains(result, c, "non-excluded base item kept");
            TestAssert.IsFalse(result.Contains(b), "excluded item removed from base pool");
        }

        [EmpireTest("Inventory")]
        public static void ComputeAllowed_ForceAllowOverridesExclude()
        {
            ThingDef a = new ThingDef { defName = "EmpireTest_A" };
            ThingDef b = new ThingDef { defName = "EmpireTest_B" };

            HashSet<ThingDef> result = MilitaryInventoryUtil.ComputeAllowed(
                new[] { a, b }, new[] { b }, new[] { b });

            TestAssert.Contains(result, b, "force-allow must win over exclude (allow > exclude)");
        }

        [EmpireTest("Inventory")]
        public static void ComputeAllowed_IgnoresNulls()
        {
            ThingDef a = new ThingDef { defName = "EmpireTest_A" };

            HashSet<ThingDef> result = MilitaryInventoryUtil.ComputeAllowed(
                new ThingDef[] { a, null }, new ThingDef[] { null }, new ThingDef[] { null });

            TestAssert.Contains(result, a, "valid item kept");
            TestAssert.IsFalse(result.Contains(null), "null never added");
        }

        /*-*-*-*-* Structural never-carriable rules *-*-*-*-*/

        [EmpireTest("Inventory")]
        public static void IsNeverCarriable_BlocksBuildingsAndMinified()
        {
            // Positive market value so the zero-value rule can't be what trips these.
            List<StatModifier> value = new List<StatModifier>
            {
                new StatModifier { stat = StatDefOf.MarketValue, value = 100f }
            };

            // Buildings are blocked by category, even turrets/mortars (category == Building),
            // and regardless of whether they carry a thingCategory.
            ThingDef building = new ThingDef { category = ThingCategory.Building, statBases = value };
            TestAssert.IsTrue(MilitaryInventoryUtil.IsNeverCarriable(building), "buildings blocked by category");

            // Minified wrappers are category Item, so they rely on the thingClass rule.
            ThingDef minified = new ThingDef
            {
                category = ThingCategory.Item, thingClass = typeof(MinifiedThing), statBases = value
            };
            TestAssert.IsTrue(MilitaryInventoryUtil.IsNeverCarriable(minified), "minified wrapper blocked by class");
        }

        [EmpireTest("Inventory")]
        public static void IsNeverCarriable_BlocksZeroValueButAllowsOrdinaryItem()
        {
            // Bare def has no MarketValue stat -> BaseMarketValue 0 -> blocked.
            ThingDef worthless = new ThingDef { category = ThingCategory.Item };
            TestAssert.IsTrue(MilitaryInventoryUtil.IsNeverCarriable(worthless), "zero market value blocked");

            ThingDef item = new ThingDef
            {
                category = ThingCategory.Item,
                statBases = new List<StatModifier> { new StatModifier { stat = StatDefOf.MarketValue, value = 100f } }
            };
            TestAssert.IsFalse(MilitaryInventoryUtil.IsNeverCarriable(item), "ordinary positive-value item not blocked");
        }

        /*-*-*-*-* Real-database membership (depends on the default blocklist def) *-*-*-*-*/

        [EmpireTest("Inventory")]
        public static void AllowedItems_BlocklistMembership()
        {
            HashSet<ThingDef> allowed = MilitaryInventoryUtil.AllowedItems();

            // Allowed: weapons, meds, drugs, food meals, pemmican, shells, chemfuel.
            AssertMembership(allowed, "Gun_Autopistol", true);
            AssertMembership(allowed, "MedicineIndustrial", true);
            AssertMembership(allowed, "Penoxycyline", true);
            AssertMembership(allowed, "MealSimple", true);
            AssertMembership(allowed, "Pemmican", true);
            AssertMembership(allowed, "Shell_HighExplosive", true);
            AssertMembership(allowed, "Chemfuel", true);

            // Blocked: raw materials, components, neutroamine, raw food, apparel.
            AssertMembership(allowed, "Steel", false);
            AssertMembership(allowed, "WoodLog", false);
            AssertMembership(allowed, "ComponentIndustrial", false);
            AssertMembership(allowed, "Neutroamine", false);
            AssertMembership(allowed, "RawPotatoes", false);
            AssertMembership(allowed, "Apparel_Pants", false);
        }

        private static void AssertMembership(HashSet<ThingDef> set, string defName, bool expected)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (def is null) return; // tolerate DLC/version differences
            TestAssert.AreEqual(expected, set.Contains(def), defName + " membership");
        }
    }
}
