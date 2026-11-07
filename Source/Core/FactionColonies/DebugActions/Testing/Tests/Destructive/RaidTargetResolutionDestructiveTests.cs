namespace FactionColonies;

/* DESTRUCTIVE: creates the empire NPC faction if the player has not founded it yet, because
   CompleteBattle only treats an op as defensive when its defender is the real empire faction.

   Regression tests for MilitaryOperation.CompleteBattle's IRaidTarget dispatch (review issue H3).
   Before the fix, an externally-registered IRaidTarget attacked by an enemy never received
   OnRaidWon/OnRaidLost and its IsUnderAttack flag was never cleared, so the raid-selection pool
   (FactionFC filters on !IsUnderAttack) permanently excluded it for the rest of the save.

   These build a minimal Engaged op whose targetObject is the target's WorldObject, drive
   CompleteBattle, and assert the correct callback fired and the flag was cleared. defender.faction
   is set to the empire so IsDefensive is true and the op's `victory` == DefenderVictory (the raid
   target is the defender). Squads are left null, so CompleteBattle's EnterCooldown short-circuits
   to a safe Resolve() on this never-registered synthetic op. */
public static class RaidTargetResolutionDestructiveTests
{
    private class CountingRaidTarget : IRaidTarget
    {
        private readonly WorldObject _obj;
        public int WonCount;
        public int LostCount;
        public bool ThrowOnCallback;

        public CountingRaidTarget(WorldObject obj) { _obj = obj; }

        public WorldObject WorldObject => _obj;
        public string Name => "TestCountingRaidTarget";
        // Any valid tile: RaidTargetRegistry skips targets with an invalid Tile.
        public PlanetTile Tile => new(0);
        public int MilitaryLevel => 1;
        public bool IsUnderAttack { get; set; }

        public void OnRaidWon(BattleResult result)
        {
            WonCount++;
            if (ThrowOnCallback) throw new InvalidOperationException("test");
        }

        public void OnRaidLost(BattleResult result)
        {
            LostCount++;
            if (ThrowOnCallback) throw new InvalidOperationException("test");
        }
    }

    /// <summary>
    /// Minimal Engaged defensive op whose targetObject is <paramref name="targetObj"/>. Not
    /// registered with the manager — a transient stand-in that resolves itself in CompleteBattle.
    /// Creates the empire faction first if needed so the op counts as defensive.
    /// </summary>
    private static MilitaryOperation MakeEngagedRaidOp(WorldObject targetObj)
    {
        var op = new MilitaryOperation(-1, null, PlanetTile.Invalid, targetObj);
        op.defender.faction = DestructiveTestUtil.EnsureEmpireFaction(); // IsDefensive => victory == DefenderVictory
        op.phase = MilitaryOperationPhase.Engaged;  // CompleteBattle requires Engaged
        return op;
    }

    private static BattleResult MakeResult(BattleWinner winner) => new() { winner = winner };

    [EmpireDestructiveTest("Destructive.Battle")]
    public static void RaidTarget_DefenderVictory_FiresOnRaidWon_AndClearsFlag()
    {
        var obj = new WorldObject();
        var target = new CountingRaidTarget(obj) { IsUnderAttack = true };
        MilitaryOperation op = MakeEngagedRaidOp(obj);
        RaidTargetRegistry.Register(target);
        try
        {
            op.CompleteBattle(MakeResult(BattleWinner.Defender));
            TestAssert.AreEqual(1, target.WonCount, "OnRaidWon should fire on a defender victory");
            TestAssert.AreEqual(0, target.LostCount, "OnRaidLost should not fire on a defender victory");
            TestAssert.IsFalse(target.IsUnderAttack, "IsUnderAttack should be cleared on resolution");
        }
        finally { RaidTargetRegistry.Unregister(target); }
    }

    [EmpireDestructiveTest("Destructive.Battle")]
    public static void RaidTarget_AttackerVictory_FiresOnRaidLost_AndClearsFlag()
    {
        var obj = new WorldObject();
        var target = new CountingRaidTarget(obj) { IsUnderAttack = true };
        MilitaryOperation op = MakeEngagedRaidOp(obj);
        RaidTargetRegistry.Register(target);
        try
        {
            op.CompleteBattle(MakeResult(BattleWinner.Attacker));
            TestAssert.AreEqual(1, target.LostCount, "OnRaidLost should fire on an attacker victory");
            TestAssert.AreEqual(0, target.WonCount, "OnRaidWon should not fire on an attacker victory");
            TestAssert.IsFalse(target.IsUnderAttack, "IsUnderAttack should be cleared on resolution");
        }
        finally { RaidTargetRegistry.Unregister(target); }
    }

    [EmpireDestructiveTest("Destructive.Battle")]
    public static void RaidTarget_ErrorResult_SkipsCallbacks_ButClearsFlag()
    {
        var obj = new WorldObject();
        var target = new CountingRaidTarget(obj) { IsUnderAttack = true };
        MilitaryOperation op = MakeEngagedRaidOp(obj);
        RaidTargetRegistry.Register(target);
        try
        {
            op.CompleteBattle(MakeResult(BattleWinner.Error));
            TestAssert.AreEqual(0, target.WonCount, "No win/loss callback on an Error result");
            TestAssert.AreEqual(0, target.LostCount, "No win/loss callback on an Error result");
            TestAssert.IsFalse(target.IsUnderAttack,
                "IsUnderAttack must still be cleared on an Error result so the target isn't stranded");
        }
        finally { RaidTargetRegistry.Unregister(target); }
    }

    [EmpireDestructiveTest("Destructive.Battle")]
    public static void RaidTarget_ThrowingHandler_StillClearsFlag()
    {
        var obj = new WorldObject();
        var target = new CountingRaidTarget(obj) { IsUnderAttack = true, ThrowOnCallback = true };
        MilitaryOperation op = MakeEngagedRaidOp(obj);
        RaidTargetRegistry.Register(target);
        try
        {
            // The dispatch catches third-party handler exceptions; the flag is cleared in a finally.
            TestAssert.DoesNotThrow(() => op.CompleteBattle(MakeResult(BattleWinner.Defender)));
            TestAssert.AreEqual(1, target.WonCount, "Handler should have been invoked");
            TestAssert.IsFalse(target.IsUnderAttack,
                "IsUnderAttack must be cleared even when the handler throws");
        }
        finally { RaidTargetRegistry.Unregister(target); }
    }
}
