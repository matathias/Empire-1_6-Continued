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
            // Restore the Empty fallback if the saved def no longer resolves (or is absent);
            // the building window dereferences underConstructionDef.label for a slot under construction.
            if (Scribe.mode == LoadSaveMode.LoadingVars && underConstructionDef is null)
                underConstructionDef = BuildingFCDefOf.Empty;
            Scribe_Values.Look(ref startedTick, "startedtick");
            Scribe_Values.Look(ref completionTick, "completionTick");
            Scribe_Values.Look(ref active, "active", true);
        }
    }
}