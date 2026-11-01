using FactionColonies.util;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using Verse;

namespace FactionColonies
{
    public enum BattleWinner
    {
        Attacker = 0,
        Defender = 1,
        Error = -1
    }

    public enum BattleSubPhase
    {
        Preparing = 0,
        Engaged = 1,
        RollsInProgress = 2,
        Resolved = 3
    }

    /*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*/
    /* BattleResult                                                                */
    /*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*/

    /// <summary>
    /// Single shape spanning a battle's lifecycle:
    ///   1. Live in-flight object during per-round auto-resolve (one round per hour).
    ///   2. Completion record passed to <see cref="MilitaryOperation.CompleteBattle"/>
    ///      and the handler <c>ApplyResult</c> methods.
    ///   3. Archive entry stored in <see cref="WorldComponent_Archive"/>.
    /// <para>For manual battles a synthetic stub is built with <c>rounds</c> empty
    /// and <see cref="wasManualBattle"/> set; the rendering window shows a "no per-round
    /// detail" placeholder for these.</para>
    /// </summary>
    public class BattleResult : IExposable
    {
        /* -*- Outcome -*- */
        public BattleWinner winner;
        public int totalRounds;

        /* -*- Force snapshots -*-
         * defenderInitialForce already includes the FCSettings.defenderAdvantage multiplier. */
        public double attackerInitialForce;
        public double defenderInitialForce;
        public double attackerForceRemaining;
        public double defenderForceRemaining;

        /* -*- Battle context -*-
         * Faction references are kept so the report viewer can render the faction icon
         * even after the originating op has been disposed. The string `*FactionName`
         * fields are snapshots used as fallbacks when the faction has been removed from
         * the world entirely. Defeated factions stay valid references — we only fall
         * back when the lookup actually returns null. */
        public double attackerEfficiency;
        public double defenderEfficiency;
        public string attackerLabel;
        public string defenderLabel;
        public string attackerFactionName;
        public string defenderFactionName;
        public Faction attackerFaction;
        public Faction defenderFaction;
        public PlanetTile targetTile = PlanetTile.Invalid;

        /* -*- Live state (mutates during auto-resolve, frozen after Resolved) -*- */
        public BattleSubPhase subPhase = BattleSubPhase.Resolved;
        public List<RoundEntry> rounds = new List<RoundEntry>();

        /* -*- Archive metadata, populated when WorldComponent_Archive accepts the result -*- */
        public int reportId;
        public int recordedTick;
        public BattleOperationKind kind = BattleOperationKind.Other;
        public bool wasManualBattle;
        /* True when the player voluntarily withdrew from a manual battle rather than being beaten.
         * A retreat is a failed op, but never an overwhelming/crushing outcome, so it suppresses
         * IsOverwhelmingVictory (and thus the crushing-defeat letter + cooldown extension + badge). */
        public bool wasWithdrawal;

        public bool AttackerVictory => winner == BattleWinner.Attacker;
        public bool DefenderVictory => winner == BattleWinner.Defender;

        /* True when the winning side took zero casualties. Suppressed when both sides
         * started below force 3; at the lowest force levels, the zero-casualty condition
         * is basically guaranteed (1v1 always ends 1-0 thanks to MilitaryForce's
         * Math.Max(1, ...) floor), so without this gate OV/CD would fire on every
         * micro-skirmish. As long as one side opens with 3+ force, OV/CD is eligible. */
        public bool IsOverwhelmingVictory =>
            !wasWithdrawal &&
            (Math.Max(attackerInitialForce, defenderInitialForce) >= 3.0) &&
            ((winner == BattleWinner.Attacker && attackerInitialForce > 0
                && attackerForceRemaining >= attackerInitialForce) ||
             (winner == BattleWinner.Defender && defenderInitialForce > 0
                && defenderForceRemaining >= defenderInitialForce));

        /* Crushing Defeat is Overwhelming Victory viewed from the loser's side: the loser
         * inflicted zero casualties on the winner. Mathematically identical to
         * IsOverwhelmingVictory; exposed as a separate accessor so penalty-side code reads
         * clearly. The "ForAttacker"/"ForDefender" variants pin the perspective so callers
         * don't need to combine winner + OV themselves. */
        public bool IsCrushingDefeat => IsOverwhelmingVictory;
        public bool IsCrushingDefeatForAttacker =>
            winner == BattleWinner.Defender && IsOverwhelmingVictory;
        public bool IsCrushingDefeatForDefender =>
            winner == BattleWinner.Attacker && IsOverwhelmingVictory;

        /// <summary>
        /// True when one side has been depleted. Used by <see cref="MilitaryOperation.AdvanceBattleProgress"/>
        /// to decide whether to schedule another round or hand off to <c>CompleteBattle</c>.
        /// </summary>
        public bool IsComplete => attackerForceRemaining <= 0 || defenderForceRemaining <= 0;

        public void ExposeData()
        {
            Scribe_Values.Look(ref winner, "winner");
            Scribe_Values.Look(ref totalRounds, "totalRounds");
            Scribe_Values.Look(ref attackerInitialForce, "attackerInitialForce");
            Scribe_Values.Look(ref defenderInitialForce, "defenderInitialForce");
            Scribe_Values.Look(ref attackerForceRemaining, "attackerForceRemaining");
            Scribe_Values.Look(ref defenderForceRemaining, "defenderForceRemaining");
            Scribe_Values.Look(ref attackerEfficiency, "attackerEfficiency");
            Scribe_Values.Look(ref defenderEfficiency, "defenderEfficiency");
            Scribe_Values.Look(ref attackerLabel, "attackerLabel");
            Scribe_Values.Look(ref defenderLabel, "defenderLabel");
            Scribe_Values.Look(ref attackerFactionName, "attackerFactionName");
            Scribe_Values.Look(ref defenderFactionName, "defenderFactionName");
            Scribe_References.Look(ref attackerFaction, "attackerFaction");
            Scribe_References.Look(ref defenderFaction, "defenderFaction");
            Scribe_Values.Look(ref targetTile, "targetTile", PlanetTile.Invalid);
            Scribe_Values.Look(ref subPhase, "subPhase", BattleSubPhase.Resolved);
            Scribe_Collections.Look(ref rounds, "rounds", LookMode.Deep);
            Scribe_Values.Look(ref reportId, "reportId", 0);
            Scribe_Values.Look(ref recordedTick, "recordedTick", 0);
            Scribe_Values.Look(ref kind, "kind", BattleOperationKind.Other);
            Scribe_Values.Look(ref wasManualBattle, "wasManualBattle", false);
            Scribe_Values.Look(ref wasWithdrawal, "wasWithdrawal", false);

            if (Scribe.mode == LoadSaveMode.PostLoadInit && rounds is null)
                rounds = new List<RoundEntry>();
        }
    }

    /// <summary>
    /// One round of an auto-resolved battle. Captures both the raw d20 roll (1..20) and the
    /// post-dampening final score for both sides, the round winner, and the resulting force
    /// remaining on each side. Pre- and post-efficiency values are both stored so the player
    /// can audit upset victories without inferring the dampening formula.
    /// </summary>
    public class RoundEntry : IExposable
    {
        public int roundNumber;
        public int attackerRawRoll;       // 1..20
        public int defenderRawRoll;       // 1..20
        public double attackerScore;       // raw * dampened efficiency
        public double defenderScore;
        public bool attackerWonRound;
        public double attackerForceAfter;  // force remaining on attacker after this round
        public double defenderForceAfter;

        public void ExposeData()
        {
            Scribe_Values.Look(ref roundNumber, "roundNumber");
            Scribe_Values.Look(ref attackerRawRoll, "attackerRawRoll");
            Scribe_Values.Look(ref defenderRawRoll, "defenderRawRoll");
            Scribe_Values.Look(ref attackerScore, "attackerScore");
            Scribe_Values.Look(ref defenderScore, "defenderScore");
            Scribe_Values.Look(ref attackerWonRound, "attackerWonRound");
            Scribe_Values.Look(ref attackerForceAfter, "attackerForceAfter");
            Scribe_Values.Look(ref defenderForceAfter, "defenderForceAfter");
        }
    }
}
