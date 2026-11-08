using UnityEngine;

namespace FactionColonies;

/// <summary>
/// Shared frame for the faction policy and trait selection windows: the title header with its
/// divider line, the panel area, the alert line and the centered Confirm button, plus the
/// "available" list whose rows set <see cref="hovered"/> for a hover preview.
/// </summary>
public abstract class FCWindow_PolicySelectionBase : Window
{
    protected const float margin = 5f;
    protected const float smallMargin = 3f;
    protected const float rowHeight = 30f;

    protected FactionFC faction;
    public string header;
    protected string alertText = "";

    /// <summary>Row under the mouse in the available list this frame, or null.</summary>
    protected FCPolicyDef hovered;

    protected FCWindow_PolicySelectionBase(FactionFC faction, string header)
    {
        forcePause = false;
        draggable = true;
        doCloseX = true;
        preventCameraMotion = false;
        this.faction = faction;
        this.header = header;
    }

    /// <summary>Extra controls on the header row, drawn right after the title.</summary>
    protected virtual void DrawHeaderExtras(Rect inRect) { }

    /// <summary>Draws the panels between the header and the alert line.</summary>
    protected abstract void DrawPanels(Rect inRect, float panelTop, float panelBottom);

    /// <summary>The alert line shown above the Confirm button.</summary>
    protected abstract string ComputeAlertText();

    /// <summary>Applies the selection when Confirm is clicked; the window closes afterwards.</summary>
    protected abstract void ApplySelection();

    public override void DoWindowContents(Rect inRect)
    {
        GameFont fontBefore = Text.Font;
        TextAnchor anchorBefore = Text.Anchor;

        hovered = null;

        // Header
        Rect labelHeader = new(0, 0, 200, 40);
        float headerHeight = labelHeader.yMax + (margin * 4);

        Text.Anchor = TextAnchor.MiddleLeft;
        Text.Font = GameFont.Medium;
        UIUtil.ClampedLabel(labelHeader, header);
        Widgets.DrawLineHorizontal(labelHeader.x, labelHeader.yMax + margin, InitialSize.x - (Margin * 2));

        Text.Font = GameFont.Small;

        DrawHeaderExtras(inRect);

        // Confirm button
        Rect buttonConfirm = new((inRect.xMax - 200) / 2f, inRect.yMax - 50, 200, 30);

        // Alert text
        Rect alertRect = new(inRect.x + margin, buttonConfirm.y - 25, inRect.width - (margin * 2), 20);

        DrawPanels(inRect, headerHeight, alertRect.y - margin);

        alertText = ComputeAlertText();

        Text.Font = GameFont.Tiny;
        Text.Anchor = TextAnchor.MiddleCenter;
        UIUtil.ClampedLabel(alertRect, alertText);

        // Confirm button
        Text.Font = GameFont.Small;
        if (UIUtil.ClampedButtonText(buttonConfirm, "FCConfirmChanges".Translate()))
        {
            ApplySelection();
            Find.WindowStack.TryRemove(this);
        }

        Text.Font = fontBefore;
        Text.Anchor = anchorBefore;
    }

    /// <summary>
    /// Draws a titled, scrollable list of subtle buttons, one per def. Selected rows read
    /// "&gt;&gt; name &lt;&lt;"; rows that <paramref name="isBlocked"/> flags get a "(!)" suffix and are
    /// dimmed. <paramref name="onClick"/> runs only when <paramref name="clickable"/> is true.
    /// </summary>
    protected void DrawAvailableList(Rect inRect, string title, List<FCPolicyDef> defs, ref Vector2 scroll,
        List<FCPolicyDef> selected, Func<FCPolicyDef, bool> isBlocked, bool clickable, Action<FCPolicyDef> onClick)
    {
        Rect listHeader = new(inRect.x, inRect.y, inRect.width, 22f);
        Widgets.DrawHighlight(listHeader);
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.MiddleLeft;
        UIUtil.ClampedLabel(new Rect(listHeader.x + margin, listHeader.y, listHeader.width - margin, listHeader.height),
            title);

        float listY = listHeader.yMax + smallMargin;
        float listHeight = inRect.yMax - listY;
        float contentHeight = defs.Count * (rowHeight + smallMargin);

        Rect viewRect = new(inRect.x, listY, inRect.width, listHeight);

        Rect scrollRect = ScrollUtil.BeginScrollView(viewRect, ref scroll, Mathf.Max(contentHeight, listHeight));

        for (int i = 0; i < defs.Count; i++)
        {
            FCPolicyDef def = defs[i];
            bool isSelected = selected.Contains(def);
            bool blocked = isBlocked?.Invoke(def) ?? false;
            Rect row = new(scrollRect.x, scrollRect.y + i * (rowHeight + smallMargin), scrollRect.width, rowHeight);

            string name = def.LabelCap;
            string buttonLabel;
            if (isSelected)
                buttonLabel = $">> {name} <<";
            else if (blocked)
                buttonLabel = $"{name} (!)";
            else
                buttonLabel = name;

            if (blocked && !isSelected)
                GUI.color = new Color(1f, 1f, 1f, 0.5f);

            if (Widgets.ButtonTextSubtle(row, buttonLabel, highlight: isSelected) && clickable)
            {
                onClick(def);
            }

            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;

            if (Mouse.IsOver(row))
                hovered = def;
        }

        ScrollUtil.EndScrollView();
    }

    protected static string PolicyDescription(FCPolicyDef def)
    {
        return FactionCache.FCPolicyDescs?[def] ?? def.PolicyDesc();
    }
}
