namespace FactionColonies;

/// <summary>
/// Shared battle-map teardown handling for Empire-faction pawns. Used by both manual defense
/// (<see cref="WorldSettlementFC.Notify_MyMapAboutToBeRemoved"/>) and manual offense (whose map
/// parent is a vanilla enemy Settlement and so never fires that hook), so both preserve the
/// squad identically.
/// </summary>
public static class SquadMapTeardownUtil
{
    /// <summary>
    /// Cleans up Empire-faction pawns on a battle map about to be removed. Squad pawns
    /// (mercenaries and their bonded sub-pawns) are despawned and faction-restored so the squad
    /// holds them off-map (not the world pawn pool) for redeployment; genuine generated Empire
    /// defenders are destroyed as disposable. Non-Empire pawns are left alone.
    /// </summary>
    public static void PreserveEmpirePawns(Map map)
    {
        Faction empireFaction = FindFC.EmpireFaction;
        if (empireFaction is null || map is null) return;

        foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned.ToList())
        {
            // Squad pawns — mercenaries AND their bonded sub-pawns (mechs / companion animals) —
            // persist between battles. IsMercenary() self-heals against a lagging cache, so it
            // reliably recognizes a freshly-bonded mech. Despawn so the squad holds them off-map,
            // and restore the Empire faction as a safety net so they redeploy correctly next
            // battle. Only genuine generated defenders get destroyed.
            if (pawn.IsMercenary())
            {
                if (pawn.Spawned) pawn.DeSpawn();
                if (pawn.Faction != empireFaction) pawn.SetFaction(empireFaction);
                continue;
            }

            // Generated (non-squad) Empire defenders are disposable — destroy them so they don't
            // ghost into the world pawn pool.
            if (pawn.Faction != empireFaction) continue;
            pawn.DeSpawn();
            if (!pawn.Destroyed)
                pawn.Destroy();
        }
    }

    /// <summary>
    /// Despawns <paramref name="pawns"/>, tends every injury, and sends them to the player's home
    /// map as an injured-return delivery from <paramref name="from"/>; a loss adds a day to the
    /// trip. Returns the "returning home" message for the caller to show, or null when there is
    /// no home map (nomad play), in which case nothing is delivered.
    /// </summary>
    public static string ReturnPlayerPawnsHome(List<Pawn> pawns, PlanetTile from, bool won)
    {
        if (pawns is null || pawns.Count == 0) return null;

        foreach (Pawn pawn in pawns)
            if (pawn.Spawned) pawn.DeSpawn();
        foreach (Pawn pawn in pawns)
            if (!pawn.Dead) TendFully(pawn);

        Map home = Find.AnyPlayerHomeMap;
        if (home is null) return null;
        int travelTicks = TravelUtil.ReturnTicksToArrive(from, home.Tile);
        if (!won) travelTicks += GenDate.TicksPerDay;

        DeliveryEvent.CreateDeliveryEvent(new FCEvent
        {
            location = home.Tile,
            source = from,
            goods = [.. pawns],
            customDescription = won
                ? DeliveryNotification.ShuttleEventInjuredString
                : DeliveryNotification.ShuttleEventInjuredLostString,
            timeTillTrigger = Find.TickManager.TicksGame + travelTicks
        });
        string travelDays = ((float)travelTicks / GenDate.TicksPerDay).ToString("0.#");
        return "FCInjuredCaravanMembersReturning".Translate(pawns.Count, travelDays);
    }

    private static void TendFully(Pawn pawn)
    {
        int iterations = 0;
        while (pawn.health.HasHediffsNeedingTend())
        {
            if (++iterations > 10000)
            {
                LogUtil.Error($"SquadMapTeardownUtil: too many tend iterations on {pawn.LabelShort}.");
                break;
            }
            TendUtility.DoTend(null, pawn, null);
        }
    }

    /// <summary>
    /// Last-resort safety net: a battle map is about to be removed but still has live player pawns
    /// spawned on it. The keep-map-open mechanism (<see cref="BattlefieldContext.DeleteMap"/>'s
    /// container-aware check plus <c>WorldSettlementFC.ShouldRemoveMapNow</c>) is supposed to keep
    /// the map alive whenever a live player pawn is present, so any pawn caught here means an
    /// upstream gap slipped through. Logs a warning with enough detail to trace that gap, then
    /// delivers the pawns home instead of letting the map removal destroy them.
    /// </summary>
    public static void EvacuatePlayerPawns(Map map)
    {
        Faction player = Faction.OfPlayer;
        if (map is null || player is null) return;

        List<Pawn> toEvacuate = [];
        foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned.ToList())
        {
            if (pawn.Faction != player || pawn.Dead) continue;
            toEvacuate.Add(pawn);
        }
        if (toEvacuate.Count == 0) return;

        // Tripwire: reaching here should be impossible once the keep-map-open guards hold. Log
        // per-pawn so a future recurrence points at which teardown let the pawn through.
        foreach (Pawn pawn in toEvacuate)
        {
            LogUtil.Warning($"Player pawn '{pawn.LabelShortCap}' (downed={pawn.Downed}) was still on the " +
                $"battle map at tile {map.Tile} as it was being removed — the keep-map-open mechanism " +
                $"missed it. Delivering it home instead of destroying it; please report this as a teardown gap.");
        }

        List<Thing> goods = new(toEvacuate.Count);
        foreach (Pawn pawn in toEvacuate)
        {
            if (pawn.Spawned) pawn.DeSpawn();
            goods.Add(pawn);
        }
        DeliveryEvent.CreateDeliveryEvent(goods, map.Tile);
    }
}
