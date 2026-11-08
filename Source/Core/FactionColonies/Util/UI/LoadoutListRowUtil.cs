using UnityEngine;

namespace FactionColonies;

/* Row pieces shared by the unit designer's list widgets (animals, mechs, apparel, inventory,
 * implants). Each helper draws exactly what the widgets used to draw inline, including the font
 * and anchor it sets, so callers keep their layouts. */
internal static class LoadoutListRowUtil
{
    public const float RemoveButtonSize = 20f;
    public const float StepperButtonSize = 20f;

    /* Square remove-button rect whose right edge sits at rightEdge, vertically centered in row. */
    public static Rect RemoveButtonRect(Rect row, float rightEdge)
        => new(rightEdge - RemoveButtonSize, row.y + (row.height - RemoveButtonSize) / 2f, RemoveButtonSize, RemoveButtonSize);

    /* Draws the "X" remove button; true when clicked. */
    public static bool RemoveButton(Rect removeRect)
    {
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.MiddleCenter;
        return UIUtil.ClampedButtonText(removeRect, "X");
    }

    /* Draws a count right-aligned against cursorRight: "- xN +" steppers when editable, a plain
     * "xN" otherwise. Moves cursorRight past what it drew. Returns -1 or +1 when a stepper was
     * clicked, else 0. */
    public static int CountStepper(Rect row, ref float cursorRight, int count, bool editable)
    {
        int clicked = 0;
        string countLabel = $"x{Mathf.Max(1, count)}";
        if (editable)
        {
            Rect plusRect = new(cursorRight - StepperButtonSize, row.y + (row.height - StepperButtonSize) / 2f, StepperButtonSize, StepperButtonSize);
            Rect countRect = new(plusRect.x - 26f, row.y, 26f, row.height);
            Rect minusRect = new(countRect.x - StepperButtonSize, plusRect.y, StepperButtonSize, StepperButtonSize);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            if (UIUtil.ClampedButtonText(minusRect, "-")) clicked = -1;
            UIUtil.ClampedLabel(countRect, countLabel);
            if (UIUtil.ClampedButtonText(plusRect, "+")) clicked = 1;
            cursorRight = minusRect.x - 6f;
        }
        else
        {
            Rect countRect = new(cursorRight - 30f, row.y, 30f, row.height);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleRight;
            UIUtil.ClampedLabel(countRect, countLabel);
            cursorRight = countRect.x - 6f;
        }
        return clicked;
    }

    /* Tiny left-aligned label, cut with an ellipsis to fit; the full text shows as a tooltip when cut. */
    public static void TruncatedLabel(Rect labelRect, string label)
    {
        Text.Font = GameFont.Tiny;
        Text.Anchor = TextAnchor.MiddleLeft;
        string shown = Text.ClampTextWithEllipsis(labelRect, label);
        UIUtil.ClampedLabel(labelRect, shown);
        if (shown != label) TooltipHandler.TipRegion(labelRect, label);
    }
}
