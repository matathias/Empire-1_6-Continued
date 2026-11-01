namespace FactionColonies
{
    /*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*/
    /* An over-cap upgrade must clamp DOWN to the lower of the global cap and the
     * settlement type's own def cap, not jump UP to the global max. Exercises the
     * pure WorldSettlementFC.ClampSettlementLevel helper so no live settlement
     * state is mutated.
     *-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*/
    public static class SettlementUpgradeClampTests
    {
        [EmpireTest("Settlement")]
        public static void ClampSettlementLevel_OverDefCap_ClampsToDefCap()
        {
            // Def cap 2 is below any sane global max, so the def cap is the binding limit.
            var def = new WorldSettlementDef { maxSettlementLevel = 2 };
            int clamped = WorldSettlementFC.ClampSettlementLevel(2 + 5, def);
            TestAssert.AreEqual(2, clamped,
                "over-cap upgrade should clamp down to the def cap, not up to the global max");
        }

        [EmpireTest("Settlement")]
        public static void ClampSettlementLevel_GlobalCapBinds_WhenDefCapHigher()
        {
            // Def cap far above the global cap -> the global cap is the binding limit.
            var def = new WorldSettlementDef { maxSettlementLevel = 9999 };
            int clamped = WorldSettlementFC.ClampSettlementLevel(9999, def);
            TestAssert.AreEqual(FCSettings.settlementMaxLevel, clamped,
                "global cap should bind when the def cap is higher");
        }

        [EmpireTest("Settlement")]
        public static void ClampSettlementLevel_Negative_ClampsToZero()
        {
            var def = new WorldSettlementDef { maxSettlementLevel = 5 };
            TestAssert.AreEqual(0, WorldSettlementFC.ClampSettlementLevel(-3, def),
                "negative level should clamp to 0");
        }
    }
}
