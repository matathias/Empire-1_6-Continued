using System.Collections.Generic;
using RimWorld;
using Verse;

namespace FactionColonies
{
    /* Tests for LoadoutUpgradeUtil's order-independent row comparators (InventoryEquivalent /
       ApparelEquivalent). Regression cover for the positional-comparison bug: same-def rows differing
       only in stuff/quality must compare as equivalent regardless of list order — otherwise the Upgrade
       button reports a perpetual difference and never clears. Pure/deterministic; no world needed.
       Category "LoadoutUpgrade" — run via Debug menu -> Empire -> Run Tests by Category. */
    public static class LoadoutUpgradeUtilTests
    {
        private static ThingDef Def(string name) => new ThingDef { defName = name };

        private static SavedThing Row(ThingDef thing, ThingDef stuff, QualityCategory? q = null, int count = 1)
            => new SavedThing(thing, stuff, count, q);

        // -*- InventoryEquivalent: same def, different stuff, reversed order -*-

        [EmpireTest("LoadoutUpgrade")]
        public static void InventoryEquivalent_SameDefDifferentStuff_OrderIndependent()
        {
            ThingDef pants = Def("Apparel_Pants");
            ThingDef cloth = Def("Cloth");
            ThingDef devil = Def("DevilstrandCloth");

            var a = new List<SavedThing> { Row(pants, cloth), Row(pants, devil) };
            var b = new List<SavedThing> { Row(pants, devil), Row(pants, cloth) };

            TestAssert.IsTrue(LoadoutUpgradeUtil.InventoryEquivalent(a, b),
                "Same multiset in reversed order must be equivalent");
        }

        [EmpireTest("LoadoutUpgrade")]
        public static void InventoryEquivalent_DifferentStuff_NotEquivalent()
        {
            ThingDef pants = Def("Apparel_Pants");
            ThingDef cloth = Def("Cloth");
            ThingDef devil = Def("DevilstrandCloth");

            var a = new List<SavedThing> { Row(pants, cloth), Row(pants, devil) };
            var b = new List<SavedThing> { Row(pants, cloth), Row(pants, cloth) };

            TestAssert.IsFalse(LoadoutUpgradeUtil.InventoryEquivalent(a, b),
                "Genuinely different stuff must not be equivalent");
        }

        // -*- ApparelEquivalent: same def differing in stuff, then in quality -*-

        [EmpireTest("LoadoutUpgrade")]
        public static void ApparelEquivalent_SameDefDifferentStuff_OrderIndependent()
        {
            ThingDef helmet = Def("Apparel_Helmet");
            ThingDef steel = Def("Steel");
            ThingDef plasteel = Def("Plasteel");

            var a = new List<SavedThing> { Row(helmet, steel), Row(helmet, plasteel) };
            var b = new List<SavedThing> { Row(helmet, plasteel), Row(helmet, steel) };

            TestAssert.IsTrue(LoadoutUpgradeUtil.ApparelEquivalent(a, b),
                "Same apparel multiset in reversed order must be equivalent");
        }

        [EmpireTest("LoadoutUpgrade")]
        public static void ApparelEquivalent_SameDefDifferentQuality_OrderIndependent()
        {
            ThingDef helmet = Def("Apparel_Helmet");
            ThingDef steel = Def("Steel");

            var a = new List<SavedThing> { Row(helmet, steel, QualityCategory.Normal), Row(helmet, steel, QualityCategory.Masterwork) };
            var b = new List<SavedThing> { Row(helmet, steel, QualityCategory.Masterwork), Row(helmet, steel, QualityCategory.Normal) };

            TestAssert.IsTrue(LoadoutUpgradeUtil.ApparelEquivalent(a, b),
                "Same def differing only in quality must be equivalent regardless of order");
        }

        [EmpireTest("LoadoutUpgrade")]
        public static void ApparelEquivalent_DifferentQuality_NotEquivalent()
        {
            ThingDef helmet = Def("Apparel_Helmet");
            ThingDef steel = Def("Steel");

            var a = new List<SavedThing> { Row(helmet, steel, QualityCategory.Normal), Row(helmet, steel, QualityCategory.Masterwork) };
            var b = new List<SavedThing> { Row(helmet, steel, QualityCategory.Normal), Row(helmet, steel, QualityCategory.Normal) };

            TestAssert.IsFalse(LoadoutUpgradeUtil.ApparelEquivalent(a, b),
                "A quality mismatch must not be equivalent");
        }
    }
}
