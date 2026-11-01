using System;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    public class FCWindow_ConfirmSettle : Window
    {
        private const float WindowWidth = 450f;
        private const float TitleHeight = 30f;
        private const float ConfirmTextHeight = 40f;
        private const float CheckboxHeight = 24f;
        private const float ButtonHeight = 30f;
        private const float Padding = 10f;

        private readonly WorldSettlementDef settlementType;
        private readonly int silverCost;
        private readonly Action onConfirm;
        private readonly Action<bool> onCheckboxChanged;
        private readonly float computedHeight;

        private bool checkboxState;

        public override Vector2 InitialSize => new Vector2(WindowWidth, computedHeight);

        public FCWindow_ConfirmSettle(WorldSettlementDef settlementType, int silverCost, Action onConfirm, Action<bool> onCheckboxChanged)
        {
            this.settlementType = settlementType;
            this.silverCost = silverCost;
            this.onConfirm = onConfirm;
            this.onCheckboxChanged = onCheckboxChanged;

            draggable = true;
            doCloseX = true;
            absorbInputAroundWindow = true;
            forcePause = false;

            float cardWidth = WindowWidth - Padding * 2 - Window.StandardMargin * 2;
            float cardHeight = SettlementCardDrawer.GetCardHeight(settlementType, cardWidth);

            computedHeight = Window.StandardMargin * 2
                + TitleHeight + Padding
                + cardHeight + Padding
                + ConfirmTextHeight
                + CheckboxHeight + Padding
                + ButtonHeight + Padding;
        }

        public override void DoWindowContents(Rect inRect)
        {
            GameFont fontBefore = Text.Font;
            TextAnchor anchorBefore = Text.Anchor;

            float curY = 0f;

            // Title
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;
            UIUtil.ClampedLabel(new Rect(0, curY, inRect.width, TitleHeight), "FCConfirmDecision".Translate());
            curY += TitleHeight + Padding;

            // Settlement card
            float cardWidth = inRect.width;
            float cardHeight = SettlementCardDrawer.GetCardHeight(settlementType, cardWidth);
            Rect cardRect = new Rect(inRect.x, curY, cardWidth, cardHeight);

            // Subtle background for the card area
            Widgets.DrawHighlight(cardRect);
            SettlementCardDrawer.DrawSettlementCard(cardRect, settlementType);
            curY += cardHeight + Padding;

            // Confirm text
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            UIUtil.ClampedLabel(new Rect(0, curY, inRect.width, ConfirmTextHeight),
                "FCConfirmSettle".Translate(settlementType.LabelCap, silverCost));
            curY += ConfirmTextHeight;

            // Checkbox
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            Rect checkboxRect = new Rect(inRect.width / 2f - 100f, curY, 200f, CheckboxHeight);
            Rect checkboxBoundingRect = new Rect(checkboxRect.x - 3f, checkboxRect.y, checkboxRect.width + 6f,
                checkboxRect.height);
            Widgets.DrawMenuSection(checkboxBoundingRect);
            Widgets.CheckboxLabeled(checkboxRect, "FCConfirmDontShowAgain".Translate(), ref checkboxState);
            curY += CheckboxHeight + Padding;

            // Confirm button
            Rect buttonRect = new Rect(inRect.width / 2f - 45f, curY, 90f, ButtonHeight);
            if (UIUtil.ClampedButtonText(buttonRect, "FCConfirm".Translate()))
            {
                onCheckboxChanged?.Invoke(checkboxState);
                onConfirm?.Invoke();
                Close();
            }

            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;
        }
    }
}
