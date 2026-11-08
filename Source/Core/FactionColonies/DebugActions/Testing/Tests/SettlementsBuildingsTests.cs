using System.IO;

namespace FactionColonies;

/* Non-destructive coverage for settlement economy, event, policy and building helpers: demolition
 * order, tithe quality filtering, paused-stock disbursement, event construction, and the save keys
 * of the shared cooldown-behavior and exclusive-spot base classes. Everything here works on fresh,
 * unregistered objects and never touches the live faction or world. */
public static class SettlementsBuildingsTests
{
    /*-*-*-*-*/
    /* Helpers */
    /*-*-*-*-*/

    private static BuildingFCDef MakeBuildingDef(string name, params BuildingFCDef[] requires) =>
        new() { defName = $"EmpireTest_{name}", requiredBuildings = [.. requires] };

    /// <summary>A standalone buildings comp whose slots hold <paramref name="defs"/> in order.</summary>
    private static WorldObjectComp_SettlementBuildings MakeBuildingsComp(params BuildingFCDef[] defs)
    {
        WorldObjectComp_SettlementBuildings comp = new();
        foreach (BuildingFCDef def in defs)
            comp.Buildings.Add(new BuildingFC { def = def });
        return comp;
    }

    private static List<int> AllSlots(WorldObjectComp_SettlementBuildings comp) =>
        [.. Enumerable.Range(0, comp.Buildings.Count)];

    /// <summary>Exposes the protected save key of a cooldown behavior.</summary>
    private sealed class PacifistProbe : FCPolicyBehavior_Pacifist { public string Key => CooldownSaveKey; }
    private sealed class FeudalProbe : FCPolicyBehavior_Feudal { public string Key => CooldownSaveKey; }
    private sealed class MilitaristicProbe : FCPolicyBehavior_Militaristic
    {
        public string Key => CooldownSaveKey;
        public CooldownAbility Cooldown => cooldown;
    }

    /// <summary>Exposes the protected save key of an exclusive spot.</summary>
    private sealed class TaxSpotProbe : Building_TaxSpot { public string Key => ActiveSaveKey; }
    private sealed class CapitalSpotProbe : Building_CapitalSpot { public string Key => ActiveSaveKey; }

    /// <summary>
    /// Loads one IExposable from <paramref name="saveableXml"/>, an element named "saveable" as
    /// written by <see cref="ScribeSaver.DebugOutputFor"/>, through the real loader (vars,
    /// cross-references, post-load init).
    /// </summary>
    private static T LoadSaveable<T>(string saveableXml) where T : class, IExposable
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, $"<root>{saveableXml}</root>");
            T loaded = null;
            Scribe.loader.InitLoading(path);
            try
            {
                Scribe_Deep.Look(ref loaded, "saveable");
                Scribe.loader.FinalizeLoading();
            }
            catch
            {
                Scribe.ForceStop();
                throw;
            }
            return loaded;
        }
        finally
        {
            File.Delete(path);
        }
    }

    /*-*-*-*-*/
    /* BuildingDemolitionUtil.DependentsFirst */
    /*-*-*-*-*/

    [EmpireTest("SettlementBuildings", GameState = TestGameState.Any)]
    public static void DependentsFirst_Chain_DemolishesDependentsBeforeRequirements()
    {
        BuildingFCDef a = MakeBuildingDef("A");
        BuildingFCDef b = MakeBuildingDef("B", a);
        BuildingFCDef c = MakeBuildingDef("C", b);
        // Slots deliberately out of dependency order: A, C, B.
        WorldObjectComp_SettlementBuildings comp = MakeBuildingsComp(a, c, b);

        List<int> order = BuildingDemolitionUtil.DependentsFirst(comp, AllSlots(comp));

        TestAssert.AreEqual(3, order.Count, "Every slot should be in the result");
        TestAssert.AreEqual(1, order[0], "C (requires B) should go first");
        TestAssert.AreEqual(2, order[1], "B (requires A) should go after C");
        TestAssert.AreEqual(0, order[2], "A (required by B) should go last");
    }

    [EmpireTest("SettlementBuildings", GameState = TestGameState.Any)]
    public static void DependentsFirst_AmongFreeSlots_MostRequirementsFirst()
    {
        BuildingFCDef a = MakeBuildingDef("A");
        BuildingFCDef b = MakeBuildingDef("B");
        BuildingFCDef one = MakeBuildingDef("One", a);
        BuildingFCDef two = MakeBuildingDef("Two", a, b);
        WorldObjectComp_SettlementBuildings comp = MakeBuildingsComp(a, b, one, two);

        List<int> order = BuildingDemolitionUtil.DependentsFirst(comp, AllSlots(comp));

        TestAssert.AreEqual(3, order[0], "The slot with two requirements should go first");
        TestAssert.AreEqual(2, order[1], "The slot with one requirement should go next");
        TestAssert.IsTrue(order.IndexOf(0) > order.IndexOf(2) && order.IndexOf(1) > order.IndexOf(3),
            "Requirements must come after every dependent");
    }

    [EmpireTest("SettlementBuildings", GameState = TestGameState.Any)]
    public static void DependentsFirst_CycleAndUnresolvedSlots_KeptAtEndInOrder()
    {
        BuildingFCDef x = MakeBuildingDef("X");
        BuildingFCDef y = MakeBuildingDef("Y", x);
        x.requiredBuildings.Add(y); // X and Y require each other
        BuildingFCDef free = MakeBuildingDef("Free");
        WorldObjectComp_SettlementBuildings comp = MakeBuildingsComp(x, null, free, y);

        List<int> order = BuildingDemolitionUtil.DependentsFirst(comp, AllSlots(comp));

        TestAssert.AreEqual(4, order.Count, "Every slot should be in the result");
        TestAssert.AreEqual(2, order[0], "The only slot that can go is demolished first");
        TestAssert.AreEqual(0, order[1], "Leftover slots keep their input order");
        TestAssert.AreEqual(1, order[2], "Leftover slots keep their input order");
        TestAssert.AreEqual(3, order[3], "Leftover slots keep their input order");
    }

    [EmpireTest("SettlementBuildings", GameState = TestGameState.Any)]
    public static void DependentsFirst_DoesNotModifyInput()
    {
        BuildingFCDef a = MakeBuildingDef("A");
        BuildingFCDef b = MakeBuildingDef("B", a);
        WorldObjectComp_SettlementBuildings comp = MakeBuildingsComp(a, b);
        List<int> input = AllSlots(comp);

        List<int> order = BuildingDemolitionUtil.DependentsFirst(comp, input);

        TestAssert.IsFalse(ReferenceEquals(input, order), "A new list should be returned");
        TestAssert.AreEqual(0, input[0], "The input list should be left as it was");
        TestAssert.AreEqual(1, input[1], "The input list should be left as it was");
    }

    /*-*-*-*-*/
    /* ResourceFC and ThingQualityTuple helpers */
    /*-*-*-*-*/

    [EmpireTest("Resource", GameState = TestGameState.Any)]
    public static void GetValidTitheQualities_FiltersACopy_SharedListUnchanged()
    {
        int before = QualityUtility.AllQualityCategories.Count;

        List<QualityCategory> valid = new ResourceFC().GetValidTitheQualities(QualityCategory.Normal);

        TestAssert.AreEqual(before, QualityUtility.AllQualityCategories.Count,
            "The shared vanilla quality list must not be modified");
        TestAssert.IsFalse(ReferenceEquals(valid, QualityUtility.AllQualityCategories), "A copy should be returned");
        TestAssert.IsTrue(valid.All(q => q <= QualityCategory.Normal), "No quality above the cap should be returned");
        TestAssert.Contains(valid, QualityCategory.Awful, "The lowest quality should be kept");
        TestAssert.Contains(valid, QualityCategory.Normal, "The cap itself should be kept");
    }

    [EmpireTest("Resource", GameState = TestGameState.Any)]
    public static void ThingQualityTuple_NotEquals_WithNullOperands()
    {
        ThingQualityTuple none = null;
        ThingQualityTuple other = null;
        ThingQualityTuple some = new() { thingDef = ThingDefOf.Silver };

        TestAssert.IsFalse(none != other, "null != null should be false");
        TestAssert.IsTrue(none != some, "null != tuple should be true");
        TestAssert.IsTrue(some != none, "tuple != null should be true");
        TestAssert.IsFalse(some != new ThingQualityTuple { thingDef = ThingDefOf.Silver }, "Equal tuples are not unequal");
    }

    [EmpireTest("Resource", GameState = TestGameState.Any)]
    public static void DisburseTitheStockWhilePaused_PaysWithheldSilverOnce()
    {
        ResourceFC res = new()
        {
            tithesPaused = true,
            disburseTitheStock = true,
            randomTitheStock = 500,
            randomTitheStockSilver = 300,
        };

        TestAssert.AreEqual(300, res.DisburseTitheStockWhilePaused(), "Only the withheld silver should be paid");
        TestAssert.AreEqual(0.0, res.randomTitheStock, message: "The stock should be cleared");
        TestAssert.AreEqual(0.0, res.randomTitheStockSilver, message: "The withheld silver should be cleared");
        TestAssert.AreEqual(0, res.DisburseTitheStockWhilePaused(), "A second disbursement should pay nothing");
    }

    [EmpireTest("Resource", GameState = TestGameState.Any)]
    public static void DisburseTitheStockWhilePaused_NotPausedOrNotRequested_PaysNothing()
    {
        ResourceFC notPaused = new() { tithesPaused = false, disburseTitheStock = true, randomTitheStock = 500, randomTitheStockSilver = 500 };
        TestAssert.AreEqual(0, notPaused.DisburseTitheStockWhilePaused(), "An unpaused resource disburses in GenerateTithe instead");
        TestAssert.AreEqual(500.0, notPaused.randomTitheStock, message: "The stock should be untouched");

        ResourceFC notRequested = new() { tithesPaused = true, disburseTitheStock = false, randomTitheStock = 500, randomTitheStockSilver = 500 };
        TestAssert.AreEqual(0, notRequested.DisburseTitheStockWhilePaused(), "Nothing is paid unless disbursement is requested");
        TestAssert.AreEqual(500.0, notRequested.randomTitheStockSilver, message: "The withheld silver should be untouched");
    }

    /*-*-*-*-*/
    /* FCEventMaker.MakeRandomEvent */
    /*-*-*-*-*/

    [EmpireTest("EventSystem", GameState = TestGameState.Any)]
    public static void MakeRandomEvent_NullDef_ReturnsNull()
    {
        TestAssert.IsNull(FCEventMaker.MakeRandomEvent(null, null), "A null def should make no event");
    }

    [EmpireTest("EventSystem")]
    public static void MakeRandomEvent_VariableDuration_TriggerInsideWindow()
    {
        FCEventDef def = new() { defName = "EmpireTest_VariableEvent", timeTillTrigger = 1000, timeTillTriggerMax = 3000 };
        int now = Find.TickManager.TicksGame;

        FCEvent evt = TestAssert.NotNull(FCEventMaker.MakeRandomEvent(def, null), "MakeRandomEvent returned null");

        TestAssert.AreEqual(now, evt.tickStarted, "tickStarted should be now");
        TestAssert.AreEqual(now + 1000, evt.timeMinTrigger, "timeMinTrigger should be now + timeTillTrigger");
        TestAssert.AreEqual(now + 3000, evt.timeMaxTrigger, "timeMaxTrigger should be now + timeTillTriggerMax");
        TestAssert.IsTrue(evt.timeTillTrigger >= now + 1000 && evt.timeTillTrigger <= now + 3000,
            $"timeTillTrigger {evt.timeTillTrigger} should be inside [{now + 1000}, {now + 3000}]");
        TestAssert.IsNotNull(evt.settlementTraitLocations, "settlementTraitLocations should be an empty list, not null");
        TestAssert.AreEqual(0, evt.settlementTraitLocations.Count, "No settlement should be targeted");
    }

    [EmpireTest("EventSystem")]
    public static void MakeRandomEvent_FixedDuration_TriggerAtDuration()
    {
        FCEventDef def = new() { defName = "EmpireTest_FixedEvent", timeTillTrigger = 2500 };
        int now = Find.TickManager.TicksGame;

        FCEvent evt = TestAssert.NotNull(FCEventMaker.MakeRandomEvent(def, null), "MakeRandomEvent returned null");

        TestAssert.AreEqual(now + 2500, evt.timeTillTrigger, "timeTillTrigger should be now + timeTillTrigger");
        TestAssert.AreEqual(def, evt.def, "The event should carry its def");
    }

    /*-*-*-*-*/
    /* Save keys of the shared base classes */
    /*-*-*-*-*/

    [EmpireTest("Serialization", GameState = TestGameState.Any)]
    public static void CooldownBehaviors_KeepTheirSaveKeys()
    {
        TestAssert.AreEqual("diplomatCooldown", new PacifistProbe().Key, "Pacifist cooldown save key changed");
        TestAssert.AreEqual("mercenaryCooldown", new FeudalProbe().Key, "Feudal cooldown save key changed");
        TestAssert.AreEqual("extraSquadCooldown", new MilitaristicProbe().Key, "Militaristic cooldown save key changed");
    }

    [EmpireTest("Serialization", GameState = TestGameState.Any)]
    public static void ExclusiveSpots_KeepTheirSaveKeys()
    {
        TestAssert.AreEqual("isActiveTaxDeliverySpot", new TaxSpotProbe().Key, "Tax spot save key changed");
        TestAssert.AreEqual("isActiveCapitalSpot", new CapitalSpotProbe().Key, "Capital spot save key changed");
    }

    [EmpireTest("Serialization")]
    public static void CooldownBehavior_SavesUnderItsKey_AndRoundTrips()
    {
        MilitaristicProbe original = new();
        original.Cooldown.tickLastUsed = 12345;
        original.Cooldown.cooldownTicks = 600;

        string xml = Scribe.saver.DebugOutputFor(original);
        TestAssert.IsTrue(xml.Contains("<extraSquadCooldown>"), $"The cooldown should be saved under its key:\n{xml}");

        // Load it as the production class (the probe only adds accessors), then save again to read the values back.
        xml = xml.Replace(typeof(MilitaristicProbe).FullName, typeof(FCPolicyBehavior_Militaristic).FullName);
        FCPolicyBehavior_Militaristic loaded = TestAssert.NotNull(
            LoadSaveable<FCPolicyBehavior_Militaristic>(xml), "Loading returned null");
        string resaved = Scribe.saver.DebugOutputFor(loaded);
        TestAssert.IsTrue(resaved.Contains("<tickLastUsed>12345</tickLastUsed>"), $"tickLastUsed should survive the round trip:\n{resaved}");
        TestAssert.IsTrue(resaved.Contains("<cooldownTicks>600</cooldownTicks>"), $"cooldownTicks should survive the round trip:\n{resaved}");
    }

    [EmpireTest("Serialization")]
    public static void CooldownBehavior_LoadsSaveWrittenByThePerClassField()
    {
        // The node layout an older save has: the per-class cooldown field under its own name.
        string legacy = "<saveable Class=\"FactionColonies.FCPolicyBehavior_Militaristic\">"
            + "<extraSquadCooldown><tickLastUsed>777</tickLastUsed><cooldownTicks>60000</cooldownTicks></extraSquadCooldown>"
            + "</saveable>";
        FCPolicyBehavior_Militaristic loaded = TestAssert.NotNull(
            LoadSaveable<FCPolicyBehavior_Militaristic>(legacy), "Loading returned null");

        string resaved = Scribe.saver.DebugOutputFor(loaded);
        TestAssert.IsTrue(resaved.Contains("<tickLastUsed>777</tickLastUsed>"),
            $"tickLastUsed from the old node should be kept:\n{resaved}");
    }

    [EmpireTest("Serialization")]
    public static void CooldownBehavior_MissingNode_GetsFreshCooldown()
    {
        FCPolicyBehavior_Pacifist loaded = TestAssert.NotNull(
            LoadSaveable<FCPolicyBehavior_Pacifist>("<saveable Class=\"FactionColonies.FCPolicyBehavior_Pacifist\" />"),
            "Loading returned null");

        TestAssert.DoesNotThrow(() => loaded.DebugCooldownDays(), "A missing cooldown node must not leave the cooldown null");
        TestAssert.IsTrue(loaded.DebugCooldownReady(), "A fresh cooldown is ready");
    }
}
