using UnityEngine;

namespace FactionColonies;

public class FactionCustomizeTraitsWindowFC : FCWindow_PolicySelectionBase
{
    private const float fullwidth = 800f;
    private const float fullheight = 500f;
    public override Vector2 InitialSize => new(fullwidth, fullheight);

    List<FCPolicyDef> selectedTraits = [];

    private Vector2 traitListScroll;
    static List<Vector2> traitScrollBars = [];

    private const int slotCount = 5;

    public FactionCustomizeTraitsWindowFC(FactionFC faction)
        : base(faction, "FCTraitSelection".Translate())
    {
        traitScrollBars.Clear();
        for (int i = 0; i < slotCount; i++)
        {
            traitScrollBars.Add(new Vector2());
        }
    }

    protected override void DrawPanels(Rect inRect, float panelTop, float panelBottom)
    {
        // Left panel
        float traitWidth = inRect.width * 0.25f;
        Rect leftPanel = new(inRect.x, panelTop, traitWidth, panelBottom - panelTop);

        // Right panel
        Rect centerPanel = new(leftPanel.xMax + margin, panelTop, inRect.width - (traitWidth * 2) - (margin * 2), panelBottom - panelTop);

        Rect rightPanel = new(centerPanel.xMax + margin, panelTop, traitWidth, leftPanel.height);

        DrawTraitSlots(leftPanel);
        DrawAvailableList(rightPanel, "FCAvailableTraits".Translate(), GetAvailableTraits(), ref traitListScroll,
            selectedTraits, null, true, HandleTraitSelection);
        DrawTraitDescription(centerPanel);
    }

    protected override string ComputeAlertText()
    {
        int openSlots = CountOpenSlots();
        int remaining = openSlots - selectedTraits.Count;
        if (openSlots == 0)
            return "FCAllTraitSlotsFilled".Translate();
        if (remaining > 0)
            return "FCSelectTraitsPrompt".Translate(remaining);
        return "FCTraitSelectionReady".Translate();
    }

    protected override void ApplySelection()
    {
        if (selectedTraits.Count == 0) return;

        int traitIndex = 0;
        for (int slot = 0; slot < slotCount && traitIndex < selectedTraits.Count; slot++)
        {
            bool isLocked = faction.factionLevel < (slot + 1);
            bool isAssigned = FindFC.PolicyManager.factionTraits[slot].def != FCPolicyDefOf.empty;
            if (!isLocked && !isAssigned)
            {
                FindFC.PolicyManager.factionTraits[slot] = new FCPolicy(selectedTraits[traitIndex]);
                traitIndex++;
            }
        }
        FindFC.PolicyManager.RebuildBehaviorCache();
    }

    private void DrawTraitSlots(Rect inRect)
    {
        // Section A: Current slot status
        Rect slotHeader = new(inRect.x, inRect.y, inRect.width, 22f);
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.DrawHighlight(slotHeader);
        UIUtil.ClampedLabel(new Rect(slotHeader.x + margin, slotHeader.y, slotHeader.width - margin, slotHeader.height),
                      "FCTraitSlots".Translate());

        float y = slotHeader.yMax + smallMargin;

        int newTraitIndex = 0;
        for (int slot = 0; slot < slotCount; slot++)
        {
            Rect row = new(inRect.x, y, inRect.width, rowHeight);
            if (slot % 2 == 0) Widgets.DrawHighlight(row);

            FCPolicy existing = FindFC.PolicyManager.factionTraits[slot];
            bool isLocked = faction.factionLevel < (slot + 1);
            bool isAssigned = existing.def != FCPolicyDefOf.empty;

            string label;
            Color labelColor;

            if (isAssigned)
            {
                label = (slot + 1) + ". " + existing.def.LabelCap;
                labelColor = Color.gray;
                TooltipHandler.TipRegion(row, existing.def.PolicyText());
            }
            else if (isLocked)
            {
                label = (slot + 1) + ". " + "FCTraitLockedUntilLevel".Translate(slot + 1);
                labelColor = Color.gray;
            }
            else if (newTraitIndex < selectedTraits.Count)
            {
                label = (slot + 1) + ". " + "FCTraitSelectedPreview".Translate(selectedTraits[newTraitIndex].LabelCap);
                labelColor = Color.green;
                TooltipHandler.TipRegion(row, selectedTraits[newTraitIndex].PolicyText());
                newTraitIndex++;
            }
            else
            {
                label = (slot + 1) + ". " + "FCSelectANewTrait".Translate();
                labelColor = Color.white;
            }

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            UIUtil.DrawColoredLabel(
                new Rect(row.x + margin, row.y, row.width - (margin * 2), row.height),
                label,
                labelColor);

            y += rowHeight + smallMargin;
        }
    }

    private void HandleTraitSelection(FCPolicyDef trait)
    {
        bool alreadySelected = selectedTraits.Contains(trait);
        int openSlots = CountOpenSlots();

        if (alreadySelected)
        {
            selectedTraits.Remove(trait);
        }
        else if (selectedTraits.Count < openSlots)
        {
            selectedTraits.Add(trait);
        }
        else
        {
            Messages.Message("FCNoOpenTraitSlots".Translate(), MessageTypeDefOf.RejectInput);
        }
    }

    private void DrawTraitDescription(Rect inRect)
    {
        Widgets.DrawMenuSection(inRect);

        FCPolicyDef displayTrait = hovered;
        if (displayTrait is null && selectedTraits.Count > 0)
        {
            displayTrait = selectedTraits[selectedTraits.Count - 1];
        }

        if (displayTrait is null || displayTrait == FCPolicyDefOf.empty)
        {
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            UIUtil.ClampedLabel(inRect, "FCHoverTraitForDetails".Translate());
            return;
        }

        float innerMargin = margin * 2;
        Rect inner = new(inRect.x + innerMargin, inRect.y + innerMargin,
                              inRect.width - (innerMargin * 2), inRect.height - (innerMargin * 2));

        // Trait name
        Rect nameRect = new(inner.x, inner.y, inner.width, 30f);
        Text.Font = GameFont.Medium;
        Text.Anchor = TextAnchor.MiddleLeft;
        UIUtil.ClampedLabel(nameRect, displayTrait.LabelCap);

        // Description
        Rect descRect = new(inner.x, nameRect.yMax + margin, inner.width, inner.yMax - nameRect.yMax - margin);
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.UpperLeft;

        string desc = PolicyDescription(displayTrait);
        float textHeight = Text.CalcHeight(desc.StripTags(), descRect.width);

        if (textHeight > descRect.height)
        {
            textHeight = Text.CalcHeight(desc.StripTags(), descRect.width - 16f);
            Vector2 scrollBar = traitScrollBars.Count > 0 ? traitScrollBars[0] : new Vector2();
            Rect scrollContent = ScrollUtil.BeginScrollView(descRect, ref scrollBar, textHeight);
            Widgets.Label(scrollContent, desc);
            ScrollUtil.EndScrollView();
            if (traitScrollBars.Count > 0) traitScrollBars[0] = scrollBar;
        }
        else
        {
            Widgets.Label(descRect, desc);
        }
    }

    private int CountOpenSlots()
    {
        int count = 0;
        for (int slot = 0; slot < slotCount; slot++)
        {
            bool isLocked = faction.factionLevel < (slot + 1);
            bool isAssigned = FindFC.PolicyManager.factionTraits[slot].def != FCPolicyDefOf.empty;
            if (!isLocked && !isAssigned) count++;
        }
        return count;
    }

    private List<FCPolicyDef> GetAvailableTraits()
    {
        return FactionCache.GetPoliciesByCategory(FCPolicyCategory.Trait)
            .Where(d => !FindFC.PolicyManager.HasTrait(d) && MeetsTraitPrerequisites(d))
            .ToList();
    }

    private bool MeetsTraitPrerequisites(FCPolicyDef def)
    {
        if (def.requiredPolicies.NullOrEmpty()) return true;

        if (def.requirementMode == FCRequirementMode.Any)
        {
            foreach (FCPolicyDef req in def.requiredPolicies)
            {
                if (FindFC.PolicyManager.HasTrait(req) || selectedTraits.Contains(req)
                    || FindFC.PolicyManager.HasPolicy(req) || FindFC.PolicyManager.HasEdict(req))
                    return true;
            }
            return false;
        }

        foreach (FCPolicyDef req in def.requiredPolicies)
        {
            if (!FindFC.PolicyManager.HasTrait(req) && !selectedTraits.Contains(req)
                && !FindFC.PolicyManager.HasPolicy(req) && !FindFC.PolicyManager.HasEdict(req))
                return false;
        }
        return true;
    }
}
