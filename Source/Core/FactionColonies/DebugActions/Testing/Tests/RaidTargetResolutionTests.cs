namespace FactionColonies;

public static class RaidTargetResolutionTests
{
    /* -*- HasActiveOpTargeting: the un-stick detector used by MilitaryTick's orphan sweep -*-
       Uses a transient MilitaryOperationManager and its public `active` list, so these don't
       touch the live faction's operations. */

    [EmpireTest("Battle")]
    public static void HasActiveOpTargeting_LiveOp_ReturnsTrue()
    {
        var mgr = new MilitaryOperationManager();
        var obj = new WorldObject();
        mgr.active.Add(new MilitaryOperation(-1, null, PlanetTile.Invalid, obj)
        {
            phase = MilitaryOperationPhase.Engaged
        });
        TestAssert.IsTrue(mgr.HasActiveOpTargeting(obj),
            "A live op targeting the object should be detected");
    }

    [EmpireTest("Battle")]
    public static void HasActiveOpTargeting_NoOp_ReturnsFalse()
    {
        var mgr = new MilitaryOperationManager();
        TestAssert.IsFalse(mgr.HasActiveOpTargeting(new WorldObject()),
            "No op means the flag is orphaned — detector should report no live op");
    }

    [EmpireTest("Battle")]
    public static void HasActiveOpTargeting_FinishedOps_ReturnsFalse()
    {
        var mgr = new MilitaryOperationManager();
        var obj = new WorldObject();
        mgr.active.Add(new MilitaryOperation(-1, null, PlanetTile.Invalid, obj)
        {
            phase = MilitaryOperationPhase.CooldownPending
        });
        mgr.active.Add(new MilitaryOperation(-2, null, PlanetTile.Invalid, obj)
        {
            phase = MilitaryOperationPhase.Resolved
        });
        TestAssert.IsFalse(mgr.HasActiveOpTargeting(obj),
            "Ops whose battle is over should not keep a target flagged as under attack");
    }
}
