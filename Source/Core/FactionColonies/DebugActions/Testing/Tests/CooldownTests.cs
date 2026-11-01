using System;

namespace FactionColonies
{
    /* Coverage for CooldownAbility.SetCooldown — the stat-scaled cooldown duration setter.
     * Existing PolicyTests cover IsReady / Use / DaysRemaining; SetCooldown's scaling was untested.
     * SetCooldown(0) is multiplier-independent (pure); the scaling case is validated against the
     * live policyActionCooldownMultiplier stat (no faction -> the '?? 1' identity path). */
    public static class CooldownTests
    {
        [EmpireTest("Policy")]
        public static void SetCooldown_Zero_IsZeroRegardlessOfMultiplier()
        {
            var cd = new CooldownAbility();
            cd.SetCooldown(0);
            TestAssert.AreEqual(0, cd.cooldownTicks, "0 base ticks scales to 0");
        }

        [EmpireTest("Policy")]
        public static void SetCooldown_ScalesByPolicyActionCooldownMultiplier()
        {
            var cd = new CooldownAbility();
            FactionFC faction = FindFC.FactionComp;
            double mult = faction?.GetStatValue(FCStatDefOf.policyActionCooldownMultiplier) ?? 1.0;

            cd.SetCooldown(1000);
            int expected = (int)Math.Round(1000 * mult);
            TestAssert.AreEqual(expected, cd.cooldownTicks,
                $"cooldownTicks should equal round(1000 * {mult})");
        }
    }
}
