using UnityEngine;

namespace FactionColonies;

/// <summary>
/// Base for the searchable list pickers: a title, an optional header line, a search box, a
/// scrolling list of rows, and an empty-state label. Works in one of two modes:
/// <list type="bullet">
/// <item>Confirm (the default): clicking a row selects it, and the Confirm button picks the selection.</item>
/// <item><see cref="PicksOnClick"/>: clicking a row picks it at once and the window stays open, so
/// several items can be added in a row.</item>
/// </list>
/// </summary>
public abstract class FCWindow_SearchablePicker<T> : Window
{
    protected const float RowHeight = 30f;
    protected const float IconSize = 24f;
    protected const float SearchBarHeight = 28f;
    protected const float HeaderHeight = 24f;
    protected const float ButtonHeight = 35f;
    protected const float margin = 5f;
    private const float ButtonWidth = 120f;

    private string searchTerm = "";
    private Vector2 scrollPos;

    protected T selected;
    protected bool hasSelection;

    public override Vector2 InitialSize => new(450f, 550f);

    protected FCWindow_SearchablePicker()
    {
        draggable = true;
        doCloseX = true;
        absorbInputAroundWindow = true;
        forcePause = false;
    }

    protected abstract string Title { get; }

    /// <summary>Shown in the list box when no row matches; null shows nothing.</summary>
    protected abstract string EmptyText { get; }

    /// <summary>The rows on offer, already sorted. Called every frame, before the search filter.</summary>
    protected abstract List<T> Items();

    /// <summary>The text the search box matches against.</summary>
    protected abstract string SearchText(T item);

    /// <summary>Draws one row's contents, in scroll-view coordinates, over its highlight.</summary>
    protected abstract void DrawRow(Rect row, T item);

    /// <summary>Applies the pick: from a row click in <see cref="PicksOnClick"/> mode, otherwise
    /// from Confirm.</summary>
    protected abstract void Pick(T item);

    /// <summary>True: clicking a row picks it at once and there is no Confirm/Cancel bar.</summary>
    protected virtual bool PicksOnClick => false;

    /// <summary>In <see cref="PicksOnClick"/> mode, shows a Close button at the bottom right.</summary>
    protected virtual bool ShowCloseButton => false;

    /// <summary>Draws an optional line under the title, such as a count or a budget. Returns false
    /// when there is none.</summary>
    protected virtual bool DrawHeader(Rect rect) => false;

    /// <summary>Draws an optional button at the left of the Confirm/Cancel bar.</summary>
    protected virtual void DrawExtraButton(Rect rect) { }

    protected virtual bool IsSelected(T item) => hasSelection && EqualityComparer<T>.Default.Equals(selected, item);

    protected virtual void Select(T item)
    {
        selected = item;
        hasSelection = true;
    }

    protected virtual bool CanConfirm => hasSelection;

    protected virtual void Confirm() => Pick(selected);

    private bool ShowsButtonBar => !PicksOnClick || ShowCloseButton;

    public override void DoWindowContents(Rect inRect)
    {
        GameFont fontBefore = Text.Font;
        TextAnchor anchorBefore = Text.Anchor;

        Text.Font = GameFont.Medium;
        Text.Anchor = TextAnchor.UpperLeft;
        UIUtil.ClampedLabel(new Rect(0, 0, inRect.width, 35f), Title);

        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.MiddleLeft;
        float y = 38f;
        Rect headerRect = new(0, y, inRect.width, HeaderHeight);
        if (DrawHeader(headerRect))
            y = headerRect.yMax;
        GUI.color = Color.white;

        Rect searchRect = new(0, y + 2f, inRect.width, SearchBarHeight);
        searchTerm = Widgets.TextField(searchRect, searchTerm);

        List<T> items = Items();
        if (!string.IsNullOrEmpty(searchTerm))
            items = items.Where(i => (SearchText(i) ?? "").IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

        float listTop = searchRect.yMax + margin;
        float bottomH = ShowsButtonBar ? ButtonHeight : 0f;
        Rect scrollOutRect = new(0, listTop, inRect.width, inRect.height - listTop - bottomH - 15f);
        Widgets.DrawMenuSection(scrollOutRect);

        Rect scrollViewRect = ScrollUtil.BeginScrollView(scrollOutRect, ref scrollPos, items.Count * RowHeight);
        for (int i = 0; i < items.Count; i++)
        {
            T item = items[i];
            Rect row = new(0, i * RowHeight, scrollViewRect.width, RowHeight);
            if (!PicksOnClick && IsSelected(item))
                Widgets.DrawHighlightSelected(row);
            else if (i % 2 == 0)
                Widgets.DrawHighlight(row);

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            DrawRow(row, item);

            if (Widgets.ButtonInvisible(row))
            {
                if (PicksOnClick) Pick(item);
                else Select(item);
            }
        }
        ScrollUtil.EndScrollView();

        // Drawn after EndScrollView so it centers in the visible box, not the scrolled content.
        Text.Font = GameFont.Small;
        if (items.Count == 0 && EmptyText is string emptyText)
        {
            Text.Anchor = TextAnchor.MiddleCenter;
            UIUtil.ClampedLabel(scrollOutRect, emptyText);
        }

        if (ShowsButtonBar)
            DrawButtonBar(new Rect(0, inRect.height - ButtonHeight - 5f, inRect.width, ButtonHeight));

        Text.Font = fontBefore;
        Text.Anchor = anchorBefore;
    }

    private void DrawButtonBar(Rect bar)
    {
        Rect rightRect = new(bar.xMax - ButtonWidth, bar.y, ButtonWidth, bar.height);
        if (PicksOnClick)
        {
            if (UIUtil.ClampedButtonText(rightRect, "FCDialogPawnLoadoutClose".Translate()))
                Close();
            return;
        }

        DrawExtraButton(new Rect(bar.x, bar.y, ButtonWidth, bar.height));

        if (UIUtil.ClampedButtonText(rightRect, "CancelButton".Translate()))
            Close();

        bool canConfirm = CanConfirm;
        Rect confirmRect = new(rightRect.x - ButtonWidth - 10f, bar.y, ButtonWidth, bar.height);
        if (UIUtil.ClampedButtonText(confirmRect, "FCConfirm".Translate(), active: canConfirm) && canConfirm)
        {
            Confirm();
            Close();
        }
    }

    /// <summary>
    /// The common row layout: icon | info card | label | optional extra column | cost. A null
    /// <paramref name="thing"/> leaves the icon and info card blank.
    /// </summary>
    protected static void DrawThingRow(Rect row, ThingDef thing, string label, string cost, string extra = null)
    {
        Rect iconRect = new(row.x + margin, row.y, RowHeight, RowHeight);
        Rect infoRect = new(iconRect.xMax, row.y + 2f, RowHeight - 4f, RowHeight - 4f);
        Rect costRect = new(row.xMax - margin - 70f, row.y, 60f, RowHeight);
        Rect extraRect = new(costRect.x - 50f, row.y, 50f, RowHeight);
        float labelRight = extra is null ? costRect.x : extraRect.x;
        Rect labelRect = new(infoRect.xMax + margin, row.y, labelRight - infoRect.xMax - margin * 2, RowHeight);

        if (thing is not null)
        {
            Widgets.ThingIcon(iconRect, thing);
            Widgets.InfoCardButton(infoRect, thing);
        }

        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.MiddleLeft;
        UIUtil.ClampedLabel(labelRect, label);

        Text.Font = GameFont.Tiny;
        Text.Anchor = TextAnchor.MiddleRight;
        if (extra is not null)
            UIUtil.ClampedLabel(extraRect, extra);
        UIUtil.ClampedLabel(costRect, cost);
    }
}
