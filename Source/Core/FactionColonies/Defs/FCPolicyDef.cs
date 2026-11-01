using FactionColonies.util;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    public class FCPolicyDef : Def
    {
        public string desc;
        /// <summary>Description with {FACTION}/{FACTION_TITLE} tokens and [b]/[i] emphasis markup resolved for display.</summary>
        public string FormattedDesc => desc.Format();
        public FCPolicyCategory category;
        public TechLevel techLevelRequirement;
        public int factionLevelRequirement;
        public List<string> positiveEffects;
        public List<string> negativeEffects;

        // Additional fields for XML compatibility
        public int cost;
        public string type;
        public TechLevel techLevel = TechLevel.Undefined;
        public int enactDuration;
        public int upkeepSilver;

        public bool IsEdict => category == FCPolicyCategory.Tax
                               || category == FCPolicyCategory.Military
                               || category == FCPolicyCategory.Social
                               || category == FCPolicyCategory.Doctrine;
        // Icon paths — set in XML, resolved lazily to textures
        public string iconPathLight;
        public string iconPathDark;

        public List<FCStatModifier> statModifiers = new List<FCStatModifier>();
        public List<FCActionType> blockedActions = new List<FCActionType>();
        public List<FCActionType> enabledActions = new List<FCActionType>();
        public List<MilitaryJobDef> blockedMilitaryJobs = new List<MilitaryJobDef>();
        public List<MilitaryJobDef> enabledMilitaryJobs = new List<MilitaryJobDef>();
        public bool preventBuildingDestruction;
        public bool suppressMemberDeathPenalty;

        // Policies/traits that are incompatible with this one (mutual exclusion in selection UI)
        public List<FCPolicyDef> incompatiblePolicies = new List<FCPolicyDef>();

        // Policies/traits/edicts that must be active before this one can be enacted
        public List<FCPolicyDef> requiredPolicies = new List<FCPolicyDef>();
        public FCRequirementMode requirementMode = FCRequirementMode.All;

        [Unsaved] private Texture2D resolvedIconLight;
        [Unsaved] private Texture2D resolvedIconDark;
        [Unsaved] private bool triedResolveLight;
        [Unsaved] private bool triedResolveDark;
        [Unsaved] private FCPolicyBehaviorExtension _cachedBehaviorExt;
        [Unsaved] private bool _triedResolveBehaviorExt;

        /// <summary>
        /// Returns the behavior extension on this def, or null if none.
        /// </summary>
        public FCPolicyBehaviorExtension BehaviorExtension
        {
            get
            {
                if (!_triedResolveBehaviorExt)
                {
                    _triedResolveBehaviorExt = true;
                    _cachedBehaviorExt = this.GetModExtension<FCPolicyBehaviorExtension>();
                }
                return _cachedBehaviorExt;
            }
        }

        public bool HasBehavior => BehaviorExtension != null;

        public Texture2D IconLight
        {
            get
            {
                if (!triedResolveLight)
                {
                    triedResolveLight = true;
                    if (!iconPathLight.NullOrEmpty())
                    {
                        resolvedIconLight = ContentFinder<Texture2D>.Get(iconPathLight, false);
                        if (resolvedIconLight == null)
                            LogUtil.Warning("Could not resolve light icon at '" + iconPathLight + "' for " + defName);
                    }
                }
                return resolvedIconLight;
            }
        }

        public Texture2D IconDark
        {
            get
            {
                if (!triedResolveDark)
                {
                    triedResolveDark = true;
                    if (!iconPathDark.NullOrEmpty())
                    {
                        resolvedIconDark = ContentFinder<Texture2D>.Get(iconPathDark, false);
                        if (resolvedIconDark == null)
                            LogUtil.Warning("Could not resolve dark icon at '" + iconPathDark + "' for " + defName);
                    }
                }
                return resolvedIconDark;
            }
        }

        public bool HasNegativeEffects()
        {
            return negativeEffects != null && negativeEffects.Count > 0;
        }
        public bool HasPositiveEffects()
        {
            return positiveEffects != null && positiveEffects.Count > 0;
        }
        public string PolicyText()
        {
            // LabelCap is a TaggedString; calling .Resolve() keeps the whole expression a plain string so the
            // implicit TaggedString->string conversion (which runs StripTags) never removes the colors built below.
            return LabelCap.Resolve() + "\n\n" + CachedPolicyDesc();
        }
        public string PolicyDesc()
        {
            string str = "";

            if (HasPositiveEffects())
            {
                foreach (string positive in positiveEffects)
                {
                    str += positive.Format().Colorize(Color.green) + "\n";
                }
            }

            if (HasPositiveEffects() && HasNegativeEffects())
            {
                str += "==========\n";
            }

            if (HasNegativeEffects())
            {
                foreach (string negative in negativeEffects)
                {
                    str += negative.Format().Colorize(Color.red) + "\n";
                }
            }

            string statDesc = FCStatModifier.GetDescription(statModifiers).Resolve();
            if (!statDesc.NullOrEmpty())
            {
                if (str.Length > 0) str += "\n";
                str += statDesc;
            }

            if (upkeepSilver > 0)
            {
                if (str.Length > 0) str += "\n\n";
                str += "FCEdictUpkeep".Translate(upkeepSilver);
            }

            if (!requiredPolicies.NullOrEmpty())
            {
                if (str.Length > 0) str += "\n\n";
                string names = requirementMode == FCRequirementMode.Any
                    ? string.Join(" / ", requiredPolicies.Select(p => p.LabelCap))
                    : string.Join(", ", requiredPolicies.Select(p => p.LabelCap));
                str += "FCPolicyRequires".Translate(names);
            }

            return str.Trim();
        }
        public string CachedPolicyDesc()
        {
            return FactionCache.FCPolicyDescs?[this] ?? PolicyDesc();
        }

        /// <summary>
        /// Checks whether the faction has all required policies/traits/edicts active.
        /// </summary>
        public bool MeetsPolicyRequirements(FactionFC faction, out string failReason)
        {
            failReason = null;
            if (requiredPolicies.NullOrEmpty()) return true;

            if (requirementMode == FCRequirementMode.Any)
            {
                foreach (FCPolicyDef required in requiredPolicies)
                {
                    if (FindFC.PolicyManager.HasPolicy(required) || FindFC.PolicyManager.HasTrait(required) || FindFC.PolicyManager.HasEdict(required))
                        return true;
                }
                string allNames = string.Join(", ", requiredPolicies.Select(p => p.label));
                failReason = "FCEdictRequiresPolicyAny".Translate(LabelCap, allNames);
                return false;
            }

            List<string> missing = new List<string>();
            foreach (FCPolicyDef required in requiredPolicies)
            {
                if (!FindFC.PolicyManager.HasPolicy(required) && !FindFC.PolicyManager.HasTrait(required) && !FindFC.PolicyManager.HasEdict(required))
                    missing.Add(required.label);
            }
            if (missing.Count > 0)
            {
                failReason = "FCEdictRequiresPolicy".Translate(LabelCap, string.Join(", ", missing));
                return false;
            }
            return true;
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string err in base.ConfigErrors())
                yield return err;
            foreach (string err in FCStatModifier.ConfigErrors(statModifiers, defName))
                yield return err;
            if (!requiredPolicies.NullOrEmpty())
            {
                for (int i = 0; i < requiredPolicies.Count; i++)
                {
                    if (requiredPolicies[i] == null)
                        yield return defName + ": requiredPolicies[" + i + "] is null (unresolved defName?)";
                    else if (requiredPolicies[i] == this)
                        yield return defName + ": requiredPolicies contains self-reference";
                }
            }
        }
    }
}