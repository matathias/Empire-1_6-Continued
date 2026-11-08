namespace FactionColonies;

public class FCPolicyBehavior_Feudal : FCCooldownPolicyBehavior
{
    protected override string CooldownSaveKey => "mercenaryCooldown";

    public override void PostInitialize()
    {
        var ext = Ext<FCPolicyBehaviorExt_Feudal>();
        cooldown.readyLetterKey = ext.readyLetterKey;
        cooldown.cooldownMessageKey = ext.cooldownMessageKey;
        cooldown.SetCooldown(ext.mercenaryCooldownTicks);
    }

    public override void Tick(FactionFC faction)
    {
        cooldown.TickCheckReady();
    }

    public override IEnumerable<(TaggedString label, Action onClick)> GetMainTabActionButtons(FactionFC faction)
    {
        yield return ("FCRequestMercenary".Translate(), () =>
        {
            if (!cooldown.TryUseOrShowCooldown())
                return;

            Map map = Find.CurrentMap;
            if (map is null)
            {
                Messages.Message("FCRequestMercenaryNoMap".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }

            PawnGenerationRequest request = FCPawnGenerator.WorkerOrMilitaryRequest();
            request.ColonistRelationChanceFactor = Ext<FCPolicyBehaviorExt_Feudal>().relationChanceFactor;
            Pawn pawn = PawnGenerator.GeneratePawn(request);

            IncidentParms parms = new()
            {
                target = map,
                faction = FindFC.EmpireFaction,
                points = 999,
                raidArrivalModeForQuickMilitaryAid = true,
                raidNeverFleeIndividual = true,
                raidArrivalMode = PawnsArrivalModeDefOf.CenterDrop,
                raidStrategy = RaidStrategyDefOf.ImmediateAttackFriendly
            };

            PawnsArrivalModeWorker_EdgeWalkIn worker = new();
            worker.TryResolveRaidSpawnCenter(parms);
            worker.Arrive([pawn], parms);
            // Consumed only once the mercenary has arrived, so a failed request keeps the cooldown.
            cooldown.Use();

            Find.LetterStack.ReceiveLetter(
                "FCMercenaryJoined".Translate(),
                "FCMercenaryJoinedText".Translate(pawn.NameFullColored),
                LetterDefOf.PositiveEvent,
                new LookTargets(pawn));
            pawn.SetFaction(Faction.OfPlayer);
        }
        );
    }
}
