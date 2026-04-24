using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
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
            BattleModifierRegistry.Register(new WDStrengthBattleModifier());
            FactionCache.EmpireFactionDef.hidden = false;
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
            if (FactionCache.IsPlayerColonyFaction(f))
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
            if (empireSettlement == null) return true;

            if (traveler.Faction == null) return true;
            
            MilitaryForce WDE_AttackForce = WDEForceConverter.FromWDEStrength(traveler.Faction, traveler.Faction.def.techLevel, traveler.travelerStrength);

            // Route through Empire's defense system (1-day warning + auto-battle/manual)
            MilitaryUtilFC.AttackPlayerSettlement(WDE_AttackForce, empireSettlement, traveler.Faction);

            LogUtil.Message("WD raid on Empire settlement " + empireSettlement.Name +
                " intercepted (WD strength " + traveler.travelerStrength.ToString("F0") +
                " -> Empire force " + WDE_AttackForce.forceRemaining + ")");

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
            if (s is WorldSettlementFC)
            {
                __result = false;
            }
        }
    }

    // ================================================================
    // Patch 4: Scale enemy force based on WD settlement strength
    // Uses IBattleModifier so it integrates with Empire's existing
    // battle modifier pipeline. When Empire attacks a settlement that
    // has CompViralSpread, the defender's force is scaled from WD
    // strength instead of just tech level.
    //
    // BattleModifierRegistry calls modifiers in order:
    //   InvokeModifyForce(MFA, isAttacker=true)  <- attacker first
    //   InvokeModifyForce(MFB, isAttacker=false) <- defender second
    // We capture the attacker on the first call to find the target.
    // ================================================================
    public class WDStrengthBattleModifier : IBattleModifier
    {
        public const double SCALE_FACTOR = 100.0;

        private MilitaryForce lastAttacker;

        public void ModifyForce(MilitaryForce force, bool isAttacker)
        {
            if (isAttacker)
            {
                lastAttacker = force;
                return;
            }

            // Defender side — look up target settlement via the attacker's military comp
            MilitaryForce attacker = lastAttacker;
            lastAttacker = null;

            if (attacker == null || attacker.homeSettlement == null) return;

            WorldObjectComp_SettlementMilitary milComp = attacker.homeSettlement.MilitaryComp;
            if (milComp == null || milComp.militaryLocation < 0) return;

            Settlement target = Find.WorldObjects.SettlementAt(milComp.militaryLocation);
            if (target == null) return;

            CompViralSpread comp = target.GetComponent<CompViralSpread>();
            if (comp == null) return;

            float totalDefense = comp.GetTotalLocalDefensePower();
            if (totalDefense <= 0f) return;

            MilitaryForce WDE_DefenceForce = WDEForceConverter.FromWDEStrength(comp.parent.Faction, comp.parent.Faction.def.techLevel, totalDefense);

            force.militaryLevel = WDE_DefenceForce.militaryLevel;
            force.forceRemaining = WDE_DefenceForce.forceRemaining;

            LogUtil.Message("WD defense power " + totalDefense.ToString("F0") + " (tier " + comp.tier + ") -> Empire defender force " + force.forceRemaining);
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
            if (FactionCache.PlayerColonyFaction != null)
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
            if (FactionCache.PlayerColonyFaction != null)
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

            if (FactionCache.PlayerColonyFaction == null) return;

            SpreadLogEntry.FactionStat removed = null;
            for (int i = 0; i < __result.FactionStats.Count; i++)
            {
                if (FactionCache.IsPlayerColonyFaction(__result.FactionStats[i].faction))
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

    [HarmonyPatch(typeof(WorldActions_Utils), "GetWorldObjectsWithCompByFaction")]
    public static class Patch_AddCompToEmpireSettlements
    {
        private static Dictionary<WorldSettlementFC, int> lastUpdateTick = new Dictionary<WorldSettlementFC, int>();
        private const int UPDATE_INTERVAL_TICKS = 7 * 60000; // 7 in-game days

        private static void Postfix(ref Dictionary<Faction, List<WorldObject>> __result)
        {
            int currentTick = Find.TickManager.TicksGame;
            List<WorldObject> empireSettlementsToAdd = new List<WorldObject>();

            foreach (WorldSettlementFC empireSettlement in FactionCache.FactionComp.settlements)
            {
                if (empireSettlement == null)
                {
                    continue;
                }
                CompViralSpread comp = null;
                empireSettlement.TryGetComponent<CompViralSpread>(out comp);
                bool needsCreate = comp == null;
                bool needsUpdate = false;

                if (!needsCreate)
                {
                    if (!lastUpdateTick.TryGetValue(empireSettlement, out int lastTick))
                    {
                        needsUpdate = true;
                    }
                    else if (currentTick - lastTick > UPDATE_INTERVAL_TICKS)
                    {
                        needsUpdate = true;
                    }
                }

                if (needsCreate)
                {
                    comp = new CompViralSpread();
                    comp.parent = empireSettlement;

                    //LogUtil.Message("Adding Comp via reflection");
                    var compsField = typeof(WorldObject).GetField("comps", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (compsField != null)
                    {
                        var compsList = (List<WorldObjectComp>)compsField.GetValue(empireSettlement);
                        compsList?.Add(comp);
                    }
                    else
                    {
                        LogUtil.Error("Could not find 'comps' field on WorldObject via reflection.");
                    }

                    // Call PostAdd if it exists (some comps rely on it)
                    empireSettlement.PostAdd();
                    comp.defensiveStrength = 0;
                    comp.strength = 0;
                    needsUpdate = true;
                }

                if (needsUpdate && empireSettlement.MilitaryComp.militarySquad != null && empireSettlement.MilitaryComp.militarySquad.outfit != null)
                {
                    float equipmentCost = (float)empireSettlement.MilitaryComp.militarySquad.outfit.equipmentTotalCost;
                    comp.defensiveStrength = (float)(equipmentCost + empireSettlement.GetDefenseBonus()) / 50f;
                    comp.strength = equipmentCost / 50f;
                    comp.defenseCooldownTick = -1;
                    if (comp.strength > 650)
                    {
                        comp.tier = SettlementTier.T2;
                    } else if (comp.strength > 1150)
                    {
                        comp.tier = SettlementTier.T3;
                    } else if (comp.strength > 1750)
                    {
                        //comp.tier = SettlementTier.T4;
                    }

                    lastUpdateTick[empireSettlement] = currentTick;
                }
                
                empireSettlementsToAdd.Add(empireSettlement);
            }

            if (__result == null)
            {
                __result = new Dictionary<Faction, List<WorldObject>>();
            }
            __result.SetOrAdd(FactionCache.PlayerColonyFaction,empireSettlementsToAdd);
            LogUtil.Message("World Domination (Experimental) compatibility -> Fetched " + empireSettlementsToAdd.Count + " empire settlement candidates");
        }
    }
    
    public static class WDEForceConverter
    {
        public const double SCALE_FACTOR = 100.0;

        public static MilitaryForce FromWDEStrength(
            Faction faction,
            TechLevel techLevel,
            float strengthValue)
        {
            if (faction == null || strengthValue <= 0f)
                return null;

            double tech;
            double efficiency;

            MilitaryForce.GetMilitaryLevelAndEfficiencyFromTechLevel(
                techLevel, out tech, out efficiency);

            double wdMilitaryLevel = strengthValue / SCALE_FACTOR;

            MilitaryForce result = new MilitaryForce(
                wdMilitaryLevel,
                efficiency,
                null,
                faction);

            result.militaryLevel = wdMilitaryLevel;
            result.forceRemaining = Math.Round(wdMilitaryLevel * result.militaryEfficiency);

            return result;
        }
    }
}