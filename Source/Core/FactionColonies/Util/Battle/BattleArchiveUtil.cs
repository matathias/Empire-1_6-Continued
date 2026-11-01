using FactionColonies.util;
using Verse;

namespace FactionColonies
{
    /*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*/
    /* BattleArchiveUtil                                                           */
    /*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*/

    /// <summary>
    /// Bridge between <see cref="MilitaryJobHandler"/> implementations and
    /// <see cref="WorldComponent_Archive"/>. The job handlers pass a fully-resolved
    /// <see cref="BattleResult"/> on completion; this util records it (synthesising
    /// the missing context fields for manual battles) and returns the archive id so
    /// the outcome letter can carry it.
    /// </summary>
    internal static class BattleArchiveUtil
    {
        /// <summary>
        /// Stamp <paramref name="result"/> with op-derived metadata, fill in side labels
        /// + faction names if absent (the auto-resolve path populates them at battle
        /// start; the manual-battle path leaves them blank), then forward to
        /// <see cref="WorldComponent_Archive.RecordBattleReport"/>.
        /// </summary>
        /// <returns>Assigned report id, or 0 if no archive is available (no world yet).</returns>
        public static int ArchiveAndGetId(MilitaryOperation op, BattleResult result, BattleOperationKind kind)
        {
            if (result is null) return 0;
            WorldComponent_Archive archive = WorldComponent_Archive.Get();
            if (archive is null) return 0;

            result.kind = kind;

            // Auto-resolve battles arrive with battleResult populated and labels set during
            // BeginAutoResolveProgress. Manual battles arrive with op.battleResult == null
            // and the result here built by the comp from on-map outcome — no labels yet.
            // wasManualBattle is the discriminator the report viewer uses to decide whether
            // to render the per-round detail or the "no per-round detail" placeholder.
            bool isAutoResolve = op?.battleResult is object;
            if (!isAutoResolve)
            {
                result.wasManualBattle = true;
            }

            if (op is object)
            {
                FillContextFromOpIfMissing(result, op);
            }

            return archive.RecordBattleReport(result);
        }

        /// <summary>
        /// Populate empty side-context fields on <paramref name="result"/> from the
        /// surviving op participants. Non-destructive: a field that was already filled
        /// (auto-resolve path) is left alone.
        /// </summary>
        private static void FillContextFromOpIfMissing(BattleResult result, MilitaryOperation op)
        {
            if (result.attackerLabel.NullOrEmpty())
            {
                result.attackerLabel = op.aggressor?.squad?.DisplayName
                    ?? op.aggressor?.homeSettlement?.Name
                    ?? op.aggressor?.faction?.Name
                    ?? "?";
            }
            if (result.defenderLabel.NullOrEmpty())
            {
                result.defenderLabel = (op.targetObject as WorldSettlementFC)?.Name
                    ?? op.defender?.homeSettlement?.Name
                    ?? op.defender?.squad?.DisplayName
                    ?? op.defender?.faction?.Name
                    ?? "?";
            }
            if (result.attackerFactionName.NullOrEmpty())
                result.attackerFactionName = op.aggressor?.faction?.Name ?? "?";
            if (result.defenderFactionName.NullOrEmpty())
                result.defenderFactionName = op.defender?.faction?.Name ?? "?";
            if (result.attackerFaction is null)
                result.attackerFaction = op.aggressor?.faction;
            if (result.defenderFaction is null)
                result.defenderFaction = op.defender?.faction;
            if (!result.targetTile.Valid)
                result.targetTile = op.targetTile;
        }
    }
}
