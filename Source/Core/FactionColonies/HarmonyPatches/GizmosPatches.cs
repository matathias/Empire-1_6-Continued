using HarmonyLib;
using Verse.AI;
using Verse.AI.Group;

namespace FactionColonies;

[HarmonyPatch(typeof(Pawn), "GetGizmos")]
class PawnDraftGizmos
{
    public static void Postfix(ref Pawn __instance, ref IEnumerable<Gizmo> __result)
    {
        // Early exit checks BEFORE any allocations - most pawns will exit here
        if (__result is null || __instance?.Faction is null || __instance.Map is null)
        {
            return;
        }

        WorldSettlementFC settlementFc = __instance.Map.Parent as WorldSettlementFC;
        if (settlementFc is null)
        {
            // Offense: the map parent is a vanilla enemy Settlement under an active Empire assault.
            RimWorld.Planet.Settlement enemySettlement = __instance.Map.Parent as RimWorld.Planet.Settlement;
            if (enemySettlement is null) return;
            BattlefieldContext offBf = FindFC.MilitaryManager?.GetBattlefield(enemySettlement.Tile);
            if (offBf is null || !offBf.HasOffenseAt()) return;
            // The battle is over once teardown or the loot linger begins: no new combat can
            // happen, and a merc drafted now would be an OfPlayer pawn on a resolved
            // battlefield that the teardown reclaim loops have to unwind. Offer no
            // draft/undraft toggles past that point.
            if (offBf.endingBattle || offBf.awaitingPlayerExit) return;
            TryAddOffenseDraftGizmo(__instance, offBf, ref __result);
            return;
        }

        Faction playerColonyFaction = FindFC.EmpireFaction;

        // Only allow drafting Empire defenders during an active battle. Sub-pawns (companion animals
        // / bonded mechs) aren't individually draftable — they follow their owning merc's faction
        // automatically (see MercSubPawnsFollowFaction) — so exclude them (checked last so the
        // squad scan only runs for Empire pawns mid-battle).
        // A battlefield must exist: it owns the drafted-NPC list that restores the pawn to the
        // Empire after the battle, so drafting without one would hand the player a free colonist.
        BattlefieldContext defBf = FindFC.MilitaryManager?.GetBattlefield(settlementFc.Tile);
        if (__instance.Faction == playerColonyFaction && settlementFc.MilitaryComp?.isUnderAttack == true
            && defBf is not null && FindFC.Military?.FindSubPawnWrapper(__instance) is null)
        {
            __result = __result.Append(MakeDraftToggle(__instance, defBf, DefenseDraftGroupKey));
            return;
        }

        // Undraft toggle for drafted Empire NPCs (only during active battle)
        if (__instance.Faction == Faction.OfPlayer && __instance.Drafted
            && settlementFc.MilitaryComp?.isUnderAttack == true
            && settlementFc.MilitaryComp.draftedNPCs.Contains(__instance))
        {
            var milComp = settlementFc.MilitaryComp;
            HookUndraftToggle(ref __result, __instance, () => milComp.draftedNPCs,
                job => job is LordJob_DefendColony,
                rejoin =>
                {
                    if (!milComp.defenders.Any()) return false;
                    // Re-add the merc AND its sub-pawns to defenders + the defense lord after
                    // undrafting, so they rejoin the fight. Routes through the BattlefieldContext
                    // so per-op pawn lists stay aligned.
                    BattlefieldContext bf = FindFC.MilitaryManager?.GetBattlefield(milComp.WorldSettlement.Tile);
                    bf?.RegisterPawnsAsDefenders(rejoin, assignToLord: false);
                    return true;
                });
        }
    }

    /* Draft gizmo group keys: distinct so offense and defense draft gizmos never merge */
    const int DefenseDraftGroupKey = 81729172;
    const int OffenseDraftGroupKey = 81729173;

    /// <summary>Offense mirror of the defense draft/undraft toggle. Records drafted attackers into
    /// the offense <see cref="BattlefieldContext.draftedNPCs"/> (restored on teardown) and, on
    /// undraft, rejoins the <see cref="LordJob_AssaultColony"/> assault lord rather than the
    /// defense lord. Uses a distinct groupKey so offense and defense draft gizmos never merge.</summary>
    static void TryAddOffenseDraftGizmo(Pawn pawn, BattlefieldContext bf, ref IEnumerable<Gizmo> result)
    {
        Faction empire = FindFC.EmpireFaction;

        // Draft toggle for an Empire attacker not yet drafted (sub-pawns follow their merc's faction).
        if (pawn.Faction == empire && FindFC.Military?.FindSubPawnWrapper(pawn) is null)
        {
            result = result.Append(MakeDraftToggle(pawn, bf, OffenseDraftGroupKey));
            return;
        }

        // Undraft toggle for a drafted Empire attacker: rejoin the assault lord.
        if (pawn.Faction == Faction.OfPlayer && pawn.Drafted && bf.draftedNPCs.Contains(pawn))
        {
            HookUndraftToggle(ref result, pawn, () => bf.draftedNPCs,
                job => job is LordJob_AssaultColony, beforeRejoin: null);
        }
    }

    /// <summary>Builds the draft toggle for an undrafted Empire pawn: switches it to the player
    /// faction, drafts it, and records it in <paramref name="bf"/>'s drafted list so the battle
    /// teardown restores it to the Empire.</summary>
    static Command_Toggle MakeDraftToggle(Pawn pawn, BattlefieldContext bf, int groupKey)
    {
        Command_Toggle draft = new()
        {
            hotKey = KeyBindingDefOf.Command_ColonistDraft,
            isActive = () => false,
            toggleAction = () =>
            {
                if (pawn.Faction == Faction.OfPlayer) return;
                pawn.SetFaction(Faction.OfPlayer);
                // SetFaction -> AddAndRemoveDynamicComponents creates pawn.drafter for OfPlayer pawns
                if (pawn.drafter is not null)
                    pawn.drafter.Drafted = true;
                // Vanilla switched any bonded mechs to the player faction too, but the bandwidth
                // recalc can leave them "uncontrolled": re-assign them to the (now player)
                // mechanitor's control groups so the player can command them.
                Mercenary drafted = FindFC.Military?.FindMercByPawn(pawn);
                if (drafted is not null)
                {
                    MercenaryPawnFactory.RebindMechs(drafted);
                    // SetFaction above cascaded to the mount and ran ClearMind, ending its Mounted
                    // job (Giddy Up 2). Re-mount so the merc stays seated through the draft.
                    RemountMerc(drafted);
                }
                // Track drafted NPC for faction restoration after battle
                if (!bf.draftedNPCs.Contains(pawn))
                    bf.draftedNPCs.Add(pawn);
            },
            defaultDesc = "CommandToggleDraftDesc".Translate(),
            icon = TexCommand.Draft,
            turnOnSound = SoundDefOf.DraftOn,
            groupKey = groupKey,
            defaultLabel = "CommandDraftLabel".Translate()
        };

        if (pawn.Downed)
        {
            draft.Disable("IsIncapped".Translate(pawn.LabelShort, pawn));
        }

        draft.tutorTag = "Draft";
        return draft;
    }

    /// <summary>Replaces the vanilla undraft action of a drafted Empire pawn: returns it (and its
    /// sub-pawns) to the Empire faction, drops it from the drafted list, and adds them back to the
    /// Empire lord whose job matches <paramref name="isRejoinLordJob"/>. <paramref name="beforeRejoin"/>
    /// (optional) runs first with the rejoining pawns; returning false skips the lord rejoin.</summary>
    static void HookUndraftToggle(ref IEnumerable<Gizmo> result, Pawn found, Func<List<Pawn>> draftedNPCs,
        Predicate<LordJob> isRejoinLordJob, Func<List<Pawn>, bool> beforeRejoin)
    {
        List<Gizmo> output = result.ToList();
        foreach (Gizmo gizmo in output)
        {
            Command_Toggle action = gizmo as Command_Toggle;
            if (action is not null && action.hotKey == KeyBindingDefOf.Command_ColonistDraft)
            {
                action.toggleAction = () =>
                {
                    // SetFaction cascades to sub-pawns via MercSubPawnsFollowFaction, so the
                    // merc's animals/mechs return to the Empire faction too.
                    found.SetFaction(FindFC.EmpireFaction);
                    draftedNPCs().Remove(found);
                    Mercenary merc = FindFC.Military?.FindMercByPawn(found);
                    // Reconnect the merc's mechs to its (now Empire) mechanitor control after the
                    // faction swap back, else they sit uncontrolled.
                    if (merc is not null) MercenaryPawnFactory.RebindMechs(merc);

                    List<Pawn> rejoin = [found];
                    if (merc is not null)
                        foreach (Mercenary sub in merc.SubPawns())
                            if (sub?.pawn is not null && sub.pawn.Spawned && !sub.pawn.Dead)
                                rejoin.Add(sub.pawn);

                    if (beforeRejoin is null || beforeRejoin(rejoin))
                    {
                        // Find the active Empire lord on this map directly. Picking the first
                        // defender's lord was fragile: `found` is itself in `defenders` and its
                        // lord is null right after undrafting, so if it (or any lordless defender)
                        // came first, the rejoin was silently skipped and the pawns: having no lord
                        //: would try to leave the map instead of fighting.
                        Lord rejoinLord = found.Map?.lordManager?.lords
                            .FirstOrDefault(l => l is not null && l.faction == FindFC.EmpireFaction
                                                 && isRejoinLordJob(l.LordJob));
                        if (rejoinLord is not null)
                        {
                            foreach (Pawn p in rejoin)
                                if (!rejoinLord.ownedPawns.Contains(p))
                                    rejoinLord.AddPawn(p);
                            rejoinLord.CurLordToil?.UpdateAllDuties();
                            // Force each pawn off its current (wander/follow) job so it immediately
                            // adopts the lord's combat duty instead of idling.
                            foreach (Pawn p in rejoin)
                                p.jobs?.EndCurrentJob(JobCondition.InterruptForced);
                        }
                    }
                    // Re-mount after the faction switch (and the rejoin's EndCurrentJob, which also
                    // ended the mount's Mounted job) so the merc stays seated through the undraft.
                    if (merc is not null) RemountMerc(merc);
                };
                break;
            }
        }
        result = output;
    }

    /// <summary>Re-mounts a merc on its assigned mount (Giddy Up 2) after a draft/undraft faction
    /// switch. SetFaction runs ClearMind, which ends the mount's Mounted job (so it stops carrying the
    /// rider) and drops it from any lord; this re-establishes the mount. No-op without Giddy Up 2 or a
    /// mount.</summary>
    static void RemountMerc(Mercenary merc)
    {
        if (!FactionCompat.GiddyUp2Active || merc?.pawn is null || !merc.pawn.Spawned || merc.pawn.Dead)
            return;
        foreach (Mercenary sub in merc.SubPawns())
        {
            if (sub is null || sub.subPawnType != Mercenary.SubPawnType.Mount) continue;
            Pawn mount = sub.pawn;
            if (mount is null || !mount.Spawned || mount.Dead) continue;
            // The Mounted job runs the mount's constant think tree, which logs "ThinkNode_DutyConstant
            // with no duty" if the mount has none — and the faction switch just dropped it from its
            // lord. Give it a duty first (mirrors LordToil_DefendSelfAndMount's ordering).
            if (mount.mindState is not null && mount.mindState.duty is null)
                mount.mindState.duty = new PawnDuty(DutyDefOf.Defend, mount.Position, -1f);
            GiddyUpUtil.Mount(merc.pawn, mount);
        }
    }
}
