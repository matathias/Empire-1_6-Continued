using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    public class FCWindow_ColorPicker : Window
    {
        private Color color;
        private Color oldColor;
        private Action<Color> onAccept;
        private string title;

        private bool hsvWheelDragging;
        private string[] textfieldBuffers = new string[6];
        private Color textfieldColorBuffer;
        private string previousFocusedControlName;

        private List<Color> presetColors;

        private const float WheelSize = 140f;
        private const int SwatchSize = 22;
        private const int SwatchPadding = 2;
        private const int SwatchesPerRow = 12;
        private const float SectionGap = 6f;
        private const float LabelHeight = 20f;

        public override Vector2 InitialSize => new Vector2(620f, 600f);

        public FCWindow_ColorPicker(string title, Color initialColor, Action<Color> onAccept)
        {
            this.title = title;
            this.color = initialColor;
            this.oldColor = initialColor;
            this.onAccept = onAccept;

            forcePause = false;
            draggable = true;
            doCloseX = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
            closeOnAccept = false;

            BuildPresetColors();
        }

        private void BuildPresetColors()
        {
            presetColors = new List<Color> { Color.white, Color.black };
            foreach (ColorDef cd in DefDatabase<ColorDef>.AllDefsListForReading)
            {
                if (cd.colorType == ColorType.Ideo)
                    presetColors.Add(cd.color);
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            GameFont fontBefore = Text.Font;
            TextAnchor anchorBefore = Text.Anchor;

            float y = inRect.y;

            // Title
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            UIUtil.ClampedLabel(new Rect(inRect.x, y, inRect.width, 30f), title);
            y += 35f;
            Widgets.DrawLineHorizontal(inRect.x, y, inRect.width);
            y += SectionGap;

            // === Top section: HSV wheel (left) + preview/textfields (right) ===
            float topY = y;

            // HSV Wheel
            Rect wheelRect = new Rect(inRect.x, topY, WheelSize, WheelSize);
            Widgets.HSVColorWheel(wheelRect, ref color, ref hsvWheelDragging, null, "fcColorWheel");

            // Right side: preview + textfields (all using fixed positions, not advancing y)
            float rightX = wheelRect.xMax + 20f;

            // Current / Old color preview
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;

            float previewBoxWidth = 60f;
            float previewRowHeight = 22f;
            float labelWidth = 55f;
            float previewY = topY;

            UIUtil.ClampedLabel(new Rect(rightX, previewY, labelWidth, previewRowHeight), "Current:");
            Widgets.DrawBoxSolidWithOutline(
                new Rect(rightX + labelWidth, previewY + 1f, previewBoxWidth, previewRowHeight - 2f),
                color, Color.gray);

            previewY += previewRowHeight + 2f;

            UIUtil.ClampedLabel(new Rect(rightX, previewY, labelWidth, previewRowHeight), "Old:");
            Widgets.DrawBoxSolidWithOutline(
                new Rect(rightX + labelWidth, previewY + 1f, previewBoxWidth, previewRowHeight - 2f),
                oldColor, Color.gray);

            // Text fields: RGB column + HSV column, starting below previews
            float fieldsY = previewY + previewRowHeight + 8f;

            // RGB column
            RectAggregator rgbAgg = new RectAggregator(new Rect(rightX, fieldsY, 125f, 0f), 827364);
            Widgets.ColorTextfields(ref rgbAgg, ref color, ref textfieldBuffers,
                ref textfieldColorBuffer, previousFocusedControlName, "fcColorTextfields",
                Widgets.ColorComponents.Red | Widgets.ColorComponents.Green | Widgets.ColorComponents.Blue,
                Widgets.ColorComponents.Red | Widgets.ColorComponents.Green | Widgets.ColorComponents.Blue);

            // HSV column
            RectAggregator hsvAgg = new RectAggregator(new Rect(rightX + 140f, fieldsY, 125f, 0f), 827365);
            Widgets.ColorTextfields(ref hsvAgg, ref color, ref textfieldBuffers,
                ref textfieldColorBuffer, previousFocusedControlName, "fcColorTextfieldsHSV",
                Widgets.ColorComponents.Hue | Widgets.ColorComponents.Sat | Widgets.ColorComponents.Value,
                Widgets.ColorComponents.Hue | Widgets.ColorComponents.Sat | Widgets.ColorComponents.Value);

            if (Event.current.type == EventType.Layout)
            {
                previousFocusedControlName = GUI.GetNameOfFocusedControl();
            }

            // Brightness slider below the wheel
            float sliderY = wheelRect.yMax + 4f;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            UIUtil.ClampedLabel(new Rect(inRect.x, sliderY, 55f, 20f), "Brightness");
            Color.RGBToHSV(color, out float sliderH, out float sliderS, out float sliderV);
            float newV = Widgets.HorizontalSlider(
                new Rect(inRect.x + 58f, sliderY, WheelSize - 58f, 20f),
                sliderV, 0f, 1f);
            if (newV != sliderV)
            {
                color = Color.HSVToRGB(sliderH, sliderS, newV);
            }

            y = sliderY + 20f + SectionGap + 4f;

            // === Ideology Colors ===
            if (ModsConfig.IdeologyActive)
            {
                y = DrawIdeoSection(inRect.x, y, inRect.width);
            }

            // === Preset Colors ===
            y = DrawPresetSection(inRect.x, y, inRect.width);

            // === Saved Colors ===
            y = DrawSavedSection(inRect.x, y, inRect.width);

            // === Bottom buttons ===
            float btnWidth = 120f;
            float btnHeight = 30f;
            float btnY = inRect.yMax - btnHeight;

            if (UIUtil.ClampedButtonText(new Rect(inRect.xMax - btnWidth, btnY, btnWidth, btnHeight), "Accept".Translate()))
            {
                onAccept(color);
                Close();
            }
            if (UIUtil.ClampedButtonText(new Rect(inRect.xMax - btnWidth * 2 - 10f, btnY, btnWidth, btnHeight), "Cancel".Translate()))
            {
                Close();
            }

            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;
        }

        private float DrawIdeoSection(float x, float y, float width)
        {
            List<Ideo> ideos = null;
            try
            {
                if (Find.IdeoManager != null)
                    ideos = Find.IdeoManager.IdeosListForReading;
            }
            catch (Exception ex)
            {
                LogUtil.Warning("IdeoManager not available for color picker: " + ex);
            }

            if (ideos == null || ideos.Count == 0)
                return y;

            int visibleCount = 0;
            foreach (Ideo ideo in ideos)
            {
                if (ideo.Color != Color.white) visibleCount++;
            }
            if (visibleCount == 0) return y;

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            UIUtil.ClampedLabel(new Rect(x, y, width, LabelHeight), "fcColorPickerActiveIdeos".Translate());
            y += LabelHeight;

            float startX = x;
            int col = 0;
            foreach (Ideo ideo in ideos)
            {
                Color ideoColor = ideo.Color;
                if (ideoColor == Color.white) continue;

                Rect swatchRect = new Rect(
                    startX + col * (SwatchSize + SwatchPadding),
                    y,
                    SwatchSize, SwatchSize);

                bool isSelected = color.IndistinguishableFrom(ideoColor);
                Color outline = isSelected ? Color.white : new Color(0.4f, 0.4f, 0.4f);
                Widgets.DrawBoxSolidWithOutline(swatchRect, ideoColor, outline);

                if (Mouse.IsOver(swatchRect))
                {
                    Widgets.DrawHighlight(swatchRect);
                }

                TooltipHandler.TipRegion(swatchRect, ideo.name);

                if (Widgets.ButtonInvisible(swatchRect))
                {
                    color = ideoColor;
                }

                col++;
                if (col >= SwatchesPerRow)
                {
                    col = 0;
                    y += SwatchSize + SwatchPadding;
                }
            }

            y += SwatchSize + SwatchPadding + SectionGap;
            return y;
        }

        private float DrawPresetSection(float x, float y, float width)
        {
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            UIUtil.ClampedLabel(new Rect(x, y, width, LabelHeight), "fcColorPickerPresets".Translate());
            y += LabelHeight;

            Rect selectorRect = new Rect(x, y, width, 200f);
            float selectorHeight;
            Widgets.ColorSelector(selectorRect, ref color, presetColors, out selectorHeight);
            y += selectorHeight + SectionGap;
            return y;
        }

        private float DrawSavedSection(float x, float y, float width)
        {
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            List<Color> saved = FCSettings.savedPickerColors;
            UIUtil.ClampedLabel(new Rect(x, y, width, LabelHeight),
                "fcColorPickerSaved".Translate() + " (" + saved.Count + "/" + FCSettings.MaxSavedPickerColors + ")");
            y += LabelHeight;

            float startX = x;
            int col = 0;
            for (int i = 0; i < saved.Count; i++)
            {
                Color savedColor = saved[i];
                Rect swatchRect = new Rect(
                    startX + col * (SwatchSize + SwatchPadding),
                    y,
                    SwatchSize, SwatchSize);

                bool isSelected = color.IndistinguishableFrom(savedColor);
                Color outline = isSelected ? Color.white : new Color(0.4f, 0.4f, 0.4f);
                Widgets.DrawBoxSolidWithOutline(swatchRect, savedColor, outline);

                if (Mouse.IsOver(swatchRect))
                {
                    Widgets.DrawHighlight(swatchRect);
                }

                TooltipHandler.TipRegion(swatchRect, "fcColorPickerRightClickRemove".Translate());

                // Right click to remove (must be checked before ButtonInvisible consumes mouse events)
                if (Event.current.type == EventType.MouseDown
                    && Event.current.button == 1
                    && Mouse.IsOver(swatchRect))
                {
                    saved.RemoveAt(i);
                    WriteSavedColors();
                    Event.current.Use();
                    break;
                }

                // Left click to select
                if (Widgets.ButtonInvisible(swatchRect))
                {
                    color = savedColor;
                }

                col++;
                if (col >= SwatchesPerRow)
                {
                    col = 0;
                    y += SwatchSize + SwatchPadding;
                }
            }

            if (col > 0)
                y += SwatchSize + SwatchPadding;

            // Save Current button on its own row
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            Rect saveBtn = new Rect(x, y, 90f, SwatchSize);
            if (UIUtil.ClampedButtonText(saveBtn, "fcColorPickerSaveCurrent".Translate()))
            {
                if (saved.Count >= FCSettings.MaxSavedPickerColors)
                    saved.RemoveAt(0);
                saved.Add(color);
                WriteSavedColors();
            }

            y += SwatchSize + SectionGap;
            return y;
        }

        private static void WriteSavedColors()
        {
            LoadedModManager.GetMod<FactionColoniesMod>().WriteSettings();
        }
    }
}
