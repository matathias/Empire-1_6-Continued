using FactionColonies.util;
using HarmonyLib;
using Verse;
using Verse.AI.Group;
using RimWorld;

namespace FactionColonies
{
    [HarmonyPatch(typeof(Pawn), "Kill")]
    class MercenaryDied
    {
        static bool Prefix(Pawn __instance, DamageInfo? dinfo)
        {
            if (__instance.IsMercenary())
            {
                if (__instance.Faction != FindFC.EmpireFaction) __instance.SetFaction(FindFC.EmpireFaction);

                // Schedule the gradual happiness/unrest penalty on the merc's home settlement. Uses a
                // map-independent lookup so auto-resolved/off-map deaths are penalized too.
                EmpireDeathPenaltyUtil.HandleMercDeath(__instance, dinfo);

                var mfc = FindFC.Military;
                if (mfc is null) return true;
                MercenarySquadFC squad = mfc.ReturnSquadFromUnit(__instance);
                if (squad != null)
                {
                    Mercenary merc = mfc.ReturnMercenaryFromUnit(__instance, squad);
                    if (merc != null)
                    {
                        // Fire death event so submods can react. Auto-replacement was removed by
                        // the strict-manual outfit refactor; the merc's slot is left as an empty
                        // placeholder (pawn = null) and the player must explicitly use
                        // "Fill Empty Slots" in the inspection window to refill it.
                        MercenaryDeathEvent deathEvt = new MercenaryDeathEvent(merc, squad, squad.settlement);
                        LifecycleRegistry.InvokeOnMercenaryDeath(deathEvt);

                        // Mark the slot empty — keep the Mercenary entry so its loadout reference
                        // survives for Fill, but null its pawn.
                        merc.pawn = null;
                        FindFC.Military?.RebuildMercenaryPawnSet();
                    }
                    else
                    {
                        // Not a top-level merc — it's a sub-pawn (animal or mech). Leave the wrapper
                        // in place as a "Missing" placeholder (identity preserved) so the player pays
                        // to replace it; just null the pawn. Neither animals nor mechs are free-replaced.
                        NullDeadSubPawn(mfc.FindSubPawnWrapper(__instance));
                    }
                }
                else
                {
                    // ReturnSquadFromUnit only matches on-map pawns; a sub-pawn can die off-map.
                    // Fall back to a global, map-independent sub-pawn lookup before warning.
                    Mercenary sub = mfc.FindSubPawnWrapper(__instance);
                    if (sub != null) NullDeadSubPawn(sub);
                    else LogUtil.Warning("Mercenary Errored out. Did not find squad.");
                }

                // The merc's worn weapons and apparel now dissolve on death via the vanilla death
                // acidifier implant (see MercenaryPawnFactory.TryApplyDeathAcidifier). The corpse and any
                // gear dropped during the fight are left for the player, matching vanilla behaviour.
                return true;
            }

            // Non-merc Empire trade-caravan pawn: penalize its tagged home settlement (the lord is still
            // attached at Kill-prefix time — base.Kill severs it before Notify_MemberDied) and check for a
            // pack-animal wipe.
            if (__instance.Faction == FindFC.EmpireFaction)
            {
                Lord lord = __instance.GetLord();
                if (lord?.LordJob is LordJob_TradeWithColony)
                {
                    EmpireDeathPenaltyUtil.HandleCaravanPawnDeath(__instance, dinfo, lord);
                }
            }

            // Hired Empire laborer: a temporary player colonist whose quest home faction is the Empire.
            // Route through the settlement-happiness penalty (home == null -> highest-prosperity fallback);
            // a direct goodwill change would just be undone by the daily happiness->goodwill sync.
            if (__instance.Faction == Faction.OfPlayer
                && __instance.RaceProps.Humanlike
                && __instance.HasExtraHomeFaction(FindFC.EmpireFaction))
            {
                EmpireDeathPenaltyUtil.HandleCivilianDefenderDeath(__instance, dinfo, null);
            }

            return true;
        }

        // Clears this pawn's dedup marker after the whole Pawn.Kill (incl. Faction.Notify_MemberDied)
        // has run, so the next death starts clean and we don't pin a dead pawn reference. Keyed by
        // __instance so a nested Pawn.Kill completing mid-Kill can't wipe the outer pawn's marker.
        static void Postfix(Pawn __instance)
        {
            EmpireDeathPenaltyUtil.ClearHandledByKillPrefix(__instance);
        }

        /// <summary>Turns a dead sub-pawn wrapper into a "Missing" placeholder: severs a mech's Overseer
        /// bond (so the mechanitor doesn't keep a relation to the unsaved mech) and nulls the pawn.</summary>
        static void NullDeadSubPawn(Mercenary sub)
        {
            if (sub is null) return;
            if (sub.subPawnType == Mercenary.SubPawnType.Mech && sub.handler?.pawn != null && sub.pawn != null)
                MercenaryPawnFactory.UnbondMech(sub.handler.pawn, sub.pawn);
            sub.pawn = null;
            FindFC.Military?.RebuildMercenaryPawnSet();
        }
    }

    // Whenever a mercenary changes faction (drafted to the player, undrafted back to the Empire,
    // forced back to Empire on death/recall, etc.), carry all of its sub-pawns along. Vanilla has no
    // drafting for animals/mechs, but they must share the merc's faction so player control, mechanitor
    // bonds, and AI all behave. Sub-pawns have no sub-pawns of their own, so this never recurses.
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.SetFaction))]
    class MercSubPawnsFollowFaction
    {
        static void Postfix(Pawn __instance, Faction newFaction)
        {
            if (newFaction is null) return;
            MilitaryFC mfc = FindFC.Military;
            if (mfc is null || !mfc.IsMercenaryPawn(__instance)) return;
            Mercenary merc = mfc.FindMercByPawn(__instance);
            if (merc is null) return; // not a top-level slot merc (e.g. a sub-pawn) — nothing to cascade
            foreach (Mercenary sub in merc.SubPawns())
            {
                Pawn p = sub?.pawn;
                if (p != null && !p.Dead && !p.Destroyed && p.Faction != newFaction)
                    p.SetFaction(newFaction);
            }
        }
    }

    // [HarmonyPatch(typeof(JobGiver_AnimalFlee), "TryGiveJob")]
    class TryGiveJobFleeAnimal
    {
        static bool Prefix(Pawn pawn)
        {
            if (FindFC.Military?.IsMercenaryPawn(pawn) == true)
            {
                return false;
            }

            return true;
        }
    }

    // Strip FC_CombatEfficiency hediffs whenever a pawn leaves the map. Catches every exit path
    // (caravan reformation with captured enemies, fleeing off-map, external defender return,
    // map removal via MapDeiniter.DespawnAll) without enumerating them by hand.
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.DeSpawn))]
    class StripCombatEfficiencyOnDeSpawn
    {
        static void Prefix(Pawn __instance)
        {
            MilitaryEfficiencyUtil.RemoveCombatEfficiencyHediff(__instance);
        }
    }

}
