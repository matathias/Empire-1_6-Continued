using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace FactionColonies.util
{
    /* Caravan / drop pod / shuttle spawning for FCEvent-driven deliveries. Each Send*
       path resolves the destination via FindFC.TaxMap, emits the
       delivery letter/message via DeliveryNotification, and falls back to the tax spot
       on failure. DoDelayCaravanDueToDanger and the shuttle landing-zone check both
       reschedule via DeliveryEvent.CreateDeliveryEvent when the world isn't ready. */
    public static class DeliveryLogistics
    {
        public static TraverseParms DeliveryTraverseParms => new TraverseParms()
        {
            canBashDoors = false,
            canBashFences = false,
            alwaysUseAvoidGrid = false,
            fenceBlocked = false,
            maxDanger = Danger.Deadly,
            mode = TraverseMode.ByPawn
        };

        public static TaxDeliveryMode TaxDeliveryModeForSettlement(bool canUseShuttle, PlanetTile sourceTile)
        {
            WorldSettlementFC settlement = FindFC.Settlements.FirstOrFallback((WorldSettlementFC s) => s.Tile == sourceTile);
            if (settlement != null)
            {
                return settlement.settlementDef.GetTaxDeliveryMode(canUseShuttle, sourceTile);
            }
            LogUtil.Error($"Trying to deliver taxes for a null settlement!");
            return TaxDeliveryMode.Caravan;
        }

        public static void SendShuttle(FCEvent evt)
        {
            if (!ModsConfig.RoyaltyActive)
            {
                LogUtil.Warning("Shuttle delivery requires Royalty DLC. Falling back to drop pod.");
                SendDropPod(evt);
                return;
            }

            Map playerHomeMap = FindFC.TaxMap;
            List<ShipLandingArea> landingZones = ShipLandingBeaconUtility.GetLandingZones(playerHomeMap);

            IntVec3 landingCell = DropCellFinder.GetBestShuttleLandingSpot(playerHomeMap, Faction.OfPlayer);

            if (!landingZones.Any() || landingZones.Any(zone => zone.Clear))
            {
                DeliveryNotification.MakeDeliveryLetterAndMessage(evt);
                Thing shuttle = ThingMaker.MakeThing(ThingDefOf.Shuttle);
                TransportShip transportShip = TransportShipMaker.MakeTransportShip(TransportShipDefOf.Ship_Shuttle, evt.goods, shuttle);

                transportShip.ArriveAt(landingCell, playerHomeMap.Parent);
                transportShip.AddJobs(new ShipJobDef[]
                {
                                ShipJobDefOf.Unload,
                                ShipJobDefOf.FlyAway
                });
            }
            else
            {
                if (!evt.isDelayed)
                {
                    Messages.Message(((string)"FCShuttleLandingBlockedWithItems".Translate(evt.goods.ToLetterString())).Replace("\n", " "), MessageTypeDefOf.RejectInput);
                    evt.isDelayed = true;
                }

                if (!evt.source.Valid) evt.source = playerHomeMap.Tile;

                evt.timeTillTrigger = Find.TickManager.TicksGame + 1000;
                DeliveryEvent.CreateDeliveryEvent(evt);
            }
        }

        public static void SendDropPod(FCEvent evt)
        {
            Map playerHomeMap = FindFC.TaxMap;
            DeliveryNotification.MakeDeliveryLetterAndMessage(evt);
            IntVec3 dropCell;
            if (!PaymentUtil.CheckForTaxSpot(playerHomeMap, out dropCell))
            {
                dropCell = DropCellFinder.TradeDropSpot(playerHomeMap);
            }
            DropPodUtility.DropThingsNear(dropCell, playerHomeMap, evt.goods, 110, false, false, false, false);
        }

        public static bool DoDelayCaravanDueToDanger(FCEvent evt)
        {
            Map playerHomeMap = FindFC.TaxMap;
            if (playerHomeMap.dangerWatcher.DangerRating != StoryDanger.None)
            {

                if (!evt.isDelayed)
                {
                    Messages.Message(((string)"FCCaravanDangerTooHighWithItems".Translate(evt.goods.ToLetterString())).Replace("\n", " "), MessageTypeDefOf.RejectInput);
                    evt.isDelayed = true;
                }

                if (!evt.source.Valid) evt.source = playerHomeMap.Tile;

                evt.timeTillTrigger = Find.TickManager.TicksGame + 1000;
                DeliveryEvent.CreateDeliveryEvent(evt);
                return true;
            }

            return false;
        }

        public static void SendCaravan(FCEvent evt)
        {
            Map playerHomeMap = FindFC.TaxMap;
            if (DoDelayCaravanDueToDanger(evt)) return;

            DeliveryNotification.MakeDeliveryLetterAndMessage(evt);
            List<Pawn> pawns = new List<Pawn>();
            List<Pawn> securityGuards = new List<Pawn>();

            // Generate delivery pawns using allowed xenotypes first. Every iteration consumes exactly one
            // good (carried by a pawn, or placed directly on any failure), so the loop terminates in
            // evt.goods.Count iterations. No artificial attempt cap — the old cap counted successful
            // carries too, stranding (and silently destroying) every stack past the 100th.
            while (evt.goods.Count > 0)
            {
                try
                {
                    Pawn deliveryPawn = null;
                    // We set the xenotype in a prefix patch on GeneratePawn, so don't bother with setting the xenotype here
                    try
                    {
                        // Create request that allows ANY xenotype (including non-violent ones)
                        var request = FCPawnGenerator.CivilianRequest();

                        deliveryPawn = PawnGenerator.GeneratePawn(request);
                        if (deliveryPawn != null)
                        {
                            LogUtil.Message($"Successfully generated delivery pawn with civilian request");
                        }
                    }
                    catch (Exception ex)
                    {
                        // Guarantee progress so the loop can't spin: place this good directly rather than retrying.
                        LogUtil.Warning($"Failed to generate pawn with civilian request: {ex.Message}");
                        Thing lostItem = evt.goods[0];
                        evt.goods.RemoveAt(0);
                        PaymentUtil.PlaceThing(lostItem);
                        continue;
                    }

                    // If no xenotype worked, try a simple delivery request
                    if (deliveryPawn == null)
                    {
                        LogUtil.Warning("Failed to generate pawn with CivilianRequest, trying simple delivery request");
                        deliveryPawn = PawnGenerator.GeneratePawn(FCPawnGenerator.SimpleDeliveryRequest());
                    }

                    // If still no pawn, fall back to animals (like wolves)
                    if (deliveryPawn == null)
                    {
                        LogUtil.Warning("Failed to generate human pawn, falling back to animals");
                        var combatPool = FindFC.FactionComp?.animalFilter?.AllowedCombatAnimals ?? FactionCache.AllCombatAnimalKindDefs;
                        var availableAnimals = combatPool
                            .OrderByDescending(def => def.combatPower)
                            .Take(5)
                            .ToList();

                        if (availableAnimals.Any())
                        {
                            var animalRequest = FCPawnGenerator.AnimalRequest(availableAnimals.RandomElement());
                            deliveryPawn = PawnGenerator.GeneratePawn(animalRequest);
                        }
                    }

                    if (deliveryPawn == null)
                    {
                        LogUtil.Error("Could not generate any pawn for delivery, placing item on tax spot");
                        Thing lostItem = evt.goods[0];
                        evt.goods.RemoveAt(0);
                        PaymentUtil.PlaceThing(lostItem);
                        continue;
                    }

                    Thing next = evt.goods.First();

                    if (deliveryPawn.carryTracker.innerContainer.TryAdd(next))
                    {
                        evt.goods.Remove(next);
                    }
                    else
                    {
                        // Pawn can't carry this item (e.g. capacity issue) — place on tax spot instead of losing it
                        LogUtil.Warning($"Delivery pawn could not carry {next.LabelCap}, placing on tax spot");
                        evt.goods.Remove(next);
                        PaymentUtil.PlaceThing(next);
                    }

                    pawns.Add(deliveryPawn);
                }
                catch (Exception ex)
                {
                    LogUtil.Error($"Error generating pawn for delivery: {ex.Message}");
                    if (evt.goods.Count > 0)
                    {
                        Thing lostItem = evt.goods[0];
                        evt.goods.RemoveAt(0);
                        PaymentUtil.PlaceThing(lostItem);
                    }
                }
            }

            LogUtil.Message($"# Delivery pawns generated: {pawns.Count}");

            // Backstop: the loop above consumes every good, but never silently lose any that slipped through.
            for (int i = evt.goods.Count - 1; i >= 0; i--)
            {
                PaymentUtil.PlaceThing(evt.goods[i]);
                evt.goods.RemoveAt(i);
            }

            // Always add at least one guard animal for protection, plus extra if caravan is small
            LogUtil.Message("Adding guard animals for delivery caravan protection");

            // Add extra capable pawns using allowed xenotypes if caravan is small
            if (pawns.Count < 3)
            {
                int extraPawnsNeeded = 3 - pawns.Count;
                for (int i = 0; i < extraPawnsNeeded; i++)
                {
                    try
                    {
                        Pawn extraPawn = null;
                        // We set the xenotype in a prefix patch on GeneratePawn, so don't bother with setting the xenotype here
                        // Create request that allows ANY xenotype (including non-violent ones)
                        var request = FCPawnGenerator.CivilianRequest();
                        extraPawn = PawnGenerator.GeneratePawn(request);

                        // Fallback to simple request
                        if (extraPawn == null)
                        {
                            extraPawn = PawnGenerator.GeneratePawn(FCPawnGenerator.SimpleDeliveryRequest());
                        }

                        if (extraPawn != null)
                        {
                            pawns.Add(extraPawn);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogUtil.Warning($"Failed to spawn extra pawn: {ex.Message}");
                    }
                }
            }

            // Add guard animals (like wolves) for protection - always add at least 2 as it's good protection! Keep your highmate-only faction safe!!
            // This protects deliveries by keeping it immersive, adhering to xenotype preferences. Bears and wargs are problematic.
            var guardPool = FindFC.FactionComp?.animalFilter?.AllowedCombatAnimals ?? FactionCache.AllCombatAnimalKindDefs;
            var guardAnimals = guardPool
                .OrderByDescending(def => def.combatPower)
                .Take(5); // Take more options to ensure we can get 2 guards

            // Log available guard animals for debugging
            var availableGuardAnimals = guardAnimals.ToList();
            if (availableGuardAnimals.Any())
            {
                LogUtil.Message($"Available guard animals: {string.Join(", ", availableGuardAnimals.Select(a => $"{a.label} (Combat: {a.combatPower:F0})"))}");
            }

            int guardsAdded = 0;
            int loopCount = 0;
            int loopGuard = 10;
            while (guardsAdded < 2 && availableGuardAnimals.Any() && loopCount < loopGuard)
            {
                PawnKindDef guardAnimal = availableGuardAnimals.RandomElement();
                try
                {
                    Pawn guard = PawnGenerator.GeneratePawn(FCPawnGenerator.AnimalRequest(guardAnimal));
                    if (guard is object)
                    {
                        securityGuards.Add(guard);
                        guardsAdded++;
                        LogUtil.Message($"Added guard animal: {guardAnimal.label} (Combat Power: {guardAnimal.combatPower:F0})");
                    }
                }
                catch (Exception ex)
                {
                    LogUtil.Warning($"Failed to spawn security guard {guardAnimal.label}: {ex}");
                    availableGuardAnimals.Remove(guardAnimal);
                }

                loopCount++;
            }

            // Combine all pawns
            pawns.AddRange(securityGuards);

            if (pawns.Count == 0)
            {
                LogUtil.Warning("No caravan pawns could be generated. All items delivered via direct placement.");
                return;
            }

            PawnsArrivalModeWorker_EdgeWalkIn pawnsArrivalModeWorker = new PawnsArrivalModeWorker_EdgeWalkIn();
            IncidentParms parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.Misc, playerHomeMap);
            parms.spawnRotation = Rot4.FromAngleFlat((((Map)parms.target).Center - parms.spawnCenter).AngleFlat);

            RCellFinder.TryFindRandomPawnEntryCell(out parms.spawnCenter, playerHomeMap, CellFinder.EdgeRoadChance_Friendly);

            pawnsArrivalModeWorker.Arrive(pawns, parms);

            // Create the lord and ensure all pawns (including animals) are properly assigned
            var lord = LordMaker.MakeNewLord(FCPawnGenerator.WorkerOrMilitaryRequest().Faction, new LordJob_DeliverSupplies(parms.spawnCenter), playerHomeMap, pawns);

            // Ensure all guard animals are properly assigned to the lord and will follow the caravan
            foreach (var guard in securityGuards)
            {
                if (guard != null && guard.Map == playerHomeMap)
                {
                    // Make sure the animal is assigned to the lord
                    if (guard.GetLord() != lord)
                    {
                        lord.AddPawn(guard);
                    }

                    // Ensure the animal will leave with the caravan by setting it to follow a human pawn
                    if (guard.mindState != null)
                    {
                        var humanPawn = lord.ownedPawns.FirstOrDefault(p => !p.RaceProps.Animal);
                        if (humanPawn != null)
                        {
                            // let's make sure the guard animal stays close to the caravan
                            guard.mindState.duty = new PawnDuty(DutyDefOf.Follow, humanPawn, 3f); // 3 tile radius
                        }
                    }
                }
            }

        }

        public static void SpawnOnTaxSpot(FCEvent evt)
        {
            DeliveryNotification.MakeDeliveryLetterAndMessage(evt);
            evt.goods.ForEach(thing => PaymentUtil.PlaceThing(thing));
        }

        public static IntVec3 GetDeliveryCell(TraverseParms traverseParms, Map map)
        {
            if (!PaymentUtil.CheckForTaxSpot(map, out IntVec3 intVec3))
            {
                intVec3 = ValidLandingCell(new IntVec2(1, 1), map, true);
            }

            // Validate that we have a valid starting position
            if (!intVec3.IsValid)
            {
                // Fallback to map center if we somehow got an invalid position
                intVec3 = map.Center;
            }

            IntVec3 oldVec = intVec3;
            for (int i = 0; i < 10; i++)
            {
                // Additional validation before calling CellFinder
                if (intVec3.IsValid && intVec3.InBounds(map))
                {
                    if (CellFinder.TryFindRandomReachableNearbyCell(intVec3, map, i, traverseParms,
                        cell => cell.IsValid && cell.InBounds(map) && map.thingGrid.ThingsAt(cell) != null,
                        null, out IntVec3 foundCell))
                    {
                        if (foundCell.IsValid)
                        {
                            intVec3 = foundCell;
                            break;
                        }
                    }
                }

                if (i == 9)
                {
                    intVec3 = oldVec.IsValid ? oldVec : map.Center;
                }
            }

            // Final validation
            if (!intVec3.IsValid || !intVec3.InBounds(map))
            {
                intVec3 = map.Center;
            }

            return intVec3;
        }

        private static IntVec3 ValidLandingCell(IntVec2 requiredSpace, Map map, bool canLandRoofed = false)
        {
            IEnumerable<IntVec3> validCells = map.areaManager.Home.ActiveCells.Where(cell => (!map.roofGrid.Roofed(cell) || canLandRoofed) && cell.CellFulfilsSpaceRequirementForSkyFaller(requiredSpace, map));

            if (validCells.Count() == 0)
            {
                validCells = map.areaManager.Home.ActiveCells.Where(cell => cell.CellFulfilsSpaceRequirementForSkyFaller(requiredSpace, map));
            }

            if (validCells.Count() == 0)
            {
                validCells = map.AllCells.Where(cell => !map.areaManager.Home.ActiveCells.Contains(cell) && cell.Standable(map));
            }

            if (validCells.Count() == 0)
            {
                validCells = map.AllCells.Where(cell => cell.Standable(map));
            }

            // Final fallback - if we still have no valid cells, use map center
            if (validCells.Count() == 0)
            {
                return map.Center;
            }

            IntVec3 result = validCells.RandomElement();
            return result.IsValid ? result : map.Center;
        }
    }
}
