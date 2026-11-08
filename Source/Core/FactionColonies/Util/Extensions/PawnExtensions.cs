namespace FactionColonies.util;

static class PawnExtensions
{
    public static bool IsMercenary(this Pawn pawn) => FindFC.Military?.IsMercenaryPawn(pawn) ?? false;

    public static void ApplyIdeologyRitualWounds(this Pawn pawn)
    {
        try
        {
            if (ModsConfig.IdeologyActive && pawn?.Ideo is not null && !pawn.AnimalOrWildMan())
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
            string pawnName = (pawn?.Name is not null) ? pawn.Name.ToString() : "pawn is null!";
            LogUtil.Error("Required ritual wounds couldn't be applied to pawn: " + pawnName + ". Pawn Ideo == null: " + (pawn?.Ideo is null) + ". Exception: " + ex);
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
        int num = pawn.health.hediffSet.GetHediffCount(HediffDefOf.Scarification);

        for (int i = num; i < pawn.ideo.Ideo.RequiredScars; i++)
        {
            // Only the per-part safety checks from JobDriver_Scarify.AvailableOnNow: its other gate
            // looks at the player faction's ideos, but the scars here come from the pawn's own ideo.
            List<BodyPartRecord> list = JobDriver_Scarify.GetPartsToApplyOn(pawn)
                .Where(p => !pawn.health.WouldDieAfterAddingHediff(HediffDefOf.Scarification, p, 1f)
                    && !pawn.health.WouldLosePartAfterAddingHediff(HediffDefOf.Scarification, p, 1f))
                .ToList();
            if (list.Count == 0) return;
            JobDriver_Scarify.Scarify(pawn, list.RandomElement());
        }
    }

    public static bool HasTrait(this Pawn pawn, TraitDef trait) => pawn?.story?.traits?.HasTrait(trait) ?? false;

    public static bool DropItem(this Pawn pawn, IntVec3 DropLoc, ThingPlaceMode mode, out Thing resultingThing, Action<Thing, int> placedAction = null)
    {
        if (pawn?.carryTracker?.CarriedThing is not null)
        {
            return pawn.carryTracker.TryDropCarriedThing(DropLoc, mode, out resultingThing, placedAction);
        }

        resultingThing = null;
        return false;
    }
}
