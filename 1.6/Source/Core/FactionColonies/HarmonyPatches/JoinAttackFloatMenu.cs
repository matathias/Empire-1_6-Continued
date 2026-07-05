using System.Collections.Generic;
using HarmonyLib;
using RimWorld.Planet;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Suppresses the vanilla "Attack settlement" caravan float option while an Empire offense
    /// battle owns that settlement's tile -- the caravan should Join the ongoing assault (registered
    /// with the battle, via WorldObjectComp_OffenseControls.GetFloatMenuOptions) rather than start an
    /// unregistered vanilla attack on the same map. The Join Attack option itself is added by the
    /// comp, not here; only the vanilla attack option needs suppressing (a patch is unavoidable
    /// since it is contributed by vanilla, not by our comp).
    /// </summary>
    [HarmonyPatch(typeof(CaravanArrivalAction_AttackSettlement), nameof(CaravanArrivalAction_AttackSettlement.GetFloatMenuOptions))]
    public static class CaravanAttackSettlement_SuppressDuringOffense
    {
        public static bool Prefix(Settlement settlement, ref IEnumerable<FloatMenuOption> __result)
        {
            BattlefieldContext bf = FindFC.MilitaryManager?.GetBattlefield(settlement.Tile);
            if (bf is object && bf.HasOffenseAt())
            {
                __result = new List<FloatMenuOption>();
                return false;
            }
            return true;
        }
    }
}
