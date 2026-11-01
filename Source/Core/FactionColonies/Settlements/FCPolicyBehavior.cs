using RimWorld;
using System;
using System.Collections.Generic;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Per-policy runtime behavior instance. Only needed for policies that require
    /// procedural logic (cooldowns, periodic spawns, conditional stat mods, custom UI).
    /// Pure-XML policies (e.g., Isolationist, Industrious) need no behavior class.
    ///
    /// Created by FCPolicyBehaviorExtension.CreateBehavior() when the def has a
    /// behavior extension in its modExtensions list.
    /// Owns its own state directly.
    /// Serialized via Scribe_Deep so all state survives save/load.
    /// XML-configurable parameters live on the FCPolicyBehaviorExtension subclass;
    /// access them via Ext&lt;T&gt;().
    /// </summary>
    public abstract class FCPolicyBehavior : IExposable
    {
        /// <summary>Back-reference to the owning FCPolicy. Set after construction and after load.</summary>
        [Unsaved] public FCPolicy policy;

        /// <summary>Reference to the extension that configured this behavior. Set after construction and after load.</summary>
        [Unsaved] public FCPolicyBehaviorExtension extension;

        /// <summary>Typed access to the extension for behaviors that have a parameterized extension subclass.</summary>
        protected T Ext<T>() where T : FCPolicyBehaviorExtension
        {
            return (T)extension;
        }

        /// <summary>
        /// Called after extension and policy are wired, both on fresh creation and after load.
        /// Override to set [Unsaved] fields from extension parameters (e.g., CooldownAbility keys).
        /// </summary>
        public virtual void PostInitialize() { }

        /* Lifecycle */
        /// <summary>Called when this policy is enacted on the faction.</summary>
        public virtual void OnEnacted(FactionFC faction) { }

        /// <summary>Called when this policy is removed from the faction.</summary>
        public virtual void OnRemoved(FactionFC faction) { }

        /// <summary>Called every game tick while this policy is active.</summary>
        public virtual void Tick(FactionFC faction) { }

        /* Settlement Events */
        /// <summary>Called when a new settlement is created while this policy is active.</summary>
        public virtual void OnSettlementCreated(FactionFC faction, WorldSettlementFC settlement) { }

        /// <summary>Called when a settlement is about to be removed while this policy is active.</summary>
        public virtual void OnSettlementRemoved(FactionFC faction, WorldSettlementFC settlement) { }

        /// <summary>Called after the player pays for a new settlement.</summary>
        public virtual void OnSettlementCostPaid(FactionFC faction) { }

        /* Conditional Stat Modifier */
        /// <summary>
        /// Runtime-dependent stat modifier. Only override this for values that
        /// genuinely depend on game state at query time (e.g., Egalitarian's active
        /// tax-break penalty). For static modifiers, use FCPolicyDef.statModifiers XML instead.
        ///<para>Aggregation contract:</para>
        /// <list type="bullet">
        ///   <item>For Additive stats (IdentityValue=0): add/subtract from currentValue</item>
        ///   <item>For Multiplicative stats (IdentityValue=1): multiply currentValue</item>
        /// </list>
        /// <para>Check stat.aggregation if uncertain.</para>
        /// </summary>
        public virtual double ModifyStat(FCStatDef stat, double currentValue, WorldSettlementFC settlement)
            => currentValue;

        /// <summary>
        /// Returns a description of this behavior's runtime contribution to the given stat for tooltips.
        /// Return null or empty for stats this behavior doesn't modify.
        /// </summary>
        public virtual string GetStatDescription(FCStatDef stat, WorldSettlementFC settlement) => null;

        /* Building */
        /// <summary>
        /// Called when calculating a building's upkeep. Behaviors can modify the upkeep
        /// based on the building's properties (e.g., discount military buildings).
        /// </summary>
        public virtual double ModifyBuildingUpkeep(BuildingFCDef building, double currentUpkeep, WorldSettlementFC settlement)
            => currentUpkeep;

        /* Military Events */
        /// <summary>Called after a squad is deployed from a settlement. Fires once per home settlement
        /// involved in the op — for foreign-defender ops you'll see two calls with different
        /// <paramref name="settlement"/> values. Compare <paramref name="settlement"/> to
        /// <c>op.aggressor.homeSettlement</c> / <c>op.defender.homeSettlement</c> if the side matters.</summary>
        public virtual void OnSquadDeployed(FactionFC faction, MilitaryOperation op, WorldSettlementFC settlement, bool isExtraSquad) { }

        /// <summary>Called when a squad is recalled/returned to a settlement. Symmetric with
        /// <see cref="OnSquadDeployed"/> — fires once per home settlement involved in the op.</summary>
        public virtual void OnSquadRecalled(FactionFC faction, MilitaryOperation op, WorldSettlementFC settlement) { }

        /// <summary>Called after a fresh squad is hired from a template. Silver has been paid;
        /// <c>squad.settlement</c> is null (squad sits in the unassigned pool until the player
        /// assigns it).</summary>
        public virtual void OnSquadHired(FactionFC faction, MercenarySquadFC squad) { }

        /// <summary>Called after a squad is dismissed by the player. The squad has been removed
        /// from the faction-wide pool.</summary>
        public virtual void OnSquadDismissed(FactionFC faction, MercenarySquadFC squad) { }

        /// <summary>Called after a hired squad's loadout has been brought up to its source
        /// template via <see cref="SquadUpgradeUtil.UpgradeToTemplate"/>. Silver has been paid.</summary>
        public virtual void OnSquadUpgraded(FactionFC faction, MercenarySquadFC squad) { }

        /// <summary>Called after a battle has been resolved, before the squad enters cooldown.</summary>
        public virtual void OnBattleResolved(FactionFC faction, WorldSettlementFC settlement, MilitaryJobDef job, bool victory, BattleResult result) { }

        /* Building Events */
        /// <summary>Called after a building has been fully constructed in a settlement.</summary>
        public virtual void OnBuildingConstructed(FactionFC faction, WorldSettlementFC settlement, BuildingFCDef building, int slot) { }

        /// <summary>Called before a building is deconstructed from a settlement.</summary>
        public virtual void OnBuildingDeconstructed(FactionFC faction, WorldSettlementFC settlement, BuildingFCDef building, int slot) { }

        /* Settlement Upgrade */
        /// <summary>Called after a settlement has been upgraded (or deleveled).</summary>
        public virtual void OnSettlementUpgraded(FactionFC faction, WorldSettlementFC settlement, int newLevel) { }

        /* Settlement Type Change */
        /// <summary>Called after a settlement has transitioned to a new WorldSettlementDef.</summary>
        public virtual void OnSettlementTypeChanged(FactionFC faction, WorldSettlementFC settlement, WorldSettlementDef oldDef, WorldSettlementDef newDef) { }

        /* Research */
        /// <summary>Called after a research project has been completed.</summary>
        public virtual void OnResearchCompleted(FactionFC faction, ResearchProjectDef project) { }

        /* Tax Events */
        /// <summary>Called when taxes are collected from a settlement.</summary>
        public virtual void OnTaxCollected(FactionFC faction, WorldSettlementFC settlement) { }

        /* Random Events */
        /// <summary>
        /// Called when a random event is selected. Return true to re-roll the event selection.
        /// Only one re-roll occurs per event trigger regardless of how many behaviors request it.
        /// </summary>
        public virtual bool ShouldRerollEvent(FCEventDef eventDef) => false;

        /* Diplomacy */
        /// <summary>Handle sending a diplomatic envoy to a target faction. Return true if handled.</summary>
        public virtual bool HandleDiplomaticEnvoy(FactionFC faction, Faction targetFaction) => false;

        /* UI */
        /// <summary>Return labeled action buttons to render in the main tab button bar. Null means no buttons.</summary>
        public virtual IEnumerable<(TaggedString label, Action onClick)> GetMainTabActionButtons(FactionFC faction) => null;

        /// <summary>Return extra float menu options for a settlement's context menu. Null means none.</summary>
        public virtual IEnumerable<FloatMenuOption> GetSettlementActions(FactionFC faction, WorldSettlementFC settlement) => null;

        /// <summary>Return extra deployment options when a settlement's main squad is already deployed. Null means none.</summary>
        public virtual IEnumerable<FloatMenuOption> GetExtraDeploymentOptions(FactionFC faction, WorldSettlementFC settlement, WorldObjectComp_SettlementMilitary milComp) => null;

        /// <summary>Return additional description lines to append to the policy's tooltip.</summary>
        public virtual TaggedString GetDescription() => TaggedString.Empty;

        /* Serialization */
        public virtual void ExposeData() { }
    }
}
