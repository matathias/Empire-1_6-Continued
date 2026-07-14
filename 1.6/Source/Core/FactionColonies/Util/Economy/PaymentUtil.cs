using FactionColonies.util;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace FactionColonies
{
    public static class PaymentUtil
    {
        public const string Reason_SquadDeployment = "squad_deployment";
        public const string Reason_FireSupport = "fire_support";
        public const string Reason_BuildingConstruction = "building_construction";
        public const string Reason_BuildingDemolition = "building_demolition";
        public const string Reason_SettlementCreation = "settlement_creation";
        public const string Reason_SettlementUpgrade = "settlement_upgrade";
        public const string Reason_EventOption = "event_option";
        public const string Reason_TaxPayment = "tax_payment";
        public const string Reason_SilverPayment = "silver_payment";
        public const string Reason_PolicyRepick = "policy_repick";
        public const string Reason_SquadHire = "squad_hire";
        public const string Reason_SquadUpgrade = "squad_upgrade";
        public const string Reason_SquadFillSlot = "squad_fill_slot";
        public const string Reason_HireLaborers = "hire_laborers";

        /* returnBillTypes + AutoresolveBills moved to TaxLedger as private/instance methods. */

        public static void PlaceThing(Thing thing)
        {
            /* Active tax delivery spot (a Building_TaxSpot the player toggled on) wins
               outright -- its position and map override the default tax map. */
            if (CheckForActiveTaxDeliverySpot(out IntVec3 activeSpot, out Map activeMap))
            {
                GenPlace.TryPlaceThing(thing, activeSpot, activeMap, ThingPlaceMode.Near);
                return;
            }

            /* Otherwise drop onto the canonical tax map (capital -> current -> any home).
               Distinct out-locals above so the fallback taxMap is never clobbered. */
            Map taxMap = GetActiveTaxDeliveryMap();
            if (taxMap is null)
            {
                LogUtil.Warning("PaymentUtil.PlaceThing: no tax map available; thing not placed: "
                    + (thing?.LabelCap ?? "<null>"));
                if (thing is object && !thing.Destroyed) thing.Destroy();
                return;
            }

            if (CheckForTaxSpot(taxMap, out IntVec3 taxSpot))
            {
                GenPlace.TryPlaceThing(thing, taxSpot, taxMap, ThingPlaceMode.Near);
            }
            else
            {
                IntVec3 dropSpot = DropCellFinder.TradeDropSpot(taxMap);
                GenPlace.TryPlaceThing(thing, dropSpot, taxMap, ThingPlaceMode.Near);
            }
        }

        public static void DeliverThings(FCEvent evt, Letter let = null, Message msg = null)
        {
            DeliveryEvent.Action(evt, let, msg);
        }


        public static void DeliverThings(List<Thing> things, PlanetTile source, Letter let = null, Message msg = null)
        {
            DeliveryEvent.CreateDeliveryEvent(things, source, let, msg);
        }

        /// <summary>Side-effect-free affordability query. Runs payment modifiers (which may reduce
        /// the effective amount, e.g. via an outpost that would finance part of it) and reports
        /// whether the effective amount is covered by home storage. Modifier commit actions are NOT
        /// run, so nothing is consumed. Use this for pre-check gates that must credit the paying
        /// settlement's financing before allowing an action -- pass the same reason/settlement the
        /// eventual <see cref="TryPaySilver"/> will use. With no modifiers registered this is exactly
        /// <c>GetSilver() &gt;= amount</c>.</summary>
        public static bool CanAfford(int amount, string reason = null, WorldSettlementFC settlement = null)
        {
            SilverPaymentContext context = new SilverPaymentContext(amount, reason, settlement);
            SilverPaymentRegistry.InvokeModifiers(context);
            return context.Amount <= 0 || GetSilver() >= context.Amount;
        }

        /// <summary>Atomic affordability-checked silver payment. Runs payment modifiers, then:
        /// returns true when the effective amount is &lt;= 0 (running any modifier commit actions but
        /// deducting no home silver); returns <c>false</c> (consuming nothing) when the player lacks
        /// enough silver in storage; otherwise commits the modifiers and deducts the effective amount.
        /// The affordability check runs BEFORE any commit, so a modifier's side effects (e.g. draining
        /// an outpost) never fire for a payment that turns out to be unaffordable. Prefer this at every
        /// call site so affordability and payment stay a single atomic operation.</summary>
        public static bool TryPaySilver(int amount, string reason = null, WorldSettlementFC settlement = null)
        {
            SilverPaymentContext context = new SilverPaymentContext(amount, reason, settlement);
            SilverPaymentRegistry.InvokeModifiers(context);
            int effective = context.Amount;
            if (effective <= 0)                         // modifiers waived / fully financed it
            {
                context.RunCommit();
                return true;
            }
            if (GetSilver() < effective) return false;  // atomic: shortfall -> consume nothing
            context.RunCommit();                        // affordable: now perform modifier side effects
            DeductSilverFromStorage(effective);
            return true;
        }

        /// <summary>Best-effort UNCHECKED deduction: runs payment modifiers, commits their side effects,
        /// and removes up to the effective amount of silver from storage, returning true even if the
        /// player had less than owed. Does NOT verify affordability -- prefer <see cref="TryPaySilver"/>
        /// in all new code. Retained only as an escape hatch for callers that have already verified the
        /// balance upstream and explicitly want a best-effort deduction.</summary>
        public static bool PaySilver(int amount, string reason = null, WorldSettlementFC settlement = null)
        {
            SilverPaymentContext context = new SilverPaymentContext(amount, reason, settlement);
            SilverPaymentRegistry.InvokeModifiers(context);
            context.RunCommit();
            int effective = context.Amount;
            if (effective <= 0) return true;
            DeductSilverFromStorage(effective);
            return true;
        }

        /// <summary>Removes up to <paramref name="amount"/> silver from player-home storage stacks,
        /// destroying whole stacks then splitting the remainder. Sees only silver
        /// <see cref="ThingRequestGroup"/>-listed and <c>IsInAnyStorage()</c>.</summary>
        private static void DeductSilverFromStorage(int amount)
        {
            if (amount <= 0) return;

            List<Thing> silverStacks = new List<Thing>();
            foreach (Map map in Find.Maps)
            {
                if (map.IsPlayerHome)
                {
                    silverStacks.AddRange(
                        map.listerThings.ThingsOfDef(ThingDefOf.Silver)
                           .Where(s => s.IsInAnyStorage()));
                }
            }

            foreach (Thing stack in silverStacks)
            {
                if (amount <= 0) break;

                if (stack.stackCount <= amount)
                {
                    amount -= stack.stackCount;
                    stack.Destroy(DestroyMode.Vanish);
                }
                else
                {
                    stack.SplitOff(amount).Destroy(DestroyMode.Vanish);
                    amount = 0;
                }
            }
        }
        /* CreateDeploymentCostBill moved to TaxLedger as an instance factory method. */

        public static int GetSilver()
        {
            int silver = 0;

            foreach (Map map in Find.Maps)
            {
                if (map.IsPlayerHome)
                {
                    foreach (Thing thing in map.listerThings.ThingsOfDef(ThingDefOf.Silver).Where(s => s.IsInAnyStorage()))
                    {
                        silver += thing.stackCount;
                    }
                }
            }
            return silver;
        }

        public static bool CheckForTaxSpot(Map map, out IntVec3 dropSpot)
        {
            if (map is null)
            {
                LogUtil.Warning("CheckForTaxSpot received null map, bailing out");
                dropSpot = new IntVec3();
                return false;
            }
            foreach (Building building in map.listerBuildings.allBuildingsColonist.Where(b => b?.def?.defName == "TaxSpot"))
            {
                if (building is Building_TaxSpot taxSpot && taxSpot.IsActiveTaxDeliverySpot)
                {
                    dropSpot = taxSpot.Position;
                    return true;
                }
            }

            dropSpot = new IntVec3();
            return false;
        }

        public static ThingSetMakerParams ReturnThingSetMakerParams(int baseValue, int rangeMod)
        {
            ThingSetMakerParams parms = new ThingSetMakerParams();
            parms.techLevel = Find.FactionManager.OfPlayer.def.techLevel;
            parms.totalMarketValueRange = new FloatRange(baseValue - rangeMod, baseValue + rangeMod);
            return parms;
        }

        public static List<Thing> GenerateRaidLoot(int lootLevel, TechLevel techLevel)
        {
            FactionFC faction = FindFC.FactionComp;

            float lootMultiplier = (float)faction.GetStatValue(FCStatDefOf.lootMultiplier);

            List<Thing> things = new List<Thing>();
            ThingSetMaker thingSetMaker = new ThingSetMaker_MarketValue();
            ThingSetMakerParams param = new ThingSetMakerParams();
            param.totalMarketValueRange = new FloatRange((500 + (lootLevel * 200)) * lootMultiplier,
                (1000 + (lootLevel * 500)) * lootMultiplier);
            param.filter = new ThingFilter();
            param.techLevel = techLevel;
            param.countRange = new IntRange(3, 20);

            //set allow
            param.filter.SetAllow(ThingCategoryDefOf.Weapons, true);
            param.filter.SetAllow(ThingCategoryDefOf.Apparel, true);
            param.filter.SetAllow(ThingCategoryDefOf.BuildingsArt, true);
            param.filter.SetAllow(ThingCategoryDefOf.Drugs, true);
            param.filter.SetAllow(ThingCategoryDefOf.Items, true);
            param.filter.SetAllow(ThingCategoryDefOf.Medicine, true);
            param.filter.SetAllow(ThingCategoryDefOf.Techprints, true);
            param.filter.SetAllow(ThingCategoryDefOf.Buildings, true);

            //set disallow
            param.filter.SetAllow(DefDatabase<ThingDef>.GetNamedSilentFail("Teachmat"), false);

            things = thingSetMaker.Generate(param);
            return things;
        }

        public static Pawn GeneratePrisoner(Faction faction)
        {
            Pawn pawn;

            PawnKindDef raceChoice;
            raceChoice = faction.RandomPawnKind();

            pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind: raceChoice,
                faction: FindFC.EmpireFaction, context: PawnGenerationContext.NonPlayer, tile: -1,
                forceGenerateNewPawn: false, allowDead: false, allowDowned: false,
                canGeneratePawnRelations: false, mustBeCapableOfViolence: true, colonistRelationChanceFactor: 0,
                forceAddFreeWarmLayerIfNeeded: false, allowGay: false, allowFood: false, allowAddictions: false,
                inhabitant: false, certainlyBeenInCryptosleep: false, forceRedressWorldPawnIfFormerColonist: false,
                worldPawnFactionDoesntMatter: false, biocodeWeaponChance: 0, extraPawnForExtraRelationChance: null,
                relationWithExtraPawnChanceFactor: 0));
            pawn.equipment.DestroyAllEquipment();
            pawn.apparel.DestroyAll();
            pawn.SetFaction(faction);
            pawn.guest.guestStatusInt = GuestStatus.Prisoner;

            return pawn;
        }

        public static List<Thing> GenerateRewardThings(double valueBase, ResourceEventRewardDef rewardDef)
        {
            if (rewardDef == null)
            {
                LogUtil.Error("GenerateRewardThings called with null rewardDef");
                return new List<Thing>();
            }

            ThingSetMakerParams param = rewardDef.BuildParams(valueBase, out ThingSetMaker thingSetMaker);
            List<Thing> things = null;
            for (int attempts = 0; attempts < 100; attempts++)
            {
                things = thingSetMaker.Generate(param);
                if (PaymentUtil.ReturnValueOfTithe(things) >= param.totalMarketValueRange.Value.min)
                {
                    return things;
                }
            }

            LogUtil.Warning($"GenerateRewardThings failed to meet minimum value after 100 attempts for {rewardDef.defName}. Returning last result.");
            return things;
        }

        public static double ReturnValueOfTithe(List<Thing> things)
        {
            double totalValue = 0;
            foreach (Thing thing in things)
            {
                totalValue += thing.stackCount * thing.MarketValue;
            }

            return totalValue;
        }

        private static Map GetActiveTaxDeliveryMap()
        {
            // First try to find a map with an active tax delivery spot
            foreach (Map map in Find.Maps)
            {
                if (!map.IsPlayerHome) continue;

                foreach (Building building in map.listerBuildings.allBuildingsColonist)
                {
                    if (building is Building_TaxSpot taxSpot && taxSpot.IsActiveTaxDeliverySpot)
                    {
                        return map;
                    }
                }
            }

            // Fallback to existing tax map logic
            return FindFC.TaxMap;
        }

        public static bool CheckForActiveTaxDeliverySpot(out IntVec3 dropSpot, out Map taxMap)
        {
            // Search all player home maps for an active tax delivery spot
            foreach (Map map in Find.Maps)
            {
                if (!map.IsPlayerHome) continue;

                foreach (Building building in map.listerBuildings.allBuildingsColonist)
                {
                    if (building is Building_TaxSpot taxSpot && taxSpot.IsActiveTaxDeliverySpot)
                    {
                        dropSpot = building.Position;
                        taxMap = map;
                        return true;
                    }
                }
            }

            dropSpot = IntVec3.Invalid;
            taxMap = null;
            return false;
        }
    }
}