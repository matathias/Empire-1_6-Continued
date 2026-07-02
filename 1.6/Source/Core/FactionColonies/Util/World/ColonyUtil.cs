using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using Verse;

namespace FactionColonies.util
{
    public static class ColonyUtil
    {
        /// <summary>
        /// Picks the default settlement type for a tile based on its planet layer.
        /// Use when creating a settlement where the caller has no specific type in mind
        /// (capturing a vanilla NPC settlement, fallback for null input, etc.).
        /// </summary>
        public static WorldSettlementDef DefaultSettlementDefForTile(PlanetTile tile)
        {
            if (ModsConfig.OdysseyActive && tile.Valid && tile.Layer == Find.WorldGrid.Orbit)
            {
                return WorldSettlementDefOf.WorldSettlementDef_Orbital;
            }
            return WorldSettlementDefOf.WorldSettlementDef_Surface;
        }

        /// <summary>
        /// Per-stat founding value for a tile's biome: folds the biome's own statModifiers into the
        /// faction stat value. Shared by the Found screen and the outpost-conversion path so they
        /// compute identical costs. (Mirrors what CreateColonyWindowFc previously did inline.)
        /// </summary>
        public static double CombinedFoundingStat(FCStatDef stat, BiomeResourceDef biome, FactionFC faction)
        {
            double biomeVal = stat.IdentityValue;
            if (biome?.statModifiers != null)
            {
                foreach (FCStatModifier m in biome.statModifiers)
                {
                    if (m.stat == stat)
                        biomeVal = stat.aggregation == FCStatAggregation.Additive ? biomeVal + m.value : biomeVal * m.value;
                }
            }
            double fac = faction is object ? faction.GetStatValue(stat) : stat.IdentityValue;
            return stat.aggregation == FCStatAggregation.Additive ? fac + biomeVal : fac * biomeVal;
        }

        /// <summary>
        /// Silver cost to found a settlement of the given type/biome BEFORE the final
        /// settlementCostMultiplier. Exposed so callers can detect whether that multiplier
        /// actually changed the cost (used to fire policy "cost paid" hooks).
        /// </summary>
        public static int GetFoundingBaseCost(WorldSettlementDef type, BiomeResourceDef biome, FactionFC faction)
        {
            double mult = CombinedFoundingStat(FCStatDefOf.createSettlementMultiplier, biome, faction);
            double baseAdd = CombinedFoundingStat(FCStatDefOf.createSettlementBaseCost, biome, faction);
            return (int)(mult * (type.GetSettlementTypeExtension().GetCreationCost() + baseAdd));
        }

        /// <summary>
        /// Full silver cost to found a settlement of the given type at a tile with the given biome.
        /// </summary>
        public static int GetFoundingCost(WorldSettlementDef type, BiomeResourceDef biome, FactionFC faction)
        {
            int baseCost = GetFoundingBaseCost(type, biome, faction);
            return (int)(baseCost * CombinedFoundingStat(FCStatDefOf.settlementCostMultiplier, biome, faction));
        }

        public static WorldSettlementFC CreatePlayerColonySettlement(PlanetTile tile, WorldSettlementDef settlementType)
        {
            if (settlementType == null)
            {
                settlementType = DefaultSettlementDefForTile(tile);
                LogUtil.Error($"Tried to create a settlement with null WorldSettlementDef! Defaulting to {settlementType.defName} based on tile layer.");
            }

            /* Do any pre-settlement-creation demanded of the settlement type */
            settlementType.GetSettlementTypeExtension().PreCreation(ref tile, ref settlementType);

            LogUtil.Message($"Creating settlement of type {settlementType.defName}");
            Faction faction = FindFC.EmpireFaction;

            FactionFC worldcomp = FindFC.FactionComp;
            if (!worldcomp.settlements.Any())
            {
                FindFC.FactionComp.timeStart = Find.TickManager.TicksGame;
            }

            WorldSettlementFC settlement = (WorldSettlementFC)WorldObjectMaker.MakeWorldObject(DefDatabase<WorldSettlementDef>.GetNamed(settlementType.defName));
            settlement.PostPostMake(tile);

            settlement.SetFaction(faction);
            Find.WorldObjects.Add(settlement);

            worldcomp.AddSettlement(settlement);
            worldcomp.roadBuilder.FlagUpdateRoadQueues();

            /* Do any post-settlement-creation demanded of the settlement type */
            settlementType.GetSettlementTypeExtension().PostCreation(settlement);

            LifecycleRegistry.InvokeOnSettlementCreated(settlement);

            Find.LetterStack.ReceiveLetter("FCSettlementFormed".Translate(),
                "FCSettleEventCompletedDesc".Translate(settlement.Name, settlementType.LabelCap, tile.Tile.PrimaryBiome.LabelCap),
                LetterDefOf.PositiveEvent);

            return settlement;
        }

        /// <summary>
        /// Creates a player-owned Empire settlement on a just-conquered enemy tile and configures it
        /// the way a capture yields: named after the conquered base, level-boosted by the enemy's tech,
        /// and seeded with low starting morale/prosperity. Shared by the abstract capture-raid handler
        /// (<see cref="MilitaryJobHandler_Capture"/>) and the player-colony capture path so both produce
        /// an identical settlement. Callers are responsible for having already removed the old settlement
        /// world object at the tile.
        /// </summary>
        public static WorldSettlementFC SetupCapturedSettlement(PlanetTile tile, string name, TechLevel enemyTech)
        {
            WorldSettlementFC settlement = CreatePlayerColonySettlement(tile, DefaultSettlementDefForTile(tile));
            settlement.Name = name;

            int upgradeTimes;
            switch (enemyTech)
            {
                case TechLevel.Archotech:
                case TechLevel.Ultra:
                case TechLevel.Spacer:
                    upgradeTimes = 2;
                    break;
                case TechLevel.Industrial:
                    upgradeTimes = 1;
                    break;
                default:
                    upgradeTimes = 0;
                    break;
            }
            settlement.UpgradeSettlement(upgradeTimes);

            settlement.loyalty = 15;
            settlement.happiness = 25;
            settlement.unrest = 20;
            settlement.prosperity = 70;

            return settlement;
        }

        public static void RemovePlayerSettlement(WorldSettlementFC settlement)
        {
            settlement.settlementDef.GetSettlementTypeExtension()?.PreDestruction(settlement);
            settlement.PrepareDestroy();
            FactionFC faction = FindFC.FactionComp;

            // Squad-first: unassign any squads billeted here so they return to the pool rather
            // than dangling with a destroyed settlement reference. The faction-wide pool
            // survives settlement removal — squads aren't owned by settlements.
            MilitaryFC mfc = faction.military;
            if (mfc?.mercenarySquads is object)
            {
                foreach (MercenarySquadFC s in mfc.mercenarySquads)
                {
                    if (s is object && s.settlement == settlement)
                    {
                        s.settlement = null;
                        s.autoDefend = false;
                    }
                }
            }

            // Tear down buildings first so each building's comp/listener gets a proper OnDeconstruct.
            settlement.BuildingsComp?.DeconstructAllBuildings();

            LifecycleRegistry.InvokeOnSettlementRemoved(settlement);
            faction.settlements.Remove(settlement);

            // Clean up any pending bills for the destroyed settlement
            int removedBills = faction.taxLedger.RemoveBillsWhere(b => b.settlement == settlement);
            if (removedBills > 0)
                LogUtil.Message($"RemovePlayerSettlement: removed {removedBills} orphaned bill(s) for destroyed settlement {settlement.Name}");

            faction.DirtyFactionProfitCache();
            faction.DirtyAveragesCache();
            faction.roadBuilder.FlagUpdateRoadQueues();
            Messages.Message("FCSettlementRemoved".Translate(settlement.Name), MessageTypeDefOf.NegativeEvent);

            Find.WorldObjects.Remove(Find.World.worldObjects.WorldObjectOfDefAt(DefDatabase<WorldObjectDef>.GetNamed(settlement.def.defName), settlement.Tile));

            // Cancel any military operation involving this settlement. Walk the manager's ops
            // by participant rather than scanning FCEvents, since the op is the canonical owner
            // of the aggressor / defender / target relationships.
            MilitaryOperationManager manager = FindFC.MilitaryManager;
            if (manager?.active is object)
            {
                // Snapshot to avoid mutation during iteration: Resolve / ChangeDefendingMilitaryForce
                // unregister or replace ops in-place.
                var opSnapshot = new List<MilitaryOperation>(manager.active);
                foreach (MilitaryOperation op in opSnapshot)
                {
                    if (op is null) continue;

                    bool aggressorIsRemovedSettlement = op.aggressor?.homeSettlement == settlement;
                    bool defenderIsRemovedSettlement = op.defender?.homeSettlement == settlement;
                    bool targetIsRemovedSettlement = op.targetObject == settlement;

                    if (!aggressorIsRemovedSettlement && !defenderIsRemovedSettlement && !targetIsRemovedSettlement)
                        continue;

                    // Defender role and the target is a different (still-existing) settlement:
                    // reassign defense back to the target's own forces instead of cancelling.
                    if (defenderIsRemovedSettlement && !targetIsRemovedSettlement
                        && op.targetObject is WorldSettlementFC targetSettlement)
                    {
                        FCEvent warning = op.sourceEvents?.Find(e => e?.def == FCEventDefOf.settlementBeingAttacked);
                        if (warning is object)
                        {
                            MilitaryOperationsUtil.ChangeDefendingMilitaryForce(warning, targetSettlement);
                            continue;
                        }
                    }

                    // External raid target with the removed settlement as foreign defender: just
                    // cancel — the raid target stays alive.
                    if (defenderIsRemovedSettlement && !targetIsRemovedSettlement && op.targetObject is object)
                    {
                        IRaidTarget raidTarget = RaidTargetRegistry.FindByWorldObject(op.targetObject);
                        if (raidTarget != null) raidTarget.IsUnderAttack = false;
                    }

                    // Aggressor / target / external-defender cases: cancel the op entirely. Resolve
                    // also fires LifecycleRegistry hooks and removes the op from the manager's
                    // indices; sourceEvents will be GC'd when their handlers no-op against a
                    // missing op id.
                    foreach (FCEvent srcEvt in new List<FCEvent>(op.sourceEvents ?? new List<FCEvent>()))
                    {
                        if (srcEvt is object) faction.RemoveEvent(srcEvt);
                    }
                    op.Resolve();
                }
            }

            // Non-op event sweep: settlement-build / upgrade / cooldown / random-trait events.
            HashSet<FCEvent> toRemove = new HashSet<FCEvent>();
            foreach (FCEvent evt in faction.Events)
            {
                if (evt is null) continue;
                if (evt.HasLinkedOperation) continue; // op-linked events already handled above

                if (evt.def == FCEventDefOf.constructBuilding || evt.def == FCEventDefOf.enactSettlementPolicy
                    || evt.def == FCEventDefOf.upgradeSettlement || evt.def == FCEventDefOf.cooldownMilitary)
                {
                    if (evt.source == settlement.Tile) toRemove.Add(evt);
                }

                if (evt.def.isRandomEvent && evt.settlementTraitLocations.Count > 0)
                {
                    if (evt.settlementTraitLocations.Contains(settlement))
                    {
                        evt.settlementTraitLocations.Remove(settlement);
                        if (evt.settlementTraitLocations.Count == 0) toRemove.Add(evt);
                    }
                }

                // Let extensions cancel their own custom events
                if (!toRemove.Contains(evt))
                {
                    FCEventHandlerExtension handler = evt.def.GetModExtension<FCEventHandlerExtension>();
                    if (handler != null && handler.ShouldCancelOnSettlementRemoval(evt, settlement))
                    {
                        toRemove.Add(evt);
                    }
                }
            }

            bool anyRemoved = false;
            foreach (FCEvent evt in toRemove)
            {
                if (faction.RemoveEvent(evt)) anyRemoved = true;
            }
            if (anyRemoved)
            {
                faction.InvalidateFactionStatCache();
            }

            // Drop this settlement's situations and their stat-modifier sources (idempotent — a
            // self-destroying terminal situation may already be gone).
            faction.situationManager?.OnSettlementRemoved(settlement);
        }
        public static Faction CreatePlayerColonyFaction()
        {
            FactionFC worldcomp = FindFC.FactionComp;
            if (worldcomp == null)
            {
                LogUtil.Error("FactionFC world component is missing! Cannot create player colony faction.");
                return null;
            }
            LogUtil.Message("Creating new player faction");
            worldcomp.SetCapital();

            FactionDef facDef = DefDatabase<FactionDef>.GetNamed("PColony");
            Faction faction = new Faction
            {
                def = facDef
            };
            faction.def.techLevel = Faction.OfPlayer.def.techLevel;
            faction.loadID = Find.UniqueIDsManager.GetNextFactionID();
            faction.colorFromSpectrum = FactionGenerator.NewRandomColorFromSpectrum(faction);
            faction.Name = "FCPlayerColony".Translate();
            faction.def.classicIdeo = Faction.OfPlayer.def.classicIdeo;
            // Inherit only the player's primary ideology, in an independent tracker
            // (assigning Faction.OfPlayer.ideos wholesale would copy every minor ideo
            // and share the player's tracker instance by reference).
            faction.ideos = new FactionIdeosTracker(faction);
            Ideo playerPrimary = Faction.OfPlayer.ideos?.PrimaryIdeo;
            if (playerPrimary is object)
            {
                faction.ideos.SetPrimary(playerPrimary);
            }

            worldcomp.DirtyTechLevelCache();
            //<DevAdd> Copy player faction relationships  
            foreach (Faction other in Find.FactionManager.AllFactionsListForReading)
            {
                faction.TryMakeInitialRelationsWith(other);
            }
            // Set starting goodwill to Player
            faction.TryAffectGoodwillWith(Faction.OfPlayer, 200);

            // Generate Leader
            if (faction.leader == null || faction.leader.Dead)
            {
                CreatePlayerFactionLeader(faction);
            }

            Find.FactionManager.Add(faction);
            RelationsUtilFC.ResetPlayerColonyRelations();
            worldcomp.OnCreation();
            return faction;
        }

        public static bool CreatePlayerFactionLeader(Faction faction)
        {
            bool success = true;
            bool leaderGenerated = false;
            try
            {
                leaderGenerated = faction.TryGenerateNewLeader();
            }
            catch (System.Exception ex)
            {
                LogUtil.Warning("TryGenerateNewLeader threw an exception. Falling back to manual generation. Exception: " + ex);
            }

            if (!leaderGenerated)
            {
                LogUtil.Warning("TryGenerateNewLeader failed. Falling back to manual generation.");
                PawnKindDef fallbackKind = faction.RandomPawnKind();
                if (fallbackKind is null)
                {
                    fallbackKind = PawnKindDefOf.Villager;
                    LogUtil.Warning("RandomPawnKind returned null. Using Villager as last-resort fallback.");
                }
                LogUtil.Message($"Fallback pawnkind: {fallbackKind?.defName ?? "null"}");
                faction.leader = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind: fallbackKind,
                faction: faction, context: PawnGenerationContext.NonPlayer,
                forceGenerateNewPawn: true, allowDead: false, allowDowned: false,
                canGeneratePawnRelations: true, mustBeCapableOfViolence: true, colonistRelationChanceFactor: 0,
                forceAddFreeWarmLayerIfNeeded: false, worldPawnFactionDoesntMatter: false));
                if (faction.leader == null)
                {
                    LogUtil.Warning("Fallback leader generation also failed!");
                    success = false;
                }
                else
                {
                    if (!Find.WorldPawns.Contains(faction.leader))
                    {
                        Find.WorldPawns.PassToWorld(faction.leader, PawnDiscardDecideMode.KeepForever);
                    }
                    LogUtil.Message($"Created leader {faction.leader.Name} ({faction.leader.ThingID}), " +
                                    $"pawnKind: {faction.leader.kindDef?.defName ?? "null"}, " +
                                    $"title: {faction.LeaderTitle}, " +
                                    $"ideo: {faction.leader.Ideo?.name ?? "none"}, " +
                                    $"faction: {faction.Name}");
                }
            }
            else
            {
                LogUtil.Message($"TryGenerateNewLeader succeeded. Leader: {faction.leader?.Name} ({faction.leader?.ThingID}), " +
                                $"pawnKind: {faction.leader?.kindDef?.defName ?? "null"}, " +
                                $"title: {faction.LeaderTitle}, " +
                                $"ideo: {faction.leader?.Ideo?.name ?? "none"}");
            }

            return success;
        }
    }
}
