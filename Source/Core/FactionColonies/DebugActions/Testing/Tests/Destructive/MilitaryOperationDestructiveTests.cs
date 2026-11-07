namespace FactionColonies;

/* DESTRUCTIVE: creates the empire NPC faction if the player has not founded it yet, since an
   op's IsDefensive / IsOffensive compare its participants against the real empire faction.
   The ops themselves are standalone and never registered. */
public static class MilitaryOperationDestructiveTests
{
    [EmpireDestructiveTest("Destructive.Military")]
    public static void IsDefensive_PlayerFactionAsDefender_True()
    {
        Faction empire = DestructiveTestUtil.EnsureEmpireFaction();

        var op = MilitaryOperationTests.Make();
        op.defender.faction = empire;
        TestAssert.IsTrue(op.IsDefensive);
        TestAssert.IsFalse(op.IsOffensive);
    }

    [EmpireDestructiveTest("Destructive.Military")]
    public static void IsOffensive_PlayerFactionAsAggressor_True()
    {
        Faction empire = DestructiveTestUtil.EnsureEmpireFaction();

        var op = MilitaryOperationTests.Make();
        op.aggressor.faction = empire;
        TestAssert.IsTrue(op.IsOffensive);
        TestAssert.IsFalse(op.IsDefensive);
    }
}
