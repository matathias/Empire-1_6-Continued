using UnityEngine;

namespace FactionColonies;

public abstract class MilitaryWindow
{
    public string selectedText;
    public float scroll;

    public abstract MilitaryWindowSlot Slot { get; }

    public abstract void DrawTab(Rect rect);

    public virtual void Select(IExposable selecting)
    {
        throw new ApplicationException("Trying to select " + selecting + " on window " + this);
    }

    public void ScrollWindow(float num, float maxScroll)
    {
        if (scroll - num * 5 < -1 * maxScroll)
        {
            scroll = -1f * maxScroll;
        }
        else if (scroll - num * 5 > 0)
        {
            scroll = 0;
        }
        else
        {
            scroll -= (int)Event.current.delta.y * 5;
        }

        Event.current.Use();
    }

    /* Template sidebar: search box, template list, and a 2x2 Create/Import/Delete/Export grid */

    private const float SidebarRowHeight = 30f;
    private const float SidebarSearchHeight = 28f;
    private const float SidebarButtonHeight = 30f;
    private const float SidebarIconSize = 24f;
    private const float SidebarMargin = 5f;

    /// <summary>
    /// Draws the template sidebar shared by the unit, squad and fire-support designers: a search
    /// box, the matching templates (clicking one calls <paramref name="select"/>), and a 2x2 button
    /// grid. Delete and Export only show while <paramref name="selected"/> is set.
    /// <paramref name="drawIcon"/>, when given, draws an icon left of each name.
    /// </summary>
    protected static void DrawTemplateSidebar<T>(Rect rect, ref string searchTerm, ref Vector2 scrollPos,
        List<T> templates, T selected, Func<T, string> nameOf, Action<T> select,
        (string label, Action onClick) create, (string label, Action onClick) import,
        (string label, Action onClick) delete, (string label, Action onClick) export,
        Action<Rect, T> drawIcon = null) where T : class
    {
        GameFont fontBefore = Text.Font;
        TextAnchor anchorBefore = Text.Anchor;

        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.MiddleLeft;
        Rect searchRect = new(rect.x, rect.y, rect.width, SidebarSearchHeight);
        searchTerm = Widgets.TextField(searchRect, searchTerm);

        // The list fills the space between the search bar and the buttons
        float buttonsHeight = SidebarButtonHeight * 2 + SidebarMargin;
        float listHeight = rect.yMax - searchRect.yMax - SidebarMargin - buttonsHeight - SidebarMargin;
        Rect listOutRect = new(rect.x, searchRect.yMax + SidebarMargin, rect.width, listHeight);
        Widgets.DrawMenuSection(listOutRect);

        string term = searchTerm;
        templates ??= [];
        List<T> filtered = string.IsNullOrEmpty(term)
            ? templates
            : templates.Where(t => (nameOf(t) ?? "").IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

        Rect scrollViewRect = ScrollUtil.BeginScrollView(listOutRect, ref scrollPos, filtered.Count * SidebarRowHeight);
        for (int i = 0; i < filtered.Count; i++)
        {
            T template = filtered[i];
            Rect row = new(scrollViewRect.x, scrollViewRect.y + i * SidebarRowHeight, scrollViewRect.width, SidebarRowHeight);

            if (template == selected)
                Widgets.DrawHighlightSelected(row);
            else if (i % 2 == 0)
                Widgets.DrawHighlight(row);

            Rect labelRect = new(row.x + 4f, row.y, row.width - 6f, SidebarRowHeight);
            if (drawIcon is not null)
            {
                Rect iconRect = new(row.x + 2f, row.y + 3f, SidebarIconSize, SidebarIconSize);
                drawIcon(iconRect, template);
                labelRect = new Rect(iconRect.xMax + 4f, row.y, row.xMax - iconRect.xMax - 6f, SidebarRowHeight);
            }
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            UIUtil.ClampedLabel(labelRect, nameOf(template));

            if (Widgets.ButtonInvisible(row))
                select(template);
        }
        ScrollUtil.EndScrollView();

        float btnY = listOutRect.yMax + SidebarMargin;
        float buttonW = (rect.width - SidebarMargin) / 2f;
        float rightX = rect.x + buttonW + SidebarMargin;
        float lowerY = btnY + SidebarButtonHeight + SidebarMargin;

        Text.Font = GameFont.Tiny;
        Text.Anchor = TextAnchor.MiddleCenter;

        if (UIUtil.ClampedButtonText(new Rect(rect.x, btnY, buttonW, SidebarButtonHeight), create.label))
            create.onClick();
        if (UIUtil.ClampedButtonText(new Rect(rightX, btnY, buttonW, SidebarButtonHeight), import.label))
            import.onClick();
        if (selected is not null)
        {
            if (UIUtil.ClampedButtonText(new Rect(rect.x, lowerY, buttonW, SidebarButtonHeight), delete.label))
                delete.onClick();
            if (UIUtil.ClampedButtonText(new Rect(rightX, lowerY, buttonW, SidebarButtonHeight), export.label))
                export.onClick();
        }

        Text.Font = fontBefore;
        Text.Anchor = anchorBefore;
    }
}