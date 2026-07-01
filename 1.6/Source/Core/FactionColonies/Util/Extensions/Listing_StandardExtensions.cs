using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    /*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*/
    /* Listing_Standard helpers for the mod settings window.                       */
    /*                                                                             */
    /* SliderTextField draws, on one line: a name label, a read-only value         */
    /* readout, a slider, an editable numeric box (right-aligned), and an optional  */
    /* unit suffix. The player can drag or type an exact value; the two stay in     */
    /* sync, and an external change to the bound value (Reset-to-defaults, a         */
    /* difficulty preset, the min/max nudge) resyncs the box on the next frame.     */
    /* Rows zebra-stripe and highlight on mouseover when they carry a tooltip.       */
    /*                                                                             */
    /* Call ResetRowStripe() at the top of each tab so striping restarts cleanly.   */
    /*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*/
    public static class Listing_StandardExtensions
    {
        // Per-field text box contents and last-returned values, keyed by the caller's
        // unique key. The settings window is single-threaded GUI code, so static is safe.
        private static readonly Dictionary<string, string> buffers = new Dictionary<string, string>();
        private static readonly Dictionary<string, float> lastValues = new Dictionary<string, float>();

        // Zebra-stripe row counter; only slider rows advance it, so consecutive sliders
        // always alternate even with checkboxes/headers between them.
        private static int stripe;

        private const float RowHeight = 28f;
        private const float SliderHeight = 22f;
        private const float FieldHeight = 24f;
        private const float FieldWidth = 54f;
        private const float ValueWidth = 52f;
        private const float UnitWidth = 26f;
        private const float LabelPct = 0.45f;
        private const float Gap = 6f;

        /// <summary>Restarts zebra striping. Call once at the top of each settings tab draw.</summary>
        public static void ResetRowStripe()
        {
            stripe = 0;
        }

        /* Lays out [ name ] [ value ] [ slider ] [ box ] [ unit ] on one row, draws the
         * zebra/hover background, name label, value readout, and unit, and returns the
         * slider and box rects for the caller. Advances the zebra counter. */
        private static void LayoutRow(Listing_Standard ls, string label, string valueText,
            string unit, string tooltip, out Rect sliderRect, out Rect fieldRect)
        {
            Rect row = ls.GetRect(RowHeight);

            if (stripe % 2 == 1) Widgets.DrawAltRect(row);
            if (!tooltip.NullOrEmpty())
            {
                Widgets.DrawHighlightIfMouseover(row);
                TooltipHandler.TipRegion(row, tooltip);
            }
            stripe++;

            TextAnchor prevAnchor = Text.Anchor;

            // Name label (far left).
            Rect labelRect = new Rect(row.x, row.y, row.width * LabelPct, row.height);
            Text.Anchor = TextAnchor.MiddleLeft;
            UIUtil.ClampedLabel(labelRect, label);

            // Unit area on the far right — always reserved so the boxes line up.
            Rect unitRect = new Rect(row.xMax - UnitWidth, row.y, UnitWidth, row.height);
            if (!unit.NullOrEmpty()) UIUtil.ClampedLabel(unitRect, unit);

            // Editable box, left of the reserved unit area.
            fieldRect = new Rect(unitRect.x - Gap - FieldWidth, row.y + (RowHeight - FieldHeight) / 2f, FieldWidth, FieldHeight);

            // Value readout, right-aligned so it hugs the slider's left edge.
            Rect valueRect = new Rect(labelRect.xMax + Gap, row.y, ValueWidth, row.height);
            Text.Anchor = TextAnchor.MiddleRight;
            UIUtil.ClampedLabel(valueRect, valueText);
            Text.Anchor = prevAnchor;

            // Slider fills the gap between the value readout and the box.
            float sliderX = valueRect.xMax + Gap;
            sliderRect = new Rect(sliderX, row.y + (RowHeight - SliderHeight) / 2f, fieldRect.x - Gap - sliderX, SliderHeight);
        }

        /* Draws a right-aligned numeric text box. CurTextFieldStyle is a shared GUIStyle,
         * so its alignment is restored immediately after the draw. */
        private static void DrawNumericField<T>(Rect rect, ref T value, ref string buffer, float min, float max) where T : struct
        {
            GUIStyle fieldStyle = Text.CurTextFieldStyle;
            TextAnchor prevAlign = fieldStyle.alignment;
            fieldStyle.alignment = TextAnchor.MiddleRight;
            Widgets.TextFieldNumeric(rect, ref value, ref buffer, min, max);
            fieldStyle.alignment = prevAlign;
        }

        /// <summary>
        /// Draws a label, value readout, slider, and editable numeric box on one row.
        /// Returns the (possibly changed) value. <paramref name="key"/> must be unique per
        /// setting — the field's translation key works well.
        /// </summary>
        public static float SliderTextField(this Listing_Standard ls, string key, string label,
            float value, float min, float max, int decimals = 2, string unit = null, string tooltip = null)
        {
            string format = decimals <= 0 ? "0" : "0." + new string('0', decimals);

            LayoutRow(ls, label, value.ToString(format) + (unit ?? ""), unit, tooltip,
                out Rect sliderRect, out Rect fieldRect);

            // Resync the box when the value was changed from outside this widget.
            if (!lastValues.TryGetValue(key, out float last) || !Mathf.Approximately(last, value))
            {
                buffers[key] = value.ToString(format);
            }

            // HorizontalSlider returns the input value unchanged when the thumb isn't being
            // dragged, so only round/resync when the player actually moved the slider — that
            // keeps typed precision in the box intact mid-edit.
            float sliderVal = Widgets.HorizontalSlider(sliderRect, value, min, max);
            if (!Mathf.Approximately(sliderVal, value))
            {
                value = (float)Math.Round(sliderVal, Math.Max(0, decimals));
                buffers[key] = value.ToString(format);
            }

            string buffer = buffers.TryGetValue(key, out string b) ? b : value.ToString(format);
            DrawNumericField(fieldRect, ref value, ref buffer, min, max);
            buffers[key] = buffer;
            lastValues[key] = value;
            return value;
        }

        /// <summary>Integer overload of <see cref="SliderTextField(Listing_Standard,string,string,float,float,float,int,string,string)"/>.</summary>
        public static int SliderTextField(this Listing_Standard ls, string key, string label,
            int value, int min, int max, string unit = null, string tooltip = null)
        {
            LayoutRow(ls, label, value.ToString() + (unit ?? ""), unit, tooltip,
                out Rect sliderRect, out Rect fieldRect);

            if (!lastValues.TryGetValue(key, out float last) || last != value)
            {
                buffers[key] = value.ToString();
            }

            int sliderVal = (int)Widgets.HorizontalSlider(sliderRect, value, min, max);
            if (sliderVal != value)
            {
                value = sliderVal;
                buffers[key] = value.ToString();
            }

            string buffer = buffers.TryGetValue(key, out string b) ? b : value.ToString();
            DrawNumericField(fieldRect, ref value, ref buffer, min, max);
            buffers[key] = buffer;
            lastValues[key] = value;
            return value;
        }
    }
}
