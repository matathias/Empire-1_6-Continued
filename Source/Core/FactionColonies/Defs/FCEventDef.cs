using FactionColonies.util;
using RimWorld;
using System.Collections.Generic;
using Verse;

namespace FactionColonies
{
    public class FCEventDef : Def
    {
        public int timeTillTrigger = -1;
        public int timeTillTriggerMax = -1;
        public string desc;
        /// <summary>Description with {FACTION}/{FACTION_TITLE} tokens and [b]/[i] emphasis markup resolved for display.</summary>
        public string FormattedDesc => desc.Format();
        public FCEventCategoryDef category;

        //Random Event Information
        public bool isRandomEvent = false;
        public bool activateAtStart;
        // Force a condition-triggered event (neither isRandomEvent nor activateAtStart) into the
        // settings enable/disable list so the player can toggle it.
        public bool includeInSettingsList = false;
        public int requiredWealth = 0;
        public IntRange rangeSettlementsAffected = new IntRange(0, 0);
        public bool targetAllSettlements = false;
        public bool settlementsCarryOver = true;
        public bool useProximity = true;
        public float proximityFalloff = 20f;
        public int weight = 0;
        public int minimumHappiness = 0;
        public int maximumHappiness = 100;
        public int minimumLoyalty = 0;
        public int maximumLoyalty = 100;
        public int minimumUnrest = 0;
        public int maximumUnrest = 100;
        public int minimumProsperity = 0;
        public int maximumProsperity = 100;
        public ResourceTypeDef requiredResource;
        public List<ResearchProjectDef> requiredResearch = new List<ResearchProjectDef>();
        public TechLevel minTechLevel = TechLevel.Undefined;
        public TechLevel maxTechLevel = TechLevel.Undefined;
        public List<WorldSettlementDef> allowedSettlementTypes = new List<WorldSettlementDef>();
        public List<WorldSettlementDef> blockedSettlementTypes = new List<WorldSettlementDef>();
        public List<FCEventDef> incompatibleEvents = new List<FCEventDef>();
        public int cooldownTicks = 0;
        public int maxFireCount = -1;
        public int minSettlements = 0;
        public FCPolicyDef requiredPolicy;
        public int minDaysSinceFounded = 0;

        //Options
        public List<FCOptionDef> options = new List<FCOptionDef>();
        public string optionDescription = "";

        //Event chain
        public bool eventFollows = false;
        public FCEventDef followingEvent = null;
        public FCEventDef followingEvent2 = null;
        public bool splitEventFollows = false;
        public int splitEventChance = 50;

        // A follow-up exists if EITHER flag is set: eventFollows = deterministic follow-up,
        // splitEventFollows = probabilistic/branching follow-up. Requiring both was a footgun.
        public bool HasFollowUp => eventFollows || splitEventFollows;

        //Rewards
        public List<ThingDef> loot = new List<ThingDef>();
        public int randomThingValue = 0;
        public ResourceEventRewardDef randomThingRewardDef;
        public int prosperityLost = 0;
        public List<string> applicableBiomes = new List<string>();
        public List<string> restrictedBiomes = new List<string>();

        //Stat modifiers during event (removed when event expires)
        public List<FCStatModifier> statModifiers = new List<FCStatModifier>();

        //Permanent stat modifiers (persist on settlement after event expires)
        public List<FCStatModifier> permanentStatModifiers = new List<FCStatModifier>();

        public bool isMilitaryEvent = false;
        public bool isNegative = false;

        public bool HasVariableDuration => timeTillTriggerMax > timeTillTrigger && timeTillTrigger >= 0;

        public bool BiomeAllowed(string biome)
        {
            if (applicableBiomes.Count > 0)
                return applicableBiomes.Contains(biome);
            if (restrictedBiomes.Count > 0)
                return !restrictedBiomes.Contains(biome);
            return true;
        }

        /// <summary>
        /// Checks whether this event can target the given settlement type.
        /// Uses depth-based resolution matching <see cref="BuildingFCDef.CanBeBuiltForSettlementType"/>.
        /// Both allow and block lists can coexist; most specific (shallowest depth) wins, tie goes to block.
        /// </summary>
        public bool SettlementTypeAllowed(WorldSettlementDef settlementDef)
        {
            int allowDepth = (allowedSettlementTypes.Count > 0)
                ? settlementDef.DepthInList(allowedSettlementTypes)
                : -1;
            int blockDepth = (blockedSettlementTypes.Count > 0)
                ? settlementDef.DepthInList(blockedSettlementTypes)
                : -1;

            if (allowDepth >= 0 || blockDepth >= 0)
            {
                if (allowDepth >= 0 && blockDepth >= 0)
                    return allowDepth < blockDepth;
                return allowDepth >= 0;
            }

            if (allowedSettlementTypes.Count > 0)
                return false;
            return true;
        }

        /// <summary>
        /// Checks whether this event's tech level and research prerequisites are met.
        /// Tech level is checked against <see cref="FactionFC.techLevel"/>, which is
        /// already clamped by <see cref="FCSettings.medievalTechOnly"/>.
        /// </summary>
        public bool SatisfiesTechRequirements(TechLevel factionTechLevel)
        {
            if (minTechLevel != TechLevel.Undefined && factionTechLevel < minTechLevel)
                return false;
            if (maxTechLevel != TechLevel.Undefined && factionTechLevel > maxTechLevel)
                return false;
            foreach (ResearchProjectDef project in requiredResearch)
            {
                if (!project.IsFinished)
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
            foreach (string err in FCStatModifier.ConfigErrors(permanentStatModifiers, defName + ".permanentStatModifiers"))
                yield return err;
            if (targetAllSettlements && rangeSettlementsAffected.max != 0)
                yield return $"{defName}: targetAllSettlements is true but rangeSettlementsAffected.max is {rangeSettlementsAffected.max}";
            foreach (string biome in applicableBiomes)
            {
                if (DefDatabase<BiomeDef>.GetNamed(biome, false) == null)
                    yield return $"{defName}: applicableBiomes contains unknown biome '{biome}'";
            }

            foreach (string biome in restrictedBiomes)
            {
                if (DefDatabase<BiomeDef>.GetNamed(biome, false) == null)
                    yield return $"{defName}: restrictedBiomes contains unknown biome '{biome}'";
            }

            if (minTechLevel != TechLevel.Undefined && maxTechLevel != TechLevel.Undefined
                && minTechLevel > maxTechLevel)
                yield return $"{defName}: minTechLevel ({minTechLevel}) > maxTechLevel ({maxTechLevel})";
            for (int i = 0; i < requiredResearch.Count; i++)
            {
                if (requiredResearch[i] == null)
                    yield return $"{defName}: requiredResearch contains a null entry at index {i} (bad defName?)";
            }
            if (cooldownTicks < 0)
                yield return $"{defName}: cooldownTicks ({cooldownTicks}) must not be negative";
            if (maxFireCount != -1 && maxFireCount <= 0)
                yield return $"{defName}: maxFireCount ({maxFireCount}) must be -1 (unlimited) or a positive integer";
            if (minSettlements < 0)
                yield return $"{defName}: minSettlements ({minSettlements}) must not be negative";
            if (minDaysSinceFounded < 0)
                yield return $"{defName}: minDaysSinceFounded ({minDaysSinceFounded}) must not be negative";
            if (timeTillTriggerMax != -1 && timeTillTriggerMax < timeTillTrigger)
                yield return $"{defName}: timeTillTriggerMax ({timeTillTriggerMax}) < timeTillTrigger ({timeTillTrigger})";

            /* An event that presents options must always offer at least one the player can pick:
             * free (no silver / dynamic cost) and ungated (no policy or meme requirement). The
             * FCOptionWindow has no close button, so without a guaranteed option a player who can't
             * afford or doesn't qualify for any choice would be soft-locked. This also makes the
             * Ideology-off meme hiding safe — the guaranteed option has no meme gate, so it always
             * survives the filter. */
            if (options.Count > 0)
            {
                bool hasFreeUngated = false;
                foreach (FCOptionDef opt in options)
                {
                    if (opt is null) continue;
                    if (opt.silverCost != 0) continue;
                    if (opt.requiredPolicies != null && opt.requiredPolicies.Count > 0) continue;
                    if (opt.requiredMemes != null && opt.requiredMemes.Count > 0) continue;

                    // A positive dynamic cost makes the option not truly free.
                    FCDynamicCostExtension dyn = opt.GetModExtension<FCDynamicCostExtension>();
                    if (dyn != null && (dyn.costPerEmpireIncomeUnit > 0f
                        || (dyn.costPerResourceDelta != null && dyn.costPerResourceDelta.Count > 0)))
                        continue;

                    hasFreeUngated = true;
                    break;
                }
                if (!hasFreeUngated)
                    yield return $"{defName}: has options but no free, non-gated option — players could be unable to dismiss the event window";
            }
        }
    }
}