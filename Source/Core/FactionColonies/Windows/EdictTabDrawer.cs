using FactionColonies.util;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Draws the Edicts tab content in the main faction window.
    /// Four columns (Social, Tax, Doctrine, Military), each showing available edicts
    /// with radio-style selection and upkeep display.
    /// </summary>
    public static class EdictTabDrawer
    {
        private static readonly FCPolicyCategory[] EdictCategories =
        {
            FCPolicyCategory.Social,
            FCPolicyCategory.Tax,
            FCPolicyCategory.Doctrine,
            FCPolicyCategory.Military
        };

        private static Dictionary<FCPolicyCategory, Vector2> columnScrollPositions = new Dictionary<FCPolicyCategory, Vector2>();

        private const float Margin = 5f;
        private const float ColumnGap = 8f;
        private const float HeaderHeight = 30f;
        private const float LabelHeight = 22f;
        private const float UpkeepHeight = 20f;
        private const float RowPadding = 4f;
        private const float BottomBarHeight = 35f;
        private const float RadioSize = 24f;
        private const float CategoryPadding = 6f;

        private const float margin = 5f;

        public static void OnTabSwitch()
        {
            columnScrollPositions.Clear();
        }

        private static string GetCategoryLabel(FCPolicyCategory category)
        {
            switch (category)
            {
                case FCPolicyCategory.Social: return "FCEdictCategorySocial".Translate();
                case FCPolicyCategory.Tax: return "FCEdictCategoryTax".Translate();
                case FCPolicyCategory.Military: return "FCEdictCategoryMilitary".Translate();
                case FCPolicyCategory.Doctrine: return "FCEdictCategoryDoctrine".Translate();
                default: return category.ToString();
            }
        }

        private static void GetColumnColors(FCPolicyCategory category, out Color bodyColor, out Color headerColor)
        {
            switch (category)
            {
                case FCPolicyCategory.Social:
                    bodyColor = new Color(0.20f, 0.17f, 0.10f, 0.5f);
                    headerColor = new Color(0.28f, 0.24f, 0.15f, 0.8f);
                    break;
                case FCPolicyCategory.Tax:
                    bodyColor = new Color(0.10f, 0.18f, 0.10f, 0.5f);
                    headerColor = new Color(0.15f, 0.25f, 0.15f, 0.8f);
                    break;
                case FCPolicyCategory.Military:
                    bodyColor = new Color(0.20f, 0.12f, 0.10f, 0.5f);
                    headerColor = new Color(0.28f, 0.16f, 0.13f, 0.8f);
                    break;
                case FCPolicyCategory.Doctrine:
                    bodyColor = new Color(0.12f, 0.15f, 0.20f, 0.5f);
                    headerColor = new Color(0.16f, 0.22f, 0.30f, 0.8f);
                    break;
                default:
                    bodyColor = new Color(0.15f, 0.15f, 0.15f, 0.5f);
                    headerColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
                    break;
            }
        }

        public static void Draw(Rect rect, FactionFC faction)
        {
            // Description header
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            Rect descRect = new Rect(rect.x + Margin, rect.y + Margin, rect.width - Margin * 2, 22f);
            UIUtil.ClampedLabel(descRect, "FCEdictsDesc".Translate());

            float topY = descRect.yMax + Margin;
            float bottomBarY = rect.yMax - BottomBarHeight - margin;
            float columnsHeight = bottomBarY - topY - Margin;
            float columnWidth = (rect.width - Margin * 2 - ColumnGap * (EdictCategories.Length - 1)) / EdictCategories.Length;

            // Draw columns
            for (int i = 0; i < EdictCategories.Length; i++)
            {
                FCPolicyCategory category = EdictCategories[i];
                float colX = rect.x + Margin + i * (columnWidth + ColumnGap);
                Rect colRect = new Rect(colX, topY, columnWidth, columnsHeight);
                DrawColumn(colRect, category, FactionCache.GetPoliciesByCategory(category), faction);
            }

            // Bottom bar - total upkeep
            DrawBottomBar(new Rect(rect.x + Margin, bottomBarY, rect.width - Margin * 2, BottomBarHeight), faction);

            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
        }

        private static void DrawColumn(Rect rect, FCPolicyCategory category, List<FCPolicyDef> edicts, FactionFC faction)
        {
            // Column background with category tint
            Color bodyColor, headerColor;
            GetColumnColors(category, out bodyColor, out headerColor);
            Widgets.DrawBoxSolid(rect, bodyColor);

            bool unlocked = FindFC.PolicyManager.IsEdictCategoryUnlocked(category);
            int requiredLevel;
            PolicyManager.EdictCategoryUnlockLevels.TryGetValue(category, out requiredLevel);

            // Header
            Rect headerRect = new Rect(rect.x, rect.y, rect.width, HeaderHeight);
            Widgets.DrawBoxSolid(headerRect, headerColor);
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;
            UIUtil.ClampedLabel(headerRect, GetCategoryLabel(category));

            float contentY = headerRect.yMax + CategoryPadding;

            if (!unlocked)
            {
                // Locked state
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleCenter;
                Rect lockedRect = new Rect(rect.x + Margin, contentY, rect.width - Margin * 2, 40f);
                UIUtil.DrawColoredLabel(lockedRect, "FCEdictLockedUntilLevel".Translate(requiredLevel), Color.gray);
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            // Active edict status
            FCPolicy activeEdict = FindFC.PolicyManager.GetActiveEdict(category);
            Rect statusRect = new Rect(rect.x + CategoryPadding, contentY, rect.width - CategoryPadding * 2, 22f);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;

            if (activeEdict != null)
            {
                string statusText = "FCEdictActive".Translate() + ": " + activeEdict.def.LabelCap;
                UIUtil.ClampedLabel(statusRect, statusText);

                // Activating indicator
                if (!activeEdict.IsFullyActive)
                {
                    contentY = statusRect.yMax;
                    Rect activatingRect = new Rect(rect.x + CategoryPadding, contentY, rect.width - CategoryPadding * 2, 22f);
                    float daysRemaining = (activeEdict.def.enactDuration - (Find.TickManager.TicksGame - activeEdict.timeEnacted)) / 60000f;
                    UIUtil.DrawColoredLabel(activatingRect, "FCEdictActivating".Translate(daysRemaining.ToString("F1")), Color.yellow);
                    contentY = activatingRect.yMax;
                }
                else
                {
                    contentY = statusRect.yMax;
                }

                // Active edict effects
                string effectsText = FCStatModifier.GetDescription(activeEdict.def.statModifiers).Resolve();
                if (!effectsText.NullOrEmpty())
                {
                    float effectsWidth = rect.width - CategoryPadding * 2;
                    Text.Font = GameFont.Tiny;
                    Text.Anchor = TextAnchor.UpperLeft;
                    float effectsHeight = Text.CalcHeight(effectsText, effectsWidth);
                    Rect effectsRect = new Rect(rect.x + CategoryPadding, contentY + 2f, effectsWidth, effectsHeight);
                    if (!activeEdict.IsFullyActive)
                        GUI.color = Color.gray;
                    Widgets.Label(effectsRect, effectsText);
                    GUI.color = Color.white;
                    contentY = effectsRect.yMax + 2f;
                    Text.Font = GameFont.Small;
                }

                // Revoke button
                Rect revokeRect = new Rect(rect.x + CategoryPadding, contentY, rect.width - CategoryPadding * 2, 24f);
                if (UIUtil.ClampedButtonText(revokeRect, "FCEdictRevoke".Translate()))
                {
                    FCPolicy edictToRevoke = activeEdict;
                    FCPolicyCategory cat = category;
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                        "FCEdictRevokeConfirmation".Translate(edictToRevoke.def.LabelCap),
                        delegate
                        {
                            FindFC.PolicyManager.RevokeEdict(cat);
                        }));
                }
                contentY = revokeRect.yMax + CategoryPadding;
            }
            else
            {
                UIUtil.ClampedLabel(statusRect, "FCEdictActive".Translate() + ": " + "FCEdictNone".Translate());
                contentY = statusRect.yMax + CategoryPadding;
            }

            // Separator line
            Widgets.DrawLineHorizontal(rect.x + CategoryPadding, contentY, rect.width - CategoryPadding * 2);
            contentY += CategoryPadding;

            // Edict list with scroll view
            float listHeight = rect.yMax - contentY;
            Rect listOuterRect = new Rect(rect.x + CategoryPadding, contentY, rect.width - CategoryPadding * 2, listHeight);
            // Use scrollbar-adjusted width for height calculation to avoid underestimating
            // when the scrollbar narrows the view and causes more text wrapping
            float textWidthForLayout = listOuterRect.width - RadioSize - Margin - 16f;

            // Calculate total content height with dynamic row sizes
            float totalContentHeight = 0f;
            foreach (FCPolicyDef def in edicts)
                totalContentHeight += GetEdictRowHeight(def, textWidthForLayout) + 2f;

            Vector2 scrollPos;
            if (!columnScrollPositions.TryGetValue(category, out scrollPos))
                scrollPos = Vector2.zero;

            Rect listViewRect = ScrollUtil.BeginScrollView(listOuterRect, ref scrollPos, totalContentHeight);
            columnScrollPositions[category] = scrollPos;

            // Recalculate text width if scrollbar narrowed the view
            float actualTextWidth = listViewRect.width - RadioSize - Margin;

            float rowY = listViewRect.y;
            foreach (FCPolicyDef def in edicts)
            {
                float rowHeight = GetEdictRowHeight(def, actualTextWidth);
                Rect rowRect = new Rect(listViewRect.x, rowY, listViewRect.width, rowHeight);
                DrawEdictRow(rowRect, def, faction, activeEdict);
                rowY = rowRect.yMax + 2f;
            }

            ScrollUtil.EndScrollView();

            Text.Anchor = TextAnchor.UpperLeft;
        }

        private static FCPolicyDef GetBlockingPolicy(FCPolicyDef def, FactionFC faction)
        {
            if (def.incompatiblePolicies.NullOrEmpty()) return null;
            foreach (FCPolicyDef blocked in def.incompatiblePolicies)
            {
                if (FindFC.PolicyManager.HasPolicy(blocked) || FindFC.PolicyManager.HasTrait(blocked))
                    return blocked;
            }
            return null;
        }

        private static float GetEdictRowHeight(FCPolicyDef def, float textWidth)
        {
            GameFont prev = Text.Font;
            Text.Font = GameFont.Tiny;
            float descHeight = Text.CalcHeight(def.FormattedDesc, textWidth);
            Text.Font = prev;
            return RowPadding + LabelHeight + descHeight + UpkeepHeight;
        }

        private static void DrawEdictRow(Rect rect, FCPolicyDef def, FactionFC faction, FCPolicy activeEdict)
        {
            bool isActive = activeEdict != null && activeEdict.def == def;
            bool meetsLevel = def.factionLevelRequirement <= 0 || faction.factionLevel >= def.factionLevelRequirement;
            FCPolicyDef blocker = GetBlockingPolicy(def, faction);
            string prereqFailReason;
            bool meetsPrereqs = def.MeetsPolicyRequirements(faction, out prereqFailReason);
            bool available = meetsLevel && blocker == null && meetsPrereqs;

            // Row background
            if (isActive)
                Widgets.DrawBoxSolid(rect, new Color(0.1f, 0.3f, 0.1f, 0.4f));
            else if (Mouse.IsOver(rect))
                Widgets.DrawHighlight(rect);

            // Radio button visual (display only, not the click target)
            Rect radioRect = new Rect(rect.x, rect.y + (rect.height - RadioSize) / 2f, RadioSize, RadioSize);

            // Label and description
            float textX = radioRect.xMax + Margin;
            float textWidth = rect.width - RadioSize - Margin;

            Rect labelRect = new Rect(textX, rect.y + 2f, textWidth, 22f);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;

            if (!available)
                GUI.color = Color.gray;

            UIUtil.ClampedLabel(labelRect, def.LabelCap);

            // Description — fills remaining space between label and upkeep
            float descHeight = rect.height - LabelHeight - UpkeepHeight - RowPadding;
            Rect descRect = new Rect(textX, labelRect.yMax, textWidth, descHeight);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.UpperLeft;
            Widgets.Label(descRect, def.FormattedDesc);

            // Upkeep
            Rect upkeepRect = new Rect(textX, descRect.yMax, textWidth, 20f);
            UIUtil.DrawColoredLabel(upkeepRect,
                "FCEdictUpkeep".Translate(def.upkeepSilver),
                available ? new Color(1f, 0.85f, 0.4f) : Color.gray);

            Text.Font = GameFont.Small;

            // Draw radio button visual (not interactive)
            if (available)
                Widgets.RadioButton(radioRect.x, radioRect.y, isActive);
            else
            {
                GUI.color = Color.gray;
                Widgets.RadioButton(radioRect.x, radioRect.y, false);
                GUI.color = Color.white;
            }

            // Whole-row click handling
            if (available && !isActive && Widgets.ButtonInvisible(rect))
            {
                if (activeEdict != null)
                {
                    // Swapping — confirm first
                    float enactDays = def.enactDuration / 60000f;
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                        "FCEdictSwapConfirmation".Translate(activeEdict.def.LabelCap, def.LabelCap, enactDays.ToString("F0")),
                        delegate
                        {
                            FindFC.PolicyManager.EnactEdict(def);
                        }));
                }
                else
                {
                    FindFC.PolicyManager.EnactEdict(def);
                }
            }

            // Tooltip
            string tooltip = def.PolicyText();
            if (!meetsLevel)
                tooltip += "\n\n" + "FCEdictLevelRequired".Translate(def.factionLevelRequirement);
            if (blocker != null)
                tooltip += "\n\n" + "FCEdictIncompatible".Translate(def.LabelCap, blocker.LabelCap);
            if (!meetsPrereqs)
                tooltip += "\n\n" + prereqFailReason;
            TooltipHandler.TipRegion(rect, tooltip);

            Text.Anchor = TextAnchor.UpperLeft;
        }

        private static void DrawBottomBar(Rect rect, FactionFC faction)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.2f, 0.2f, 0.2f, 0.8f));

            int totalUpkeep = FindFC.PolicyManager.GetEdictUpkeep();

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            Rect upkeepLabelRect = new Rect(rect.x + Margin, rect.y, rect.width - Margin * 2, rect.height);
            UIUtil.ClampedLabel(upkeepLabelRect, "FCEdictTotalUpkeep".Translate(totalUpkeep));
            Text.Anchor = TextAnchor.UpperLeft;
        }
    }
}
