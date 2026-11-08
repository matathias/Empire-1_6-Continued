namespace FactionColonies;

/// <summary>
/// Shared helpers for the DESTRUCTIVE test tier (see <see cref="EmpireDestructiveTestAttribute"/>).
/// No snapshot/restore: destructive tests thrash live state and are not cleaned up. These helpers
/// (a) FORCE preconditions (tests never skip, so missing state is created, and setup that cannot be
/// built fails the test), (b) provide the create/destroy recipe (also used by
/// <c>DebugUtil.CreateTenRandomSettlements</c>), and (c) supply the post-mutation invariant battery
/// that asserts the faction is still internally consistent after the thrash.
/// </summary>
public static class DestructiveTestUtil
{
    /// <summary>Returns the live faction component. It exists in every loaded game, and the runner
    /// only runs Playing tests in a loaded game, so a null here is a real failure.</summary>
    public static FactionFC RequireFaction() =>
        TestAssert.NotNull(FindFC.FactionComp, "No FactionFC world component in a loaded game");

    /// <summary>Returns the empire's NPC faction, creating it (as founding the first settlement
    /// does) if the player has not founded one yet.</summary>
    public static Faction EnsureEmpireFaction()
    {
        RequireFaction();
        Faction faction = FindFC.EmpireFaction ?? ColonyUtil.CreatePlayerColonyFaction();
        return TestAssert.NotNull(faction, "Could not create the empire faction");
    }

    /// <summary>Raises the faction to at least <paramref name="level"/> (capped at
    /// <see cref="FactionFC.MaxFactionLevel"/>) without the level-up letters, so level-gated
    /// features such as edict categories are unlocked. Never lowers the level.</summary>
    public static void EnsureFactionLevel(int level)
    {
        FactionFC f = RequireFaction();
        level = Math.Min(level, FactionFC.MaxFactionLevel);
        if (f.factionLevel >= level) return;

        f.factionLevel = level;
        f.factionXPCurrent = 0;
        f.factionXPGoal = f.UpdateFactionLevelGoalXP(level);
    }

    /*-*-*-*-*/
    /* Settlements */
    /*-*-*-*-*/

    /// <summary>
    /// Creates a real player settlement on a randomly-found valid surface tile, reusing the same
    /// recipe as the "Create 10 Random Settlements" debug action. Returns null if no valid tile
    /// was found within <paramref name="maxAttempts"/>; prefer <see cref="RequireTransientSettlement"/>,
    /// which fails the test instead. This mutates live world state.
    /// </summary>
    public static WorldSettlementFC CreateTransientSettlement(int maxAttempts = 500, WorldSettlementDef def = null)
    {
        def ??= WorldSettlementDefOf.WorldSettlementDef_Surface;
        EnsureEmpireFaction();

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            if (TryFindSettleableTile(def, out PlanetTile tile))
                return ColonyUtil.CreatePlayerColonySettlement(tile, def);
        }
        return null;
    }

    /// <summary>
    /// One attempt at a random surface tile where a <paramref name="def"/> settlement may be founded:
    /// a valid settlement tile, accepted by <see cref="WorldTileChecker"/>, with no settlement caravan
    /// already heading there. Callers loop over this for as many attempts as they allow.
    /// </summary>
    public static bool TryFindSettleableTile(WorldSettlementDef def, out PlanetTile tile)
    {
        tile = TileFinder.RandomSettlementTileFor(Find.WorldGrid.Surface, FindFC.EmpireFaction);
        if (!tile.Valid) return false;
        if (!WorldTileChecker.IsValidTileForNewSettlement(tile, def)) return false;
        return FindFC.FactionComp?.CheckSettlementCaravansList(tile) != true;
    }

    /// <summary><see cref="CreateTransientSettlement"/> that fails the test if no settlement could be created.</summary>
    public static WorldSettlementFC RequireTransientSettlement(WorldSettlementDef def = null) =>
        TestAssert.NotNull(CreateTransientSettlement(def: def),
            $"Could not create a transient {(def ?? WorldSettlementDefOf.WorldSettlementDef_Surface).defName} settlement");

    /// <summary>
    /// Returns the first existing settlement matching <paramref name="match"/> (any settlement if
    /// null), or creates a new one of type <paramref name="def"/> when none matches. A freshly
    /// created settlement that still fails <paramref name="match"/> fails the test.
    /// </summary>
    public static WorldSettlementFC EnsureSettlement(Predicate<WorldSettlementFC> match = null, WorldSettlementDef def = null)
    {
        FactionFC f = RequireFaction();
        WorldSettlementFC existing = f.settlements?.FirstOrDefault(s => s is not null && (match is null || match(s)));
        if (existing is not null) return existing;

        WorldSettlementFC created = RequireTransientSettlement(def);
        if (match is not null && !match(created))
            TestAssert.Fail($"A newly created settlement '{created.Name}' does not meet the test's precondition");
        return created;
    }

    /// <summary>Returns at least <paramref name="count"/> distinct settlements, creating transient
    /// ones to make up any shortfall.</summary>
    public static List<WorldSettlementFC> EnsureSettlements(int count, WorldSettlementDef def = null)
    {
        FactionFC f = RequireFaction();
        List<WorldSettlementFC> result = f.settlements?.Where(s => s is not null).Take(count).ToList() ?? [];
        while (result.Count < count)
            result.Add(RequireTransientSettlement(def));
        return result;
    }

    /// <summary>
    /// Returns the first NPC settlement hostile to the empire that matches <paramref name="match"/>,
    /// or founds a new one for a visible humanlike faction hostile to the empire (e.g. pirates) on
    /// a random surface tile. Fails if no such faction exists or the new settlement does not match.
    /// </summary>
    public static Settlement EnsureHostileSettlement(Predicate<Settlement> match = null)
    {
        Faction empire = EnsureEmpireFaction();
        Settlement existing = Find.WorldObjects.Settlements.FirstOrDefault(s =>
            s.Faction is not null && s.Faction != empire && !s.Faction.IsPlayer
            && s.Faction.HostileTo(empire) && (match is null || match(s)));
        if (existing is not null) return existing;

        Faction enemy = TestAssert.NotNull(Find.FactionManager.AllFactionsListForReading.FirstOrDefault(fac =>
                fac is not null && !fac.IsPlayer && fac != empire && !fac.Hidden && !fac.defeated
                && fac.def.humanlikeFaction && fac.HostileTo(empire)),
            "No visible humanlike faction is hostile to the empire, so no hostile settlement can be founded");

        PlanetTile tile = TileFinder.RandomSettlementTileFor(Find.WorldGrid.Surface, enemy);
        TestAssert.IsTrue(tile.Valid, $"No valid tile to found a settlement for {enemy.Name}");

        Settlement created = (Settlement)WorldObjectMaker.MakeWorldObject(WorldObjectDefOf.Settlement);
        created.SetFaction(enemy);
        created.Tile = tile;
        created.Name = SettlementNameGenerator.GenerateSettlementName(created);
        Find.WorldObjects.Add(created);

        if (match is not null && !match(created))
            TestAssert.Fail($"A newly founded hostile settlement '{created.Name}' does not meet the test's precondition");
        return created;
    }

    /*-*-*-*-*/
    /* Silver, pawns, military */
    /*-*-*-*-*/

    /// <summary>
    /// Tops the player's stored silver (what <see cref="PaymentUtil.GetSilver"/> counts) up to at
    /// least <paramref name="amount"/> by spawning silver into a new stockpile zone on a player home
    /// map. Fails the test if there is no home map or not enough free cells.
    /// </summary>
    public static void EnsureSilver(int amount)
    {
        int shortfall = amount - PaymentUtil.GetSilver();
        if (shortfall <= 0) return;

        Map map = TestAssert.NotNull(Find.AnyPlayerHomeMap, "EnsureSilver: no player home map to store silver on");
        int stackLimit = Math.Max(1, ThingDefOf.Silver.stackLimit);
        Zone_Stockpile zone = new(StorageSettingsPreset.DefaultStockpile, map.zoneManager);
        map.zoneManager.RegisterZone(zone);

        foreach (IntVec3 cell in GenRadial.RadialCellsAround(map.Center, GenRadial.MaxRadialPatternRadius - 1f, true))
        {
            if (shortfall <= 0) break;
            if (!cell.InBounds(map) || !cell.Standable(map) || cell.GetEdifice(map) is not null) continue;
            if (map.zoneManager.ZoneAt(cell) is not null || cell.GetFirstItem(map) is not null) continue;

            zone.AddCell(cell);
            Thing silver = ThingMaker.MakeThing(ThingDefOf.Silver);
            silver.stackCount = Math.Min(stackLimit, shortfall);
            GenSpawn.Spawn(silver, cell, map);
            shortfall -= silver.stackCount;
        }

        TestAssert.IsTrue(PaymentUtil.GetSilver() >= amount,
            $"EnsureSilver: could only reach {PaymentUtil.GetSilver()} of {amount} stored silver");
    }

    /// <summary>Generates a fresh pawn and passes it to the world (so it is a valid world pawn and
    /// reference target). Defaults to an empire fighter of the empire faction.</summary>
    public static Pawn GenerateWorldPawn(PawnKindDef kind = null, Faction faction = null)
    {
        faction ??= EnsureEmpireFaction();
        kind ??= PColonyPawnKindDefOf.PColony_Fighter;
        Pawn pawn = TestAssert.NotNull(
            PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, faction, forceGenerateNewPawn: true)),
            $"Could not generate a {kind.defName} pawn");
        Find.WorldPawns.PassToWorld(pawn);
        return pawn;
    }

    /// <summary>Returns the first squad template with at least one non-blank unit, designing a
    /// one-unit "Test Squad" (and its unit) if the player has none.</summary>
    public static MilSquadFC EnsureSquadTemplate()
    {
        RequireFaction();
        MilitaryFC mil = TestAssert.NotNull(FindFC.Military, "No MilitaryFC on the faction");
        if (mil.squads is null) mil.ResetSquads();

        MilSquadFC existing = mil.squads.FirstOrDefault(s => s?.Units?.Any(u => u is not null && !u.isBlank) == true);
        if (existing is not null) return existing;

        EnsureEmpireFaction();
        MilUnitFC unit = MilTemplateFactory.CreateUnit(false);
        unit.name = "Test Unit";
        mil.units.Add(unit);

        MilSquadFC squad = MilTemplateFactory.CreateSquad(true);
        squad.name = "Test Squad";
        squad.NewSquad();
        squad.SetUnit(0, unit);
        mil.squads.Add(squad);
        return squad;
    }

    /// <summary>Tops up stored silver so <see cref="MilitaryFC.HireSquad"/> can afford
    /// <paramref name="template"/>, for tests that call HireSquad themselves.</summary>
    public static void EnsureSilverToHire(MilSquadFC template) =>
        EnsureSilver((int)Math.Round(template.GetEquipmentTotalCost() * FCSettings.squadHireCostMultiplier) + 1);

    /// <summary>Hires a squad from <paramref name="template"/> (default: <see cref="EnsureSquadTemplate"/>),
    /// first topping up silver to cover the hire cost. The squad lands in the unassigned pool.</summary>
    public static MercenarySquadFC EnsureHiredSquad(MilSquadFC template = null)
    {
        template ??= EnsureSquadTemplate();
        EnsureSilverToHire(template);
        return TestAssert.NotNull(FindFC.Military.HireSquad(template), $"HireSquad('{template.name}') returned null");
    }

    /// <summary>Removes a settlement via the real teardown path, swallowing+logging any exception
    /// so cleanup-as-an-action never masks the test's actual result.</summary>
    public static void SafeRemoveSettlement(WorldSettlementFC settlement)
    {
        if (settlement is null) return;
        try
        {
            ColonyUtil.RemovePlayerSettlement(settlement);
        }
        catch (Exception ex)
        {
            LogUtil.Warning($"DestructiveTestUtil.SafeRemoveSettlement threw (ignored): {ex}");
        }
    }

    /// <summary>Resolves a hand-built (un-managed) military operation and drops any events it
    /// scheduled, so it doesn't linger as a phantom recurring event. Swallows errors so cleanup
    /// never masks the test result.</summary>
    public static void ResolveAndSweepOp(MilitaryOperation op)
    {
        if (op is null) return;
        try { op.Resolve(); }
        catch (Exception ex) { LogUtil.Warning($"ResolveAndSweepOp Resolve threw (ignored): {ex}"); }
        if (op.sourceEvents is not null)
        {
            foreach (FCEvent e in op.sourceEvents.ToList())
            {
                try { FindFC.EventManager?.Remove(e); }
                catch (Exception ex) { LogUtil.Warning($"ResolveAndSweepOp event remove threw (ignored): {ex}"); }
            }
        }
    }

    /// <summary>
    /// The post-mutation assertion battery. After a destructive test has thrashed state, this
    /// asserts the faction is still internally consistent: existing engine validators run without
    /// throwing, caches recompute, and there are no null/duplicate settlements, dangling bills, or
    /// orphaned military operations. Every engine call is wrapped so a throw becomes a structured
    /// FAIL rather than an uncaught crash.
    /// </summary>
    public static void AssertEmpireInvariants(FactionFC f, string context)
    {
        if (f is null) return;

        // Existing engine validators must survive the thrashed state.
        TestAssert.DoesNotThrow(() => f.ValidateSettlementCaravansList(),
            $"{context}: ValidateSettlementCaravansList threw");
        TestAssert.DoesNotThrow(() => FindFC.Military?.CheckMilitaryUtilForErrors(),
            $"{context}: CheckMilitaryUtilForErrors threw");
        TestAssert.DoesNotThrow(() => FindFC.MilitaryManager?.RebuildIndices(),
            $"{context}: MilitaryManager.RebuildIndices threw");

        // Caches must recompute cleanly after the mutations.
        TestAssert.DoesNotThrow(() =>
        {
            f.DirtyFactionProfitCache();
            f.DirtyAveragesCache();
            f.DirtyTechLevelCache();
            f.InvalidateFactionStatCache();
            if (double.IsNaN(f.profit)) LogUtil.Warning($"{context}: faction.profit is NaN after thrash");
        }, $"{context}: cache recompute threw");

        // No null / duplicate settlements.
        List<WorldSettlementFC> settlements = f.settlements;
        if (settlements is not null)
        {
            TestAssert.IsFalse(settlements.Any(s => s is null),
                $"{context}: faction.settlements contains a null entry");
            TestAssert.AreEqual(settlements.Count, settlements.Distinct().Count(),
                $"{context}: faction.settlements contains duplicates");

            // No tax bill references a settlement that is no longer part of the faction.
            if (FindFC.TaxLedger?.Bills is not null)
            {
                foreach (BillFC bill in FindFC.TaxLedger.Bills)
                {
                    if (bill?.settlement is null) continue;
                    TestAssert.IsTrue(settlements.Contains(bill.settlement),
                        $"{context}: tax bill references settlement '{bill.settlement.Name}' not in faction.settlements");
                }
            }

            // No active military operation references a home settlement / squad that no longer exists.
            MilitaryOperationManager mgr = FindFC.MilitaryManager;
            List<MercenarySquadFC> squads = FindFC.Military?.mercenarySquads;
            if (mgr?.Active is not null)
            {
                foreach (MilitaryOperation op in mgr.Active)
                {
                    if (op is null) continue;
                    AssertHomeSettlementLive(op.aggressor?.homeSettlement, settlements, context, "aggressor");
                    AssertHomeSettlementLive(op.defender?.homeSettlement, settlements, context, "defender");
                    AssertSquadLive(op.aggressor?.squad, squads, context, "aggressor");
                    AssertSquadLive(op.defender?.squad, squads, context, "defender");
                }
            }
        }
    }

    private static void AssertHomeSettlementLive(WorldSettlementFC home,
        List<WorldSettlementFC> settlements, string context, string side)
    {
        if (home is null) return;
        TestAssert.IsTrue(settlements.Contains(home),
            $"{context}: active op {side}.homeSettlement '{home.Name}' is not in faction.settlements (orphan)");
    }

    private static void AssertSquadLive(MercenarySquadFC squad,
        List<MercenarySquadFC> squads, string context, string side)
    {
        if (squad is null || squads is null) return;
        TestAssert.IsTrue(squads.Contains(squad),
            $"{context}: active op {side}.squad '{squad.DisplayName}' is not in the mercenary pool (orphan)");
    }
}
