using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace FactionColonies
{
    public class LordToil_HuntColonists : LordToil
    {
        public override bool ForceHighStoryDanger => true;

        public override bool AllowSatisfyLongNeeds => false;

        public override void Init()
        {
            base.Init();
            LessonAutoActivator.TeachOpportunity(ConceptDefOf.Drafting, OpportunityType.Critical);
            // One-time on assault entry: wake the defense lord and announce. Init runs once per
            // toil entry (not on load or duty refresh), so this can't re-broadcast per refresh.
            Find.SignalManager.SendSignal(new Signal("startAssault"));
            Messages.Message(new Message("FCAssaultBeginning".Translate(), MessageTypeDefOf.ThreatSmall));
        }

        public override void UpdateAllDuties()
        {
            foreach (Pawn pawn in lord.ownedPawns)
            {
                pawn.mindState.duty = new PawnDuty(DutyDefOf.AssaultColony); //new PawnDuty(DefDatabase<DutyDef>.GetNamed("HuntColonists"));
                pawn.mindState.canFleeIndividual = false;
            }
        }
    }
}