using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using FactionColonies.util;

namespace FactionColonies
{
    /// <summary>
    /// A mutually-exclusive stance the player can adopt on an active situation. Exactly one approach
    /// is active at a time; switching is free and immediate. The active approach contributes a signed
    /// addend to the bar's daily delta, charges an ongoing silver upkeep per tax cycle, and applies
    /// its <see cref="statModifiers"/> while active.
    ///
    /// Referenced by defName from <see cref="FCSituationDef.approaches"/>.
    /// </summary>
    public class FCSituationApproachDef : Def
    {
        /* Signed addend applied to the bar's daily delta while this approach is active. The sign is
         * authored here and added unconditionally — it is NOT reoriented to the advance/recede
         * direction. A positive approach pushes the bar up even while the base rate recedes it. */
        public float ratePerDay = 0f;

        /* Silver charged per day while this approach is active. */
        public int upkeepSilver = 0;

        /* Gating — reuses the FCOptionDef policy-gate shape. */
        public List<FCPolicyDef> requiredPolicies = new List<FCPolicyDef>();
        public FCRequirementMode requirementMode = FCRequirementMode.All;

        /* Research gate — declared directly here (requiredResearch lives on FCEventDef, not FCOptionDef). */
        public List<ResearchProjectDef> requiredResearch = new List<ResearchProjectDef>();

        /* Applied while this approach is active. */
        public List<FCStatModifier> statModifiers = new List<FCStatModifier>();

        /// <summary>
        /// Whether this approach's static gates (policies + research) are currently satisfied.
        /// Runtime/handler overrides are layered on top of this by the UI.
        /// </summary>
        public bool MeetsStaticRequirements(out string reason)
        {
            reason = null;

            if (requiredResearch != null)
            {
                foreach (ResearchProjectDef project in requiredResearch)
                {
                    if (project != null && !project.IsFinished)
                    {
                        reason = "FCSituationApproachNeedsResearch".Translate(project.label);
                        return false;
                    }
                }
            }

            if (requiredPolicies == null || requiredPolicies.Count == 0)
                return true;

            if (requirementMode == FCRequirementMode.Any)
            {
                foreach (FCPolicyDef required in requiredPolicies)
                    if (HasPolicyOrTrait(required)) return true;

                reason = "FCSituationApproachNeedsPolicyAny".Translate(
                    string.Join(", ", requiredPolicies.Select(p => p.label)));
                return false;
            }

            List<string> missing = new List<string>();
            foreach (FCPolicyDef required in requiredPolicies)
                if (!HasPolicyOrTrait(required)) missing.Add(required.label);

            if (missing.Count > 0)
            {
                reason = "FCSituationApproachNeedsPolicy".Translate(string.Join(", ", missing));
                return false;
            }
            return true;
        }

        private static bool HasPolicyOrTrait(FCPolicyDef def)
        {
            if (def == null) return false;
            foreach (FCPolicy p in FindFC.PolicyManager.policies)
                if (p.def == def) return true;
            foreach (FCPolicy t in FindFC.PolicyManager.factionTraits)
                if (t.def == def) return true;
            return false;
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string err in base.ConfigErrors())
                yield return err;

            foreach (string err in FCStatModifier.ConfigErrors(statModifiers, defName))
                yield return err;
        }
    }
}
