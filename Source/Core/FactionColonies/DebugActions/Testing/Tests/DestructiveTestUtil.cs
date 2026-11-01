using System;
using System.Collections.Generic;
using System.Linq;
using FactionColonies.util;
using RimWorld.Planet;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Shared helpers for the DESTRUCTIVE test tier (see <see cref="EmpireDestructiveTestAttribute"/>).
    /// No snapshot/restore: destructive tests thrash live state and are not cleaned up. These helpers
    /// only (a) guard preconditions, (b) provide the create/destroy recipe reused from
    /// <c>DebugUtil.CreateTenRandomSettlements</c>, and (c) supply the post-mutation invariant battery
    /// that asserts the faction is still internally consistent after the thrash.
    /// </summary>
    public static class DestructiveTestUtil
    {
        /// <summary>Returns the live faction component, or skips the test if there is no game/faction.</summary>
        public static FactionFC RequireFaction()
        {
            FactionFC f = FindFC.FactionComp;
            if (f is null) TestAssert.Skip("No FactionFC world component (no active game?)");
            return f;
        }

        /// <summary>First squad template the player has designed, or null (caller skips). Hireability
        /// also depends on silver — <see cref="MilitaryFC.HireSquad"/> returns null if unaffordable.</summary>
        public static MilSquadFC FirstHireableTemplate()
        {
            return FindFC.Military?.squads?.FirstOrDefault();
        }

        /// <summary>
        /// Creates a real player settlement on a randomly-found valid surface tile, reusing the same
        /// recipe as the "Create 10 Random Settlements" debug action. Returns null if no valid tile
        /// was found within <paramref name="maxAttempts"/> (caller skips). This mutates live world state.
        /// </summary>
        public static WorldSettlementFC CreateTransientSettlement(int maxAttempts = 500, WorldSettlementDef def = null)
        {
            if (def is null) def = WorldSettlementDefOf.WorldSettlementDef_Surface;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                PlanetTile tile = TileFinder.RandomSettlementTileFor(Find.WorldGrid.Surface, FindFC.EmpireFaction);
                if (!tile.Valid) continue;
                if (!WorldTileChecker.IsValidTileForNewSettlement(tile, def)) continue;
                if (FindFC.FactionComp.CheckSettlementCaravansList(tile)) continue;

                return ColonyUtil.CreatePlayerColonySettlement(tile, def);
            }
            return null;
        }

        /// <summary>Removes a settlement via the real teardown path, swallowing+logging any exception
        /// so cleanup-as-an-action never masks the test's actual result.</summary>
        public static void SafeRemoveSettlement(WorldSettlementFC settlement)
        {
            if (settlement is null) return;
            try
            {
                ColonyUtil.RemovePlayerSettlement(settlement);
            }
            catch (Exception ex)
            {
                LogUtil.Warning($"DestructiveTestUtil.SafeRemoveSettlement threw (ignored): {ex}");
            }
        }

        /// <summary>Resolves a hand-built (un-managed) military operation and drops any events it
        /// scheduled, so it doesn't linger as a phantom recurring event. Swallows errors so cleanup
        /// never masks the test result.</summary>
        public static void ResolveAndSweepOp(MilitaryOperation op)
        {
            if (op is null) return;
            try { op.Resolve(); }
            catch (Exception ex) { LogUtil.Warning($"ResolveAndSweepOp Resolve threw (ignored): {ex}"); }
            if (op.sourceEvents != null)
            {
                foreach (FCEvent e in op.sourceEvents.ToList())
                {
                    try { FindFC.EventManager?.Remove(e); }
                    catch (Exception ex) { LogUtil.Warning($"ResolveAndSweepOp event remove threw (ignored): {ex}"); }
                }
            }
        }

        /// <summary>
        /// The post-mutation assertion battery. After a destructive test has thrashed state, this
        /// asserts the faction is still internally consistent: existing engine validators run without
        /// throwing, caches recompute, and there are no null/duplicate settlements, dangling bills, or
        /// orphaned military operations. Every engine call is wrapped so a throw becomes a structured
        /// FAIL rather than an uncaught crash.
        /// </summary>
        public static void AssertEmpireInvariants(FactionFC f, string context)
        {
            if (f is null) return;

            // Existing engine validators must survive the thrashed state.
            TestAssert.DoesNotThrow(() => f.ValidateSettlementCaravansList(),
                $"{context}: ValidateSettlementCaravansList threw");
            TestAssert.DoesNotThrow(() => FindFC.Military?.CheckMilitaryUtilForErrors(),
                $"{context}: CheckMilitaryUtilForErrors threw");
            TestAssert.DoesNotThrow(() => FindFC.MilitaryManager?.RebuildIndices(),
                $"{context}: MilitaryManager.RebuildIndices threw");

            // Caches must recompute cleanly after the mutations.
            TestAssert.DoesNotThrow(() =>
            {
                f.DirtyFactionProfitCache();
                f.DirtyAveragesCache();
                f.DirtyTechLevelCache();
                f.InvalidateFactionStatCache();
                if (double.IsNaN(f.profit)) LogUtil.Warning($"{context}: faction.profit is NaN after thrash");
            }, $"{context}: cache recompute threw");

            // No null / duplicate settlements.
            List<WorldSettlementFC> settlements = f.settlements;
            if (settlements != null)
            {
                TestAssert.IsFalse(settlements.Any(s => s is null),
                    $"{context}: faction.settlements contains a null entry");
                TestAssert.AreEqual(settlements.Count, settlements.Distinct().Count(),
                    $"{context}: faction.settlements contains duplicates");

                // No tax bill references a settlement that is no longer part of the faction.
                if (FindFC.TaxLedger?.Bills != null)
                {
                    foreach (BillFC bill in FindFC.TaxLedger.Bills)
                    {
                        if (bill?.settlement is null) continue;
                        TestAssert.IsTrue(settlements.Contains(bill.settlement),
                            $"{context}: tax bill references settlement '{bill.settlement.Name}' not in faction.settlements");
                    }
                }

                // No active military operation references a home settlement / squad that no longer exists.
                MilitaryOperationManager mgr = FindFC.MilitaryManager;
                List<MercenarySquadFC> squads = FindFC.Military?.mercenarySquads;
                if (mgr?.Active != null)
                {
                    foreach (MilitaryOperation op in mgr.Active)
                    {
                        if (op is null) continue;
                        AssertHomeSettlementLive(op.aggressor?.homeSettlement, settlements, context, "aggressor");
                        AssertHomeSettlementLive(op.defender?.homeSettlement, settlements, context, "defender");
                        AssertSquadLive(op.aggressor?.squad, squads, context, "aggressor");
                        AssertSquadLive(op.defender?.squad, squads, context, "defender");
                    }
                }
            }
        }

        private static void AssertHomeSettlementLive(WorldSettlementFC home,
            List<WorldSettlementFC> settlements, string context, string side)
        {
            if (home is null) return;
            TestAssert.IsTrue(settlements.Contains(home),
                $"{context}: active op {side}.homeSettlement '{home.Name}' is not in faction.settlements (orphan)");
        }

        private static void AssertSquadLive(MercenarySquadFC squad,
            List<MercenarySquadFC> squads, string context, string side)
        {
            if (squad is null || squads is null) return;
            TestAssert.IsTrue(squads.Contains(squad),
                $"{context}: active op {side}.squad '{squad.DisplayName}' is not in the mercenary pool (orphan)");
        }
    }
}
