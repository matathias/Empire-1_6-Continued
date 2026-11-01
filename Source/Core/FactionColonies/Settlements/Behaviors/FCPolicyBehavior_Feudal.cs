using FactionColonies.util;
using RimWorld;
using System;
using System.Collections.Generic;
using Verse;

namespace FactionColonies
{
    public class FCPolicyBehavior_Feudal : FCPolicyBehavior
    {
        private CooldownAbility mercenaryCooldown = new CooldownAbility();

        public override void PostInitialize()
        {
            var ext = Ext<FCPolicyBehaviorExt_Feudal>();
            mercenaryCooldown.readyLetterKey = ext.readyLetterKey;
            mercenaryCooldown.cooldownMessageKey = ext.cooldownMessageKey;
            mercenaryCooldown.SetCooldown(ext.mercenaryCooldownTicks);
        }

        public override void Tick(FactionFC faction)
        {
            mercenaryCooldown.TickCheckReady();
        }

        public override IEnumerable<(TaggedString label, Action onClick)> GetMainTabActionButtons(FactionFC faction)
        {
            yield return ("FCRequestMercenary".Translate(), () =>
            {
                if (!mercenaryCooldown.TryUseOrShowCooldown())
                    return;

                mercenaryCooldown.Use();

                PawnGenerationRequest request = FCPawnGenerator.WorkerOrMilitaryRequest();
                request.ColonistRelationChanceFactor = Ext<FCPolicyBehaviorExt_Feudal>().relationChanceFactor;
                Pawn pawn = PawnGenerator.GeneratePawn(request);

                IncidentParms parms = new IncidentParms
                {
                    target = Find.CurrentMap,
                    faction = FindFC.EmpireFaction,
                    points = 999,
                    raidArrivalModeForQuickMilitaryAid = true,
                    raidNeverFleeIndividual = true,
                    raidArrivalMode = PawnsArrivalModeDefOf.CenterDrop,
                    raidStrategy = RaidStrategyDefOf.ImmediateAttackFriendly
                };

                PawnsArrivalModeWorker_EdgeWalkIn worker = new PawnsArrivalModeWorker_EdgeWalkIn();
                worker.TryResolveRaidSpawnCenter(parms);
                worker.Arrive(new List<Pawn> { pawn }, parms);

                Find.LetterStack.ReceiveLetter(
                    "FCMercenaryJoined".Translate(),
                    "FCMercenaryJoinedText".Translate(pawn.NameFullColored),
                    LetterDefOf.PositiveEvent,
                    new LookTargets(pawn));
                pawn.SetFaction(Faction.OfPlayer);
            }
            );
        }

        public override void ExposeData()
        {
            Scribe_Deep.Look(ref mercenaryCooldown, "mercenaryCooldown");
            mercenaryCooldown = mercenaryCooldown ?? new CooldownAbility();
        }

        // Debug accessors
        public bool DebugCooldownReady() => mercenaryCooldown.IsReady;
        public float DebugCooldownDays() => mercenaryCooldown.DaysRemaining;
        public void DebugResetCooldown() => mercenaryCooldown.tickLastUsed = -1;
    }
}
