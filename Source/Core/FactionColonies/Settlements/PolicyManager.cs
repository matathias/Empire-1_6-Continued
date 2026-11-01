using System;
using System.Collections.Generic;
using FactionColonies.util;
using RimWorld;
using Verse;

namespace FactionColonies
{
    /* Owns the faction's policy/trait/edict state and behavior caches.
     * Lives on FactionFC.policyManager. Accessed externally via FindFC.PolicyManager.
     *
     * State persisted via nested <policyManager> Scribe element. Legacy flat fields
     * (factionPolicies, factionTraits, edicts) on old saves are migrated by
     * FactionFC.ExposeData's migration shim, which calls SeedFromLegacy() once
     * during PostLoadInit. */
    public class PolicyManager : IExposable
    {
        /* Owned state */
        public List<FCPolicy> policies = new List<FCPolicy>();
        public List<FCPolicy> factionTraits = SeedDefaultTraits();
        public Dictionary<FCPolicyCategory, FCPolicy> edicts = new Dictionary<FCPolicyCategory, FCPolicy>();
        internal HashSet<FCPolicyCategory> pendingEdictActivations = new HashSet<FCPolicyCategory>();

        /* Transient caches (not scribed) */
        private List<FCPolicyBehavior> _cachedBehaviors;
        // Subset of _cachedBehaviors whose type actually overrides Tick(FactionFC). Rebuilt alongside _cachedBehaviors.
        private List<FCPolicyBehavior> _tickingBehaviors;
        private HashSet<FCActionType> _cachedBlockedActions;
        private HashSet<FCActionType> _cachedEnabledActions;
        private HashSet<MilitaryJobDef> _cachedBlockedJobs;
        private HashSet<MilitaryJobDef> _cachedEnabledJobs;

        /* Lazy-resolved parent ref via FindFC. Robust against load/init order. */
        private FactionFC _cachedFactionFC = null;
        public FactionFC Faction => _cachedFactionFC ?? (_cachedFactionFC = FindFC.FactionComp);

        /* Minimum faction level required to unlock each edict category */
        public static readonly Dictionary<FCPolicyCategory, int> EdictCategoryUnlockLevels = new Dictionary<FCPolicyCategory, int>
        {
            { FCPolicyCategory.Social, 2 },
            { FCPolicyCategory.Tax, 3 },
            { FCPolicyCategory.Doctrine, 3 },
            { FCPolicyCategory.Military, 4 }
        };

        public List<FCPolicyBehavior> CachedBehaviors
        {
            get
            {
                if (_cachedBehaviors is null) RebuildBehaviorCache();
                return _cachedBehaviors;
            }
        }

        /* Same accessor name as the prior FactionFC property to ease the call-site migration. */
        public List<FCPolicyBehavior> cachedBehaviors => CachedBehaviors;

        public void ExposeData()
        {
            Scribe_Collections.Look(ref policies, "policies", LookMode.Deep);
            Scribe_Collections.Look(ref factionTraits, "factionTraits", LookMode.Deep);
            Scribe_Collections.Look(ref edicts, "edicts", LookMode.Value, LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (policies == null) policies = new List<FCPolicy>();
                if (factionTraits == null) factionTraits = SeedDefaultTraits();
                if (edicts == null) edicts = new Dictionary<FCPolicyCategory, FCPolicy>();
                pendingEdictActivations = new HashSet<FCPolicyCategory>();
                ScrubDeadPolicies();
            }
        }

        /// <summary>
        /// Removes policy/edict entries whose def failed to resolve (removed content), mirroring
        /// the event manager's null-def scrub. factionTraits is intentionally left alone: it uses
        /// fixed slots seeded with FCPolicyDefOf.empty and is def-null-guarded at every read, so
        /// removing entries would shrink the slot list.
        /// </summary>
        internal void ScrubDeadPolicies()
        {
            policies?.RemoveAll(p => p?.def is null);
            if (edicts != null)
            {
                List<FCPolicyCategory> deadEdicts = null;
                foreach (KeyValuePair<FCPolicyCategory, FCPolicy> kv in edicts)
                {
                    if (kv.Value?.def is null)
                        (deadEdicts ?? (deadEdicts = new List<FCPolicyCategory>())).Add(kv.Key);
                }
                if (deadEdicts != null)
                    foreach (FCPolicyCategory cat in deadEdicts)
                        edicts.Remove(cat);
            }
        }

        internal void SeedFromLegacy(
            List<FCPolicy> legacyPolicies,
            List<FCPolicy> legacyTraits,
            Dictionary<FCPolicyCategory, FCPolicy> legacyEdicts)
        {
            if (legacyPolicies != null) policies = legacyPolicies;
            if (legacyTraits != null) factionTraits = legacyTraits;
            if (legacyEdicts != null) edicts = legacyEdicts;
        }

        private static List<FCPolicy> SeedDefaultTraits() => new List<FCPolicy>
        {
            new FCPolicy(FCPolicyDefOf.empty),
            new FCPolicy(FCPolicyDefOf.empty),
            new FCPolicy(FCPolicyDefOf.empty),
            new FCPolicy(FCPolicyDefOf.empty),
            new FCPolicy(FCPolicyDefOf.empty)
        };

        #region Behavior System

        /// <summary>
        /// Rebuilds the cached behavior list from active policies and traits.
        /// Order: policies first (in list order), then traits (in slot order).
        /// This order determines ModifyStat chaining — currently no two behaviors modify the same stat.
        /// </summary>
        public void RebuildBehaviorCache()
        {
            LogUtil.Message("Rebuilding faction behavior cache");
            _cachedBehaviors = new List<FCPolicyBehavior>();
            foreach (FCPolicy p in policies)
            {
                if (p?.behavior != null)
                    _cachedBehaviors.Add(p.behavior);
            }
            foreach (FCPolicy p in factionTraits)
            {
                if (p?.def is null || p.def == FCPolicyDefOf.empty) continue;
                if (p.behavior != null)
                    _cachedBehaviors.Add(p.behavior);
            }
            foreach (FCPolicy edict in edicts.Values)
            {
                if (edict?.behavior != null)
                    _cachedBehaviors.Add(edict.behavior);
            }

            // Cache the subset that actually overrides Tick, so the per-tick dispatch skips no-op behaviors.
            _tickingBehaviors = new List<FCPolicyBehavior>();
            foreach (FCPolicyBehavior b in _cachedBehaviors)
            {
                if (TickOverrideUtil.Overrides(b.GetType(), "Tick", typeof(FCPolicyBehavior), typeof(FactionFC)))
                    _tickingBehaviors.Add(b);
            }

            RebuildActionCache();

            // Policy/trait changes affect faction-level stat values and behavior ModifyStat results
            Faction?.InvalidateFactionStatCache();
        }

        private void RebuildActionCache()
        {
            _cachedBlockedActions = new HashSet<FCActionType>();
            _cachedEnabledActions = new HashSet<FCActionType>();
            _cachedBlockedJobs = new HashSet<MilitaryJobDef>();
            _cachedEnabledJobs = new HashSet<MilitaryJobDef>();
            foreach (FCPolicy p in policies)
            {
                if (p?.def is null) continue;
                if (p.def.blockedActions != null) foreach (var a in p.def.blockedActions) _cachedBlockedActions.Add(a);
                if (p.def.enabledActions != null) foreach (var a in p.def.enabledActions) _cachedEnabledActions.Add(a);
                if (p.def.blockedMilitaryJobs != null) foreach (var j in p.def.blockedMilitaryJobs) _cachedBlockedJobs.Add(j);
                if (p.def.enabledMilitaryJobs != null) foreach (var j in p.def.enabledMilitaryJobs) _cachedEnabledJobs.Add(j);
            }
            foreach (FCPolicy p in factionTraits)
            {
                if (p?.def is null || p.def == FCPolicyDefOf.empty) continue;
                if (p.def.blockedActions != null) foreach (var a in p.def.blockedActions) _cachedBlockedActions.Add(a);
                if (p.def.enabledActions != null) foreach (var a in p.def.enabledActions) _cachedEnabledActions.Add(a);
                if (p.def.blockedMilitaryJobs != null) foreach (var j in p.def.blockedMilitaryJobs) _cachedBlockedJobs.Add(j);
                if (p.def.enabledMilitaryJobs != null) foreach (var j in p.def.enabledMilitaryJobs) _cachedEnabledJobs.Add(j);
            }
            foreach (FCPolicy edict in edicts.Values)
            {
                if (edict?.def is null || !edict.IsFullyActive) continue;
                if (edict.def.blockedActions != null) foreach (var a in edict.def.blockedActions) _cachedBlockedActions.Add(a);
                if (edict.def.enabledActions != null) foreach (var a in edict.def.enabledActions) _cachedEnabledActions.Add(a);
                if (edict.def.blockedMilitaryJobs != null) foreach (var j in edict.def.blockedMilitaryJobs) _cachedBlockedJobs.Add(j);
                if (edict.def.enabledMilitaryJobs != null) foreach (var j in edict.def.enabledMilitaryJobs) _cachedEnabledJobs.Add(j);
            }
            _cachedEnabledActions.ExceptWith(_cachedBlockedActions);
            _cachedEnabledJobs.ExceptWith(_cachedBlockedJobs);
        }

        /// <summary>
        /// Per-tick behavior dispatch. Iterates only behaviors that override Tick (no closure allocation,
        /// no no-op virtual calls). Called every game tick from <see cref="FactionFC.TickActions"/>.
        /// </summary>
        public void TickBehaviors(FactionFC faction)
        {
            if (_tickingBehaviors is null) RebuildBehaviorCache();
            foreach (FCPolicyBehavior behavior in _tickingBehaviors)
            {
                try
                {
                    behavior.Tick(faction);
                }
                catch (Exception e)
                {
                    LogUtil.Error($"Policy behavior tick error: {e}");
                }
            }
        }

        public void ForEachBehavior(Action<FCPolicyBehavior> action)
        {
            foreach (FCPolicyBehavior b in CachedBehaviors)
            {
                try
                {
                    action(b);
                }
                catch (Exception e)
                {
                    LogUtil.Error($"Policy behavior error: {e}");
                }
            }
        }

        public T FoldBehaviors<T>(T seed, Func<FCPolicyBehavior, T, T> folder)
        {
            foreach (FCPolicyBehavior b in CachedBehaviors)
            {
                try
                {
                    seed = folder(b, seed);
                }
                catch (Exception e)
                {
                    LogUtil.Error($"Policy behavior fold error: {e}");
                }
            }
            return seed;
        }

        /// <summary>
        /// Calls OnRemoved on all behaviors in the given policy list, then clears it.
        /// Use this instead of directly clearing/replacing policy lists.
        /// </summary>
        public void RemoveAllPolicies(List<FCPolicy> policyList)
        {
            FactionFC faction = Faction;
            foreach (FCPolicy p in policyList)
            {
                if (p?.behavior != null)
                {
                    try { p.behavior.OnRemoved(faction); }
                    catch (Exception e) { LogUtil.Error($"FCPolicyBehavior.OnRemoved error for '{p.def?.defName}': {e}"); }
                }
            }
            policyList.Clear();
        }

        #endregion

        #region Policy & Action Checks

        public bool HasPolicy(FCPolicyDef def)
        {
            //Don't game the system
            if (policies.Count < 2)
            {
                return false;
            }

            foreach (FCPolicy policy in policies)
            {
                if (policy.def == def)
                    return true;
            }

            return false;
        }

        public bool HasTrait(FCPolicyDef def)
        {
            foreach (FCPolicy trait in factionTraits)
            {
                if (trait.def == def)
                    return true;
            }

            return false;
        }

        #region Edicts

        public bool IsEdictCategoryUnlocked(FCPolicyCategory category)
        {
            if (!EdictCategoryUnlockLevels.TryGetValue(category, out int required))
                return false;
            return (Faction?.factionLevel ?? 0) >= required;
        }

        public FCPolicy GetActiveEdict(FCPolicyCategory category)
        {
            edicts.TryGetValue(category, out FCPolicy edict);
            return edict;
        }

        public bool HasEdict(FCPolicyDef def)
        {
            if (!edicts.TryGetValue(def.category, out FCPolicy edict)) return false;
            return edict.def == def;
        }

        public void EnactEdict(FCPolicyDef def)
        {
            if (!def.IsEdict)
            {
                LogUtil.Error($"EnactEdict called on non-edict def '{def.defName}'");
                return;
            }
            FactionFC faction = Faction;
            if (!IsEdictCategoryUnlocked(def.category))
            {
                Messages.Message("FCEdictCategoryLocked".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }
            if (def.factionLevelRequirement > 0 && (faction?.factionLevel ?? 0) < def.factionLevelRequirement)
            {
                Messages.Message("FCEdictLevelRequired".Translate(def.factionLevelRequirement), MessageTypeDefOf.RejectInput);
                return;
            }

            // Check incompatibility with active core policies and traits
            if (!def.incompatiblePolicies.NullOrEmpty())
            {
                foreach (FCPolicyDef blocked in def.incompatiblePolicies)
                {
                    if (HasPolicy(blocked) || HasTrait(blocked))
                    {
                        Messages.Message("FCEdictIncompatible".Translate(def.LabelCap, blocked.LabelCap), MessageTypeDefOf.RejectInput);
                        return;
                    }
                }
            }

            // Check policy prerequisites
            if (!def.MeetsPolicyRequirements(faction, out string failReason))
            {
                Messages.Message(failReason, MessageTypeDefOf.RejectInput);
                return;
            }

            // Revoke existing edict in this category (if any)
            RevokeEdict(def.category, silent: true);

            FCPolicy edict = new FCPolicy(def);
            edicts[def.category] = edict;
            if (def.enactDuration > 0)
                pendingEdictActivations.Add(def.category);
            RebuildBehaviorCache();
            faction?.DirtyFactionProfitCache();
            Messages.Message("FCEdictEnacted".Translate(def.LabelCap), MessageTypeDefOf.PositiveEvent);
        }

        public void RevokeEdict(FCPolicyCategory category, bool silent = false)
        {
            if (!edicts.TryGetValue(category, out FCPolicy edict)) return;

            FactionFC faction = Faction;
            if (edict.behavior != null)
            {
                try { edict.behavior.OnRemoved(faction); }
                catch (Exception e) { LogUtil.Error($"Edict behavior OnRemoved error for '{edict.def?.defName}': {e}"); }
            }

            string label = edict.def?.LabelCap ?? "";
            FCPolicyDef revokedDef = edict.def;
            edicts.Remove(category);
            pendingEdictActivations.Remove(category);
            RebuildBehaviorCache();
            faction?.DirtyFactionProfitCache();
            if (!silent)
                Messages.Message("FCEdictRevoked".Translate(label), MessageTypeDefOf.NeutralEvent);

            // Cascade: revoke any active edicts that depended on the one just removed
            if (revokedDef != null)
            {
                List<FCPolicyCategory> toRevoke = new List<FCPolicyCategory>();
                foreach (KeyValuePair<FCPolicyCategory, FCPolicy> kvp in edicts)
                {
                    if (kvp.Value.def.requiredPolicies.NullOrEmpty()) continue;
                    if (!kvp.Value.def.MeetsPolicyRequirements(faction, out _))
                        toRevoke.Add(kvp.Key);
                }
                foreach (FCPolicyCategory cat in toRevoke)
                {
                    if (!edicts.TryGetValue(cat, out FCPolicy policy)) continue;
                    string depLabel = policy.def?.LabelCap ?? "";
                    Messages.Message("FCEdictRevokedDependency".Translate(depLabel, label), MessageTypeDefOf.NeutralEvent);
                    RevokeEdict(cat, silent: true);
                }
            }
        }

        public void RevokeAllEdicts()
        {
            // Copy keys to avoid modifying collection during iteration
            List<FCPolicyCategory> categories = new List<FCPolicyCategory>(edicts.Keys);
            foreach (FCPolicyCategory cat in categories)
            {
                RevokeEdict(cat, silent: true);
            }
        }

        public int GetEdictUpkeep()
        {
            int total = 0;
            foreach (FCPolicy edict in edicts.Values)
            {
                if (edict.IsFullyActive)
                    total += edict.def.upkeepSilver;
            }
            return total;
        }

        public void CheckEdictActivations()
        {
            bool anyActivated = false;
            List<FCPolicyCategory> toRemove = new List<FCPolicyCategory>();
            foreach (FCPolicyCategory cat in pendingEdictActivations)
            {
                if (!edicts.TryGetValue(cat, out FCPolicy edict) || edict.IsFullyActive)
                {
                    toRemove.Add(cat);
                    if (edicts.ContainsKey(cat))
                        anyActivated = true;
                }
            }
            foreach (FCPolicyCategory cat in toRemove)
                pendingEdictActivations.Remove(cat);

            if (anyActivated)
            {
                // A newly-active edict's action/job gates are only folded into the caches
                // for IsFullyActive edicts, so rebuild now that enactment finished.
                RebuildActionCache();
                FactionFC faction = Faction;
                faction?.InvalidateFactionStatCache();
                faction?.DirtyFactionProfitCache();
            }
        }

        #endregion

        public bool AnyPolicyBlocks(FCActionType action) => _cachedBlockedActions?.Contains(action) ?? false;
        public bool AnyPolicyEnables(FCActionType action) => _cachedEnabledActions?.Contains(action) ?? false;

        /// <summary>
        /// Unified check: for opt-out actions, returns true unless blocked. For opt-in actions, returns true only if enabled.
        /// </summary>
        public bool IsActionAllowed(FCActionType action)
        {
            if (FCActionTypeUtil.RequiresEnable(action))
                return AnyPolicyEnables(action);
            return !AnyPolicyBlocks(action);
        }

        /// <summary>
        /// Checks if a specific military job is allowed. Respects defaultEnabled on the job def,
        /// plus policy/trait overrides. Does NOT check the DeployMilitary action gate — caller must check that separately.
        /// </summary>
        public bool IsMilitaryJobAllowed(MilitaryJobDef job)
        {
            if (_cachedBlockedJobs != null && _cachedBlockedJobs.Contains(job)) return false;
            if (!job.defaultEnabled)
                return _cachedEnabledJobs != null && _cachedEnabledJobs.Contains(job);
            return true;
        }

        /// <summary>
        /// Returns true if any active policy prevents building destruction on battle loss.
        /// </summary>
        public bool AnyPolicyPreventsBuildingDestruction()
        {
            foreach (FCPolicy p in policies)
            {
                if (p?.def != null && p.def.preventBuildingDestruction)
                    return true;
            }
            foreach (FCPolicy p in factionTraits)
            {
                if (p?.def is null || p.def == FCPolicyDefOf.empty) continue;
                if (p.def.preventBuildingDestruction)
                    return true;
            }
            foreach (FCPolicy edict in edicts.Values)
            {
                if (edict?.def != null && edict.IsFullyActive && edict.def.preventBuildingDestruction)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Returns true if any active policy suppresses member death penalties.
        /// </summary>
        public bool AnyPolicySuppressesMemberDeathPenalty()
        {
            foreach (FCPolicy p in policies)
            {
                if (p?.def != null && p.def.suppressMemberDeathPenalty)
                    return true;
            }
            foreach (FCPolicy p in factionTraits)
            {
                if (p?.def is null || p.def == FCPolicyDefOf.empty) continue;
                if (p.def.suppressMemberDeathPenalty)
                    return true;
            }
            foreach (FCPolicy edict in edicts.Values)
            {
                if (edict?.def != null && edict.IsFullyActive && edict.def.suppressMemberDeathPenalty)
                    return true;
            }
            return false;
        }

        #endregion
    }
}
