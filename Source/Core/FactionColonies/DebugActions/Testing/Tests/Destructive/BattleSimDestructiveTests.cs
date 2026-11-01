using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace FactionColonies
{
    /* DESTRUCTIVE: drives REAL battle simulation — creates offensive/defensive ops, runs the
       auto-resolve engine, and applies battle results (which can damage settlement morale and
       buildings). Self-built ops are resolved and their scheduled events swept so they don't
       linger, but the side effects of applied results are NOT reverted. */
    public static class BattleSimDestructiveTests
    {
        private static WorldSettlementFC FirstOrTransientSettlement()
        {
            WorldSettlementFC s = FindFC.Settlements?.FirstOrDefault();
            if (s is null) s = DestructiveTestUtil.CreateTransientSettlement();
            return s;
        }

        private static Faction AnyOtherFaction()
        {
            return Find.FactionManager?.AllFactionsListForReading
                .FirstOrDefault(fac => fac != null && !fac.IsPlayer && fac != FindFC.EmpireFaction);
        }

        [EmpireDestructiveTest("Destructive.Battle")]
        public static void CreateOffensiveOp_Guarded()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();
            WorldSettlementFC home = FirstOrTransientSettlement();
            if (home is null) TestAssert.Skip("No home settlement available");

            MilSquadFC template = DestructiveTestUtil.FirstHireableTemplate();
            if (template is null) TestAssert.Skip("No squad templates designed");
            MercenarySquadFC squad = null;
            TestAssert.DoesNotThrow(() => squad = FindFC.Military.HireSquad(template), "HireSquad threw");
            if (squad is null) TestAssert.Skip("Could not afford to hire a squad");
            FindFC.Military.Assign(squad, home, true); // CreateOffensiveOp requires source.settlement

            Settlement enemy = Find.WorldObjects.Settlements
                .FirstOrDefault(s => s.Faction != null && s.Faction.HostileTo(FindFC.EmpireFaction));
            if (enemy is null) TestAssert.Skip("No hostile settlement to raid");

            MilitaryOperation op = null;
            TestAssert.DoesNotThrow(() => op = FindFC.MilitaryManager.CreateOffensiveOp(
                squad, enemy, MilitaryJobDefOf.RaidEnemySettlement, enemy.Faction, GenDate.TicksPerDay),
                "CreateOffensiveOp threw");
            if (op is null) TestAssert.Skip("Offensive op rejected (morale lock?)");

            TestAssert.IsTrue(FindFC.MilitaryManager.Active.Contains(op), "Offensive op should be registered");
            DestructiveTestUtil.ResolveAndSweepOp(op); // cancel the in-transit raid so it doesn't spawn a real battle later
            TestAssert.IsFalse(FindFC.MilitaryManager.Active.Contains(op), "Op should be gone after Resolve");
            DestructiveTestUtil.AssertEmpireInvariants(f, "CreateOffensiveOp_Guarded");
        }

        [EmpireDestructiveTest("Destructive.Battle")]
        public static void AutoResolveProgress_InitializesAndCompletes()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();

            var op = new MilitaryOperation(-1, MilitaryJobDefOf.RaidEnemySettlement, PlanetTile.Invalid, null);
            op.aggressor.faction = FindFC.EmpireFaction;
            op.aggressor.force = new MilitaryForce(10.0, 1.0, null, FindFC.EmpireFaction);
            op.defender.force = new MilitaryForce(3.0, 1.0, null, null);
            op.phase = MilitaryOperationPhase.Engaged;

            TestAssert.DoesNotThrow(() => op.BeginAutoResolveProgress(), "BeginAutoResolveProgress threw");
            TestAssert.IsNotNull(op.battleResult, "BeginAutoResolveProgress should initialize battleResult");

            TestAssert.DoesNotThrow(() => op.CompleteBattle(op.battleResult), "CompleteBattle threw");
            TestAssert.IsTrue(op.phase == MilitaryOperationPhase.CooldownPending || op.phase == MilitaryOperationPhase.Resolved,
                $"After CompleteBattle phase should be CooldownPending/Resolved, was {op.phase}");

            TestAssert.DoesNotThrow(() => op.EnterCooldown(), "EnterCooldown threw");
            DestructiveTestUtil.ResolveAndSweepOp(op);
            DestructiveTestUtil.AssertEmpireInvariants(f, "AutoResolveProgress_InitializesAndCompletes");
        }

        [EmpireDestructiveTest("Destructive.Battle")]
        public static void CompleteBattle_DefensiveLoss_AppliesToSettlement()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();
            WorldSettlementFC settlement = FirstOrTransientSettlement();
            if (settlement is null) TestAssert.Skip("No settlement available");

            var op = new MilitaryOperation(-1, MilitaryJobDefOf.DefendOwnSettlement, settlement.Tile, settlement);
            op.defender.faction = FindFC.EmpireFaction;
            op.defender.homeSettlement = settlement;
            op.defender.force = new MilitaryForce(5.0, 1.0, settlement, FindFC.EmpireFaction);
            op.aggressor.faction = AnyOtherFaction();
            op.aggressor.force = new MilitaryForce(8.0, 1.0, null, op.aggressor.faction);
            op.phase = MilitaryOperationPhase.Engaged;

            // Attacker victory => the defending settlement takes the loss: this runs the real
            // MilitaryJobHandler_Defend.ApplyResult (loyalty/happiness/building destruction).
            var result = new BattleResult { winner = BattleWinner.Attacker };
            TestAssert.DoesNotThrow(() => op.CompleteBattle(result), "CompleteBattle (defensive loss) threw");
            DestructiveTestUtil.ResolveAndSweepOp(op);
            DestructiveTestUtil.AssertEmpireInvariants(f, "CompleteBattle_DefensiveLoss_AppliesToSettlement");
        }

        [EmpireDestructiveTest("Destructive.Battle")]
        public static void CreateDefensiveOp_Guarded()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();
            WorldSettlementFC settlement = FirstOrTransientSettlement();
            if (settlement is null) TestAssert.Skip("No settlement available");

            Faction attacker = AnyOtherFaction();
            var attackerForce = new MilitaryForce(8.0, 1.0, null, attacker);

            MilitaryOperation op = null;
            TestAssert.DoesNotThrow(() => op = FindFC.MilitaryManager.CreateDefensiveOp(settlement, attackerForce, attacker),
                "CreateDefensiveOp threw");
            if (op != null)
            {
                TestAssert.IsTrue(FindFC.MilitaryManager.Active.Contains(op), "Defensive op should be registered");
                DestructiveTestUtil.ResolveAndSweepOp(op);
            }
            DestructiveTestUtil.AssertEmpireInvariants(f, "CreateDefensiveOp_Guarded");
        }

        [EmpireDestructiveTest("Destructive.Battle")]
        public static void BeginEngagement_ResolvesDefenderForce_NoCrash()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();
            Faction enemy = AnyOtherFaction();
            if (enemy is null) TestAssert.Skip("No other faction to fight");

            var op = new MilitaryOperation(-1, MilitaryJobDefOf.RaidEnemySettlement, PlanetTile.Invalid, null);
            op.aggressor.faction = FindFC.EmpireFaction;
            op.aggressor.force = new MilitaryForce(10.0, 1.0, null, FindFC.EmpireFaction);
            op.defender.faction = enemy; // BeginEngagement lazily resolves the defender force from the faction

            TestAssert.DoesNotThrow(() => op.BeginEngagement(), "BeginEngagement threw");
            TestAssert.AreEqual((object)MilitaryOperationPhase.Engaged, (object)op.phase, "BeginEngagement should set phase Engaged");
            DestructiveTestUtil.ResolveAndSweepOp(op);
            DestructiveTestUtil.AssertEmpireInvariants(f, "BeginEngagement_ResolvesDefenderForce");
        }
    }
}
