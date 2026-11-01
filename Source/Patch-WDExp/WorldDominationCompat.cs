using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Reflection;
using TSA_WorldDomination;
using Verse;

namespace FactionColonies.WDExp
{
    /// <summary>
    /// Compatibility patches for "World Domination - Experimental" (TSA.WorldDominationExperimental).
    /// This assembly is only loaded when WD is active (via LoadFolders.xml).
    ///
    /// Fixes:
    /// 1. Excludes PColony from WD's daily action queue (prevents automated raids/growth/expansion)
    /// 2. Intercepts WD raids on Empire settlements (routes through Empire's defense system)
    /// 3. Prevents WD from using Empire settlements as raid actors
    /// 4. Scales enemy force in Empire battles based on WD settlement strength
    /// 5. Syncs PColony diplomacy after WD allegiance changes
    /// 6. Excludes PColony from WD's leader/underdog/balance mechanics
    /// 7. Prevents WD's CheckDefeated intercept from destroying Empire settlements
    /// </summary>
    [StaticConstructorOnStartup]
    public static class WorldDominationCompatInit
    {
        static WorldDominationCompatInit()
        {
            new Harmony("com.Matathias.Empire.WDExp").PatchAll(Assembly.GetExecutingAssembly());
            EmpireRegistry.Register(new WDStrengthSettlementModifier());
            LogUtil.MessageForce("World Domination (Experimental) compatibility module loaded.");
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
    // Patch 2: Intercept WD raids targeting Empire settlements
    // WD's ExecuteTravelerRaid checks target.Faction.IsPlayer
    // to route player attacks, but PColony is not IsPlayer.
    // Without this patch, WD runs simulated combat and ResolveVictory
    // calls target.Destroy(), permanently deleting Empire settlement data.
    // ================================================================
    [HarmonyPatch(typeof(Raid_Simulated), "ExecuteTravelerRaid")]
    public static class Patch_ExecuteTravelerRaid
    {
        private static bool Prefix(WorldObject_Traveler traveler, WorldComponent_SpreadManager manager)
        {
            WorldSettlementFC empireSettlement = traveler.targetObject as WorldSettlementFC;
            if (empireSettlement is null) return true;
            if (traveler.Faction is null) return true;

            // Sample the baseline through the worldcomp so faction-level efficiency modifiers apply.
            // WD's traveler strength dictates militaryLevel, so we override the sampled level with it.
            EnemyPower entry = FindFC.EnemyPower?.GetOrCompute(traveler.Faction);
            MilitaryForce attackingForce = entry?.SampleBattleForce(traveler.Faction);
            if (attackingForce is null)
            {
                MilitaryDeploymentUtil.GetTechLevelBaseline(traveler.Faction.def.techLevel, out double _, out double efficiency);
                attackingForce = new MilitaryForce(1, efficiency, null, traveler.Faction);
            }

            double wdMilitaryLevel = traveler.travelerStrength / WDStrengthSettlementModifier.SCALE_FACTOR;
            attackingForce.militaryLevel = wdMilitaryLevel;
            attackingForce.forceRemaining = Math.Max(1, Math.Round(attackingForce.militaryLevel * attackingForce.militaryEfficiency));

            // Route through Empire's defense system (1-day warning + auto-battle/manual)
            bool queued = MilitaryOperationsUtil.AttackPlayerSettlement(attackingForce, empireSettlement, traveler.Faction);

            if (queued)
            {
                LogUtil.Message("WD raid on Empire settlement " + empireSettlement.Name +
                    " intercepted (WD strength " + traveler.travelerStrength.ToString("F0") +
                    " -> Empire force " + attackingForce.forceRemaining + ")");
            }
            else
            {
                LogUtil.Message("WD raid on Empire settlement " + empireSettlement.Name +
                    " dropped (helper rejected — likely missing MilitaryComp or MilitaryManager).");
            }

            return false;
        }
    }

    // ================================================================
    // Patch 3: Prevent WD from using Empire settlements as raid actors
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
    // Patch 4: Scale enemy settlement power based on WDExp local defense.
    // Implements ISettlementPowerModifier so the override is baked into
    // the cached EnemyPower entry at recompute time — squad-attack window
    // and actual battle agree without per-engagement work.
    // ================================================================
    public class WDStrengthSettlementModifier : SettlementPowerModifierBase
    {
        public const double SCALE_FACTOR = 100.0;

        protected override string LogLabel => "WD defense power";

        protected override bool TryGetLevel(Settlement settlement, out double level)
        {
            level = 0;
            CompViralSpread comp = settlement.GetComponent<CompViralSpread>();
            if (comp == null) return false;

            float totalDefense = comp.GetTotalLocalDefensePower();
            if (totalDefense <= 0f) return false;

            level = totalDefense / SCALE_FACTOR;
            return true;
        }
    }

    // ================================================================
    // Patch 5: Sync PColony relations after WD diplomacy changes
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
    // Patch 6: Exclude PColony from WD's world power statistics
    // GetWorldPowerStats collects all non-player factions. PColony
    // passes this filter (it's not Faction.OfPlayer). If included,
    // PColony could be selected as world leader (triggering handicap)
    // or underdog (triggering buff), both of which are inappropriate
    // for a player-controlled empire.
    // ================================================================
    [HarmonyPatch(typeof(WorldStatsUtils), "GetWorldPowerStats")]
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
                for (int t = 1; t <= 4; t++)
                {
                    __result.GlobalTierStr[t] -= removed.strength[t];
                }
            }
        }
    }

    // ================================================================
    // Patch 7: Prevent WD's CheckDefeated intercept from destroying
    // Empire settlements. WD's Patch_InterceptDefeat runs at
    // Priority.High and calls factionBase.Destroy() on defeated
    // non-player settlements. PColony is not IsPlayer, so Empire
    // settlements would be destroyed and replaced with ruins/outpost
    // opportunities. This prefix-on-the-prefix skips WD's logic
    // for WorldSettlementFC, letting Empire's own base patch handle it.
    // ================================================================
    [HarmonyPatch(typeof(Patch_InterceptDefeat), "Prefix")]
    public static class Fix_WD_InterceptDefeat
    {
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(Settlement factionBase)
        {
            return !(factionBase is WorldSettlementFC);
        }
    }
}
