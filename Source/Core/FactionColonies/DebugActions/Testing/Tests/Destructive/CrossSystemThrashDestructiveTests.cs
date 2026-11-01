using System.Linq;
using RimWorld.Planet;
using Verse;

namespace FactionColonies
{
    /* DESTRUCTIVE: chains multiple subsystems together to stress cross-system consistency — the
       harshest tier. Nothing is reverted; the goal is that after the thrash the faction is still
       internally consistent (AssertEmpireInvariants) and the game has not crashed. */
    public static class CrossSystemThrashDestructiveTests
    {
        [EmpireDestructiveTest("Destructive.Thrash")]
        public static void FullCycle_CreateHireTaxBattleEventLevelEdict()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();

            // 1. Settlement
            WorldSettlementFC s = DestructiveTestUtil.CreateTransientSettlement();
            if (s is null) s = FindFC.Settlements?.FirstOrDefault();
            if (s is null) TestAssert.Skip("No settlement available");

            // 2. Hire + assign a squad (best effort)
            MilSquadFC template = DestructiveTestUtil.FirstHireableTemplate();
            if (template != null)
            {
                MercenarySquadFC squad = null;
                TestAssert.DoesNotThrow(() => squad = FindFC.Military.HireSquad(template), "HireSquad threw");
                if (squad != null) FindFC.Military.Assign(squad, s, true);
            }

            // 3. Tax cycle
            TestAssert.DoesNotThrow(() => FindFC.TaxLedger.AddTax(f), "AddTax threw");
            TestAssert.DoesNotThrow(() => FindFC.TaxLedger.AutoresolveBills(), "AutoresolveBills threw");
            TestAssert.DoesNotThrow(() => FindFC.TaxLedger.ProcessBills(), "ProcessBills threw");

            // 4. Battle: apply a real defensive loss to the settlement
            var op = new MilitaryOperation(-1, MilitaryJobDefOf.DefendOwnSettlement, s.Tile, s);
            op.defender.faction = FindFC.EmpireFaction;
            op.defender.homeSettlement = s;
            op.defender.force = new MilitaryForce(5.0, 1.0, s, FindFC.EmpireFaction);
            op.aggressor.force = new MilitaryForce(8.0, 1.0, null, null);
            op.phase = MilitaryOperationPhase.Engaged;
            TestAssert.DoesNotThrow(() => op.CompleteBattle(new BattleResult { winner = BattleWinner.Attacker }),
                "CompleteBattle threw");
            DestructiveTestUtil.ResolveAndSweepOp(op);

            // 5. Event
            FCEvent evt = null;
            TestAssert.DoesNotThrow(() => evt = FCEventMaker.MakeEvent(FCEventDefOf.cooldownMilitary), "MakeEvent threw");
            if (evt != null)
            {
                TestAssert.DoesNotThrow(() => FindFC.EventManager.AddEvent(evt), "AddEvent threw");
                TestAssert.DoesNotThrow(() => FindFC.EventManager.RemoveWhere(e => ReferenceEquals(e, evt)), "RemoveWhere threw");
            }

            // 6. Faction leveling
            TestAssert.DoesNotThrow(() => f.AddExperienceToFactionLevel(100000f), "AddExperienceToFactionLevel threw");

            // 7. Edicts
            TestAssert.DoesNotThrow(() =>
            {
                FCPolicyDef edict = DefDatabase<FCPolicyDef>.AllDefsListForReading
                    .FirstOrDefault(d => d.IsEdict && !FindFC.PolicyManager.HasEdict(d));
                if (edict != null) FindFC.PolicyManager.EnactEdict(edict);
                FindFC.PolicyManager.RevokeAllEdicts();
            }, "Edict enact/revoke threw");

            DestructiveTestUtil.AssertEmpireInvariants(f, "FullCycle_CreateHireTaxBattleEventLevelEdict");
        }

        [EmpireDestructiveTest("Destructive.Thrash")]
        public static void OrphanSweep_RemoveSettlementDuringOp()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();

            WorldSettlementFC s = DestructiveTestUtil.CreateTransientSettlement();
            if (s is null) TestAssert.Skip("No valid tile found for a new settlement");

            // Best-effort: stand up a deploy op against the settlement so removal must cancel it.
            MilSquadFC template = DestructiveTestUtil.FirstHireableTemplate();
            if (template != null)
            {
                MercenarySquadFC squad = null;
                TestAssert.DoesNotThrow(() => squad = FindFC.Military.HireSquad(template), "HireSquad threw");
                if (squad != null)
                {
                    FindFC.Military.Assign(squad, s, true);
                    PlanetTile tile = s.Tile;
                    TestAssert.DoesNotThrow(() => FindFC.MilitaryManager.CreateDeployOp(squad, tile), "CreateDeployOp threw");
                }
            }

            // Remove the settlement out from under any op referencing it.
            DestructiveTestUtil.SafeRemoveSettlement(s);

            if (FindFC.MilitaryManager?.Active != null)
            {
                TestAssert.IsFalse(FindFC.MilitaryManager.Active.Any(o =>
                        o != null && (o.aggressor?.homeSettlement == s || o.defender?.homeSettlement == s || o.targetObject == s)),
                    "No active op should still reference the removed settlement");
            }
            DestructiveTestUtil.AssertEmpireInvariants(f, "OrphanSweep_RemoveSettlementDuringOp");
        }

        [EmpireDestructiveTest("Destructive.Thrash")]
        public static void RapidCreateDestroy_x3()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();
            int baseline = f.settlements.Count;

            for (int round = 0; round < 3; round++)
            {
                WorldSettlementFC a = DestructiveTestUtil.CreateTransientSettlement();
                WorldSettlementFC b = DestructiveTestUtil.CreateTransientSettlement();
                if (a is null && b is null) TestAssert.Skip($"No valid tiles found on round {round}");

                TestAssert.DoesNotThrow(() => FindFC.TaxLedger.AddTax(f), $"AddTax threw on round {round}");

                DestructiveTestUtil.SafeRemoveSettlement(a);
                DestructiveTestUtil.SafeRemoveSettlement(b);
                DestructiveTestUtil.AssertEmpireInvariants(f, $"RapidCreateDestroy round {round}");
            }

            TestAssert.AreEqual(baseline, f.settlements.Count, "Settlement count should return to baseline");
        }
    }
}
