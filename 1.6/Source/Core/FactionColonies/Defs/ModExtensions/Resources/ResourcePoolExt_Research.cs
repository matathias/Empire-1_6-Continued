using RimWorld;
using System;
using System.Collections.Generic;
using Verse;

namespace FactionColonies
{
    public class ResourcePoolExt_Research : ResourcePoolExtension
    {
        public override double CreatePool(double production, WorldSettlementFC settlement = null)
        {
            FactionFC faction = FindFC.FactionComp;

            double result = Math.Max(Math.Round(production * FCSettings.productionResearchBase), 0);
            result *= faction.GetStatValue(FCStatDefOf.researchContributionMultiplier, settlement);
            return (float)result;
        }
        public override bool ResetAtTaxTime()
        {
            return false;
        }
        public override void AddedToGlobalPool(double value)
        {
            Messages.Message("FCPointsAddedToResearchPool".Translate(value), MessageTypeDefOf.PositiveEvent);
        }

        public override IEnumerable<FloatMenuOption> GetFactionMenuFloatMenuOptions(ResourcePool pool)
        {
            IEnumerable<FloatMenuOption> boptions = base.GetFactionMenuFloatMenuOptions(pool);
            if (boptions != null)
            {
                foreach (FloatMenuOption option in boptions)
                {
                    yield return option;
                }
            }
            FactionFC faction = FindFC.FactionComp;

            yield return new FloatMenuOption("FCActivateResearch".Translate(), delegate
            {
                DailyUpdate(pool);
            });

            yield return new FloatMenuOption("FCResearchLevel".Translate(), delegate
            {
                // Mirror mode pins Empire tech to the player faction, so research doesn't advance it.
                string msg = FCSettings.mirrorPlayerTechLevel
                    ? "FCCurrentResearchLevelMirror".Translate(FindFC.EmpireName, faction.techLevel.ToString())
                    : "FCCurrentResearchLevel".Translate(FindFC.EmpireName, faction.techLevel.ToString(), faction.ReturnNextTechToLevel());
                Messages.Message(msg, MessageTypeDefOf.NeutralEvent);
            });
        }
        public override void DailyUpdate(ResourcePool pool)
        {
            double researchPointPool = pool.pool;
            //Research adding
            if ((Find.ResearchManager.GetProject() == null) && researchPointPool != 0)
            {
                Messages.Message("FCNoResearchExpended".Translate(Math.Round(researchPointPool)), MessageTypeDefOf.NeutralEvent);
            }
            else if (researchPointPool != 0 && Find.ResearchManager.GetProject() != null)
            {
                float neededPoints;
                neededPoints = (float)Math.Ceiling(Find.ResearchManager.GetProject().CostApparent - Find.ResearchManager.GetProject().ProgressApparent);
                LogUtil.Message("Needed points: " + neededPoints);

                double expendedPoints;
                if (researchPointPool >= neededPoints)
                {
                    researchPointPool -= neededPoints;
                    expendedPoints = neededPoints;
                }
                else
                {
                    expendedPoints = researchPointPool;
                    researchPointPool = 0;
                    LogUtil.Message("Used all research points in the pool.");
                }

                LogUtil.Message("Expended points: " + expendedPoints);

                Find.LetterStack.ReceiveLetter(
                    "FCResearchPointsExpended".Translate(),
                    "FCResearchExpended".Translate(Math.Round(expendedPoints),
                    Find.ResearchManager.GetProject().LabelCap,
                    Math.Round(researchPointPool)),
                    LetterDefOf.PositiveEvent);
                if (Find.ColonistBar.GetColonistsInOrder().Count > 0)
                {
                    Pawn pawn = Find.ColonistBar.GetColonistsInOrder()[0];
                    TechLevel techLevel = pawn.Faction?.def?.techLevel ?? FindFC.FactionComp?.techLevel ?? TechLevel.Industrial;
                    Find.ResearchManager.ResearchPerformed(
                        (float)Math.Ceiling(((1 * Find.ResearchManager.GetProject().CostFactor(techLevel)) /
                            (0.00825 * Find.Storyteller.difficulty.researchSpeedFactor)) * expendedPoints),
                        pawn);
                }
                else
                {
                    LogUtil.Message("Could not find colonist to research with");
                    Find.ResearchManager.ResearchPerformed((float)Math.Ceiling((1 /
                        (0.00825 * Find.Storyteller.difficulty.researchSpeedFactor)) * expendedPoints), null);
                }
                pool.pool = researchPointPool;
            }
        }
    }
}
