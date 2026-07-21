using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Post-generation apparel safety net for Empire-faction humanlike pawns. Vanilla apparel
    /// generation can leave a pawn with no torso garment when its rolled apparelMoney can't afford one
    /// (common for low-tech HAR races whose cheapest apparel sits near the budget floor), which spawns
    /// the pawn naked. This forces the cheapest tag-appropriate torso garment onto such pawns so they
    /// are always clothed in race-correct apparel.
    ///
    /// Exception: when Ideology is active and the Empire faction's ideo has the Nudism meme, the
    /// faction's pawns are meant to be naked, so we strip all generated apparel instead.
    ///
    /// Mercenaries are re-equipped from the player's designs (MercenaryPawnFactory.CreateNewPawn) after
    /// generation, so this postfix's result is overwritten for them and designs remain authoritative.
    /// </summary>
    [HarmonyPatch(typeof(PawnApparelGenerator))]
    [HarmonyPatch(nameof(PawnApparelGenerator.GenerateStartingApparelFor))]
    class Patch_PawnApparelGenerator_EnsureEmpireApparel
    {
        // All loaded apparel that covers the Torso, cheapest-first. Built once, lazily.
        private static List<ThingDef> torsoApparelCheapestFirst;

        // Nudism meme (Ideology). Cached; stays null when Ideology is inactive or the def is absent.
        private static MemeDef nudismMeme;
        private static bool nudismResolved;

        static void Postfix(Pawn pawn, PawnGenerationRequest request)
        {
            if (pawn?.apparel is null) return;
            if (!pawn.RaceProps.Humanlike) return;
            if (pawn.Faction is null || pawn.Faction != FindFC.EmpireFaction) return;

            // Nudism meme -> the faction's pawns spawn naked.
            if (ModsConfig.IdeologyActive && EmpireHasNudism())
            {
                pawn.apparel.DestroyAll();
                return;
            }

            // Already wearing something over the torso: nothing to do.
            if (pawn.apparel.WornApparel.Any(a =>
                    a.def.apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.Torso)))
            {
                return;
            }

            ThingDef pick = FindCheapestWearableTorso(pawn);
            if (pick is null) return;

            ThingDef stuff = pick.MadeFromStuff ? GenStuff.DefaultStuffFor(pick) : null;
            if (!(ThingMaker.MakeThing(pick, stuff) is Apparel ap)) return;

            // PostProcessApparel (which applies faction colour) has already run, so colour it here as
            // SquadEquipmentTracker.WearApparelItem does.
            Color resolved = FindFC.FactionComp?.ResolveApparelColor(ap.def) ?? Color.white;
            ap.SetColor(resolved, reportFailure: false);
            pawn.apparel.Wear(ap, dropReplacedApparel: false);
        }

        private static bool EmpireHasNudism()
        {
            if (!nudismResolved)
            {
                nudismMeme = DefDatabase<MemeDef>.GetNamedSilentFail("Nudism");
                nudismResolved = true;
            }
            if (nudismMeme is null) return false;
            Ideo ideo = FindFC.EmpireFaction?.ideos?.PrimaryIdeo;
            return ideo?.HasMeme(nudismMeme) ?? false;
        }

        /// <summary>Cheapest torso-covering apparel the pawn can wear, preferring a match against the
        /// pawn kind's apparelTags (so a HAR race gets its own apparel) and falling back to the cheapest
        /// wearable torso garment when no tag matches. Null only if nothing torso-covering is wearable.</summary>
        private static ThingDef FindCheapestWearableTorso(Pawn pawn)
        {
            EnsureTorsoApparelList();
            List<string> tags = pawn.kindDef?.apparelTags;
            bool hasTags = tags != null && tags.Count > 0;

            ThingDef fallback = null;
            foreach (ThingDef def in torsoApparelCheapestFirst)
            {
                if (!def.apparel.PawnCanWear(pawn)) continue;
                if (fallback is null) fallback = def;                 // cheapest wearable, tag-agnostic
                if (!hasTags) return def;                             // no tag preference -> cheapest wearable
                if (def.apparel.tags != null && def.apparel.tags.Any(t => tags.Contains(t)))
                    return def;                                       // cheapest tag-matching wearable
            }
            return fallback;
        }

        private static void EnsureTorsoApparelList()
        {
            if (torsoApparelCheapestFirst != null) return;
            torsoApparelCheapestFirst = DefDatabase<ThingDef>.AllDefsListForReading
                .Where(d => d.IsApparel && d.apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.Torso))
                .OrderBy(d => d.BaseMarketValue)
                .ToList();
        }
    }
}
