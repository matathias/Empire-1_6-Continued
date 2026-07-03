using UnityEngine;
using Verse;

namespace FactionColonies
{
    public static class SettlementCardDrawer
    {
        public const float margin = 5f;
        public const float NameHeight = 22f;
        public const float ResourceIconSize = 18f;
        public const float ResourceIconGap = 3f;
        public const float ResourceRowHeight = 22f;
        public const float AccentBarWidth = 4f;
        public const float RowPadding = 6f;

        public static float GetCardHeight(WorldSettlementDef def, float width)
        {
            float contentWidth = width - AccentBarWidth - margin * 3;

            Text.Font = GameFont.Tiny;
            float descHeight = Text.CalcHeight(def.FormattedDesc, contentWidth);

            float height = RowPadding + NameHeight + descHeight + margin;

            if (def.resources != null && def.resources.Count > 0)
            {
                height += ResourceRowHeight;
            }

            height += RowPadding;
            return height;
        }

        public static void DrawSettlementCard(Rect rect, WorldSettlementDef def)
        {
            // Accent bar
            if (def.accentColor.HasValue)
            {
                Widgets.DrawBoxSolid(new Rect(rect.x, rect.y, AccentBarWidth, rect.height), def.accentColor.Value);
            }

            float xOffset = rect.x + AccentBarWidth + margin;
            float contentWidth = rect.width - AccentBarWidth - margin * 3;
            float curY = rect.y + RowPadding;

            // Name
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            UIUtil.ClampedLabel(new Rect(xOffset, curY, contentWidth, NameHeight), def.LabelCap);
            curY += NameHeight;

            // Description
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.UpperLeft;
            float descHeight = Text.CalcHeight(def.FormattedDesc, contentWidth);
            Widgets.Label(new Rect(xOffset, curY, contentWidth, descHeight), def.FormattedDesc);
            curY += descHeight + margin;

            // Resource icons
            if (def.resources != null && def.resources.Count > 0)
            {
                DrawResourceRow(new Rect(xOffset, curY, contentWidth, ResourceRowHeight), def);
            }
        }

        public static void DrawResourceRow(Rect rect, WorldSettlementDef def)
        {
            float xCursor = rect.x;

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            float labelWidth = Text.CalcSize("FCSettlementResources".Translate()).x + margin;
            UIUtil.ClampedLabel(new Rect(xCursor, rect.y, labelWidth, rect.height), "FCSettlementResources".Translate());
            xCursor += labelWidth;

            foreach (ResourceAvailability ra in def.resources)
            {
                if (ra.resourceDef == null) continue;

                Rect iconRect = new Rect(xCursor, rect.y + (rect.height - ResourceIconSize) / 2f, ResourceIconSize, ResourceIconSize);
                GUI.DrawTexture(iconRect, ra.resourceDef.Icon);
                TooltipHandler.TipRegion(iconRect, GetResourceTooltip(ra));
                xCursor += ResourceIconSize;

                string bonusText = GetBonusText(ra);
                if (bonusText != null)
                {
                    Text.Font = GameFont.Tiny;
                    float bonusWidth = Text.CalcSize(bonusText).x + 2f;
                    UIUtil.ClampedLabel(new Rect(xCursor, rect.y, bonusWidth, rect.height), bonusText);
                    xCursor += bonusWidth;
                }

                xCursor += ResourceIconGap;
            }
        }

        public static string GetResourceTooltip(ResourceAvailability ra)
        {
            string tooltip = ra.resourceDef.LabelCap;
            if (ra.additive != 0)
                tooltip += "\n+" + ra.additive;
            if (ra.multiplier != 1)
                tooltip += "\nx" + ra.multiplier;
            return tooltip;
        }

        public static string GetBonusText(ResourceAvailability ra)
        {
            if (ra.additive != 0 && ra.multiplier != 1)
                return "+" + ra.additive + " x" + ra.multiplier;
            if (ra.additive != 0)
                return "+" + ra.additive;
            if (ra.multiplier != 1)
                return "x" + ra.multiplier;
            return null;
        }
    }
}
