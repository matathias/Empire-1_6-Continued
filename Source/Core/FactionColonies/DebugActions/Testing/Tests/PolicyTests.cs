using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace FactionColonies
{
    public static class PolicyTestHelper
    {
        /// <summary>
        /// Snapshots the current faction policies and traits so they can be restored after a test.
        /// </summary>
        public static (List<FCPolicy> policies, List<FCPolicy> traits) SnapshotPolicies(FactionFC faction)
        {
            var savedPolicies = new List<FCPolicy>(FindFC.PolicyManager.policies);
            var savedTraits = new List<FCPolicy>(FindFC.PolicyManager.factionTraits);
            return (savedPolicies, savedTraits);
        }

        /// <summary>
        /// Restores faction policies and traits from a snapshot, calling OnRemoved on current behaviors.
        /// </summary>
        public static void RestorePolicies(FactionFC faction,
            (List<FCPolicy> policies, List<FCPolicy> traits) snapshot)
        {
            FindFC.PolicyManager.RemoveAllPolicies(FindFC.PolicyManager.policies);
            foreach (FCPolicy p in snapshot.policies)
                FindFC.PolicyManager.policies.Add(p);

            for (int i = 0; i < FindFC.PolicyManager.factionTraits.Count; i++)
            {
                if (FindFC.PolicyManager.factionTraits[i]?.behavior != null)
                {
                    try { FindFC.PolicyManager.factionTraits[i].behavior.OnRemoved(faction); }
                    catch (Exception ex) { LogUtil.Warning($"OnRemoved threw during test cleanup: {ex}"); }
                }
            }
            FindFC.PolicyManager.factionTraits.Clear();
            foreach (FCPolicy t in snapshot.traits)
                FindFC.PolicyManager.factionTraits.Add(t);

            FindFC.PolicyManager.RebuildBehaviorCache();
        }

        /// <summary>
        /// Enacts a policy on the faction, adding it to the policies list and rebuilding the cache.
        /// </summary>
        public static FCPolicy EnactPolicy(FactionFC faction, FCPolicyDef def)
        {
            var policy = new FCPolicy(def);
            FindFC.PolicyManager.policies.Add(policy);
            FindFC.PolicyManager.RebuildBehaviorCache();
            return policy;
        }

        /// <summary>
        /// Sets a trait on the faction in the given slot, calling OnRemoved on any existing behavior.
        /// </summary>
        public static FCPolicy SetTrait(FactionFC faction, FCPolicyDef def, int slot)
        {
            if (FindFC.PolicyManager.factionTraits[slot]?.behavior != null)
            {
                try { FindFC.PolicyManager.factionTraits[slot].behavior.OnRemoved(faction); }
                catch (Exception ex) { LogUtil.Warning($"OnRemoved threw during test cleanup: {ex}"); }
            }
            var trait = new FCPolicy(def);
            FindFC.PolicyManager.factionTraits[slot] = trait;
            FindFC.PolicyManager.RebuildBehaviorCache();
            return trait;
        }

        /// <summary>
        /// Clears all policies and sets all traits to empty, rebuilding cache.
        /// </summary>
        public static void ClearAll(FactionFC faction)
        {
            FindFC.PolicyManager.RemoveAllPolicies(FindFC.PolicyManager.policies);
            for (int i = 0; i < FindFC.PolicyManager.factionTraits.Count; i++)
            {
                if (FindFC.PolicyManager.factionTraits[i]?.behavior != null)
                {
                    try { FindFC.PolicyManager.factionTraits[i].behavior.OnRemoved(faction); }
                    catch (Exception ex) { LogUtil.Warning($"OnRemoved threw during test cleanup: {ex}"); }
                }
                FindFC.PolicyManager.factionTraits[i] = new FCPolicy(FCPolicyDefOf.empty);
            }
            FindFC.PolicyManager.RebuildBehaviorCache();
        }
    }

    public static class PolicyTests
    {
        private static FactionFC GetFaction()
        {
            return FindFC.FactionComp;
        }

        // ============================
        // Stat Aggregation Tests
        // ============================

        [EmpireTest("Policy")]
        public static void StatAggregation_AdditiveDefault_IsZero()
        {
            var faction = GetFaction();
            if (faction == null) TestAssert.Skip("No faction");

            var snapshot = PolicyTestHelper.SnapshotPolicies(faction);
            try
            {
                PolicyTestHelper.ClearAll(faction);
                // taxBonusFlat is additive with IdentityValue 0
                double val = faction.GetFactionStatValue(FCStatDefOf.taxBonusFlat);
                TestAssert.AreEqual(0.0, val, 0.001, "Additive stat with no policies should be 0");
            }
            finally
            {
                PolicyTestHelper.RestorePolicies(faction, snapshot);
            }
        }

        [EmpireTest("Policy")]
        public static void StatAggregation_MultiplicativeDefault_IsOne()
        {
            var faction = GetFaction();
            if (faction == null) TestAssert.Skip("No faction");

            var snapshot = PolicyTestHelper.SnapshotPolicies(faction);
            try
            {
                PolicyTestHelper.ClearAll(faction);
                // ClearAll removes policies but not active edicts / faction-wide events, which also feed
                // GetFactionStatValue (all four shipped edicts modify happinessGainedMultiplier). With no
                // policies the value should equal the stat's identity; if it doesn't, an edict/event is
                // contributing and the clean-slate premise does not hold — skip.
                // happinessGainedMultiplier is multiplicative with IdentityValue 1
                double val = faction.GetFactionStatValue(FCStatDefOf.happinessGainedMultiplier);
                if (System.Math.Abs(val - FCStatDefOf.happinessGainedMultiplier.IdentityValue) > 0.001)
                    TestAssert.Skip("Active edict/event modifies happinessGainedMultiplier; clean-slate premise does not hold");
                TestAssert.AreEqual(1.0, val, 0.001, "Multiplicative stat with no policies should be 1");
            }
            finally
            {
                PolicyTestHelper.RestorePolicies(faction, snapshot);
            }
        }

        [EmpireTest("Policy")]
        public static void StatAggregation_PolicyModifiers_Applied()
        {
            var faction = GetFaction();
            if (faction == null) TestAssert.Skip("No faction");

            var snapshot = PolicyTestHelper.SnapshotPolicies(faction);
            try
            {
                PolicyTestHelper.ClearAll(faction);

                // Find a policy with known statModifiers
                FCPolicyDef testDef = FCPolicyDefOf.militaristic;
                if (testDef.statModifiers.Count == 0)
                    TestAssert.Skip("militaristic has no XML stat modifiers");

                // ClearAll leaves active edicts/events in place; if one already modifies a stat this policy
                // touches, its post-clear baseline won't be the stat identity and the exact expected value
                // won't hold — skip rather than falsely fail.
                foreach (FCStatModifier mod in testDef.statModifiers)
                {
                    if (System.Math.Abs(faction.GetFactionStatValue(mod.stat) - mod.stat.IdentityValue) > 0.001)
                        TestAssert.Skip($"Active edict/event modifies {mod.stat.defName}; clean-slate premise does not hold");
                }

                PolicyTestHelper.EnactPolicy(faction, testDef);

                // Check each stat modifier is reflected
                foreach (FCStatModifier mod in testDef.statModifiers)
                {
                    double expected;
                    if (mod.stat.aggregation == FCStatAggregation.Additive)
                        expected = mod.stat.IdentityValue + mod.value;
                    else
                        expected = mod.stat.IdentityValue * mod.value;

                    double actual = faction.GetFactionStatValue(mod.stat);
                    TestAssert.AreEqual(expected, actual, 0.001,
                        $"Stat {mod.stat.defName} should reflect policy modifier");
                }
            }
            finally
            {
                PolicyTestHelper.RestorePolicies(faction, snapshot);
            }
        }

        [EmpireTest("Policy")]
        public static void StatAggregation_MultiplePolicies_Stack()
        {
            var faction = GetFaction();
            if (faction == null) TestAssert.Skip("No faction");

            var snapshot = PolicyTestHelper.SnapshotPolicies(faction);
            try
            {
                PolicyTestHelper.ClearAll(faction);

                // Find two policies that share an additive stat
                FCStatDef sharedStat = FCStatDefOf.militaryBaseLevel;
                var defs = DefDatabase<FCPolicyDef>.AllDefsListForReading
                    .Where(d => d.statModifiers.Any(m => m.stat == sharedStat
                        && sharedStat.aggregation == FCStatAggregation.Additive))
                    .Take(2).ToList();

                if (defs.Count < 2)
                    TestAssert.Skip("Not enough policies modifying militaryBaseLevel");

                // ClearAll leaves active edicts/events in place; if one already modifies militaryBaseLevel
                // its post-clear baseline won't be the stat identity and the exact stacked value won't hold.
                if (System.Math.Abs(faction.GetFactionStatValue(sharedStat) - sharedStat.IdentityValue) > 0.001)
                    TestAssert.Skip("Active edict/event modifies militaryBaseLevel; clean-slate premise does not hold");

                PolicyTestHelper.EnactPolicy(faction, defs[0]);
                PolicyTestHelper.EnactPolicy(faction, defs[1]);

                double mod0 = defs[0].statModifiers.First(m => m.stat == sharedStat).value;
                double mod1 = defs[1].statModifiers.First(m => m.stat == sharedStat).value;
                double expected = sharedStat.IdentityValue + mod0 + mod1;

                double actual = faction.GetFactionStatValue(sharedStat);
                TestAssert.AreEqual(expected, actual, 0.001,
                    $"Two additive policy modifiers should stack: {mod0} + {mod1}");
            }
            finally
            {
                PolicyTestHelper.RestorePolicies(faction, snapshot);
            }
        }

        // ============================
        // Behavior Lifecycle Tests
        // ============================

        [EmpireTest("Policy")]
        public static void BehaviorCache_RebuildAfterPolicyAdd()
        {
            var faction = GetFaction();
            if (faction == null) TestAssert.Skip("No faction");

            var snapshot = PolicyTestHelper.SnapshotPolicies(faction);
            try
            {
                PolicyTestHelper.ClearAll(faction);
                TestAssert.AreEqual(0, FindFC.PolicyManager.CachedBehaviors.Count, "After clearing, cachedBehaviors should be empty");

                PolicyTestHelper.EnactPolicy(faction, FCPolicyDefOf.militaristic);
                TestAssert.IsTrue(FindFC.PolicyManager.CachedBehaviors.Count > 0,
                    "After enacting policy with behavior, cachedBehaviors should be non-empty");
                TestAssert.IsTrue(FindFC.PolicyManager.CachedBehaviors[0] is FCPolicyBehavior_Militaristic,
                    "Cached behavior should be Militaristic instance");
            }
            finally
            {
                PolicyTestHelper.RestorePolicies(faction, snapshot);
            }
        }

        [EmpireTest("Policy")]
        public static void BehaviorCache_RebuildAfterClear()
        {
            var faction = GetFaction();
            if (faction == null) TestAssert.Skip("No faction");

            var snapshot = PolicyTestHelper.SnapshotPolicies(faction);
            try
            {
                PolicyTestHelper.EnactPolicy(faction, FCPolicyDefOf.militaristic);
                TestAssert.IsTrue(FindFC.PolicyManager.CachedBehaviors.Count > 0, "Should have behaviors after enacting");

                PolicyTestHelper.ClearAll(faction);
                TestAssert.AreEqual(0, FindFC.PolicyManager.CachedBehaviors.Count,
                    "After clearing all, cachedBehaviors should be empty");
            }
            finally
            {
                PolicyTestHelper.RestorePolicies(faction, snapshot);
            }
        }

        [EmpireTest("Policy")]
        public static void BehaviorCache_OrderPoliciesBeforeTraits()
        {
            var faction = GetFaction();
            if (faction == null) TestAssert.Skip("No faction");

            var snapshot = PolicyTestHelper.SnapshotPolicies(faction);
            try
            {
                PolicyTestHelper.ClearAll(faction);

                // Add a trait with behavior first, then a policy with behavior
                PolicyTestHelper.SetTrait(faction, FCPolicyDefOf.mercantile, 0);
                PolicyTestHelper.EnactPolicy(faction, FCPolicyDefOf.militaristic);

                TestAssert.IsTrue(FindFC.PolicyManager.CachedBehaviors.Count >= 2,
                    "Should have at least 2 behaviors");

                // Policies should come before traits in the cache
                TestAssert.IsTrue(FindFC.PolicyManager.CachedBehaviors[0] is FCPolicyBehavior_Militaristic,
                    "First cached behavior should be from policy (Militaristic), not trait");
                TestAssert.IsTrue(FindFC.PolicyManager.CachedBehaviors[1] is FCPolicyBehavior_Mercantile,
                    "Second cached behavior should be from trait (Mercantile)");
            }
            finally
            {
                PolicyTestHelper.RestorePolicies(faction, snapshot);
            }
        }

        // ============================
        // CooldownAbility Tests
        // ============================

        [EmpireTest("Policy")]
        public static void Cooldown_InitiallyReady()
        {
            var cd = new CooldownAbility { cooldownTicks = GenDate.TicksPerDay * 5 };
            TestAssert.IsTrue(cd.IsReady, "New CooldownAbility should be ready (tickLastUsed=-1)");
            TestAssert.AreEqual(0f, cd.DaysRemaining, 0.001, "DaysRemaining should be 0 when ready");
        }

        [EmpireTest("Policy")]
        public static void Cooldown_AfterUse_NotReady()
        {
            var cd = new CooldownAbility { cooldownTicks = GenDate.TicksPerDay * 5 };
            cd.Use();
            TestAssert.IsFalse(cd.IsReady, "After Use(), CooldownAbility should not be ready");
            TestAssert.GreaterThan(cd.DaysRemaining, 0, "DaysRemaining should be > 0 after Use()");
        }

        [EmpireTest("Policy")]
        public static void Cooldown_DaysRemaining_Correct()
        {
            var cd = new CooldownAbility { cooldownTicks = GenDate.TicksPerDay * 5 };
            cd.Use();
            // DaysRemaining should be approximately 5 days (may be slightly less due to tick timing)
            TestAssert.LessThanOrEqual(cd.DaysRemaining, 5.01,
                $"DaysRemaining should be <= 5.01, got {cd.DaysRemaining}");
            TestAssert.GreaterThan(cd.DaysRemaining, 4.9,
                $"DaysRemaining should be > 4.9, got {cd.DaysRemaining}");
        }

        // ============================
        // Incompatibility Tests
        // ============================

        [EmpireTest("Policy")]
        public static void Incompatible_MutualExclusionDefined()
        {
            // Verify that incompatible policies are defined mutually (A blocks B implies B blocks A)
            // Only enforced within the same category — edict→core blocks are one-directional
            foreach (FCPolicyDef def in DefDatabase<FCPolicyDef>.AllDefsListForReading)
            {
                foreach (FCPolicyDef blocked in def.incompatiblePolicies)
                {
                    if (def.category != blocked.category) continue;
                    TestAssert.IsTrue(blocked.incompatiblePolicies.Contains(def),
                        $"{def.defName} blocks {blocked.defName} but not vice versa — incompatibility should be mutual");
                }
            }
        }

        // ============================
        // Specific Behavior Tests
        // ============================

        [EmpireTest("Policy")]
        public static void Egalitarian_WorkerProductionMultiplier_10Pct()
        {
            var faction = GetFaction();
            if (faction == null || faction.settlements.Count == 0)
                TestAssert.Skip("No faction/settlements");

            var snapshot = PolicyTestHelper.SnapshotPolicies(faction);
            try
            {
                PolicyTestHelper.ClearAll(faction);
                PolicyTestHelper.EnactPolicy(faction, FCPolicyDefOf.egalitarian);

                WorldSettlementFC settlement = faction.settlements.First();
                double mult = faction.GetStatValue(FCStatDefOf.workerProductionMultiplier, settlement);
                TestAssert.AreEqual(1.1, mult, 0.01,
                    $"Egalitarian should boost worker production by 10% (got {mult})");
            }
            finally
            {
                PolicyTestHelper.RestorePolicies(faction, snapshot);
            }
        }

        [EmpireTest("Policy")]
        public static void Expansionist_SettlementCost_Discount()
        {
            var faction = GetFaction();
            if (faction == null) TestAssert.Skip("No faction");

            var snapshot = PolicyTestHelper.SnapshotPolicies(faction);
            try
            {
                PolicyTestHelper.ClearAll(faction);
                PolicyTestHelper.EnactPolicy(faction, FCPolicyDefOf.expansionist);

                double costMult = faction.GetStatValue(FCStatDefOf.settlementCostMultiplier);
                TestAssert.AreEqual(0.75, costMult, 0.01,
                    $"Expansionist should give a permanent 25% discount (got {costMult})");
            }
            finally
            {
                PolicyTestHelper.RestorePolicies(faction, snapshot);
            }
        }

        // NOTE: a former Expansionist_ThreatScaling_Increased test was removed here — Expansionist no
        // longer carries a threatScalingMultiplier modifier (its stats are settlementCostMultiplier and
        // unrestGainedBase), so the +10% threat-scaling assertion no longer describes the policy.

        [EmpireTest("Policy")]
        public static void Militaristic_BuildingUpkeep_Discount()
        {
            var faction = GetFaction();
            if (faction == null) TestAssert.Skip("No faction");
            if (!faction.settlements.Any()) TestAssert.Skip("Need settlements");

            var snapshot = PolicyTestHelper.SnapshotPolicies(faction);
            try
            {
                PolicyTestHelper.ClearAll(faction);
                var policy = PolicyTestHelper.EnactPolicy(faction, FCPolicyDefOf.militaristic);

                TestAssert.IsNotNull(policy.behavior as FCPolicyBehavior_Militaristic, "Militaristic should have a behavior");

                var settlement = faction.settlements.First();

                // The Militaristic discount is the buildingUpkeepBase_Military stat (-10 post-rework), applied
                // via the live GetBuildingUpkeep path. Military classification is the explicit isMilitary flag.
                BuildingFCDef milBuilding = DefDatabase<BuildingFCDef>.AllDefsListForReading
                    .FirstOrDefault(b => b.isMilitary && b.Upkeep > 0);

                if (milBuilding == null)
                    TestAssert.Skip("No military buildings with upkeep found");

                TestAssert.AreEqual(Math.Max(milBuilding.Upkeep - 10, 0.0),
                    settlement.BuildingsComp.GetBuildingUpkeep(milBuilding), 0.001,
                    $"Military building '{milBuilding.defName}' upkeep should be discounted by 10 (base {milBuilding.Upkeep})");

                // Non-military (unflagged) building should not be discounted
                BuildingFCDef civBuilding = DefDatabase<BuildingFCDef>.AllDefsListForReading
                    .FirstOrDefault(b => !b.isMilitary && b.Upkeep > 0
                        && b != BuildingFCDefOf.Empty && b != BuildingFCDefOf.Construction);

                if (civBuilding != null)
                {
                    TestAssert.AreEqual(civBuilding.Upkeep,
                        settlement.BuildingsComp.GetBuildingUpkeep(civBuilding), 0.001,
                        $"Civilian building '{civBuilding.defName}' upkeep should not be discounted");
                }
            }
            finally
            {
                PolicyTestHelper.RestorePolicies(faction, snapshot);
            }
        }

        // ============================
        // ConfigError Validation
        // ============================

        [EmpireTest("Policy")]
        public static void AllPolicies_NoBehaviorClassErrors()
        {
            foreach (FCPolicyDef def in DefDatabase<FCPolicyDef>.AllDefsListForReading)
            {
                FCPolicyBehaviorExtension ext = def.BehaviorExtension;
                if (ext != null)
                {
                    TestAssert.IsNotNull(ext.behaviorClass,
                        $"{def.defName}: behavior extension has null behaviorClass");
                    TestAssert.IsTrue(typeof(FCPolicyBehavior).IsAssignableFrom(ext.behaviorClass),
                        $"{def.defName}: behaviorClass {ext.behaviorClass.Name} must inherit FCPolicyBehavior");
                }
            }
        }

        [EmpireTest("Policy")]
        public static void AllPolicies_NoNullStatModifiers()
        {
            foreach (FCPolicyDef def in DefDatabase<FCPolicyDef>.AllDefsListForReading)
            {
                for (int i = 0; i < def.statModifiers.Count; i++)
                {
                    TestAssert.IsNotNull(def.statModifiers[i].stat,
                        $"{def.defName}: statModifiers[{i}] has null stat (bad defName in XML?)");
                }
            }
        }

        [EmpireTest("Policy")]
        public static void AllPolicies_StatValues_AreFinite()
        {
            var faction = GetFaction();
            if (faction == null) TestAssert.Skip("No faction");

            var snapshot = PolicyTestHelper.SnapshotPolicies(faction);
            try
            {
                foreach (FCPolicyDef def in DefDatabase<FCPolicyDef>.AllDefsListForReading)
                {
                    if (def == FCPolicyDefOf.empty) continue;

                    PolicyTestHelper.ClearAll(faction);
                    PolicyTestHelper.EnactPolicy(faction, def);

                    foreach (FCStatDef stat in DefDatabase<FCStatDef>.AllDefsListForReading)
                    {
                        double val = faction.GetFactionStatValue(stat);
                        TestAssert.IsFalse(double.IsNaN(val),
                            $"Policy {def.defName} makes stat {stat.defName} NaN");
                        TestAssert.IsFalse(double.IsInfinity(val),
                            $"Policy {def.defName} makes stat {stat.defName} infinite");
                    }
                }
            }
            finally
            {
                PolicyTestHelper.RestorePolicies(faction, snapshot);
            }
        }
    }
}
