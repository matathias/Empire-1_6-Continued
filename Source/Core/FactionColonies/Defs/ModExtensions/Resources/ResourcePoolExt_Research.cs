using FactionColonies.util;
using RimWorld;
using RimWorld.Planet;
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

            // When nothing can be researched, leftover points would otherwise pile up unused.
            // Offer to cash them out at 2 points -> 1 silver, shipped from the nearest settlement.
            if (!Find.ResearchManager.AnyProjectIsAvailable && pool.pool >= 2 && (FindFC.Settlements?.Count ?? 0) > 0)
            {
                int silver = (int)Math.Floor(pool.pool) / 2;
                yield return new FloatMenuOption("FCConvertResearchToSilver".Translate(silver * 2, silver), delegate
                {
                    ConvertResearchToSilver(pool);
                });
            }
        }
        public override void DailyUpdate(ResourcePool pool)
        {
            double researchPointPool = pool.pool;
            //Research adding
            if ((Find.ResearchManager.GetProject() == null) && researchPointPool != 0 && Find.ResearchManager.AnyProjectIsAvailable)
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

        /* Cash out leftover research points once nothing can be researched: 2 points -> 1 silver,
           shipped from the settlement nearest the player's colony so it travels like a tax delivery. */
        private static void ConvertResearchToSilver(ResourcePool pool)
        {
            int silver = (int)Math.Floor(pool.pool) / 2;
            if (silver <= 0) return;

            // The capital tile is a player-colony tile, not a settlement, so find the closest settlement by distance.
            PlanetTile capital = FindFC.CapitalLocation;
            WorldSettlementFC nearest = null;
            float best = float.MaxValue;
            foreach (WorldSettlementFC settlement in FindFC.Settlements)
            {
                float dist = capital.Valid ? Find.WorldGrid.ApproxDistanceInTiles(capital, settlement.Tile) : 0f;
                if (dist < best)
                {
                    best = dist;
                    nearest = settlement;
                }
            }
            if (nearest is null) return;

            int pointsConsumed = silver * 2;
            pool.pool -= pointsConsumed;

            // Split into stack-limit-sized silver piles, matching how tax deliveries are built.
            List<Thing> goods = new List<Thing>();
            int remaining = silver;
            while (remaining > 0)
            {
                Thing thing = ThingMaker.MakeThing(ThingDefOf.Silver);
                int amount = Math.Min(remaining, thing.def.stackLimit);
                thing.stackCount = amount;
                goods.Add(thing);
                remaining -= amount;
            }

            Messages.Message("FCResearchConvertedToSilver".Translate(pointsConsumed, silver, nearest.Name),
                MessageTypeDefOf.PositiveEvent);

            DeliveryEvent.CreateDeliveryEvent(new FCEvent
            {
                source = nearest.Tile,
                goods = goods,
                customDescription = "",
                timeTillTrigger = Find.TickManager.TicksGame +
                    (capital.Valid ? TravelUtil.ReturnTicksToArrive(nearest.Tile, capital) : 10)
            });
        }
    }
}
