using FactionColonies.util;
using RimWorld;
using System;
using System.Linq;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace FactionColonies
{
    public class LordToilData_DeliverSupplies : LordToilData
    {
        public bool sendMessage;
        public bool cellIsSet;
        public IntVec3 deliveryCell;
        public IntVec3 enterCell = IntVec3.Invalid;
        public int lastSetCellAttemptTick;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref sendMessage, "sendMessage");
            Scribe_Values.Look(ref cellIsSet, "cellIsSet");
            Scribe_Values.Look(ref deliveryCell, "deliveryCell");
            Scribe_Values.Look(ref enterCell, "enterCell", IntVec3.Invalid);
            Scribe_Values.Look(ref lastSetCellAttemptTick, "lastSetCellAttemptTick");
        }
    }

    class LordToil_DeliverSupplies : LordToil
    {
        public override bool AllowSatisfyLongNeeds => false;

        private bool NoPawnCarries => lord.ownedPawns.All(pawn => pawn.carryTracker.CarriedThing == null);

        // State lives in LordToilData so it survives save/load — LordToil instances are rebuilt
        // fresh from CreateGraph on load; only their data field is restored.
        private LordToilData_DeliverSupplies Data => (LordToilData_DeliverSupplies)data;

        public LordToil_DeliverSupplies()
        {
            data = new LordToilData_DeliverSupplies();
        }

        public bool LeavingModeEngaged { get => Data.sendMessage; }

        private void SetCell()
        {
            if (!Data.cellIsSet)
            {
                // Fix: Ensure we have valid pawns before proceeding
                if (lord.ownedPawns.NullOrEmpty())
                {
                    Data.cellIsSet = true;
                    return;
                }

                Pawn leadPawn = lord.ownedPawns[0];

                // Get fallback location from the lord job
                LordJob_DeliverSupplies deliveryJob = lord.LordJob as LordJob_DeliverSupplies;
                IntVec3 fallbackPos = (deliveryJob != null && deliveryJob.fallbackLocation.IsValid)
                    ? deliveryJob.fallbackLocation
                    : IntVec3.Invalid;

                // IMPORTANT: Wait for pawns to be properly spawned before proceeding
                if (!leadPawn.Spawned || !leadPawn.Position.IsValid)
                {
                    // PERFORMANCE FIX: Only try once per second to avoid busy loop
                    if (Find.TickManager.TicksGame - Data.lastSetCellAttemptTick < 60)
                    {
                        return; // Too soon to try again
                    }
                    Data.lastSetCellAttemptTick = Find.TickManager.TicksGame;

                    // If pawns aren't spawned yet, use fallback position but don't mark as set
                    if (fallbackPos.IsValid)
                    {
                        Data.enterCell = fallbackPos;
                        Data.deliveryCell = fallbackPos;
                    }
                    else
                    {
                        // Find a reasonable default position
                        if (PaymentUtil.CheckForTaxSpot(lord.Map, out IntVec3 taxSpot))
                        {
                            Data.enterCell = taxSpot;
                            Data.deliveryCell = taxSpot;
                        }
                        else
                        {
                            Data.enterCell = lord.Map.Center;
                            Data.deliveryCell = lord.Map.Center;
                        }
                    }
                    // Don't set cellIsSet = true yet, so we'll try again later
                    return;
                }

                // Now we have a properly spawned pawn with valid position
                Data.enterCell = leadPawn.Position;

                // Try to get a proper delivery cell
                try
                {
                    TraverseParms traverseParms = DeliveryLogistics.DeliveryTraverseParms;
                    traverseParms.pawn = leadPawn;

                    // First try to find tax spot
                    if (PaymentUtil.CheckForTaxSpot(lord.Map, out Data.deliveryCell))
                    {
                        // Validate tax spot
                        if (!Data.deliveryCell.IsValid || !Data.deliveryCell.InBounds(lord.Map))
                        {
                            Data.deliveryCell = lord.Map.Center;
                        }

                        // Check if the tax spot is reachable
                        if (!leadPawn.CanReach(Data.deliveryCell, PathEndMode.OnCell, Danger.Deadly))
                        {
                            // Tax spot exists but not reachable, find alternative
                            IntVec3 alternativeCell;
                            if (CellFinder.TryFindRandomReachableNearbyCell(Data.deliveryCell, lord.Map, 10, traverseParms, null, null, out alternativeCell) && alternativeCell.IsValid)
                            {
                                Data.deliveryCell = alternativeCell;
                            }
                            else
                            {
                                // Can't find reachable cell near tax spot, use map center
                                Data.deliveryCell = lord.Map.Center;
                            }
                        }
                    }
                    else
                    {
                        // No tax spot, use the GetDeliveryCell method
                        Data.deliveryCell = DeliveryLogistics.GetDeliveryCell(traverseParms, lord.Map);

                        // Validate the result
                        if (!Data.deliveryCell.IsValid || !Data.deliveryCell.InBounds(lord.Map))
                        {
                            Data.deliveryCell = lord.Map.Center;
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogUtil.Warning($"Error finding delivery cell: {ex.Message}. Using fallback position.");
                    Data.deliveryCell = fallbackPos.IsValid ? fallbackPos : lord.Map.Center;
                }

                Data.cellIsSet = true;
            }
        }

        public override void UpdateAllDuties()
        {
            for (int i = 0; i < lord.ownedPawns.Count; i++)
            {
                SetCell();
                Pawn pawn = lord.ownedPawns[i];
                pawn.mindState.canFleeIndividual = true;
                if (!NoPawnCarries)
                {
                    if (i == 0)
                    {
                        pawn.mindState.duty = new PawnDuty(DefDatabase<DutyDef>.GetNamed("FCDeliverItem"))
                        {
                            focus = Data.deliveryCell
                        };
                    }
                    else
                    {
                        TraverseParms traverseParms = DeliveryLogistics.DeliveryTraverseParms;
                        traverseParms.pawn = pawn;
                        pawn.mindState.duty = new PawnDuty(DefDatabase<DutyDef>.GetNamed("FCFollowAndDeliverItem"))
                        {
                            focus = (lord.ownedPawns[0].carryTracker.CarriedThing == null) ? (LocalTargetInfo)Data.deliveryCell : lord.ownedPawns[0],
                        };
                    }
                    continue;
                }

                if (!Data.sendMessage)
                {
                    Messages.Message("FCDeliveryPawnsLeavingMap".Translate(), MessageTypeDefOf.NeutralEvent);
                    Data.sendMessage = true;
                }

                if (Data.enterCell.IsValid)
                {
                    pawn.mindState.duty = new PawnDuty(DutyDefOf.ExitMapNearDutyTarget)
                    {
                        locomotion = LocomotionUrgency.Sprint,
                        focus = Data.enterCell,
                        canDig = false,
                    };
                }
                else
                {
                    pawn.mindState.duty = new PawnDuty(DutyDefOf.ExitMapBest)
                    {
                        locomotion = LocomotionUrgency.Sprint,
                        canDig = false
                    };
                }
            }
        }

        public override void Notify_ReachedDutyLocation(Pawn pawn)
        {
            UpdateAllDuties();
            base.Notify_ReachedDutyLocation(pawn);
        }
    }
}

