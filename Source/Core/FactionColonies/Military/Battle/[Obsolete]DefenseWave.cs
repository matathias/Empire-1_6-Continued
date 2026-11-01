using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using Verse;

// DefenseWave is itself [Obsolete]; the class also reads [Obsolete] FCEvent.externalDefenderSource
// from its sourceEvent argument. Both go away together when the per-wave MilitaryOperation
// model fully replaces this type.
#pragma warning disable 0618

namespace FactionColonies
{
    /// <summary>
    /// Tracks one wave of attackers (and any reinforcement defenders spawned for it)
    /// within a multi-wave manual defense battle. The settlement's MilitaryComp holds
    /// a <c>List&lt;DefenseWave&gt;</c> for all active waves.
    /// </summary>
    [Obsolete("Replaced by BattlefieldContext + per-wave MilitaryOperation. Kept for one version for save round-trip safety.")]
    public class DefenseWave : IExposable
    {
        /// <summary>The triggering settlementBeingAttacked event (consumed from the queue).</summary>
        public FCEvent sourceEvent;

        /// <summary>The attacking force's stats snapshot.</summary>
        public MilitaryForce attackerForce;

        /// <summary>The defending force assigned to counter this wave.</summary>
        public MilitaryForce defenderForce;

        /// <summary>The faction that sent this wave of attackers.</summary>
        public Faction attackerFaction;

        /// <summary>Pawns spawned for this wave's attackers (subset of the comp's flat <c>attackers</c> list).</summary>
        public List<Pawn> waveAttackers = new List<Pawn>();

        /// <summary>Defenders spawned as reinforcements for this wave (subset of the comp's flat <c>defenders</c> list).</summary>
        public List<Pawn> waveDefenders = new List<Pawn>();

        /// <summary>True once all of this wave's attackers are dead or removed.</summary>
        public bool resolved;

        /// <summary>External auto-defender source (VOE outposts, etc.), persisted for post-battle callbacks.</summary>
        public WorldObject externalDefenderSource;

        public DefenseWave() { }

        public DefenseWave(FCEvent sourceEvent, MilitaryForce attackerForce, MilitaryForce defenderForce, Faction attackerFaction)
        {
            this.sourceEvent = sourceEvent;
            this.attackerForce = attackerForce;
            this.defenderForce = defenderForce;
            this.attackerFaction = attackerFaction;
            this.externalDefenderSource = sourceEvent?.externalDefenderSource;
        }

        public void ExposeData()
        {
            Scribe_Deep.Look(ref attackerForce, "attackerForce");
            Scribe_Deep.Look(ref defenderForce, "defenderForce");
            Scribe_References.Look(ref attackerFaction, "attackerFaction");
            Scribe_Collections.Look(ref waveAttackers, "waveAttackers", LookMode.Reference);
            Scribe_Collections.Look(ref waveDefenders, "waveDefenders", LookMode.Reference);
            Scribe_Values.Look(ref resolved, "resolved");
            Scribe_References.Look(ref externalDefenderSource, "externalDefenderSource");

            // sourceEvent is consumed from the queue before battle starts and is not scribed.
            // Post-load, it remains null — wave data is self-contained.

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (waveAttackers is null) waveAttackers = new List<Pawn>();
                if (waveDefenders is null) waveDefenders = new List<Pawn>();
                // Keep pod-bound pawns (not Spawned but held inside a Skyfaller) — they'll
                // spawn when the pod opens. Only prune null / destroyed / orphaned-despawned.
                waveAttackers.RemoveAll(p => p is null || p.Destroyed || (!p.Spawned && p.ParentHolder is null));
                waveDefenders.RemoveAll(p => p is null || p.Destroyed || (!p.Spawned && p.ParentHolder is null));
            }
        }
    }
}
