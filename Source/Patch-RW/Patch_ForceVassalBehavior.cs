using System.Collections.Generic;
using HarmonyLib;
using RimWar;
using RimWar.Planet;
using Verse;

namespace FactionColonies.RW
{
    /// <summary>
    /// RimWar assigns a faction's RimWarData.behavior only at initial enrollment
    /// (GenerateFactionBehavior -> RandomizeFactionBehavior, which classifies PColony as
    /// Vassal via WorldUtility.IsVassalFaction). That behavior is then serialized and NEVER
    /// re-derived for an already-tracked faction on load (AddRimWarFaction only runs for
    /// factions not already in the restored list).
    ///
    /// So a save whose PColony RimWarData was persisted with a non-Vassal behavior - e.g. it was
    /// first enrolled before the Vassal classification applied, or the field defaulted to the
    /// serialized fallback (Expansionist) - loads as a normal AI faction. RimWar then dispatches
    /// trade caravans / scouts / warbands from the player's Empire settlements every evaluation
    /// cycle, flooding the player with caravans. (And because Empire rebuilds PColony's trader
    /// pawnGroupMaker at runtime, those dispatches can fail vanilla CanGenerateFrom, spamming
    /// "no usable PawnGroupMakers for Trader".)
    ///
    /// This postfix re-asserts the Vassal classification on load for the Empire faction only: the
    /// single RimWarData whose faction is Empire's PColony (matched via FactionCache, not RimWar's
    /// broader IsVassalFaction) is forced back to Vassal if its behavior drifted. Vassal is RimWar's
    /// own intended classification for PColony - it excludes the faction from both the per-settlement
    /// action loop and the global action dispatch, while preserving the vassal-relation/heat handling
    /// the rest of Empire's RimWar compat relies on (unlike Excluded, which would drop PColony from
    /// RimWar entirely). Runs at PostLoadInit only; the value is then re-saved as Vassal, so the
    /// repair is permanent for that save.
    ///
    /// </summary>
    [HarmonyPatch(typeof(WorldComponent_PowerTracker))]
    [HarmonyPatch("ExposeData")]
    public static class Patch_ForceVassalBehavior
    {
        private static void Postfix(WorldComponent_PowerTracker __instance)
        {
            if (Scribe.mode != LoadSaveMode.PostLoadInit) return;

            List<RimWarData> data = __instance.RimWarData;
            if (data is null) return;

            foreach (RimWarData rwd in data)
            {
                if (rwd is null || !FindFC.IsEmpireFaction(rwd.RimWarFaction)) continue;

                // Exactly one Empire faction exists; handle it and stop.
                if (rwd.behavior != RimWarBehavior.Vassal)
                {
                    LogUtil.MessageForce("RimWar compat: forcing Empire faction (" + rwd.RimWarFaction.Name
                        + ") RimWar behavior from " + rwd.behavior
                        + " to Vassal (stale behavior was driving unwanted trade caravans).");
                    rwd.behavior = RimWarBehavior.Vassal;
                }
                return;
            }
        }
    }
}
