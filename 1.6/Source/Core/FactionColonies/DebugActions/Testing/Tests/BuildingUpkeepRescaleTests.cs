using System.Linq;

namespace FactionColonies
{
    public static class BuildingUpkeepRescaleTests
    {
        private static WorldSettlementFC GetFirstSettlement()
        {
            var s = FindFC.Settlements;
            return (s == null || s.Count == 0) ? null : s[0];
        }

        // A postRework def's base term is taken as-is (no /5); a legacy def is divided by LEGACY_UPKEEP_DIVISOR.
        [EmpireTest("BuildingUpkeepRescale")]
        public static void PostReworkDef_NotDivided_LegacyDef_Divided()
        {
            var settlement = GetFirstSettlement();
            if (settlement is null) TestAssert.Skip("No settlement");

            var modern = new BuildingFCDef { upkeep = 20, postRework = true };
            var legacy = new BuildingFCDef { upkeep = 100, postRework = false };

            int modernUpkeep = settlement.BuildingsComp.GetBuildingUpkeep(modern);
            int legacyUpkeep = settlement.BuildingsComp.GetBuildingUpkeep(legacy);

            // With default stats (base 0, mult 1, difficultyMult 1): modern == 20, legacy == 100/5 == 20.
            TestAssert.AreEqual(20, modernUpkeep, message: "postRework def base term should not be divided");
            TestAssert.AreEqual(20, legacyUpkeep, message: "legacy def base term should be divided by 5");
        }

        // A designed-income building (negative authored upkeep) yields a negative (income) value, not floored to 0.
        [EmpireTest("BuildingUpkeepRescale")]
        public static void NegativeUpkeepDef_StaysSigned()
        {
            var settlement = GetFirstSettlement();
            if (settlement is null) TestAssert.Skip("No settlement");

            var income = new BuildingFCDef { upkeep = -10, postRework = true };
            int value = settlement.BuildingsComp.GetBuildingUpkeep(income);
            TestAssert.LessThan(value, 0, "designed-income building should return a negative (income) value");
        }
    }
}
