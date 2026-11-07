namespace FactionColonies;

/* DESTRUCTIVE: temporarily pins the live auto-resolve casualty settings (death threshold and max
   death fraction, restored afterwards) so the ComputeDeathChance ramp is exercised at a known
   interior threshold regardless of where the player has set the sliders. */
public static class BattleCasualtyApplicatorDestructiveTests
{
    private const float PinnedThreshold = 0.5f;
    private const float PinnedMaxDeathFraction = 0.4f;

    private static void WithPinnedCasualtySettings(Action body)
    {
        float savedThreshold = FCSettings.autoResolveCasualtyDeathThreshold;
        float savedMaxFrac = FCSettings.autoResolveCasualtyMaxDeathFraction;
        FCSettings.autoResolveCasualtyDeathThreshold = PinnedThreshold;
        FCSettings.autoResolveCasualtyMaxDeathFraction = PinnedMaxDeathFraction;
        try { body(); }
        finally
        {
            FCSettings.autoResolveCasualtyDeathThreshold = savedThreshold;
            FCSettings.autoResolveCasualtyMaxDeathFraction = savedMaxFrac;
        }
    }

    /// <summary>ComputeDeathChance's final step: scale by the faction-wide death-chance multiplier
    /// (non-positive treated as 1) and clamp to [0, 1].</summary>
    private static double ApplyDeathMultiplier(double ramp)
    {
        double mult = FindFC.FactionComp?.GetStatValue(FCStatDefOf.mercenaryDeathChanceMultiplier, null, null) ?? 1.0;
        return Math.Clamp(ramp * (mult > 0 ? mult : 1.0), 0.0, 1.0);
    }

    [EmpireDestructiveTest("Destructive.BattleCasualty")]
    public static void ComputeDeathChance_RateBelowThreshold_ReturnsZero() => WithPinnedCasualtySettings(() =>
    {
        double chance = BattleCasualtyApplicator.ComputeDeathChance(null, PinnedThreshold / 2.0, null);
        TestAssert.AreEqual(0.0, chance, message: "A casualty rate below the threshold should never kill");
    });

    [EmpireDestructiveTest("Destructive.BattleCasualty")]
    public static void ComputeDeathChance_RateFull_ReturnsMaxDeathFraction() => WithPinnedCasualtySettings(() =>
    {
        // rate = 1.0 -> the ramp reaches exactly maxDeathFraction.
        double chance = BattleCasualtyApplicator.ComputeDeathChance(null, 1.0, null);
        TestAssert.AreEqual(ApplyDeathMultiplier(PinnedMaxDeathFraction), chance, tolerance: 0.001,
            message: "A total wipe should yield the max death fraction");
    });

    [EmpireDestructiveTest("Destructive.BattleCasualty")]
    public static void ComputeDeathChance_RatePartiallyAboveThreshold_RampsLinearly() => WithPinnedCasualtySettings(() =>
    {
        // Midway between threshold and 1.0 -> half of maxDeathFraction.
        double midpoint = (PinnedThreshold + 1.0) / 2.0;
        double chance = BattleCasualtyApplicator.ComputeDeathChance(null, midpoint, null);
        TestAssert.AreEqual(ApplyDeathMultiplier(PinnedMaxDeathFraction / 2.0), chance, tolerance: 0.001,
            message: "The death chance should ramp linearly from the threshold to a total wipe");
    });
}
