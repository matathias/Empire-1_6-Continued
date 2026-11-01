using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System.Reflection;
using TSA_WorldDomination.WorldActions;
using Verse;
using WorldDomination;

namespace FactionColonies.WD
{
    /// <summary>
    /// Compatibility patches for "World Domination" (TSA.WorldDomination).
    /// This assembly is only loaded when WD is active (via LoadFolders.xml).
    ///
    /// Fixes:
    /// 1. Excludes PColony from WD's daily action queue (prevents automated raids/growth/expansion)
    /// 2. Prevents WD from using Empire settlements as raid actors
    /// 3. Scales enemy force in Empire battles based on WD settlement strength
    /// 4. Syncs PColony diplomacy after WD allegiance changes
    /// 5. Excludes PColony from WD's leader/underdog/balance mechanics
    /// </summary>
    [StaticConstructorOnStartup]
    public static class WorldDominationCompatInit
    {
        static WorldDominationCompatInit()
        {
            new Harmony("com.Matathias.Empire.WD").PatchAll(Assembly.GetExecutingAssembly());
            EmpireRegistry.Register(new WDStrengthSettlementModifier());
            LogUtil.MessageForce("World Domination compatibility module loaded.");
        }
    }

    // ================================================================
    // Patch 1: Exclude PColony from WD's action queue
    // WD checks IsExcludedFaction to decide which factions get daily
    // actions. PColony is not f.IsPlayer, so it passes by default.
    // ================================================================
    [HarmonyPatch(typeof(WorldActions_Utils), "IsExcludedFaction")]
    public static class Patch_IsExcludedFaction
    {
        private static void Postfix(Faction f, ref bool __result)
        {
            if (__result) return;
            if (FindFC.IsEmpireFaction(f))
            {
                __result = true;
            }
        }
    }

    // ================================================================
    // Patch 2: Prevent WD from using Empire settlements as raid actors
    // Belt-and-suspenders with Patch 1. If PColony somehow enters the
    // action queue, this prevents its settlements from being selected.
    // ================================================================
    [HarmonyPatch(typeof(WorldActions_Utils), "IsSettlementProtected")]
    public static class Patch_IsSettlementProtected
    {
        private static void Postfix(Settlement s, ref bool __result)
        {
            if (__result) return;
            if (s is WorldSettlementFC)
            {
                __result = true;
            }
        }
    }

    // ================================================================
    // Patch 3: Scale enemy settlement power based on WD CompViralSpread.
    // Implements ISettlementPowerModifier so the override is baked into
    // the cached EnemyPower entry at recompute time — squad-attack window
    // and actual battle agree without per-engagement work.
    // ================================================================
    public class WDStrengthSettlementModifier : SettlementPowerModifierBase
    {
        public const double SCALE_FACTOR = 100.0;

        protected override string LogLabel => "WD strength";

        protected override bool TryGetLevel(Settlement settlement, out double level)
        {
            level = 0;
            CompViralSpread comp = settlement.GetComponent<CompViralSpread>();
            if (comp == null || comp.strength <= 0f) return false;

            level = comp.strength / SCALE_FACTOR;
            return true;
        }
    }

    // ================================================================
    // Patch 4: Sync PColony relations after WD diplomacy changes
    // WD randomly shifts faction allegiances and forms coalitions.
    // PColony must mirror the player faction's relations.
    // ================================================================
    [HarmonyPatch(typeof(WorldActions_DiplomacyBuffsNerfs), "TryChangeAllegiances")]
    public static class Patch_TryChangeAllegiances
    {
        private static void Postfix()
        {
            if (FindFC.EmpireFaction != null)
            {
                RelationsUtilFC.ResetPlayerColonyRelations();
            }
        }
    }

    [HarmonyPatch(typeof(WorldActions_DiplomacyBuffsNerfs), "FormAntiLeaderCoalition")]
    public static class Patch_FormAntiLeaderCoalition
    {
        private static void Postfix()
        {
            if (FindFC.EmpireFaction != null)
            {
                RelationsUtilFC.ResetPlayerColonyRelations();
            }
        }
    }

    // ================================================================
    // Patch 5: Exclude PColony from WD's world power statistics
    // GetWorldPowerStats collects all non-player factions. PColony
    // passes this filter (it's not Faction.OfPlayer). If included,
    // PColony could be selected as world leader (triggering handicap)
    // or underdog (triggering buff), both of which are inappropriate
    // for a player-controlled empire.
    // ================================================================
    [HarmonyPatch(typeof(WorldActions_Utils), "GetWorldPowerStats")]
    public static class Patch_GetWorldPowerStats
    {
        private static void Postfix(SpreadLogEntry.GlobalWorldStats __result)
        {
            if (__result == null) return;

            if (FindFC.EmpireFaction == null) return;

            SpreadLogEntry.FactionStat removed = null;
            for (int i = 0; i < __result.FactionStats.Count; i++)
            {
                if (FindFC.IsEmpireFaction(__result.FactionStats[i].faction))
                {
                    removed = __result.FactionStats[i];
                    __result.FactionStats.RemoveAt(i);
                    break;
                }
            }

            if (removed != null)
            {
                __result.GlobalTotalStr -= removed.TotalStr;
            }
        }
    }
}
