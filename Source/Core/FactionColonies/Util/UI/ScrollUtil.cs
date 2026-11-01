using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    public static class ScrollUtil
    {
        public const float ScrollbarWidth = 10f;

        // Built once on first use, reused every frame
        private static GUIStyle trackStyle;
        private static GUIStyle thumbStyle;

        // Stack of saved originals — supports nested scroll views
        private static readonly Stack<GUIStyle> savedTracks = new Stack<GUIStyle>();
        private static readonly Stack<GUIStyle> savedThumbs = new Stack<GUIStyle>();

        private static void EnsureStyles()
        {
            if (trackStyle is object) return;

            trackStyle = new GUIStyle(GUI.skin.verticalScrollbar);
            trackStyle.normal.background = TexLoad.scrollTrack;
            trackStyle.fixedWidth = ScrollbarWidth;

            thumbStyle = new GUIStyle(GUI.skin.verticalScrollbarThumb);
            thumbStyle.normal.background = TexLoad.scrollThumb;
            thumbStyle.hover.background = TexLoad.scrollThumbHover;
            thumbStyle.active.background = TexLoad.scrollThumbActive;
            thumbStyle.fixedWidth = ScrollbarWidth;
        }

        /// <summary>
        /// Begins a scroll view with a custom-styled scrollbar. Returns the viewRect for content layout.
        /// Pair with <see cref="EndScrollView"/>.
        /// </summary>
        public static Rect BeginScrollView(Rect outRect, ref Vector2 scrollPosition, float contentHeight)
        {
            EnsureStyles();

            bool needsScroll = contentHeight > outRect.height;
            float viewWidth = needsScroll ? outRect.width - (ScrollbarWidth + 1f) : outRect.width;
            Rect viewRect = new Rect(0f, 0f, viewWidth, Mathf.Max(contentHeight, outRect.height));

            savedTracks.Push(GUI.skin.verticalScrollbar);
            savedThumbs.Push(GUI.skin.verticalScrollbarThumb);
            GUI.skin.verticalScrollbar = trackStyle;
            GUI.skin.verticalScrollbarThumb = thumbStyle;

            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
            return viewRect;
        }

        /// <summary>
        /// Ends a scroll view started with <see cref="BeginScrollView"/> and restores the original scrollbar styles.
        /// </summary>
        public static void EndScrollView()
        {
            Widgets.EndScrollView();

            GUI.skin.verticalScrollbar = savedTracks.Pop();
            GUI.skin.verticalScrollbarThumb = savedThumbs.Pop();
        }
    }
}
