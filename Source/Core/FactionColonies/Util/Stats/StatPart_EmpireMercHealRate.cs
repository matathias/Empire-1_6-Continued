using RimWorld;
using Verse;

namespace FactionColonies
{
    /* Multiplies InjuryHealingFactor for off-map mercenary pawns currently registered for
     * Empire's hourly heal tick. Composes the user-facing slider
     * (FCSettings.mercenaryHealRatePerHour, semantically a multiplier now) and the
     * per-settlement mercHealRateMultiplier stat into a single factor.
     *
     * Rebaseline math: at slider 1.0, an Empire merc heals at the same per-day rate as
     * a vanilla pawn in a basic bed being tended with industrial medicine by an unskilled
     * (Medicine 0) doctor. Vanilla per-check at those conditions is ~0.216 IHF HP
     * (natural 0.16 from bed+rest, tended 0.056 from quality-0.20 medicine), fired every
     * 600 ticks for ~21.6 HP/day. Empire calls HealthTickInterval once per game hour with
     * the merc at standing+bedless baseline (per-call ~0.18 IHF HP combining both branches
     * at Empire's null-doctor quality 0.75); 24 calls/day at the bare rate would be ~4.32
     * IHF HP/day. The 5.0x multiplier lifts that to ~21.6 HP/day, matching the vanilla
     * target. Slider > 1.0 heals proportionally faster; < 1.0 slower. */
    public class StatPart_EmpireMercHealRate : StatPart
    {
        private const float RebaselineFactor = 5.0f;

        public override void TransformValue(StatRequest req, ref float val)
        {
            if (TryGetMercFactor(req, out float factor))
                val *= factor;
        }

        public override string ExplanationPart(StatRequest req)
        {
            if (!TryGetMercFactor(req, out float factor)) return null;
            return "FCEmpireMercHealRateStatExplanation".Translate() + ": x" + factor.ToStringPercent();
        }

        private static bool TryGetMercFactor(StatRequest req, out float factor)
        {
            factor = 1f;
            if (!req.HasThing || !(req.Thing is Pawn pawn)) return false;

            Mercenary merc = FindFC.Military?.GetRegisteredInjuredMerc(pawn);
            if (merc is null) return false;

            // On-map mercs follow vanilla healing — we only boost while abstracted at base.
            if (pawn.Map != null) return false;

            WorldSettlementFC settlement = merc.settlement ?? merc.squad?.getSettlement;
            FactionFC faction = FindFC.FactionComp;
            // unit context (the merc) lets per-unit design/accolade heal-rate modifiers fold in for this soldier
            double settlementMult = (faction is object)
                ? faction.GetStatValue(FCStatDefOf.mercHealRateMultiplier, settlement, null, merc)
                : 1.0;

            factor = RebaselineFactor * FCSettings.mercenaryHealRatePerHour * (float)settlementMult;
            return true;
        }
    }
}
