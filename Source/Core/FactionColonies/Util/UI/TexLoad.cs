using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    [StaticConstructorOnStartup]
    public static class TexLoad
    {
        static TexLoad()
        {
            var icons = ContentFinder<Texture2D>.GetAllInFolder("FactionIcons");
            factionIcons = icons.ToList();
            if (factionIcons.NullOrEmpty())
            {
                LogUtil.Error("No faction icons found, will probably result in Empire not working properly.");
            }
            checkerboard = CreateCheckerboard();
            gradientHorizontal = CreateHorizontalGradient();
            gradientVertical = CreateVerticalGradient();
            scrollTrack = SolidTex("ScrollTrack", new Color(0.1f, 0.1f, 0.1f, 0.3f));
            scrollThumb = SolidTex("ScrollThumb", new Color(0.5f, 0.5f, 0.5f, 0.7f));
            scrollThumbHover = SolidTex("ScrollThumbHover", new Color(0.7f, 0.7f, 0.7f, 0.85f));
            scrollThumbActive = SolidTex("ScrollThumbActive", new Color(0.8f, 0.8f, 0.8f, 0.9f));
        }

        public static readonly Texture2D questionmark = ContentFinder<Texture2D>.Get("GUI/questionmark");
        public static readonly Texture2D buildingLocked = ContentFinder<Texture2D>.Get("GUI/LockedBuildingSlot");
        public static readonly Texture2D refreshIcon = ContentFinder<Texture2D>.Get("GUI/Buttons/Refresh");

        // Tynan made the definitions in Verse internal so we gotta get them here
        public static readonly Texture2D deleteX = ContentFinder<Texture2D>.Get("UI/Buttons/Delete");

        //test icons
        public static readonly Texture2D iconHappiness = ContentFinder<Texture2D>.Get("GUI/Happiness");
        public static readonly Texture2D iconLoyalty = ContentFinder<Texture2D>.Get("GUI/Loyalty");
        public static readonly Texture2D iconUnrest = ContentFinder<Texture2D>.Get("GUI/Unrest");
        public static readonly Texture2D iconProsperity = ContentFinder<Texture2D>.Get("GUI/Prosperity");
        public static readonly Texture2D iconMilitary = ContentFinder<Texture2D>.Get("GUI/MilitaryLevel");
        public static readonly Texture2D iconCustomize = ContentFinder<Texture2D>.Get("GUI/customizebutton");

        public static readonly Texture2D iconTrade = ContentFinder<Texture2D>.Get("UI/Commands/Trade");
        public static readonly Texture2D codexLogo = ContentFinder<Texture2D>.Get("UI/Icons/EmpireLogo");

        public static List<Texture2D> factionIcons = new List<Texture2D>();
        public static readonly Texture2D checkerboard;
        public static readonly Texture2D gradientHorizontal;
        public static readonly Texture2D gradientVertical;

        // Scrollbar textures (1x1 solid color, used by ScrollUtil GUIStyles)
        public static readonly Texture2D scrollTrack;
        public static readonly Texture2D scrollThumb;
        public static readonly Texture2D scrollThumbHover;
        public static readonly Texture2D scrollThumbActive;

        private static Texture2D CreateHorizontalGradient()
        {
            const int width = 256;
            Texture2D tex = new Texture2D(width, 1, TextureFormat.ARGB32, false);
            tex.name = "GradientHorizontalTex";
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            for (int x = 0; x < width; x++)
            {
                float alpha = 1f - (float)x / (width - 1);
                tex.SetPixel(x, 0, new Color(1f, 1f, 1f, alpha));
            }

            tex.Apply();
            return tex;
        }

        /// <summary>
        /// Draws a horizontal gradient that fades from <paramref name="color"/> to transparent.
        /// Left-to-right by default; pass <paramref name="reversed"/> = true to flip (transparent-to-color).
        /// Uses the cached gradient texture tinted via GUI.color.
        /// </summary>
        public static void DrawHorizontalGradient(Rect rect, Color color, bool reversed = false)
        {
            Color prev = GUI.color;
            GUI.color = color;
            if (reversed)
                GUI.DrawTextureWithTexCoords(rect, gradientHorizontal, new Rect(1, 0, -1, 1));
            else
                GUI.DrawTexture(rect, gradientHorizontal, ScaleMode.StretchToFill, true);
            GUI.color = prev;
        }
        public static void DrawHorizontalGradientLine(float x, float y, float width, Color color)
        {
            DrawHorizontalGradient(new Rect(x, y, width, 1f), color);
        }

        private static Texture2D CreateVerticalGradient()
        {
            const int height = 256;
            Texture2D tex = new Texture2D(1, height, TextureFormat.ARGB32, false);
            tex.name = "GradientVerticalTex";
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            for (int y = 0; y < height; y++)
            {
                float alpha = (float)y / (height - 1);
                tex.SetPixel(0, y, new Color(1f, 1f, 1f, alpha));
            }

            tex.Apply();
            return tex;
        }

        /// <summary>
        /// Draws a vertical gradient that fades from <paramref name="color"/> to transparent (top to bottom).
        /// Uses the cached gradient texture tinted via GUI.color.
        /// </summary>
        public static void DrawVerticalGradient(Rect rect, Color color)
        {
            Color prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, gradientVertical, ScaleMode.StretchToFill, true);
            GUI.color = prev;
        }
        public static void DrawVerticalGradientLine(float x, float y, float height, Color color)
        {
            DrawVerticalGradient(new Rect(x, y, 1f, height), color);
        }

        /// <summary>
        /// Draws a horizontal gradient that fades in from transparent, peaks in the middle, and fades back out.
        /// </summary>
        public static void DrawHorizontalPeakGradient(Rect rect, Color color)
        {
            DrawHorizontalPeakGradient(rect, color, 0.5f, 0.5f);
        }
        public static void DrawHorizontalPeakGradientLine(float x, float y, float width, Color color)
        {
            DrawHorizontalPeakGradient(new Rect(x, y, width, 1f), color);
        }

        /// <summary>
        /// Draws a horizontal gradient with three segments: fade-in, constant alpha, and fade-out.
        /// <paramref name="fadeInFraction"/> and <paramref name="fadeOutFraction"/> are 0..1 fractions of the rect width.
        /// The remainder is filled at constant alpha.
        /// </summary>
        public static void DrawHorizontalPeakGradient(Rect rect, Color color, float fadeInFraction, float fadeOutFraction)
        {
            float total = fadeInFraction + fadeOutFraction;
            if (total > 1f)
            {
                fadeInFraction /= total;
                fadeOutFraction /= total;
            }

            Color prev = GUI.color;
            GUI.color = color;

            float fadeInWidth = rect.width * fadeInFraction;
            float fadeOutWidth = rect.width * fadeOutFraction;
            float constantWidth = rect.width - fadeInWidth - fadeOutWidth;

            if (fadeInFraction > 0f)
            {
                Rect fadeInRect = new Rect(rect.x, rect.y, fadeInWidth, rect.height);
                GUI.DrawTextureWithTexCoords(fadeInRect, gradientHorizontal, new Rect(1, 0, -1, 1));
            }

            if (constantWidth > 0f)
            {
                Rect constantRect = new Rect(rect.x + fadeInWidth, rect.y, constantWidth, rect.height);
                GUI.DrawTexture(constantRect, BaseContent.WhiteTex, ScaleMode.StretchToFill, true);
            }

            if (fadeOutFraction > 0f)
            {
                Rect fadeOutRect = new Rect(rect.x + fadeInWidth + constantWidth, rect.y, fadeOutWidth, rect.height);
                GUI.DrawTexture(fadeOutRect, gradientHorizontal, ScaleMode.StretchToFill, true);
            }

            GUI.color = prev;
        }

        /// <summary>
        /// Draws a vertical gradient that fades in from transparent, peaks in the middle, and fades back out.
        /// </summary>
        public static void DrawVerticalPeakGradient(Rect rect, Color color)
        {
            DrawVerticalPeakGradient(rect, color, 0.5f, 0.5f);
        }
        public static void DrawVerticalPeakGradientLine(float x, float y, float height, Color color)
        {
            DrawVerticalPeakGradient(new Rect(x, y, 1f, height), color);
        }

        /// <summary>
        /// Draws a vertical gradient with three segments: fade-in, constant alpha, and fade-out.
        /// <paramref name="fadeInFraction"/> and <paramref name="fadeOutFraction"/> are 0..1 fractions of the rect height.
        /// The remainder is filled at constant alpha.
        /// </summary>
        public static void DrawVerticalPeakGradient(Rect rect, Color color, float fadeInFraction, float fadeOutFraction)
        {
            float total = fadeInFraction + fadeOutFraction;
            if (total > 1f)
            {
                fadeInFraction /= total;
                fadeOutFraction /= total;
            }

            Color prev = GUI.color;
            GUI.color = color;

            float fadeInHeight = rect.height * fadeInFraction;
            float fadeOutHeight = rect.height * fadeOutFraction;
            float constantHeight = rect.height - fadeInHeight - fadeOutHeight;

            if (fadeInFraction > 0f)
            {
                Rect fadeInRect = new Rect(rect.x, rect.y, rect.width, fadeInHeight);
                GUI.DrawTextureWithTexCoords(fadeInRect, gradientVertical, new Rect(0, 1, 1, -1));
            }

            if (constantHeight > 0f)
            {
                Rect constantRect = new Rect(rect.x, rect.y + fadeInHeight, rect.width, constantHeight);
                GUI.DrawTexture(constantRect, BaseContent.WhiteTex, ScaleMode.StretchToFill, true);
            }

            if (fadeOutFraction > 0f)
            {
                Rect fadeOutRect = new Rect(rect.x, rect.y + fadeInHeight + constantHeight, rect.width, fadeOutHeight);
                GUI.DrawTexture(fadeOutRect, gradientVertical, ScaleMode.StretchToFill, true);
            }

            GUI.color = prev;
        }

        private static Texture2D SolidTex(string name, Color color)
        {
            Texture2D tex = new Texture2D(1, 1, TextureFormat.ARGB32, false);
            tex.name = name;
            tex.SetPixel(0, 0, color);
            tex.Apply();
            return tex;
        }

        private static Texture2D CreateCheckerboard()
        {
            int size = 8;
            int cellSize = 4;
            Color light = new Color(1f, 1f, 1f, 0.15f);
            Color dark = new Color(0f, 0f, 0f, 0.1f);

            Texture2D tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
            tex.name = "CheckerboardTex";
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Repeat;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool isLight = ((x / cellSize) + (y / cellSize)) % 2 == 0;
                    tex.SetPixel(x, y, isLight ? light : dark);
                }
            }

            tex.Apply();
            return tex;
        }

    }
}
