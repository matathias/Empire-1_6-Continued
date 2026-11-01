using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using Verse;

namespace FactionColonies
{
    public static class MilitaryOperationsUtil
    {
        /// <summary>
        /// Schedules a defensive operation against an Empire settlement: creates a
        /// <see cref="MilitaryOperation"/> via the manager, runs auto-defender selection,
        /// and queues the 24-hour <c>settlementBeingAttacked</c> warning event linked back
        /// to the op. Returns true on success, false if the attack was rejected
        /// (no MilitaryComp or MilitaryManager unavailable).
        /// </summary>
        public static bool AttackPlayerSettlement(MilitaryForce attackingForce, WorldSettlementFC settlement, Faction enemyFaction)
        {
            if (settlement?.MilitaryComp is null)
            {
                LogUtil.Warning($"AttackPlayerSettlement rejected: {settlement?.Name ?? "null"} has no MilitaryComp. " +
                    $"Attacker {enemyFaction?.Name ?? "null"} dropped.");
                return false;
            }

            MilitaryOperationManager manager = FindFC.MilitaryManager;
            if (manager is null)
            {
                LogUtil.Error("AttackPlayerSettlement: MilitaryManager unavailable.");
                return false;
            }

            // Manager handles op creation, auto-defender selection, warning event scheduling
            // (with linkedOperation back-reference), and the "settlement in danger" letter.
            MilitaryOperation op = manager.CreateDefensiveOp(settlement, attackingForce, enemyFaction);
            return op is object;
        }

        /// <summary>
        /// Attacks an external <see cref="IRaidTarget"/> registered via <see cref="RaidTargetRegistry"/>.
        /// Routes through <see cref="MilitaryOperationManager.CreateDefensiveOp"/> with the target's
        /// world object as the op's <c>targetObject</c>. The 24-hour warning, auto-defender selection,
        /// and forecast letter all happen inside the manager.
        /// </summary>
        public static void AttackRaidTarget(MilitaryForce attackingForce, IRaidTarget target, Faction enemyFaction)
        {
            if (target?.WorldObject is null) return;
            MilitaryOperationManager manager = FindFC.MilitaryManager;
            if (manager is null)
            {
                LogUtil.Error("AttackRaidTarget: MilitaryManager unavailable.");
                return;
            }

            MilitaryOperation op = manager.CreateDefensiveOp(target.WorldObject, attackingForce, enemyFaction);
            if (op is object)
            {
                target.IsUnderAttack = true;
            }
        }

        /// <summary>
        /// Replaces the defending side of the op linked to <paramref name="evt"/> with a new
        /// Empire settlement (<paramref name="settlementOfMilitaryForce"/>). No-op if no linked op
        /// exists (the warning event must be op-linked, which is true for any save processed by
        /// <see cref="MilitaryMigrationUtil"/> on load).
        /// </summary>
        public static void ChangeDefendingMilitaryForce(FCEvent evt, WorldSettlementFC settlementOfMilitaryForce)
        {
            FactionFC factionfc = FindFC.FactionComp;
            if (factionfc is null) return;
            WorldSettlementFC homeSettlement = factionfc.ReturnSettlementByLocation(evt.location);

            MilitaryOperationManager manager = FindFC.MilitaryManager;
            MilitaryOperation op = evt.linkedOperation;
            if (op is null)
            {
                LogUtil.Warning($"ChangeDefendingMilitaryForce: warning event at tile {evt.location} has no linked op.");
                return;
            }

            if (settlementOfMilitaryForce == op.defender?.homeSettlement)
            {
                Messages.Message("FCMilitaryAlreadyDefendingSettlement".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }

            // Release the previous defender's commitment (foreign settlement marker or external).
            ReleaseCurrentDefender(op);

            // Reindex by detaching the op from manager indices, mutating the defender, then
            // re-registering. _bySquad / _bySettlement are keyed off op.defender.* and would
            // otherwise lag the swap.
            manager.Unregister(op);

            MilitaryForce newForce;
            if (settlementOfMilitaryForce == homeSettlement)
            {
                newForce = MilitaryForce.CreateMilitaryForceFromSettlement(homeSettlement);
                op.defender.homeSettlement = homeSettlement;
                op.defender.squad = PickFirstAvailableStationedSquad(homeSettlement);
                op.defender.force = newForce;
                op.externalDefenderSource = null;
                Messages.Message("FCDefendingMilitaryReset".Translate(), MessageTypeDefOf.NeutralEvent);
            }
            else
            {
                newForce = MilitaryForce.CreateMilitaryForceFromSettlement(settlementOfMilitaryForce);
                op.defender.homeSettlement = settlementOfMilitaryForce;
                op.defender.squad = PickFirstAvailableStationedSquad(settlementOfMilitaryForce);
                op.defender.force = newForce;
                op.externalDefenderSource = null;

                Find.LetterStack.ReceiveLetter("FCMilitaryAction".Translate(), "FCForeignMilitarySwitch".Translate(
                    settlementOfMilitaryForce.Name, homeSettlement?.Name ?? "", newForce.militaryLevel.ToString("F1")),
                    LetterDefOf.NeutralEvent);
            }

            manager.Register(op);
        }

        /// <summary>
        /// Replaces the defending side of the op linked to <paramref name="evt"/> with the
        /// given <paramref name="squad"/>. The squad's <see cref="MercenarySquadFC.settlement"/>
        /// becomes the new <c>op.defender.homeSettlement</c>. The defending force is
        /// squad-only: the settlement being defended contributes nothing.
        /// <para>User-facing entry. For the auto-defender / debug paths still routed through settlements,
        /// see <see cref="ChangeDefendingMilitaryForce"/>.</para>
        /// </summary>
        public static void ChangeDefendingToSquad(FCEvent evt, MercenarySquadFC squad)
        {
            if (squad?.settlement is null) return;

            FactionFC factionfc = FindFC.FactionComp;
            if (factionfc is null) return;
            WorldSettlementFC homeSettlement = factionfc.ReturnSettlementByLocation(evt.location);

            MilitaryOperationManager manager = FindFC.MilitaryManager;
            MilitaryOperation op = evt.linkedOperation;
            if (op is null)
            {
                LogUtil.Warning($"ChangeDefendingToSquad: warning event at tile {evt.location} has no linked op.");
                return;
            }

            if (op.defender?.squad == squad)
            {
                Messages.Message("FCMilitaryAlreadyDefendingSettlement".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }

            ReleaseCurrentDefender(op);

            // Reindex by detaching the op from manager indices, mutating the defender, then
            // re-registering. _bySquad / _bySettlement are keyed off op.defender.* and would
            // otherwise lag the swap.
            manager.Unregister(op);

            MilitaryForce newForce = MilitaryForce.CreateMilitaryForceFromSquad(squad);
            op.defender.homeSettlement = squad.settlement;
            op.defender.squad = squad;
            op.defender.force = newForce;
            op.externalDefenderSource = null;

            if (squad.settlement == homeSettlement)
            {
                Messages.Message("FCDefendingMilitaryReset".Translate(), MessageTypeDefOf.NeutralEvent);
            }
            else
            {
                Find.LetterStack.ReceiveLetter("FCMilitaryAction".Translate(), "FCForeignMilitarySwitch".Translate(
                    squad.settlement.Name, homeSettlement?.Name ?? "", (newForce?.militaryLevel ?? 0).ToString("F1")),
                    LetterDefOf.NeutralEvent);
            }

            manager.Register(op);
        }

        /// <summary>
        /// Replaces the op's defender with the given external <see cref="IAutoDefender"/>.
        /// No-op if no linked op exists.
        /// </summary>
        public static void ChangeDefendingToExternalForce(FCEvent evt, IAutoDefender defender)
        {
            FactionFC factionfc = FindFC.FactionComp;
            if (factionfc is null) return;

            MilitaryOperationManager manager = FindFC.MilitaryManager;
            MilitaryOperation op = evt.linkedOperation;
            if (op is null)
            {
                LogUtil.Warning($"ChangeDefendingToExternalForce: warning event at tile {evt.location} has no linked op.");
                return;
            }

            if (op.externalDefenderSource is object && op.externalDefenderSource == defender.WorldObject)
            {
                Messages.Message("FCMilitaryAlreadyDefendingSettlement".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }

            ReleaseCurrentDefender(op);

            // Reindex via Unregister + Register so the manager indices reflect the new
            // defender.homeSettlement / defender.squad (both go null for an external defender).
            manager.Unregister(op);

            op.defender.homeSettlement = null;
            op.defender.squad = null;
            op.defender.force = defender.CreateDefendingForce();
            op.externalDefenderSource = defender.WorldObject;
            // Pledge marks the defender busy now (warning window), not at engagement. ReleaseCurrentDefender
            // above already fired OnDefenseReplaced on any prior external defender.
            defender.OnDefensePledged(op.targetObject);

            manager.Register(op);

            // OnDefenseStarted fires from MilitaryOperation.BeginEngagement when the warning event
            // resolves — not here. Firing it now would cause a double-fire: once at swap time and
            // again at engagement, with only a single matching OnDefenseComplete.

            Messages.Message("FCExternalDefenderAssigned".Translate(defender.WorldObject.LabelCap),
                MessageTypeDefOf.NeutralEvent);
        }

        /// <summary>
        /// Clears the previous defender's commitment markers. For external defenders, fires the
        /// auto-defender's <c>OnDefenseReplaced</c> hook. Foreign Empire settlement defenders are
        /// released by the caller assigning a new <c>op.defender.homeSettlement</c>.
        /// </summary>
        private static void ReleaseCurrentDefender(MilitaryOperation op)
        {
            if (op.externalDefenderSource is object)
            {
                IAutoDefender old = AutoDefenderRegistry.FindByWorldObject(op.externalDefenderSource);
                old?.OnDefenseReplaced();
                op.externalDefenderSource = null;
            }
        }

        public static FCEvent ReturnMilitaryEventByLocation(PlanetTile location)
        {
            return FindFC.FactionComp.FindEventByDefAndLocation(FCEventDefOf.settlementBeingAttacked, location);
        }

        public static IReadOnlyList<FCEvent> ReturnMilitaryEventsByLocation(PlanetTile location)
        {
            return FindFC.FactionComp.FindAllEventsByDefAndLocation(FCEventDefOf.settlementBeingAttacked, location);
        }

        /// <summary>Returns the first available squad stationed at <paramref name="settlement"/>,
        /// or <c>null</c> if none qualify. Used when wiring an op's defender squad after a
        /// manual defender swap.</summary>
        private static MercenarySquadFC PickFirstAvailableStationedSquad(WorldSettlementFC settlement)
        {
            if (settlement is null) return null;
            foreach (MercenarySquadFC squad in settlement.StationedSquads)
            {
                if (squad is null) continue;
                if (squad.IsAvailable) return squad;
            }
            // Fall back to the first stationed squad even if busy (rare: caller manually swapped).
            List<MercenarySquadFC> stationed = settlement.StationedSquads;
            return stationed.Count > 0 ? stationed[0] : null;
        }
    }
}
