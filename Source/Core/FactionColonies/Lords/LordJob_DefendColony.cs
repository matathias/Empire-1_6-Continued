using Verse.AI.Group;

namespace FactionColonies;

public class LordJob_DefendColony : LordJob
{
    private Dictionary<Pawn, Pawn> mounts;
    private List<Pawn> mountsKeys = [];
    private List<Pawn> mountsValues = [];
    private readonly HashSet<Pawn> readded = [];
    private WorldSettlementFC settlement;

    public LordJob_DefendColony()
    {
    }

    public LordJob_DefendColony(WorldSettlementFC settlement, Dictionary<Pawn, Pawn> mounts)
    {
        this.settlement = settlement;
        this.mounts = mounts;
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_References.Look(ref settlement, "settlement");
        // Dead or destroyed pawns save as null references, which break the dictionary on load.
        if (Scribe.mode == LoadSaveMode.Saving)
            mounts?.RemoveAll(kv => kv.Key is null || kv.Key.Destroyed || kv.Value is null || kv.Value.Destroyed);
        Scribe_Collections.Look(ref mounts, "mounts", LookMode.Reference, LookMode.Reference, ref mountsKeys, ref mountsValues);
    }

    /// <summary>
    /// Signal tag sent when an attacker group on <paramref name="map"/> starts its assault.
    /// Scoped to the map so an assault on one battle map does not wake defenders on another.
    /// </summary>
    public static string AssaultSignalTag(Map map) => $"startAssault_{map.uniqueID}";

    public override bool AddFleeToil => false;
    public override bool AllowStartNewGatherings => false;
    public override bool AlwaysShowWeapon => true;

    public override void LordJobTick()
    {
        base.LordJobTick();
        if (readded.Count > 0)
        {
            lord.CurLordToil.UpdateAllDuties();
            readded.Clear();
        }
    }

    public override StateGraph CreateGraph()
    {
        StateGraph stateGraph = new();
        LordToil_DefendSelfAndMount defendSelf = new(mounts);
        stateGraph.AddToil(defendSelf);
        LordToil defendColony = new LordToil_DefendColony();
        stateGraph.AddToil(defendColony);
        Transition startDefending = new(defendSelf, defendColony);
        startDefending.AddTrigger(new Trigger_MapAssaultSignal());
        stateGraph.AddTransition(startDefending);
        return stateGraph;
    }

    public override void Notify_PawnLost(Pawn pawn, PawnLostCondition condition)
    {
        if (condition == PawnLostCondition.ChangedFaction || condition == PawnLostCondition.ExitedMap)
        {
            // Player is drafting this pawn — let it go without re-adding
            if (condition == PawnLostCondition.ChangedFaction && pawn.Faction == Faction.OfPlayer)
            {
                return;
            }
            if (pawn.Spawned)
            {
                lord.AddPawn(pawn);
                readded.Add(pawn);
                return;
            }
            // Already despawned (e.g. joined an existing caravan): fall through to RemoveDefender.
        }
        if (pawn.IsMercenary() && pawn.Faction != FindFC.EmpireFaction) pawn.SetFaction(FindFC.EmpireFaction);

        settlement?.MilitaryComp?.RemoveDefender(pawn);
    }
}

/// <summary>Fires when an assault starts on the lord's own map (see <see cref="LordJob_DefendColony.AssaultSignalTag"/>).</summary>
public class Trigger_MapAssaultSignal : Trigger
{
    public override bool ActivateOn(Lord lord, TriggerSignal signal)
    {
        return signal.type == TriggerSignalType.Signal
            && lord?.Map is not null
            && signal.signal.tag == LordJob_DefendColony.AssaultSignalTag(lord.Map);
    }
}
