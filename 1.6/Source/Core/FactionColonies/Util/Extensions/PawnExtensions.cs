using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace FactionColonies.util
{
    static class PawnExtensions
    {
        public static bool IsMercenary(this Pawn pawn) => FindFC.Military?.IsMercenaryPawn(pawn) ?? false;

        public static void ApplyIdeologyRitualWounds(this Pawn pawn)
        {
            try
            {
                if (ModsConfig.IdeologyActive && pawn?.Ideo != null && !pawn.AnimalOrWildMan())
                {
                    if (pawn.Ideo.HasPrecept(PreceptDefOf.AgeReversal_Demanded))
                    {
                        pawn.ageTracker.DebugResetAgeReversalDemand();
                    }
                    if (pawn.Ideo.BlindPawnChance > 0)
                    {
                        pawn.Blind();
                    }
                    if (pawn.Ideo.RequiredScars > 0 && !pawn.HasTrait(TraitDefOf.Wimp))
                    {
                        pawn.Scarify();
                    }
                }
            }
            catch (Exception ex)
            {
                string pawnName = (pawn?.Name != null) ? pawn.Name.ToString() : "pawn is null!";
                LogUtil.Error("Required ritual wounds couldn't be applied to pawn: " + pawnName + ". Pawn Ideo == null: " + (pawn?.Ideo == null) + ". Exception: " + ex);
            }
        }

        public static void Blind(this Pawn pawn)
        {
            foreach (BodyPartRecord part in pawn.RaceProps.body.GetPartsWithTag(BodyPartTagDefOf.SightSource))
            {
                if (!pawn.health.hediffSet.PartIsMissing(part))
                {
                    Hediff_MissingPart hediff_MissingPart = (Hediff_MissingPart)HediffMaker.MakeHediff(HediffDefOf.MissingBodyPart, pawn, null);
                    hediff_MissingPart.lastInjury = HediffDefOf.Cut;
                    hediff_MissingPart.Part = part;
                    hediff_MissingPart.IsFresh = false;
                    pawn.health.AddHediff(hediff_MissingPart, part, null, null);
                }
            }

        }

        public static void Scarify(this Pawn pawn)
        {
            int num = 0;
            using (List<Hediff>.Enumerator enumerator = pawn.health.hediffSet.hediffs.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    if (enumerator.Current?.def == HediffDefOf.Scarification)
                    {
                        num++;
                    }
                }
            }

            for (int i = num; i < pawn.ideo.Ideo.RequiredScars; i++)
            {
                IEnumerable<BodyPartRecord> partsToApplyOn = JobDriver_Scarify.GetPartsToApplyOn(pawn);
                List<BodyPartRecord> list = partsToApplyOn.Where((BodyPartRecord p) => JobDriver_Scarify.AvailableOnNow(pawn, p)).ToList();
                BodyPartRecord part = list.RandomElement();
                Hediff hediff = HediffMaker.MakeHediff(HediffDefOf.Scarification, pawn, part);
                HediffComp_GetsPermanent hediffComp_GetsPermanent = hediff.TryGetComp<HediffComp_GetsPermanent>();
                hediffComp_GetsPermanent.IsPermanent = true;
                hediffComp_GetsPermanent.SetPainCategory(JobDriver_Scarify.InjuryPainCategories.RandomElementByWeight((HealthTuning.PainCategoryWeighted e) => e.weight).category);
                pawn.health.AddHediff(hediff, null, null, null);
            }
        }

        public static bool HasTrait(this Pawn pawn, TraitDef trait) => pawn?.story?.traits?.HasTrait(trait) ?? false;

        public static bool DropItem(this Pawn pawn, IntVec3 DropLoc, ThingPlaceMode mode, out Thing resultingThing, Action<Thing, int> placedAction = null)
        {
            if (pawn?.carryTracker?.CarriedThing != null)
            {
                return pawn.carryTracker.TryDropCarriedThing(DropLoc, mode, out resultingThing, placedAction);
            }

            resultingThing = null;
            return false;
        }
    }
}
