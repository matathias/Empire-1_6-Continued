using System.Collections.Generic;
using System.Linq;
using FactionColonies.util;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Lets the player capture a hostile NPC settlement they defeat with their own colonists, instead
    /// of only razing it. When a base is defeated, a forced-pause dialog offers Raze (vanilla) or
    /// Capture. Capture is deferred: vanilla still tears the base down into a DestroyedSettlement so the
    /// player can keep looting, and the tile is recorded on FactionFC; once the player leaves and the
    /// map is removed, the tile is converted into an Empire settlement (same setup as a capture raid).
    ///
    /// Empire's own WorldSettlementFC never reaches here — its TickInterval deliberately omits
    /// CheckDefeated — but the WorldSettlementFC/EmpireFaction guards below double-protect regardless.
    /// </summary>
    public static class SettlementCaptureTracker
    {
        // Transient (not serialized): a save mid-dialog simply re-prompts on load.
        public static readonly HashSet<PlanetTile> awaitingDecision = new HashSet<PlanetTile>();
        public static readonly HashSet<PlanetTile> razeApproved = new HashSet<PlanetTile>();

        // A tile lingering in awaitingDecision across a game switch would make the CheckDefeated
        // prefix suppress the re-prompt forever (stranding the base), so reset with the other
        // per-game static state on load / new game.
        public static void Reset()
        {
            awaitingDecision.Clear();
            razeApproved.Clear();
        }

        public static bool HasPendingCapture(PlanetTile tile)
        {
            FactionFC fc = FindFC.FactionComp;
            if (fc?.pendingCaptures is null) return false;
            for (int i = 0; i < fc.pendingCaptures.Count; i++)
            {
                if (fc.pendingCaptures[i].tile == tile) return true;
            }
            return false;
        }

        public static void PromptRazeOrCapture(Settlement factionBase)
        {
            PlanetTile tile = factionBase.Tile;
            string name = factionBase.LabelCap;
            TechLevel tech = factionBase.Faction.def.techLevel;

            Dialog_MessageBox dialog = new Dialog_MessageBox(
                "FCCaptureOrRazeDesc".Translate(name, FindFC.EmpireName),
                "FCCaptureButton".Translate(), () => ChooseCapture(tile, name, tech, factionBase),
                "FCRazeButton".Translate(), () => ChooseRaze(tile, factionBase),
                "FCCaptureOrRazeTitle".Translate());
            // Dismiss/Esc defaults to the safe vanilla outcome.
            dialog.cancelAction = () => ChooseRaze(tile, factionBase);
            Find.WindowStack.Add(dialog);
        }

        private static void ChooseRaze(PlanetTile tile, Settlement factionBase)
        {
            awaitingDecision.Remove(tile);
            razeApproved.Add(tile);
            // Re-enter the (patched) CheckDefeated: the razeApproved flag makes the prefix fall through
            // to the vanilla teardown this time.
            if (factionBase is object && !factionBase.Destroyed)
                SettlementDefeatUtility.CheckDefeated(factionBase);
            razeApproved.Remove(tile);
        }

        private static void ChooseCapture(PlanetTile tile, string name, TechLevel tech, Settlement factionBase)
        {
            awaitingDecision.Remove(tile);
            FactionFC fc = FindFC.FactionComp;
            if (fc is object && !HasPendingCapture(tile))
                fc.pendingCaptures.Add(new PendingSettlementCapture(tile, name, tech));
            // Let vanilla create the DestroyedSettlement so the player can loot; the pending entry
            // makes the prefix fall through, and conversion happens when the map is removed.
            if (factionBase is object && !factionBase.Destroyed)
                SettlementDefeatUtility.CheckDefeated(factionBase);
        }
    }

    /// <summary>
    /// Intercepts vanilla settlement defeat to offer the raze/capture choice for player-raided
    /// enemy bases. Returns true (run vanilla) for anything that isn't a fresh, player-defeated,
    /// non-Empire base awaiting a decision.
    /// </summary>
    [HarmonyPatch(typeof(SettlementDefeatUtility), nameof(SettlementDefeatUtility.CheckDefeated))]
    public static class SettlementDefeatUtility_CheckDefeated_Patch
    {
        public static bool Prefix(Settlement factionBase)
        {
            // An Empire manual offensive battle owns this tile -> Empire's EndOffense / ApplyResult
            // resolves the settlement's fate. Do not pop the raze/capture dialog on the same
            // garrison-cleared event (a double resolution). This guard also lets the offense
            // suppression win regardless of Harmony prefix ordering.
            if (OffenseGuardUtil.OffenseActiveAt(factionBase)) return false;
            if (!FCSettings.enableSettlementCapture) return true;
            if (factionBase is null) return true;

            Faction fac = factionBase.Faction;
            if (fac is null || fac.IsPlayer) return true;
            if (factionBase is WorldSettlementFC) return true;
            if (FindFC.IsEmpireFaction(fac)) return true;

            Map map = factionBase.Map;
            if (map is null) return true;
            if (!SettlementDefeatUtility.IsDefeated(map, fac)) return true;
            // Only prompt for a base the player actually raided (their colonists are on the map).
            if (!map.mapPawns.FreeColonistsSpawned.Any()) return true;

            PlanetTile tile = factionBase.Tile;

            // Decision already made -> run vanilla teardown. For capture, the resulting
            // DestroyedSettlement is the loot substrate; conversion happens on map removal.
            if (SettlementCaptureTracker.razeApproved.Contains(tile)) return true;
            if (SettlementCaptureTracker.HasPendingCapture(tile)) return true;

            // Dialog already open for this tile -> keep suppressing until the player picks.
            if (SettlementCaptureTracker.awaitingDecision.Contains(tile)) return false;

            SettlementCaptureTracker.awaitingDecision.Add(tile);
            SettlementCaptureTracker.PromptRazeOrCapture(factionBase);
            return false;
        }
    }

    /// <summary>
    /// When the player leaves a captured base and its map is removed, convert the tile into an
    /// Empire settlement. This is the deferred second half of a "capture" choice.
    /// </summary>
    [HarmonyPatch(typeof(Game), nameof(Game.DeinitAndRemoveMap))]
    public static class Game_DeinitAndRemoveMap_Patch
    {
        public static void Postfix(Map map)
        {
            if (map is null) return;

            FactionFC fc = FindFC.FactionComp;
            if (fc?.pendingCaptures is null || fc.pendingCaptures.Count == 0) return;

            PlanetTile tile = map.Tile;
            PendingSettlementCapture entry = null;
            for (int i = 0; i < fc.pendingCaptures.Count; i++)
            {
                if (fc.pendingCaptures[i].tile == tile) { entry = fc.pendingCaptures[i]; break; }
            }
            if (entry is null) return;

            fc.pendingCaptures.Remove(entry);

            // Clear the lingering DestroyedSettlement still on the tile so our settlement is the sole
            // occupant. (CheckRemoveMapNow would destroy it right after us anyway; doing it here first
            // just avoids a momentary same-tile overlap — its guard skips an already-destroyed object.)
            DestroyedSettlement destroyed = Find.WorldObjects.DestroyedSettlementAt(tile);
            if (destroyed is object && !destroyed.Destroyed)
                destroyed.Destroy();

            ColonyUtil.SetupCapturedSettlement(tile, entry.name, entry.tech);
        }
    }
}
