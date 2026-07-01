using System.Collections.Generic;

namespace FactionColonies.util
{
    public enum MilitaryOrder
    {
        Undefined,
        DefendPoint,
        Hunt,
        RecoverWoundedAndLeave
    }

    public enum Operation
    {
        Addition,
        Multiplication
    }

    /// <summary>
    /// The three mutable settlement morale stats that a <see cref="FactionColonies.DecayingStatPenalty"/>
    /// can target. Mapped to the matching loss FCStatDef by EmpireDeathPenaltyUtil
    /// (Happiness -> happinessLostBase, Loyalty -> loyaltyLostBase, Unrest -> unrestGainedBase).
    /// </summary>
    public enum SettlementStat
    {
        Happiness,
        Loyalty,
        Unrest
    }

    public enum PatchNoteType
    {
        Undefined,
        Hotfix,
        Patch,
        Minor,
        Major
    }

    public enum BattleMode
    {
        Auto,
        Manual,
        Hybrid
    }

    /// <summary>
    /// Hard-coded action gates that policies can block or enable via FCPolicyDef.blockedActions/enabledActions.
    /// Military job-level permissions are handled separately via MilitaryJobDef.defaultEnabled and
    /// FCPolicyDef.blockedMilitaryJobs/enabledMilitaryJobs.
    /// </summary>
    public enum FCActionType
    {
        DeployMilitary,
        SendDiplomat,
        DeployExtraSquad,
        BuildRoadsToAllies,
        UseFireSupport,
        SendPrisoner,
        SellPrisoner,
        DemolishBuilding,
        UpgradeSettlement,
        TradeWithSettlement
    }

    /// <summary>
    /// Centralizes the opt-in/opt-out distinction for FCActionType.
    /// Actions in the RequiresEnable set are opt-in (unavailable by default, require a policy/trait to enable).
    /// All other actions are opt-out (available by default, can be blocked by a policy/trait).
    /// </summary>
    public static class FCActionTypeUtil
    {
        private static readonly HashSet<FCActionType> requiresEnable = new HashSet<FCActionType>
        {
            FCActionType.SendDiplomat,
            FCActionType.DeployExtraSquad,
            FCActionType.BuildRoadsToAllies
        };

        public static bool RequiresEnable(FCActionType action) => requiresEnable.Contains(action);
    }


    public enum TaxDeliveryMode
    {
        None,
        TaxSpot,
        Caravan,
        DropPod,
        Shuttle
    }

    public enum EmpireDifficultyLevel
    {
        Peaceful = 0,
        CommunityBuilder = 1,
        AdventureStory = 2,
        StriveToSurvive = 3,
        BloodAndDust = 4,
        LosingIsFun = 5,
        Custom = 6
    }

    public enum TaxNotificationMode
    {
        All,        // Show both Letter and Message
        LetterOnly, // Only show Letter (blue notification)
        MessageOnly,// Only show Message (top-screen text)
        None        // Hide all tax delivery notifications
    }

    public enum FCPolicyCategory : byte
    {
        Undefined = 0,
        Trait = 1,
        Core = 2,
        Tax = 3,
        Military = 4,
        Social = 5,
        Doctrine = 6
    }

    public enum MilitaryWindowSlot
    {
        Units,
        Squads,
        FireSupport
    }

    /// <summary>
    /// Logical kind of a battle as recorded in the archive. Used for the archive list's
    /// row label and for picking the right offensive/defensive context when opening the
    /// report viewer (offensive ops use the defender label as the "target", defensive ops
    /// use the attacker label).
    /// </summary>
    public enum BattleOperationKind
    {
        Other = 0,
        Raid = 1,
        Capture = 2,
        Enslave = 3,
        Defense = 4
    }

    /// <summary>
    /// Which side the player is on for the report viewer's tinting and column emphasis.
    /// Computed from the op at archive time (so the archive can render correctly even if
    /// the originating op is long gone). <c>Neither</c> covers pure NPC-vs-NPC battles
    /// observed via diplomatic alliances or other indirect channels.
    /// </summary>
    public enum BattleViewerSide
    {
        Neither = 0,
        Attacker = 1,
        Defender = 2
    }

    /// <summary>
    /// Lifecycle phase of an <see cref="FCEvent"/>.
    /// Queued: in the queue, awaiting timer.
    /// Fired: tentative, mid-processing only — should never persist past a single ProcessEvents pass.
    /// Resolving: handler-driven persistent state. Set explicitly by a handler that wants the event
    /// to stick around. Stays in queue until something transitions it to Completed.
    /// Completed: done, awaiting sweep from the queue.
    /// </summary>
    public enum FCEventPhase
    {
        Queued,
        Fired,
        Resolving,
        Completed
    }

    /// <summary>
    /// Whether a <see cref="FactionColonies.FCSituationDef"/> lives on the whole faction or on a
    /// single settlement. Faction-scoped situations apply their stat-modifiers to every settlement;
    /// settlement-scoped situations carry a <c>targetSettlement</c> and apply to it alone.
    /// </summary>
    public enum FCSituationScope
    {
        Faction,
        Settlement
    }

    /// <summary>
    /// What happens when a situation's progress bar reaches one of its endpoints (top = maxProgress,
    /// bottom = 0). Each end is configured independently.
    /// Terminal: fire that end's resolution event, then remove the situation.
    /// Clamp: hold at the boundary (open-ended management).
    /// Loop: wrap to the other end, fire the loop event, increment loopCount (recurring timer).
    /// </summary>
    public enum FCSituationEndpointBehavior
    {
        Terminal,
        Clamp,
        Loop
    }

    /// <summary>
    /// Direction a situation's bar was moving when it changed stage. Passed to
    /// <see cref="FactionColonies.FCSituationHandlerExtension.OnStageChanged"/> so handlers can react
    /// differently to an escalating vs. a de-escalating crossing.
    /// </summary>
    public enum FCSituationStageDirection
    {
        Ascending,
        Descending
    }
}
