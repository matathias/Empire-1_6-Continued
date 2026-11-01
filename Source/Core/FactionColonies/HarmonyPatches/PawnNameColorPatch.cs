using HarmonyLib;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Dims the on-map name label of settlement civilians recruited onto a manual defense
    /// battle map, so the player can tell the fighting squad from civilian filler at a glance.
    /// Civilians share the defenders' Empire faction (and thus the same default label color),
    /// so they are identified via the per-tile <see cref="BattlefieldContext.civilianPawns"/>
    /// set rather than by faction or pawnkind.
    /// </summary>
    [HarmonyPatch(typeof(PawnNameColorUtility))]
    [HarmonyPatch(nameof(PawnNameColorUtility.PawnNameColorOf))]
    class Patch_PawnNameColorUtility_PawnNameColorOf
    {
        // Civilian labels are drawn as a dimmed version of the pawn's normal (Empire faction)
        // label color, so they read as a muted echo of the defenders' labels. Tunable.
        const float CivilianLabelDim = 0.6f;

        static void Postfix(Pawn pawn, ref Color __result)
        {
            if (pawn?.Map is null) return;
            if (pawn.Faction != FindFC.EmpireFaction) return;                       // cheap short-circuit
            BattlefieldContext bf = FindFC.MilitaryManager?.GetBattlefield(pawn.Map.Tile);
            if (bf?.civilianPawns is null) return;                     // not an Empire battle map
            if (!bf.civilianPawns.Contains(pawn)) return;

            // __result is the pawn's default Empire-faction color here (civilians share the
            // defenders' faction), so dimming it yields a darker shade of that same color.
            __result = new Color(__result.r * CivilianLabelDim, __result.g * CivilianLabelDim,
                                 __result.b * CivilianLabelDim, __result.a);
        }
    }
}
