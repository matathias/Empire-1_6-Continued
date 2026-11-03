using UnityEngine;

namespace FactionColonies;

/*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*
 * Scroll views with a slim, custom-drawn vertical scrollbar.
 *
 * The bar is drawn and driven here rather than styled through GUI.skin: the 1.7
 * engine's GUI.BeginScrollView ignores the skin's scrollbar styles and always
 * paints its own fixed 16px bar. The engine bar is suppressed by passing
 * showScrollbars: false, which still leaves mouse-wheel scrolling to the engine.
 *-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*/
public static class ScrollUtil
{
    public const float ScrollbarWidth = 10f;
    private const float MinThumbHeight = 20f;

    private readonly record struct ScrollState(Rect OutRect, float ContentHeight, Vector2 ScrollPosition, int Id);

    // One entry per open scroll view, so nested views draw their own bars
    private static readonly Stack<ScrollState> openViews = new();

    // Scroll view whose thumb is being dragged (0 = none), and where on the thumb it was grabbed
    private static int dragId;
    private static float dragGrabOffset;

    /// <summary>
    /// Begins a scroll view with a custom-styled scrollbar. Returns the viewRect for content layout.
    /// Pair with <see cref="EndScrollView"/>.
    /// </summary>
    public static Rect BeginScrollView(Rect outRect, ref Vector2 scrollPosition, float contentHeight)
    {
        bool needsScroll = contentHeight > outRect.height;
        float viewWidth = needsScroll ? outRect.width - (ScrollbarWidth + 1f) : outRect.width;
        Rect viewRect = new(0f, 0f, viewWidth, Mathf.Max(contentHeight, outRect.height));

        int id = IdFor(outRect);
        // Input runs before the content so a click on the bar never reaches the controls beneath it
        if (needsScroll)
            HandleScrollbarInput(outRect, contentHeight, ref scrollPosition, id);

        Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect, showScrollbars: false);
        openViews.Push(new ScrollState(outRect, contentHeight, scrollPosition, id));
        return viewRect;
    }

    /// <summary>
    /// Ends a scroll view started with <see cref="BeginScrollView"/> and draws its scrollbar.
    /// </summary>
    public static void EndScrollView()
    {
        Widgets.EndScrollView();

        ScrollState state = openViews.Pop();
        if (state.ContentHeight > state.OutRect.height && Event.current.type == EventType.Repaint)
            DrawScrollbar(state);
    }

    // Stable across events and frames for a view that stays put, unlike GUIUtility.GetControlID
    private static int IdFor(Rect outRect)
    {
        Rect screen = GUIUtility.GUIToScreenRect(outRect);
        int hash = HashCode.Combine(Mathf.RoundToInt(screen.x), Mathf.RoundToInt(screen.y),
            Mathf.RoundToInt(screen.width), Mathf.RoundToInt(screen.height));
        return hash == 0 ? 1 : hash;
    }

    private static Rect TrackRect(Rect outRect) =>
        new(outRect.xMax - ScrollbarWidth, outRect.y, ScrollbarWidth, outRect.height);

    private static float ThumbHeight(Rect outRect, float contentHeight) =>
        Mathf.Clamp(outRect.height * outRect.height / contentHeight, MinThumbHeight, outRect.height);

    private static Rect ThumbRect(Rect outRect, float contentHeight, float scrollY)
    {
        Rect track = TrackRect(outRect);
        float thumbHeight = ThumbHeight(outRect, contentHeight);
        float maxScroll = contentHeight - outRect.height;
        float travel = track.height - thumbHeight;
        float y = track.y + (maxScroll > 0f ? Mathf.Clamp01(scrollY / maxScroll) * travel : 0f);
        return new Rect(track.x, y, track.width, thumbHeight);
    }

    private static void HandleScrollbarInput(Rect outRect, float contentHeight, ref Vector2 scrollPosition, int id)
    {
        Event evt = Event.current;
        Rect track = TrackRect(outRect);
        float thumbHeight = ThumbHeight(outRect, contentHeight);
        float travel = track.height - thumbHeight;
        if (travel <= 0f) return;

        if (evt.type == EventType.MouseDown && evt.button == 0 && Mouse.IsOver(track))
        {
            Rect thumb = ThumbRect(outRect, contentHeight, scrollPosition.y);
            // Grabbing the thumb keeps the grab point; clicking the bare track centers the thumb on the cursor
            dragGrabOffset = thumb.Contains(evt.mousePosition) ? evt.mousePosition.y - thumb.y : thumbHeight * 0.5f;
            dragId = id;
        }
        else if (dragId != id)
        {
            return;
        }
        else if (!Input.GetMouseButton(0) || evt.type == EventType.MouseUp)
        {
            dragId = 0;
            if (evt.type == EventType.MouseUp) evt.Use();
            return;
        }
        else if (evt.type != EventType.MouseDrag)
        {
            return;
        }

        float fraction = Mathf.Clamp01((evt.mousePosition.y - dragGrabOffset - track.y) / travel);
        scrollPosition.y = fraction * (contentHeight - outRect.height);
        evt.Use();
    }

    private static void DrawScrollbar(ScrollState state)
    {
        GUI.DrawTexture(TrackRect(state.OutRect), TexLoad.scrollTrack);

        Rect thumb = ThumbRect(state.OutRect, state.ContentHeight, state.ScrollPosition.y);
        Texture2D thumbTex = dragId == state.Id ? TexLoad.scrollThumbActive
            : Mouse.IsOver(thumb) ? TexLoad.scrollThumbHover
            : TexLoad.scrollThumb;
        GUI.DrawTexture(thumb, thumbTex);
    }
}
