using FactionColonies.util;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace FactionColonies
{
    public class FireSupportWindow : MilitaryWindow
    {
        public override MilitaryWindowSlot Slot => MilitaryWindowSlot.FireSupport;

        private WorldSettlementFC settlementPointReference;
        private MilitaryFireSupport selectedSupport;
        private readonly MilitaryFC mfc;

        private Vector2 supportListScrollPos;
        private string supportSearchTerm = "";
        private Vector2 projectileListScrollPos;
        private Dictionary<ThingDef, string> quantityBuffers = new Dictionary<ThingDef, string>();

        // Layout constants (matching DesignUnitsWindow/DesignSquadsWindow)
        private const float SidebarWidth = 250f;
        private const float RowHeight = 30f;
        private const float SearchBarHeight = 28f;
        private const float IconSize = 24f;
        private const float margin = 5f;
        private const float ButtonHeight = 30f;
        private const float ProjectileRowHeight = 30f;

        public FireSupportWindow(MilitaryFC mfc)
        {
            this.mfc = mfc;
            selectedText = "FCSelectAFireSupport".Translate();
            mfc.CheckMilitaryUtilForErrors();
        }

        public override void Select(IExposable selecting)
        {
            MilitaryFireSupport support = (MilitaryFireSupport)selecting;
            selectedSupport = support;
            selectedText = support.name;
        }

        public override void DrawTab(Rect rect)
        {
            Widgets.DrawLineHorizontal(rect.x, rect.y + 45, rect.width);

            GameFont fontBefore = Text.Font;
            TextAnchor anchorBefore = Text.Anchor;

            float contentTop = rect.y + 45f + margin;
            float contentBottom = rect.yMax - margin;
            float rightEdge = rect.xMax - margin;

            // Left sidebar
            Rect sidebarRect = new Rect(rect.x + margin, contentTop,
                SidebarWidth, contentBottom - contentTop);
            DrawSidebar(sidebarRect);

            // Content area (right of sidebar)
            float contentLeft = sidebarRect.xMax + 10f;
            float contentWidth = rightEdge - contentLeft;

            if (selectedSupport != null)
            {
                // Header (name + cost + accuracy info + slider)
                float headerHeight = 120f;
                Rect headerRect = new Rect(contentLeft, contentTop, contentWidth, headerHeight);
                DrawHeader(headerRect);

                // Bottom bar
                float bottomBarHeight = ButtonHeight;
                Rect bottomRect = new Rect(contentLeft, contentBottom - bottomBarHeight,
                    contentWidth, bottomBarHeight);
                DrawBottomBar(bottomRect);

                // Projectile list (between header and bottom bar)
                float listTop = headerRect.yMax + margin;
                float listBottom = bottomRect.y - margin;
                Rect listRect = new Rect(contentLeft, listTop,
                    contentWidth, listBottom - listTop);
                DrawProjectileList(listRect);
            }

            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;
        }

        // --- Sidebar ---

        private void DrawSidebar(Rect rect)
        {
            GameFont fontBefore = Text.Font;
            TextAnchor anchorBefore = Text.Anchor;

            // Search bar
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            Rect searchRect = new Rect(rect.x, rect.y, rect.width, SearchBarHeight);
            supportSearchTerm = Widgets.TextField(searchRect, supportSearchTerm);

            // Button area: 2x2 grid (Create/Import, Delete/Export)
            float buttonsHeight = ButtonHeight * 2 + margin;

            // Support list (fills space between search bar and buttons)
            float listHeight = rect.yMax - searchRect.yMax - margin - buttonsHeight - margin;
            Rect listOutRect = new Rect(rect.x, searchRect.yMax + margin, rect.width, listHeight);
            Widgets.DrawMenuSection(listOutRect);

            List<MilitaryFireSupport> filteredSupports = string.IsNullOrEmpty(supportSearchTerm)
                ? mfc.fireSupportDefs ?? new List<MilitaryFireSupport>()
                : (mfc.fireSupportDefs ?? new List<MilitaryFireSupport>())
                    .Where(s => (s.name ?? "").IndexOf(supportSearchTerm, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();

            float viewHeight = filteredSupports.Count * RowHeight;
            Rect scrollViewRect = ScrollUtil.BeginScrollView(listOutRect, ref supportListScrollPos, viewHeight);

            for (int i = 0; i < filteredSupports.Count; i++)
            {
                MilitaryFireSupport support = filteredSupports[i];
                Rect row = new Rect(scrollViewRect.x, scrollViewRect.y + i * RowHeight,
                    scrollViewRect.width, RowHeight);

                if (support == selectedSupport)
                    Widgets.DrawHighlightSelected(row);
                else if (i % 2 == 0)
                    Widgets.DrawHighlight(row);

                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;
                Rect labelRect = new Rect(row.x + 4f, row.y, row.width - 6f, RowHeight);
                UIUtil.ClampedLabel(labelRect, support.name);

                if (Widgets.ButtonInvisible(row))
                {
                    selectedSupport = support;
                    selectedText = support.name;
                }
            }

            ScrollUtil.EndScrollView();

            // Buttons (2x2 grid)
            float btnY = listOutRect.yMax + margin;
            float buttonW = (rect.width - margin) / 2f;

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;

            Rect createBtn = new Rect(rect.x, btnY, buttonW, ButtonHeight);
            Rect importBtn = new Rect(rect.x + buttonW + margin, btnY, buttonW, ButtonHeight);
            Rect deleteBtn = new Rect(rect.x, btnY + ButtonHeight + margin, buttonW, ButtonHeight);
            Rect exportBtn = new Rect(rect.x + buttonW + margin, btnY + ButtonHeight + margin, buttonW, ButtonHeight);

            if (UIUtil.ClampedButtonText(createBtn, "FCCreateNewFireSupport".Translate()))
            {
                MilitaryFireSupport newSupport = new MilitaryFireSupport();
                newSupport.name = "New Fire Support " + (mfc.fireSupportDefs.Count + 1);
                newSupport.SetLoadID();
                newSupport.projectiles = new List<ThingDef>();
                selectedText = newSupport.name;
                selectedSupport = newSupport;
                mfc.fireSupportDefs.Add(newSupport);
            }

            if (UIUtil.ClampedButtonText(importBtn, "FCImportFireSupport".Translate()))
            {
                Find.WindowStack.Add(new Dialog_ManageFireSupportExportsFC(
                    FactionColoniesMilitary.SavedFireSupports.ToList()));
            }

            if (selectedSupport is object)
            {
                if (UIUtil.ClampedButtonText(deleteBtn, "FCDeleteFireSupportButton".Translate()))
                {
                    MilitaryFireSupport supportToDelete = selectedSupport;
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                        "FCConfirmDeleteFireSupport".Translate((NamedArgument)supportToDelete.name),
                        delegate
                        {
                            supportToDelete.Delete();
                            mfc.CheckMilitaryUtilForErrors();
                            if (selectedSupport == supportToDelete)
                            {
                                selectedSupport = null;
                                selectedText = "FCSelectAFireSupport".Translate();
                            }
                        }));
                }

                if (UIUtil.ClampedButtonText(exportBtn, "FCExportFireSupportButton".Translate()))
                {
                    FactionColoniesMilitary.SaveFireSupport(new SavedFireSupportFC(selectedSupport));
                    Messages.Message("FCExportFireSupport".Translate(), MessageTypeDefOf.TaskCompletion);
                }
            }

            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;
        }

        // --- Header ---

        private void DrawHeader(Rect rect)
        {
            GameFont fontBefore = Text.Font;
            TextAnchor anchorBefore = Text.Anchor;

            // Highlight banner
            Rect highlightBar = new Rect(rect.x, rect.y, rect.width, 35f);
            Widgets.DrawHighlight(highlightBar);

            // Fire support name
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            Rect nameRect = new Rect(rect.x + margin, rect.y, 400f, 30f);
            UIUtil.ClampedLabel(nameRect, selectedSupport.name);

            // Pencil icon
            float nameTextWidth = Text.CalcSize(selectedSupport.name).x;
            Rect pencilRect = new Rect(
                rect.x + Mathf.Min(nameTextWidth + 8f + margin, rect.width - 22f),
                rect.y + 4f, 22f, 22f);
            if (Widgets.ButtonImage(pencilRect, TexButton.Rename))
            {
                Find.WindowStack.Add(new FCWindow_Rename(selectedSupport.name, "FCRenameFireSupport", name => selectedSupport.name = name));
            }

            // Info line 1: Cost + projectile count
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            float infoY = highlightBar.yMax + margin;
            float halfWidth = rect.width / 2f;

            Rect costRect = new Rect(rect.x, infoY, halfWidth, 20f);
            if (settlementPointReference != null)
            {
                UIUtil.ClampedLabel(costRect, "FCFireSupportCostRefLabel".Translate(
                    selectedSupport.ReturnTotalCost(),
                    MilitaryFC.CalculateFireSupportBudget(settlementPointReference.settlementMilitaryLevel)));
            }
            else
            {
                UIUtil.ClampedLabel(costRect, "FCFireSupportCostLabel".Translate(selectedSupport.ReturnTotalCost()));
            }

            Rect countRect = new Rect(rect.x + halfWidth, infoY, halfWidth, 22f);
            UIUtil.ClampedLabel(countRect, "FCFireSupportProjectileCount".Translate(selectedSupport.projectiles.Count));

            // Info line 2: Duration
            float line2Y = infoY + 18f + 2f;
            Rect durationRect = new Rect(rect.x, line2Y, rect.width, 18f);
            UIUtil.ClampedLabel(durationRect, "FCFireSupportDuration".Translate(
                Math.Round(selectedSupport.projectiles.Count * 0.25, 2)));

            // Info line 3: Accuracy label
            float line3Y = line2Y + 18f + 2f;
            Rect accuracyLabelRect = new Rect(rect.x, line3Y, rect.width, 22f);
            UIUtil.ClampedLabel(accuracyLabelRect, "FCFireSupportAccuracyLabel".Translate(
                selectedSupport.accuracy,
                selectedSupport.ReturnAccuracyCostPercentage()));

            // Accuracy slider
            float sliderY = line3Y + 18f + 2f;
            Rect sliderRect = new Rect(rect.x, sliderY, rect.width, 20f);
            selectedSupport.accuracy = Widgets.HorizontalSlider(sliderRect,
                selectedSupport.accuracy,
                Math.Max(3, 15 - FindFC.FactionComp.ReturnHighestMilitaryLevel()), 100,
                roundTo: 1);

            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;
        }

        // --- Projectile List ---

        private struct ProjectileGroup
        {
            public ThingDef def;
            public int count;
        }

        private List<ProjectileGroup> BuildProjectileGroups(MilitaryFireSupport support)
        {
            return support.projectiles
                .GroupBy(p => p)
                .Select(g => new ProjectileGroup { def = g.Key, count = g.Count() })
                .ToList();
        }

        private void DrawProjectileList(Rect rect)
        {
            GameFont fontBefore = Text.Font;
            TextAnchor anchorBefore = Text.Anchor;

            Widgets.DrawMenuSection(rect);

            List<ProjectileGroup> groups = BuildProjectileGroups(selectedSupport);

            // Clean stale buffer entries
            HashSet<ThingDef> activeKeys = new HashSet<ThingDef>(groups.Select(g => g.def));
            List<ThingDef> staleKeys = quantityBuffers.Keys.Where(k => !activeKeys.Contains(k)).ToList();
            foreach (ThingDef key in staleKeys) quantityBuffers.Remove(key);

            float viewHeight = groups.Count * ProjectileRowHeight;
            Rect scrollViewRect = ScrollUtil.BeginScrollView(rect, ref projectileListScrollPos, viewHeight);

            for (int i = 0; i < groups.Count; i++)
            {
                Rect row = new Rect(scrollViewRect.x, scrollViewRect.y + i * ProjectileRowHeight,
                    scrollViewRect.width, ProjectileRowHeight);
                DrawProjectileRow(row, groups[i], i);
            }

            ScrollUtil.EndScrollView();

            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;
        }

        private void DrawProjectileRow(Rect row, ProjectileGroup group, int rowIndex)
        {
            GameFont fontBefore = Text.Font;
            TextAnchor anchorBefore = Text.Anchor;

            if (rowIndex % 2 == 0)
                Widgets.DrawHighlight(row);

            ThingDef def = group.def;
            float x = row.x + 2f;
            float btnSize = ProjectileRowHeight - 4f;
            float btnY = row.y + 2f;

            // Projectile icon + name
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            float iconNameWidth = 220f;
            Rect iconNameRect = new Rect(x, row.y, iconNameWidth, ProjectileRowHeight);
            Widgets.DefLabelWithIcon(iconNameRect, def);
            x = iconNameRect.xMax + 2f;

            // Cost (per unit ea. / total)
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            float perCost = (float)Math.Round(def.BaseMarketValue * 1.5, 2);
            float totalCost = perCost * group.count;
            Rect costRect = new Rect(x, row.y, 120f, ProjectileRowHeight);
            UIUtil.ClampedLabel(costRect, "$" + perCost + " ea. / $" + totalCost);
            x = costRect.xMax + 4f;

            int count = group.count;
            quantityBuffers[def] = count.ToString();
            string buffer = quantityBuffers[def];

            // [-] button
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            Rect minusRect = new Rect(x, btnY, btnSize, btnSize);
            if (UIUtil.ClampedButtonText(minusRect, "-"))
            {
                //selectedSupport.projectiles.Remove(def);
                count = Math.Max(0, count - 1);
            }
            x = minusRect.xMax + 2f;

            // Numeric text field

            Rect numFieldRect = new Rect(x, row.y + 2f, 40f, ProjectileRowHeight - 4f);
            Widgets.TextFieldNumeric(numFieldRect, ref count, ref buffer, 1, 999);
            quantityBuffers[def] = buffer;
            x = numFieldRect.xMax + 2f;

            // [+] button
            Rect plusRect = new Rect(x, btnY, btnSize, btnSize);
            if (UIUtil.ClampedButtonText(plusRect, "+"))
            {
                //selectedSupport.projectiles.Add(def);
                count++;
            }
            x = plusRect.xMax + 4f;

            group.count = count;

            // Sync flat list if count changed
            int actual = selectedSupport.projectiles.Count(p => p == def);
            if (count != actual)
            {
                if (count > actual)
                    for (int j = 0; j < count - actual; j++) selectedSupport.projectiles.Add(def);
                else
                    for (int j = 0; j < actual - count; j++) selectedSupport.projectiles.Remove(def);
            }

            // [X] delete all
            Rect deleteRect = new Rect(x, btnY, btnSize, btnSize);
            if (Widgets.ButtonImage(deleteRect, TexLoad.deleteX))
            {
                selectedSupport.projectiles.RemoveAll(p => p == def);
            }

            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;
        }

        // --- Bottom Bar ---

        private void DrawBottomBar(Rect rect)
        {
            GameFont fontBefore = Text.Font;
            TextAnchor anchorBefore = Text.Anchor;

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;

            float btnW = (rect.width - margin * 2) / 3f;

            // Add Projectile button
            Rect addBtn = new Rect(rect.x, rect.y, btnW, ButtonHeight);
            if (UIUtil.ClampedButtonText(addBtn, "FCAddNewProjectile".Translate()))
            {
                Find.WindowStack.Add(new FCWindow_ProjectilePicker(
                    selectedSupport.ReturnFireSupportOptions(),
                    def =>
                    {
                        selectedSupport.projectiles.Add(def);
                        SoundDefOf.Click.PlayOneShotOnCamera();
                    }));
            }

            // Set Point Ref button
            Rect pointRefBtn = new Rect(addBtn.xMax + margin, rect.y, btnW, ButtonHeight);
            if (UIUtil.ClampedButtonText(pointRefBtn, "FCSetPointRef".Translate()))
            {
                List<FloatMenuOption> settlementList = FindFC.FactionComp
                    .settlements.Select(settlement => new FloatMenuOption(
                        settlement.Name + "FCMilitaryLevelLabel".Translate() +
                        settlement.settlementMilitaryLevel,
                        delegate
                        {
                            settlementPointReference = settlement;
                        }))
                    .ToList();

                if (!settlementList.Any())
                {
                    settlementList.Add(new FloatMenuOption("FCNoValidSettlements".Translate(), null));
                }

                FloatMenu floatMenu = new FloatMenu(settlementList) { vanishIfMouseDistant = true };
                Find.WindowStack.Add(floatMenu);
            }

            // Reset button
            Rect resetBtn = new Rect(pointRefBtn.xMax + margin, rect.y, btnW, ButtonHeight);
            if (UIUtil.ClampedButtonText(resetBtn, "FCResetToDefault".Translate()))
            {
                selectedSupport.projectiles = new List<ThingDef>();
            }

            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;
        }

    }
}
