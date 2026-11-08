namespace FactionColonies;

public class FCPolicyBehavior_Pacifist : FCCooldownPolicyBehavior
{
    protected override string CooldownSaveKey => "diplomatCooldown";

    public override void PostInitialize()
    {
        var ext = Ext<FCPolicyBehaviorExt_Pacifist>();
        cooldown.SetCooldown(GenDate.TicksPerDay * ext.diplomatCooldownDays);
    }

    public override bool HandleDiplomaticEnvoy(FactionFC faction, Faction targetFaction)
    {
        if (!cooldown.IsReady)
        {
            Messages.Message(
                "FCXDaysToSendDiplomat".Translate(Math.Round(cooldown.DaysRemaining, 1)),
                MessageTypeDefOf.RejectInput);
            return false;
        }

        cooldown.Use();

        var ext = Ext<FCPolicyBehaviorExt_Pacifist>();
        int random = Rand.RangeInclusive(1, ext.successRollMax);
        if (random > ext.successThreshold)
        {
            int relationImprovement = Rand.Range(ext.relationImprovementMin, ext.relationImprovementMax);
            targetFaction.TryAffectGoodwillWith(Find.FactionManager.OfPlayer, relationImprovement);
            Find.LetterStack.ReceiveLetter("FCRelationImproved".Translate(),
                "FCRelationImprovedText".Translate(targetFaction.Name, relationImprovement),
                LetterDefOf.PositiveEvent);
        }
        else
        {
            Find.LetterStack.ReceiveLetter("FCRelationNotImproved".Translate(),
                "FCFailedToImproveRelationship".Translate(targetFaction.Name), LetterDefOf.NeutralEvent);
        }

        return true;
    }
}
