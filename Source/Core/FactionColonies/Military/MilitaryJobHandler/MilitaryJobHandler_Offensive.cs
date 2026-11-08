namespace FactionColonies;

/// <summary>
/// Base for offensive handlers (raid / capture / raze / enslave). Adds the manual-battle seam:
/// when the target resolves to a map-gennable enemy settlement, resolution is delegated to
/// the tile's <see cref="BattlefieldContext.StartOffense"/>, which itself re-checks the
/// manual-offense setting and the shared concurrent-map cap and falls back to auto-resolve
/// when appropriate. Handlers that extend the raw <see cref="MilitaryJobHandler"/> stay auto-only.
/// <para>Also owns the shared op flow: <see cref="OnOpCreated"/> schedules the arrival event and
/// sends the "forces sent" letter, and <see cref="ApplyResult"/> looks up the target, then calls
/// <see cref="ApplyVictory"/> or sends the failure battle report. Subclasses supply the event,
/// translation keys and victory side effects through the abstract members.</para>
/// </summary>
public abstract class MilitaryJobHandler_Offensive : MilitaryJobHandler
{
    /// <summary>Event scheduled at op creation; fires when the squad reaches the target.</summary>
    protected abstract FCEventDef ArrivalEvent { get; }

    /// <summary>Key for the arrival event's description, given the home settlement name and the
    /// target label.</summary>
    protected abstract string TravelingDescKey { get; }

    /// <summary>Key for the "forces sent" letter body, given the home settlement name and the
    /// target label.</summary>
    protected abstract string SentLetterKey { get; }

    /// <summary>Lower-case op name used in the missing-target log warning.</summary>
    protected abstract string TargetLogLabel { get; }

    /// <summary>Kind recorded on archived battle reports.</summary>
    protected abstract BattleOperationKind ReportKind { get; }

    /// <summary>Key for the failure battle report's label.</summary>
    protected abstract string FailureLabelKey { get; }

    /// <summary>Body text of the failure battle report.</summary>
    protected abstract string FailureBody(MilitaryOperation op, Settlement target);

    /// <summary>Applies the victory side effects (loot, prisoners, capture, raze) and sends the
    /// success report. <paramref name="target"/> is non-null and the op has a home settlement.</summary>
    protected abstract void ApplyVictory(MilitaryOperation op, BattleResult result, Settlement target);

    /// <summary>Whether a non-victory result sends the failure report. Default: only a
    /// defender victory does.</summary>
    protected virtual bool SendsFailureReport(BattleResult result) => result.DefenderVictory;

    /// <summary>The op's target settlement: its target object, or the settlement at its tile.</summary>
    protected static Settlement TargetSettlement(MilitaryOperation op) =>
        op.targetObject as Settlement ?? Find.WorldObjects.SettlementAt(op.targetTile);

    /// <summary>
    /// True only when the op's target is a live, non-player, non-Empire settlement whose map
    /// we can generate. The auto/manual/cap decision is made later inside StartOffense
    /// (mirrors StartDefense), so OnEventFired's exception fallback to OnAutoResolve stays
    /// correct even when this returns true.
    /// </summary>
    public override bool ResolvesManually(MilitaryOperation op)
    {
        if (op is null) return false;
        if (!op.targetTile.Valid) return false;
        Settlement target = TargetSettlement(op);
        if (target?.Faction is null) return false;
        if (target.Faction.IsPlayer || FindFC.IsEmpireFaction(target.Faction)) return false;
        return true;
    }

    public override void OnManualResolve(MilitaryOperation op)
    {
        if (op is null) return;
        MilitaryOperationManager mgr = FindFC.MilitaryManager;
        if (mgr is null) { op.BeginAutoResolveProgress(); return; }
        mgr.GetOrCreateBattlefield(op.targetTile).StartOffense(op);
    }

    public override void OnOpCreated(MilitaryOperation op)
    {
        WorldSettlementFC home = op.aggressor?.homeSettlement;
        if (home is null) return;

        int travelTicks = Math.Max(0, op.nextPhaseTick - Find.TickManager.TicksGame);
        string desc = TravelingDescKey.Translate(home.Name, op.targetObject?.Label ?? "").ToString();

        op.ScheduleEvent(ArrivalEvent, home.Tile, travelTicks, desc);

        Settlement targetSettlement = TargetSettlement(op);
        Find.LetterStack.ReceiveLetter("FCMilitaryAction".Translate(),
            SentLetterKey.Translate(home.Name, targetSettlement?.LabelCap ?? (TaggedString)""),
            LetterDefOf.NeutralEvent);
    }

    public override void ApplyResult(MilitaryOperation op, BattleResult result)
    {
        if (result is null || op.aggressor?.homeSettlement is null) return;

        Settlement target = TargetSettlement(op);
        if (target is null)
        {
            LogUtil.Warning($"Military {TargetLogLabel} target at tile {op.targetTile} no longer exists");
            return;
        }

        if (result.AttackerVictory)
            ApplyVictory(op, result, target);
        else if (SendsFailureReport(result))
            SendFailureReport(op, result, target);
    }

    private void SendFailureReport(MilitaryOperation op, BattleResult result, Settlement target)
    {
        string body = FailureBody(op, target);
        // Fold the crushing-defeat flavor into this single failure letter rather than
        // sending a separate Crushing Defeat letter.
        if (MilitaryLetterUtil.IsCrushingLoss(result, playerWon: false))
            body = $"{body}\n\n{"FCCrushingDefeatDesc".Translate()}";
        int reportId = BattleArchiveUtil.ArchiveAndGetId(op, result, ReportKind);
        MilitaryLetterUtil.SendBattleReportLetter(FailureLabelKey.Translate(),
            body, FCLetterDefOf.FCBattleReportLetterNegative,
            new LookTargets(target), reportId, op);
    }

    /// <summary>Folds the overwhelming-victory flavor and happiness/loyalty reward into a success
    /// letter body rather than sending a separate Overwhelming Victory letter.</summary>
    protected static void AppendOverwhelmingVictory(BattleResult result, WorldSettlementFC home, ref string body)
    {
        if (!MilitaryLetterUtil.IsOverwhelmingWin(result, playerWon: true)) return;
        body = $"{body}\n\n{"FCOverwhelmingVictoryDesc".Translate()}";
        MilitaryLetterUtil.ApplyOverwhelmingVictoryReward(home, ref body);
    }

    /* Fallback when the squad won but another mod blocked the target's Destroy(): send a one-line
     * notice and grant the raid victory instead (loot + optional prisoner + delivery). The notice
     * is not archived; the raid victory letter carries the battle report. */
    protected static void FallBackToRaidVictory(FactionFC faction, WorldSettlementFC home,
        Faction enemyFaction, Settlement target, MilitaryOperation op, BattleResult result,
        string labelKey, string bodyKey)
    {
        Find.LetterStack.ReceiveLetter(
            labelKey.Translate(),
            bodyKey.Translate(home.Name, target.LabelCap),
            LetterDefOf.NeutralEvent, new LookTargets(target));
        MilitaryJobHandler_Raid.ApplyVictoryToTarget(faction, home, enemyFaction, target, op, result);
    }
}
