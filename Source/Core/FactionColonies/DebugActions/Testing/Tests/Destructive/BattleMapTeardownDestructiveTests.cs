using System.Linq;
using RimWorld;
using Verse;

namespace FactionColonies
{
    /* Destructive regression tests for the battle-map teardown fixes (Fix 4):
       - 4c: SquadMapTeardownUtil.EvacuatePlayerPawns delivers a stranded player pawn home instead of
             letting the map removal destroy it.
       - 4a: BattlefieldContext.AnyLivePlayerPawnInMapContainers detects a player pawn tucked into an
             on-map transporter, so DeleteMap KEEPS the map instead of tearing it (and the transport
             with the pawn inside) down at battle end.

       These are the suite's first map-generating tests. Generating a real defense map, spawning pawns,
       and loading a transporter are all volatile, so every risky step is guarded (DoesNotThrow / Skip)
       per the destructive-test contract: it may leave the game messy, but it must never crash. State is
       cleaned up best-effort at the end (map removed, settlement removed, invariants checked). */
    public static class BattleMapTeardownDestructiveTests
    {
        [EmpireDestructiveTest("Destructive.Battle")]
        public static void EvacuatePlayerPawns_DeliversNotDestroys()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();
            WorldSettlementFC settlement = DestructiveTestUtil.CreateTransientSettlement();
            if (settlement is null) TestAssert.Skip("Could not create a transient settlement");

            BattlefieldContext ctx = FindFC.MilitaryManager?.GetOrCreateBattlefield(settlement.Tile);
            if (ctx is null)
            {
                DestructiveTestUtil.SafeRemoveSettlement(settlement);
                TestAssert.Skip("No battlefield context");
            }

            Map map = TryGenerateMap(ctx);
            if (map is null) { Teardown(f, settlement, ctx); TestAssert.Skip("Battle map not generated"); }

            Pawn pawn = TrySpawnPlayerColonist(map);
            if (pawn is null) { Teardown(f, settlement, ctx); TestAssert.Skip("Could not spawn test colonist"); }

            TestAssert.DoesNotThrow(() => SquadMapTeardownUtil.EvacuatePlayerPawns(map),
                "EvacuatePlayerPawns threw");

            TestAssert.IsFalse(pawn.Spawned, "Evacuated pawn should be despawned from the battle map");
            TestAssert.IsFalse(pawn.Destroyed, "Evacuated pawn must NOT be destroyed by teardown");
            bool queuedHome = FindFC.EventManager != null
                && FindFC.EventManager.Events.Any(e => e?.goods != null && e.goods.Contains(pawn));
            TestAssert.IsTrue(queuedHome, "A delivery event should have been queued to send the pawn home");

            // Cleanup: drop the delivery event we created + destroy the pawn, then the map + settlement.
            FindFC.FactionComp?.RemoveEventsWhere(e => e?.goods != null && e.goods.Contains(pawn));
            if (!pawn.Destroyed) pawn.Destroy();
            Teardown(f, settlement, ctx);
        }

        [EmpireDestructiveTest("Destructive.Battle")]
        public static void DeleteMap_KeepsMap_WhenPlayerPawnContainerized()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();
            WorldSettlementFC settlement = DestructiveTestUtil.CreateTransientSettlement();
            if (settlement is null) TestAssert.Skip("Could not create a transient settlement");

            BattlefieldContext ctx = FindFC.MilitaryManager?.GetOrCreateBattlefield(settlement.Tile);
            if (ctx is null)
            {
                DestructiveTestUtil.SafeRemoveSettlement(settlement);
                TestAssert.Skip("No battlefield context");
            }

            Map map = TryGenerateMap(ctx);
            if (map is null) { Teardown(f, settlement, ctx); TestAssert.Skip("Battle map not generated"); }

            // Control: a fresh map with no player pawns has none containerized.
            TestAssert.IsFalse(BattlefieldContext.AnyLivePlayerPawnInMapContainers(map),
                "Fresh map should have no containerized player pawns");

            Pawn pawn = TrySpawnPlayerColonist(map);
            if (pawn is null) { Teardown(f, settlement, ctx); TestAssert.Skip("Could not spawn test colonist"); }

            // Control: a spawned (not containerized) pawn must NOT trip the container scan.
            TestAssert.IsFalse(BattlefieldContext.AnyLivePlayerPawnInMapContainers(map),
                "A spawned pawn is not containerized");

            Thing pod = TryLoadPawnIntoTransporter(map, pawn);
            if (pod is null)
            {
                if (!pawn.Destroyed) pawn.Destroy();
                Teardown(f, settlement, ctx);
                TestAssert.Skip("Could not build a loaded transporter");
            }

            // The fix's core: the containerized (unspawned) player pawn is detected.
            TestAssert.IsFalse(pawn.Spawned, "Pawn should now be inside the transporter, not spawned");
            TestAssert.IsTrue(BattlefieldContext.AnyLivePlayerPawnInMapContainers(map),
                "Containerized live player pawn must be detected");

            // Integration: DeleteMap must KEEP the map (previously it tore down and destroyed the transport).
            TestAssert.DoesNotThrow(() => ctx.DeleteMap(true), "DeleteMap threw");
            TestAssert.IsNotNull(ctx.map, "DeleteMap must keep the map while a player pawn is in a transporter");

            // Cleanup: destroy the transporter (and the pawn inside), then the map + settlement.
            TestAssert.DoesNotThrow(() => { if (!pod.Destroyed) pod.Destroy(); });
            if (pawn is object && !pawn.Destroyed) pawn.Destroy();
            Teardown(f, settlement, ctx);
        }

        /* -*- helpers -*- */

        private static Map TryGenerateMap(BattlefieldContext ctx)
        {
            Map map = null;
            TestAssert.DoesNotThrow(() => map = ctx.GenerateMap(), "GenerateMap threw");
            return map;
        }

        private static Pawn TrySpawnPlayerColonist(Map map)
        {
            Pawn pawn = null;
            TestAssert.DoesNotThrow(() =>
            {
                Pawn p = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
                if (!CellFinder.TryFindRandomCellNear(map.Center, map, 25, c => c.Standable(map), out IntVec3 cell))
                    cell = map.Center;
                GenSpawn.Spawn(p, cell, map);
                pawn = p;
            });
            return (pawn is object && pawn.Spawned) ? pawn : null;
        }

        private static Thing TryLoadPawnIntoTransporter(Map map, Pawn pawn)
        {
            Thing pod = null;
            TestAssert.DoesNotThrow(() =>
            {
                ThingDef def = ThingDefOf.TransportPod;
                ThingDef stuff = def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null;
                Thing t = ThingMaker.MakeThing(def, stuff);
                if (!CellFinder.TryFindRandomCellNear(map.Center, map, 25, c => c.Standable(map), out IntVec3 cell))
                    cell = map.Center;
                GenSpawn.Spawn(t, cell, map);

                CompTransporter tr = t.TryGetComp<CompTransporter>();
                if (pawn.Spawned) pawn.DeSpawn();
                if (tr != null && tr.innerContainer.TryAddOrTransfer(pawn, false))
                    pod = t;
                else if (!t.Destroyed)
                    t.Destroy();
            });
            return pod;
        }

        private static void Teardown(FactionFC f, WorldSettlementFC settlement, BattlefieldContext ctx)
        {
            TestAssert.DoesNotThrow(() =>
            {
                if (ctx?.map is object && Find.Maps.Contains(ctx.map))
                {
                    Map m = ctx.map;
                    ctx.map = null;
                    Current.Game.DeinitAndRemoveMap(m, false);
                }
                if (settlement is object) FindFC.MilitaryManager?.RemoveBattlefield(settlement.Tile);
            });
            if (settlement is object) DestructiveTestUtil.SafeRemoveSettlement(settlement);
            if (f is object) DestructiveTestUtil.AssertEmpireInvariants(f, "BattleMapTeardown test");
        }
    }
}
