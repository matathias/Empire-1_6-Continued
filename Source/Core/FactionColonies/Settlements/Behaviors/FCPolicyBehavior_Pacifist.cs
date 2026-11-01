using RimWorld;
using System;
using Verse;

namespace FactionColonies
{
    public class FCPolicyBehavior_Pacifist : FCPolicyBehavior
    {
        private CooldownAbility diplomatCooldown = new CooldownAbility();

        public override void PostInitialize()
        {
            var ext = Ext<FCPolicyBehaviorExt_Pacifist>();
            diplomatCooldown.SetCooldown(GenDate.TicksPerDay * ext.diplomatCooldownDays);
        }

        public override bool HandleDiplomaticEnvoy(FactionFC faction, Faction targetFaction)
        {
            if (!diplomatCooldown.IsReady)
            {
                Messages.Message(
                    "FCXDaysToSendDiplomat".Translate(Math.Round(diplomatCooldown.DaysRemaining, 1)),
                    MessageTypeDefOf.RejectInput);
                return false;
            }

            diplomatCooldown.Use();

            var ext = Ext<FCPolicyBehaviorExt_Pacifist>();
            int random = Rand.Range(1, ext.successRollMax);
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

        public override void ExposeData()
        {
            Scribe_Deep.Look(ref diplomatCooldown, "diplomatCooldown");
            diplomatCooldown = diplomatCooldown ?? new CooldownAbility();
        }

        // Debug accessors
        public bool DebugCooldownReady() => diplomatCooldown.IsReady;
        public float DebugCooldownDays() => diplomatCooldown.DaysRemaining;
        public void DebugResetCooldown() => diplomatCooldown.tickLastUsed = -1;
    }
}
