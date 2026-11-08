using UnityEngine;
using Verse.Sound;

namespace FactionColonies;

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
    public override Vector2 InitialSize => new(620f, 700f);

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
        Rect elemRect = new(inRect.x, inRect.y, inRect.width, ElementHeight);

        Rect nameRect = new(inRect.x, inRect.y, ElementNameWidth, ElementNameHeight);

        Rect deleteRect = new(inRect.width - inRect.x - ElementDeleteWidth,
            inRect.y, ElementDeleteWidth, ElementDeleteHeight);
        Rect importRect = new(deleteRect.x - ElementImportWidth,
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
                if (tooltip is not null)
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

/// <summary>
/// An export manager over one kind of saved military template. Deleting asks for confirmation;
/// importing loads the template into the matching military designer window, opening one if none
/// is open.
/// </summary>
public abstract class Dialog_ManageExportsFC<TSaved> : Dialog_ManageExportsFC where TSaved : class
{
    private readonly List<TSaved> saved;

    protected Dialog_ManageExportsFC(List<TSaved> saved)
    {
        this.saved = saved;
    }

    protected abstract string NameOf(TSaved entry);
    /// <summary>Removes the named template from the saved-template store.</summary>
    protected abstract void RemoveSaved(string name);
    /// <summary>Loads the named template from the saved-template store as a live template.</summary>
    protected abstract IExposable ImportSaved(string name);
    /// <summary>The designer window slot that edits this kind of template.</summary>
    protected abstract MilitaryWindowSlot Slot { get; }
    protected abstract FCWindow_Military CreateDesignerWindow(FactionFC fc);
    protected abstract bool IsDegraded(TSaved entry);
    protected abstract IEnumerable<string> MissingDefs(TSaved entry);
    /// <summary>The last sentence of the degraded tooltip: what importing does with missing defs.</summary>
    protected virtual string DegradedImportNote => "Importing will substitute defaults for missing items.";

    private TSaved Entry(string name) => saved.FirstOrDefault(e => NameOf(e) == name);

    protected override IEnumerable<string> GetAll() => saved.Select(NameOf);

    protected override void OnDelete(string name)
    {
        Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
            "FCConfirmDelete".Translate((NamedArgument)name), () =>
        {
            RemoveSaved(name);
            saved.RemoveAll(e => NameOf(e) == name);
            Messages.Message("FCDeleted".Translate((NamedArgument)name), MessageTypeDefOf.PositiveEvent);
        }));
    }

    protected override void OnImport(string name)
    {
        IExposable template = ImportSaved(name);

        FCWindow_Military milWindow = (FCWindow_Military)Find.WindowStack.Windows.FirstOrDefault(
            window => window is FCWindow_Military fcw && fcw.GetMilitaryWindow().Slot == Slot);

        if (milWindow is not null)
        {
            milWindow.SetActive(template);
        }
        else
        {
            FCWindow_Military newWindow = CreateDesignerWindow(FindFC.FactionComp);
            Find.WindowStack.Add(newWindow);
            newWindow.SetActive(template);
        }

        MessageTypeDefOf.PositiveEvent.sound.PlayOneShotOnCamera();
        Messages.Message("FCImported".Translate((NamedArgument)name), MessageTypeDefOf.PositiveEvent);
        Close();
    }

    protected override bool IsEntryDegraded(string name)
    {
        TSaved entry = Entry(name);
        return entry is not null && IsDegraded(entry);
    }

    protected override string GetDegradedTooltip(string name)
    {
        TSaved entry = Entry(name);
        if (entry is null || !IsDegraded(entry)) return null;
        return "This template references defs from unloaded mods: "
            + string.Join(", ", MissingDefs(entry))
            + ".\n" + DegradedImportNote;
    }
}

public class Dialog_ManageSquadExportsFC : Dialog_ManageExportsFC<SavedSquadFC>
{
    public Dialog_ManageSquadExportsFC(List<SavedSquadFC> elements) : base(elements) { }

    protected override string NameOf(SavedSquadFC squad) => squad.name;
    protected override void RemoveSaved(string name) => FactionColoniesMilitary.RemoveSquad(name);
    protected override IExposable ImportSaved(string name) => FactionColoniesMilitary.GetSquad(name).Import();
    protected override MilitaryWindowSlot Slot => MilitaryWindowSlot.Squads;
    protected override FCWindow_Military CreateDesignerWindow(FactionFC fc) =>
        new(MilitaryWindowRegistry.CreateSquads(fc.military, fc), "Create Squads");
    protected override bool IsDegraded(SavedSquadFC squad) => squad.IsDegraded;
    protected override IEnumerable<string> MissingDefs(SavedSquadFC squad) =>
        squad.unitTemplates.Where(u => u.isDegraded).SelectMany(u => u.missingDefs);
}

public class Dialog_ManageUnitExportsFC : Dialog_ManageExportsFC<SavedUnitFC>
{
    public Dialog_ManageUnitExportsFC(List<SavedUnitFC> elements) : base(elements) { }

    protected override string NameOf(SavedUnitFC unit) => unit.name;
    protected override void RemoveSaved(string name) => FactionColoniesMilitary.RemoveUnit(name);
    protected override IExposable ImportSaved(string name) => FactionColoniesMilitary.GetUnit(name).Import();
    protected override MilitaryWindowSlot Slot => MilitaryWindowSlot.Units;
    protected override FCWindow_Military CreateDesignerWindow(FactionFC fc) =>
        new(MilitaryWindowRegistry.CreateUnits(fc.military, fc), "Create Units");
    protected override bool IsDegraded(SavedUnitFC unit) => unit.isDegraded;
    protected override IEnumerable<string> MissingDefs(SavedUnitFC unit) => unit.missingDefs;
}

public class Dialog_ManageFireSupportExportsFC : Dialog_ManageExportsFC<SavedFireSupportFC>
{
    public Dialog_ManageFireSupportExportsFC(List<SavedFireSupportFC> elements) : base(elements) { }

    protected override string NameOf(SavedFireSupportFC fs) => fs.name;
    protected override void RemoveSaved(string name) => FactionColoniesMilitary.RemoveFireSupport(name);
    protected override IExposable ImportSaved(string name) => FactionColoniesMilitary.GetFireSupport(name).Import();
    protected override MilitaryWindowSlot Slot => MilitaryWindowSlot.FireSupport;
    protected override FCWindow_Military CreateDesignerWindow(FactionFC fc) =>
        new(MilitaryWindowRegistry.CreateFireSupport(fc.military, fc), "FCMilitaryTableButtonCreateFireSupport".Translate());
    protected override bool IsDegraded(SavedFireSupportFC fs) => fs.isDegraded;
    protected override IEnumerable<string> MissingDefs(SavedFireSupportFC fs) => fs.missingDefs;
    protected override string DegradedImportNote => "Importing will drop missing projectiles.";
}
