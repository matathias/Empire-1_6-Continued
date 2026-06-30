using Verse;

namespace FactionColonies
{
    public class BuildingFC : IExposable
    {
        public BuildingFCDef def;
        public BuildingFCDef underConstructionDef = BuildingFCDefOf.Empty;
        public int startedTick;
        public int completionTick;
        /// <summary>
        /// When false, this building is mothballed: it contributes no stat modifiers and no upkeep/income
        /// (capacity bonuses still apply). Drivers (submods) toggle this via SettlementBuildings.SetBuildingActive.
        /// </summary>
        public bool active = true;

        public void ExposeData()
        {
            Scribe_Defs.Look(ref def, "buildingdef");
            Scribe_Defs.Look(ref underConstructionDef, "underConstructionDef");
            Scribe_Values.Look(ref startedTick, "startedtick");
            Scribe_Values.Look(ref completionTick, "completionTick");
            Scribe_Values.Look(ref active, "active", true);
        }
    }
}