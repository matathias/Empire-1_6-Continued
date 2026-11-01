using System;
using System.Collections.Generic;
using Verse;

namespace FactionColonies
{

    //Squad Class
    public class MilSquadFC : IExposable, ILoadReferenceable
    {
        public static int MaxSquadSize => Math.Max(1, FCSettings.maxSquadSize);

        public int loadID = -1;
        public string name;
        private List<MilUnitFC> units = new List<MilUnitFC>();
        public IReadOnlyList<MilUnitFC> Units => units;
        public double equipmentTotalCost;
        public int tickChanged;
        /// <summary>Monotonically increasing count of <see cref="MilitaryFC.HireSquad"/>
        /// invocations against this template. Drives the per-template suffix in hired squad
        /// names so every hire produces a unique number even after dismissals.</summary>
        public int hiresEverMade;

        /// <summary>
        /// Design-level stat modifiers for this squad template (squad scope). Seeded onto deployed
        /// squads' own statModifiers when assigned as their outfit. Groundwork for a squad-upgrade system.
        /// </summary>
        public List<PermanentStatModifier> statModifiers = new List<PermanentStatModifier>();

        public void AddStatModifier(PermanentStatModifier mod) { statModifiers.Add(mod); }
        public void RemoveStatModifiersBySource(string sourceId) { statModifiers.RemoveAll(m => m.sourceId == sourceId); }

        public static void UpdateEquipmentTotalCostOfSquadsContaining(MilUnitFC unit)
        {
            FindFC.Military.squads.ForEach(delegate (MilSquadFC squad)
            {
                if (squad.units.Contains(unit))
                {
                    squad.ChangeTick();
                }
            });
        }

        public MilSquadFC()
        {
        }

        public MilSquadFC(bool newSquad)
        {
            if (newSquad)
            {
                SetLoadID();
            }
        }

        public virtual void ExposeData()
        {
            Scribe_Values.Look(ref loadID, "loadID", -1);
            Scribe_Values.Look(ref name, "name");
            Scribe_Collections.Look(ref units, "units", LookMode.Reference);
            Scribe_Values.Look(ref equipmentTotalCost, "equipmentTotalCost", -1);
            Scribe_Values.Look(ref tickChanged, "tickChanged");
            Scribe_Values.Look(ref hiresEverMade, "hiresEverMade", 0);
            Scribe_Collections.Look(ref statModifiers, "statModifiers", LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (statModifiers is null) statModifiers = new List<PermanentStatModifier>();
                UpdateEquipmentTotalCost();
            }
        }

        public void SetLoadID()
        {
            loadID = FindFC.Military.NextSquadId();
        }

        private bool costDirty = true;

        public double GetEquipmentTotalCost()
        {
            if (costDirty)
            {
                UpdateEquipmentTotalCost();
                costDirty = false;
            }
            return equipmentTotalCost;
        }

        public virtual int UpdateEquipmentTotalCost()
        {
            double totalCost = 0;
            foreach (MilUnitFC unit in units)
            {
                totalCost += unit.getTotalCost;
            }

            equipmentTotalCost = totalCost;
            return (int)equipmentTotalCost;
        }

        public void NewSquad()
        {
            units = new List<MilUnitFC>();
            for (int sq = 0; sq < MaxSquadSize; sq++)
            {
                units.Add(FindFC.Military.blankUnit);
            }

            UpdateEquipmentTotalCost();
        }

        public void ChangeTick()
        {
            tickChanged = Find.TickManager.TicksGame;
            costDirty = true;
        }

        public void SetUnit(int index, MilUnitFC unit)
        {
            units[index] = unit;
            ChangeTick();
        }

        public void AddUnit(MilUnitFC unit)
        {
            units.Add(unit);
            ChangeTick();
        }

        public int FindUnitIndex(Predicate<MilUnitFC> predicate) => units.FindIndex(predicate);

        public int getLatestChanged
        {
            get
            {
                int latestChange;
                latestChange = tickChanged;
                foreach (MilUnitFC unit in units)
                {
                    latestChange = Math.Max(unit.tickChanged, latestChange);
                }

                return latestChange;
            }
        }

        public void DeleteSquad()
        {
            FindFC.Military.squads.Remove(this);
        }

        public string GetUniqueLoadID()
        {
            return $"MilSquadFC_{loadID}";
        }

        // --- Subclass Hooks ---

        /* Returns validation errors that should block mustering or flag issues in the UI.
           Base implementation is a no-op; subclasses add their own rules. */
        public virtual List<string> GetValidationErrors() => new List<string>();

        /* Creates the appropriate SavedSquadFC (or subclass) snapshot of this squad.
           Subclasses override to return their own SavedSquadFC subtype carrying their extra fields. */
        public virtual SavedSquadFC ToSavedSquad() => new SavedSquadFC(this);

        /* Called after base fields have been copied into a new instance during import.
           Subclasses override to pull their extra fields out of the SavedSquadFC subclass. */
        public virtual void LoadFromSaved(SavedSquadFC saved)
        {
        }

    }
}