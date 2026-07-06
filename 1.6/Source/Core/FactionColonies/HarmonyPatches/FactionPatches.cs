using FactionColonies.util;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using System.Collections.Generic;
using Verse;

namespace FactionColonies
{//stops friendly faction from being a group source
    [HarmonyPatch(typeof(IncidentWorker_RaidFriendly), "TryResolveRaidFaction")]
    class RaidFriendlyStopSettlementFaction
    {
        static void Postfix(ref IncidentWorker_RaidFriendly __instance, ref bool __result, IncidentParms parms)
        {
            if (parms.faction == FindFC.EmpireFaction)
            {
                parms.faction = null;
                __result = false;
            }
        }
    }

    //Goodwill by distance to settlement
    [HarmonyPatch(typeof(SettlementProximityGoodwillUtility), "AppendProximityGoodwillOffsets")]
    class GoodwillPatch
    {
        static void Postfix(PlanetTile tile, List<Pair<Settlement, int>> outOffsets, bool ignoreIfAlreadyMinGoodwill, bool ignorePermanentlyHostile)
        {
            outOffsets.RemoveAll(pair => pair.First.Faction == FindFC.EmpireFaction);
        }
    }

    //tryAffectGoodwillWith
    [HarmonyPatch(typeof(Faction), "TryAffectGoodwillWith")]
    class GoodwillPatchFunctionsGoodwillAffect
    {
        static bool Prefix(ref Faction __instance, Faction other, int goodwillChange, bool canSendMessage = true,
            bool canSendHostilityLetter = true, HistoryEventDef reason = null, GlobalTargetInfo? lookTarget = null)
        {
            if (__instance == FindFC.EmpireFaction && other == Find.FactionManager.OfPlayer)
            {
                if (reason == HistoryEventDefOf.RequestedTrader ||
                    reason == HistoryEventDefOf.GaveGift ||
                    reason == HistoryEventDefOf.Traded ||
                    reason == HistoryEventDefOf.ReachNaturalGoodwill)
                {
                    return false;
                }

                return true;
            }

            return true;
        }
    }


    //Notify_MemberDied(Pawn member, DamageInfo? dinfo, bool wasWorldPawn, Map map)
    [HarmonyPatch(typeof(Faction), "Notify_MemberDied")]
    class GoodwillPatchFunctionsMemberDied
    {
        static bool Prefix(ref Faction __instance, Pawn member, DamageInfo? dinfo, bool wasWorldPawn, Map map)
        {
            if (member.Faction == FindFC.EmpireFaction && !wasWorldPawn &&
                !PawnGenerator.IsBeingGenerated(member) && map != null &&
                (map.IsPlayerHome || map.Parent is WorldSettlementFC) &&
                !__instance.HostileTo(Faction.OfPlayer))
            {
                // Mercs and trade-caravan pawns are already penalized in the Pawn.Kill prefix
                // (EmpireDeathPenaltyUtil), which runs earlier in this same Kill call. Skip them here to
                // avoid a second hit; everything else is a settlement defender / visitor and is routed to
                // the defended settlement (or the capital). Always return false to block vanilla goodwill.
                if (!member.IsMercenary() && !EmpireDeathPenaltyUtil.WasHandledByKillPrefix(member))
                {
                    EmpireDeathPenaltyUtil.HandleCivilianDefenderDeath(member, dinfo, map.Parent as WorldSettlementFC);
                }

                return false;
            }

            return true;
        }
    }

    //Player traded
    [HarmonyPatch(typeof(Faction), "Notify_PlayerTraded")]
    class GoodwillPatchFunctionsPlayerTraded
    {
        static bool Prefix(ref Faction __instance, float marketValueSentByPlayer, Pawn playerNegotiator)
        {
            if (__instance == FindFC.EmpireFaction)
            {
                return false;
            }

            return true;
        }
    }

    //Player traded
    [HarmonyPatch(typeof(Faction), "Notify_MemberCaptured")]
    class GoodwillPatchFunctionsCapturedPawn
    {
        static bool Prefix(ref Faction __instance, Pawn member, Faction violator)
        {
            if (__instance == FindFC.EmpireFaction && violator == Faction.OfPlayer && !member.IsSlaveOfColony)
            {
                FactionFC faction = FindFC.FactionComp;
                faction.GainUnrestForReason(new Message("FCCaptureOfFactionPawn".Translate(), MessageTypeDefOf.NegativeEvent), 15d);
                faction.GainHappiness(-10d);

                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(Faction), "Notify_MemberTookDamage")]
    class GoodwillPatchFunctionsTookDamage
    {
        static bool Prefix(ref Faction __instance, Pawn member, DamageInfo dinfo)
        {
            if (__instance == FindFC.EmpireFaction)
            {
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(Faction), "Notify_BuildingTookDamage")]
    class GoodwillPatchFunctionsBuildingTookDamage
    {
        static bool Prefix(ref Faction __instance, Building building, DamageInfo dinfo)
        {
            if (__instance == FindFC.EmpireFaction)
            {
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(Faction), "Notify_MemberStripped")]
    class GoodwillPatchFunctionsMemberStripped
    {
        static bool Prefix(ref Faction __instance, Pawn member, Faction violator)
        {
            if (__instance == FindFC.EmpireFaction)
            {
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(Faction), "Notify_BuildingRemoved")]
    class GoodwillPatchFunctionsBuildingRemoved
    {
        static bool Prefix(ref Faction __instance, Building building, Pawn deconstructor)
        {
            if (__instance == FindFC.EmpireFaction)
            {
                return false;
            }

            return true;
        }
    }

    //Exclude Empire faction from quest faction selection
    [HarmonyPatch(typeof(QuestNode_GetFaction))]
    [HarmonyPatch("IsGoodFaction")]
    class QuestFactionExcludePColony
    {
        static bool Prefix(Faction faction, ref bool __result)
        {
            if (faction == FindFC.EmpireFaction)
            {
                __result = false;
                return false;
            }

            return true;
        }
    }

    //Mirror player goodwill changes to Empire faction
    [HarmonyPatch(typeof(Faction))]
    [HarmonyPatch("TryAffectGoodwillWith")]
    class MirrorGoodwillToEmpire
    {
        static void Postfix(Faction __instance, Faction other, bool __result)
        {
            if (!__result) return;

            Faction pcFaction = FindFC.EmpireFaction;
            if (pcFaction == null) return;

            Faction player = Find.FactionManager?.OfPlayer;
            if (player is null) return;

            Faction thirdParty;
            if (__instance == player && other != pcFaction)
            {
                thirdParty = other;
            }
            else if (other == player && __instance != pcFaction)
            {
                thirdParty = __instance;
            }
            else
            {
                return;
            }

            int playerGoodwill = player.RelationWith(thirdParty).baseGoodwill;
            int empireGoodwill = pcFaction.RelationWith(thirdParty).baseGoodwill;
            int delta = playerGoodwill - empireGoodwill;

            if (delta != 0)
            {
                pcFaction.TryAffectGoodwillWith(thirdParty, delta, canSendMessage: false, canSendHostilityLetter: false);
                LogUtil.Message($"TryAffectGoodwillWith Postfix: Empire faction changing relations with {thirdParty.Name} by {delta}");
            }

            FactionRelationKind playerKind = player.RelationKindWith(thirdParty);
            if (pcFaction.RelationKindWith(thirdParty) != playerKind)
            {
                RelationsUtilFC.TrySetRelationKind(pcFaction, thirdParty, playerKind, canSendLetter: false);
                LogUtil.Message($"TryAffectGoodwillWith Postfix: Empire faction changing relationkind with {thirdParty.Name} to {playerKind}");
            }
        }
    }

    //Mirror player hostility to PColony: if something is hostile to the player, PColony considers it hostile too.
    //Covers the Thing-vs-Faction overload used by AttackTargetsCache.RegisterTarget to build the hostile target cache.
    [HarmonyPatch(typeof(GenHostility))]
    [HarmonyPatch("HostileTo", typeof(Thing), typeof(Faction))]
    class PColonyMirrorHostileTo_ThingFaction
    {
        static void Postfix(Thing t, Faction fac, ref bool __result)
        {
            if (__result) return;
            if (fac != FindFC.EmpireFaction) return;
            __result = t.HostileTo(Faction.OfPlayer);
        }
    }

    //Mirror player hostility to PColony: Thing-vs-Thing overload used by GetPotentialTargetsFor double-checks
    //and direct pawn-to-pawn hostility queries.
    [HarmonyPatch(typeof(GenHostility))]
    [HarmonyPatch("HostileTo", typeof(Thing), typeof(Thing))]
    class PColonyMirrorHostileTo_ThingThing
    {
        static void Postfix(Thing a, Thing b, ref bool __result)
        {
            if (__result) return;

            Faction pcFaction = FindFC.EmpireFaction;
            if (pcFaction is null) return;

            Thing other;
            if (a.Faction == pcFaction)
                other = b;
            else if (b.Faction == pcFaction)
                other = a;
            else return;

            __result = other.HostileTo(Faction.OfPlayer);
        }
    }

    //Mirror direct relation changes to Empire faction (for factions without goodwill)
    [HarmonyPatch(typeof(Faction))]
    [HarmonyPatch("SetRelationDirect")]
    class MirrorRelationDirectToEmpire
    {
        static void Postfix(Faction __instance, Faction other, FactionRelationKind kind)
        {
            Faction pcFaction = FindFC.EmpireFaction;
            if (pcFaction == null) return;

            Faction player = Find.FactionManager.OfPlayer;

            Faction thirdParty;
            if (__instance == player && other != pcFaction)
            {
                thirdParty = other;
            }
            else if (other == player && __instance != pcFaction)
            {
                thirdParty = __instance;
            }
            else
            {
                return;
            }

            if (pcFaction.RelationKindWith(thirdParty) != kind)
            {
                pcFaction.SetRelationDirect(thirdParty, kind, canSendHostilityLetter: false);
                LogUtil.Message($"SetRelationDirect Postfix: Empire faction changing relationkind with {thirdParty.Name} to {kind}");
            }
        }
    }
}
