using FactionColonies.util;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace FactionColonies
{
    public abstract class Dialog_ManageExportsFC : Window
    {
        #region UIVars

        static float ElementPadding = 5.0f;
        static float ElementHeight = 35f;

        static float ElementNameWidth = 200f;
        static float ElementNameHeight = 35f;

        static float ElementImportWidth = 80f;
        static float ElementImportHeight = 35f;

        static float ElementDeleteWidth = 35f;
        static float ElementDeleteHeight = 35f;

        #endregion

        protected Vector2 scrollPos;
        public override Vector2 InitialSize => new Vector2(620f, 700f);

        public Dialog_ManageExportsFC()
        {
            this.doCloseX = true;
            this.draggable = true;
            this.resizeable = true;
            this.doCloseButton = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Rect rect = inRect;
            Rect view = ScrollUtil.BeginScrollView(rect, ref scrollPos, this.GetAll().Count() * (ElementHeight + ElementPadding));
            DrawElements(view);
            ScrollUtil.EndScrollView();
        }

        protected virtual void DrawElements(Rect inRect)
        {
            Text.Anchor = TextAnchor.MiddleLeft;
            Rect elemRect = new Rect(inRect.x, inRect.y, inRect.width, ElementHeight);

            Rect nameRect = new Rect(inRect.x, inRect.y, ElementNameWidth, ElementNameHeight);

            Rect deleteRect = new Rect(inRect.width - inRect.x - ElementDeleteWidth,
                inRect.y, ElementDeleteWidth, ElementDeleteHeight);
            Rect importRect = new Rect(deleteRect.x - ElementImportWidth,
                inRect.y, ElementImportWidth, ElementImportHeight);

            bool alternate = false;
            foreach (string name in GetAll())
            {
                if (alternate)
                    Widgets.DrawAltRect(elemRect);

                string displayName = name;
                bool degraded = IsEntryDegraded(name);
                if (degraded)
                {
                    GUI.color = Color.red;
                    string tooltip = GetDegradedTooltip(name);
                    if (tooltip != null)
                        TooltipHandler.TipRegion(elemRect, tooltip);
                    displayName = $"{name} ({"FCDegraded".Translate()})";
                }

                UIUtil.ClampedLabel(nameRect, displayName);

                if (degraded)
                    GUI.color = Color.white;

                if (UIUtil.ClampedButtonText(importRect, "FCImport".Translate()))
                {
                    OnImport(name);
                }

                if (Widgets.ButtonImage(deleteRect, TexLoad.deleteX))
                {
                    OnDelete(name);
                }

                elemRect.y += ElementHeight + ElementPadding;
                nameRect.y += ElementHeight + ElementPadding;
                importRect.y += ElementHeight + ElementPadding;
                deleteRect.y += ElementHeight + ElementPadding;
                alternate ^= true;
            }
            Text.Anchor = TextAnchor.UpperLeft;
        }

        protected abstract void OnDelete(string name);
        protected abstract void OnImport(string name);
        protected abstract IEnumerable<string> GetAll();
        protected virtual bool IsEntryDegraded(string name) => false;
        protected virtual string GetDegradedTooltip(string name) => null;
    }

    public class Dialog_ManageSquadExportsFC : Dialog_ManageExportsFC
    {
        private List<SavedSquadFC> squads;
        public Dialog_ManageSquadExportsFC(List<SavedSquadFC> elements)
        {
            squads = elements;
        }

        protected override void OnDelete(string name)
        {
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                "FCConfirmDelete".Translate((NamedArgument)name), () =>
            {
                FactionColoniesMilitary.RemoveSquad(name);
                this.squads.RemoveAll(squads => squads.name == name);
                Messages.Message("FCDeleted".Translate((NamedArgument)name), MessageTypeDefOf.PositiveEvent);
            }));
        }

        protected override void OnImport(string name)
        {
            FactionFC fc = FindFC.FactionComp;
            MilSquadFC squad = FactionColoniesMilitary.GetSquad(name).Import();

            FCWindow_Military milWindow = (FCWindow_Military)Find.WindowStack.Windows.FirstOrDefault(
                window => window is FCWindow_Military fcw &&
                          fcw.GetMilitaryWindow().Slot == MilitaryWindowSlot.Squads);

            if (milWindow != null)
            {
                milWindow.SetActive(squad);
            }
            else
            {
                MilitaryWindow dsw = MilitaryWindowRegistry.CreateSquads(fc.military, fc);
                FCWindow_Military newWindow = new FCWindow_Military(dsw, "Create Squads");
                Find.WindowStack.Add(newWindow);
                newWindow.SetActive(squad);
            }

            MessageTypeDefOf.PositiveEvent.sound.PlayOneShotOnCamera();
            Messages.Message("FCImported".Translate((NamedArgument)name), MessageTypeDefOf.PositiveEvent);
            this.Close();
        }
        protected override IEnumerable<string> GetAll() => squads.Select(squad => squad.name);

        protected override bool IsEntryDegraded(string name)
        {
            SavedSquadFC squad = squads.FirstOrDefault(s => s.name == name);
            return squad != null && squad.IsDegraded;
        }

        protected override string GetDegradedTooltip(string name)
        {
            SavedSquadFC squad = squads.FirstOrDefault(s => s.name == name);
            if (squad == null || !squad.IsDegraded) return null;
            List<string> allMissing = squad.unitTemplates
                .Where(u => u.isDegraded)
                .SelectMany(u => u.missingDefs)
                .ToList();
            return "This template references defs from unloaded mods: "
                + string.Join(", ", allMissing)
                + ".\nImporting will substitute defaults for missing items.";
        }
    }
    public class Dialog_ManageUnitExportsFC : Dialog_ManageExportsFC
    {
        private List<SavedUnitFC> units;
        public Dialog_ManageUnitExportsFC(List<SavedUnitFC> elements)
        {
            units = elements;
        }

        protected override void OnDelete(string name)
        {
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                "FCConfirmDelete".Translate((NamedArgument)name), () =>
            {
                FactionColoniesMilitary.RemoveUnit(name);
                this.units.RemoveAll(unit => unit.name == name);
                Messages.Message("FCDeleted".Translate((NamedArgument)name), MessageTypeDefOf.PositiveEvent);
            }));
        }

        protected override void OnImport(string name)
        {
            FactionFC fc = FindFC.FactionComp;
            MilUnitFC unit = FactionColoniesMilitary.GetUnit(name).Import();

            FCWindow_Military milWindow = (FCWindow_Military)Find.WindowStack.Windows.FirstOrDefault(
                window => window is FCWindow_Military fcw &&
                          fcw.GetMilitaryWindow().Slot == MilitaryWindowSlot.Units);

            if (milWindow != null)
            {
                milWindow.SetActive(unit);
            }
            else
            {
                MilitaryWindow duw = MilitaryWindowRegistry.CreateUnits(fc.military, fc);
                FCWindow_Military newWindow = new FCWindow_Military(duw, "Create Units");
                Find.WindowStack.Add(newWindow);
                newWindow.SetActive(unit);
            }

            MessageTypeDefOf.PositiveEvent.sound.PlayOneShotOnCamera();
            Messages.Message("FCImported".Translate((NamedArgument)name), MessageTypeDefOf.PositiveEvent);
            this.Close();
        }
        protected override IEnumerable<string> GetAll() => units.Select(unit => unit.name);

        protected override bool IsEntryDegraded(string name)
        {
            SavedUnitFC unit = units.FirstOrDefault(u => u.name == name);
            return unit != null && unit.isDegraded;
        }

        protected override string GetDegradedTooltip(string name)
        {
            SavedUnitFC unit = units.FirstOrDefault(u => u.name == name);
            if (unit == null || !unit.isDegraded) return null;
            return "This template references defs from unloaded mods: "
                + string.Join(", ", unit.missingDefs)
                + ".\nImporting will substitute defaults for missing items.";
        }
    }

    public class Dialog_ManageFireSupportExportsFC : Dialog_ManageExportsFC
    {
        private List<SavedFireSupportFC> fireSupports;

        public Dialog_ManageFireSupportExportsFC(List<SavedFireSupportFC> elements)
        {
            fireSupports = elements;
        }

        protected override void OnDelete(string name)
        {
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                "FCConfirmDelete".Translate((NamedArgument)name), () =>
            {
                FactionColoniesMilitary.RemoveFireSupport(name);
                this.fireSupports.RemoveAll(f => f.name == name);
                Messages.Message("FCDeleted".Translate((NamedArgument)name), MessageTypeDefOf.PositiveEvent);
            }));
        }

        protected override void OnImport(string name)
        {
            MilitaryFireSupport fs = FactionColoniesMilitary.GetFireSupport(name).Import();

            FCWindow_Military milWindow = (FCWindow_Military)Find.WindowStack.Windows.FirstOrDefault(
                window => window is FCWindow_Military fcw &&
                          fcw.GetMilitaryWindow().Slot == MilitaryWindowSlot.FireSupport);

            if (milWindow is object)
            {
                milWindow.SetActive(fs);
            }
            else
            {
                FactionFC fc = FindFC.FactionComp;
                MilitaryWindow fsw = MilitaryWindowRegistry.CreateFireSupport(fc.military, fc);
                FCWindow_Military newWindow = new FCWindow_Military(fsw, "FCMilitaryTableButtonCreateFireSupport".Translate());
                Find.WindowStack.Add(newWindow);
                newWindow.SetActive(fs);
            }

            MessageTypeDefOf.PositiveEvent.sound.PlayOneShotOnCamera();
            Messages.Message("FCImported".Translate((NamedArgument)name), MessageTypeDefOf.PositiveEvent);
            this.Close();
        }

        protected override IEnumerable<string> GetAll() => fireSupports.Select(f => f.name);

        protected override bool IsEntryDegraded(string name)
        {
            SavedFireSupportFC fs = fireSupports.FirstOrDefault(f => f.name == name);
            return fs is object && fs.isDegraded;
        }

        protected override string GetDegradedTooltip(string name)
        {
            SavedFireSupportFC fs = fireSupports.FirstOrDefault(f => f.name == name);
            if (fs is null || !fs.isDegraded) return null;
            return "This template references defs from unloaded mods: "
                + string.Join(", ", fs.missingDefs)
                + ".\nImporting will drop missing projectiles.";
        }
    }
}