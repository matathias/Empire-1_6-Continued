using FactionColonies.util;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    public class DesignSquadsWindow : MilitaryWindow
    {
        public override MilitaryWindowSlot Slot => MilitaryWindowSlot.Squads;

        private WorldSettlementFC settlementPointReference;
        protected readonly MilitaryFC mfc;
        protected MilSquadFC selectedSquad;

        private Vector2 squadListScrollPos;
        private string squadSearchTerm = "";
        private Vector2 unitListScrollPos;

        // Layout constants (matching DesignUnitsWindow)
        private const float SidebarWidth = 250f;
        private const float RowHeight = 30f;
        private const float SearchBarHeight = 28f;
        private const float IconSize = 24f;
        private const float margin = 5f;
        private const float ButtonHeight = 30f;
        private const float UnitRowHeight = 50f;

        public DesignSquadsWindow(MilitaryFC mfc)
        {
            this.mfc = mfc;
            selectedText = "FCSelectASquad".Translate();

            if (mfc.blankUnit is null)
            {
                mfc.blankUnit = MilTemplateFactory.CreateUnit(true);
            }

            mfc.CheckMilitaryUtilForErrors();
        }

        public override void Select(IExposable selecting)
        {
            MilSquadFC squad = (MilSquadFC)selecting;
            selectedSquad = squad;
            selectedText = squad.name;
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

            if (selectedSquad != null)
            {
                // Header
                Rect headerRect = new Rect(contentLeft, contentTop, contentWidth, 60f);
                DrawSquadHeader(headerRect);

                // Bottom bar
                float bottomBarHeight = ButtonHeight;
                Rect bottomRect = new Rect(contentLeft, contentBottom - bottomBarHeight,
                    contentWidth, bottomBarHeight);
                DrawBottomBar(bottomRect);

                // Unit list (between header and bottom bar)
                float unitListTop = headerRect.yMax + margin;
                float unitListBottom = bottomRect.y - margin;
                Rect unitListRect = new Rect(contentLeft, unitListTop,
                    contentWidth, unitListBottom - unitListTop);
                DrawUnitList(unitListRect);
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
            squadSearchTerm = Widgets.TextField(searchRect, squadSearchTerm);

            // Squad list (fills space between search bar and buttons)
            float buttonsHeight = ButtonHeight * 2 + margin;
            float listHeight = rect.yMax - searchRect.yMax - margin - buttonsHeight - margin;
            Rect listOutRect = new Rect(rect.x, searchRect.yMax + margin, rect.width, listHeight);
            Widgets.DrawMenuSection(listOutRect);

            List<MilSquadFC> filteredSquads = string.IsNullOrEmpty(squadSearchTerm)
                ? mfc.squads ?? new List<MilSquadFC>()
                : (mfc.squads ?? new List<MilSquadFC>())
                    .Where(s => (s.name ?? "").IndexOf(squadSearchTerm, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();

            float viewHeight = filteredSquads.Count * RowHeight;
            Rect scrollViewRect = ScrollUtil.BeginScrollView(listOutRect, ref squadListScrollPos, viewHeight);

            for (int i = 0; i < filteredSquads.Count; i++)
            {
                MilSquadFC squad = filteredSquads[i];
                Rect row = new Rect(scrollViewRect.x, scrollViewRect.y + i * RowHeight,
                    scrollViewRect.width, RowHeight);

                if (squad == selectedSquad)
                    Widgets.DrawHighlightSelected(row);
                else if (i % 2 == 0)
                    Widgets.DrawHighlight(row);

                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;
                Rect labelRect = new Rect(row.x + 4f, row.y, row.width - 6f, RowHeight);
                UIUtil.ClampedLabel(labelRect, squad.name);

                if (Widgets.ButtonInvisible(row))
                {
                    selectedSquad = squad;
                    selectedText = squad.name;
                    selectedSquad.UpdateEquipmentTotalCost();
                }
            }

            ScrollUtil.EndScrollView();

            // CRUD buttons (2x2 grid)
            float btnY = listOutRect.yMax + margin;
            float buttonW = (rect.width - margin) / 2f;

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;

            Rect createBtn = new Rect(rect.x, btnY, buttonW, ButtonHeight);
            Rect importBtn = new Rect(rect.x + buttonW + margin, btnY, buttonW, ButtonHeight);
            Rect deleteBtn = new Rect(rect.x, btnY + ButtonHeight + margin, buttonW, ButtonHeight);
            Rect exportBtn = new Rect(rect.x + buttonW + margin, btnY + ButtonHeight + margin,
                buttonW, ButtonHeight);

            if (UIUtil.ClampedButtonText(createBtn, "FCCreateNewSquad".Translate()))
            {
                if (mfc.squads is null)
                {
                    mfc.ResetSquads();
                }

                MilSquadFC newSquad = MilTemplateFactory.CreateSquad(true);
                newSquad.name = $"New Squad {(mfc.squads.Count + 1).ToString()}";
                selectedText = newSquad.name;
                selectedSquad = newSquad;
                selectedSquad.NewSquad();
                mfc.squads.Add(newSquad);
            }

            if (UIUtil.ClampedButtonText(importBtn, "FCImportSquad".Translate()))
            {
                Find.WindowStack.Add(new Dialog_ManageSquadExportsFC(
                    FactionColoniesMilitary.SavedSquads.ToList()));
            }

            if (selectedSquad != null)
            {
                if (UIUtil.ClampedButtonText(deleteBtn, "FCDeleteSquadButton".Translate()))
                {
                    MilSquadFC squadToDelete = selectedSquad;
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                        "FCConfirmDeleteSquad".Translate((NamedArgument)squadToDelete.name),
                        delegate
                        {
                            // Routes through DeleteTemplate so any mercenary squad referencing
                            // the template gets its outfit cleared (mercs keep their gear).
                            mfc.DeleteTemplate(squadToDelete);
                            mfc.CheckMilitaryUtilForErrors();
                            if (selectedSquad == squadToDelete)
                            {
                                selectedSquad = null;
                                selectedText = "FCSelectASquad".Translate();
                            }
                        }));
                }

                if (UIUtil.ClampedButtonText(exportBtn, "FCExportSquadButton".Translate()))
                {
                    FactionColoniesMilitary.SaveSquad(selectedSquad.ToSavedSquad());
                    Messages.Message("FCExportSquad".Translate(), MessageTypeDefOf.TaskCompletion);
                }
            }

            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;
        }

        // --- Squad Header ---

        private void DrawSquadHeader(Rect rect)
        {
            GameFont fontBefore = Text.Font;
            TextAnchor anchorBefore = Text.Anchor;

            // Highlight banner
            Rect highlightBar = new Rect(rect.x, rect.y, rect.width, 35f);
            Widgets.DrawHighlight(highlightBar);

            // Squad name
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            Rect nameRect = new Rect(rect.x + margin, rect.y, 400f, 30f);
            UIUtil.ClampedLabel(nameRect, selectedSquad.name);

            // Pencil icon
            float nameTextWidth = Text.CalcSize(selectedSquad.name).x;
            Rect pencilRect = new Rect(
                rect.x + Mathf.Min(nameTextWidth + 8f + margin, rect.width - 22f),
                rect.y + 4f, 22f, 22f);
            if (Widgets.ButtonImage(pencilRect, TexButton.Rename))
            {
                Find.WindowStack.Add(new FCWindow_Rename(selectedSquad.name, "FCRenameSquad", name => selectedSquad.name = name));
            }

            // Cost line: design equipment cost on the left, recurring deploy cost on the right.
            // Both anchor the player's mental model — upfront hire price vs the ongoing
            // deploy fee shown everywhere else in the UI.
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            float costY = highlightBar.yMax + margin;

            string equipLabel = settlementPointReference != null
                ? (string)"FCTotalSquadEquipmentCost".Translate(
                    selectedSquad.GetEquipmentTotalCost(),
                    MilitaryFC.CalculateSquadBudget(settlementPointReference.settlementMilitaryLevel))
                : (string)"FCTotalSquadEquipmentCostNoRef".Translate(selectedSquad.GetEquipmentTotalCost());

            float equipWidth = Text.CalcSize(equipLabel).x;
            UIUtil.ClampedLabel(new Rect(rect.x, costY, equipWidth, 20f), equipLabel);

            // Power readout — right-aligned on the same line. Derived from the design's equipment
            // cost via the same cost->level formula live squads use, so the number matches what a
            // hired-but-unassigned squad would project. Reserve its width so the deploy label
            // (left-anchored) can't overrun it.
            const float powerW = 160f;
            double power = SquadPowerRegistry.LevelFromCost(selectedSquad.GetEquipmentTotalCost());

            int deployCost = MilitaryDeploymentUtil.CalculateDeploymentCost(selectedSquad.GetEquipmentTotalCost());
            const float gap = 20f;
            float deployX = rect.x + equipWidth + gap;
            UIUtil.ClampedLabel(
                new Rect(deployX, costY, rect.width - (deployX - rect.x) - powerW, 20f),
                "FCSquadDesignDeployCost".Translate(deployCost));

            Text.Anchor = TextAnchor.MiddleRight;
            UIUtil.ClampedLabel(new Rect(rect.xMax - powerW, costY, powerW, 20f),
                (string)"FCSquadColPower".Translate() + ": " + power.ToString("0.0"));

            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;
        }

        // --- Unit List ---

        private void DrawUnitList(Rect rect)
        {
            GameFont fontBefore = Text.Font;
            TextAnchor anchorBefore = Text.Anchor;

            Widgets.DrawMenuSection(rect);

            var groups = BuildUnitGroups(selectedSquad);

            float viewHeight = groups.Count * UnitRowHeight;
            Rect scrollViewRect = ScrollUtil.BeginScrollView(rect, ref unitListScrollPos, viewHeight);

            for (int i = 0; i < groups.Count; i++)
            {
                Rect row = new Rect(scrollViewRect.x, scrollViewRect.y + i * UnitRowHeight,
                    scrollViewRect.width, UnitRowHeight);
                DrawUnitRow(row, groups[i].unit, groups[i].count, i);
            }

            ScrollUtil.EndScrollView();

            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;
        }

        private void DrawUnitRow(Rect row, MilUnitFC unit, int count, int index)
        {
            GameFont fontBefore = Text.Font;
            TextAnchor anchorBefore = Text.Anchor;

            if (index % 2 == 0)
                Widgets.DrawHighlight(row);

            float x = row.x + margin;

            // Pawn preview icon
            Rect pawnRect = new Rect(x, row.y + 2f, UnitRowHeight - 4f, UnitRowHeight - 4f);
            Pawn preview = unit.PreviewPawn;
            if (preview != null)
            {
                UIUtil.DrawPawnPortrait(pawnRect, preview);
            }
            else
            {
                // Fallback icon when there's no preview pawn: prefer the mount, else the first companion.
                PawnKindDef animalIcon = unit.mount ?? unit.animals.FirstOrDefault().kind;
                if (animalIcon != null)
                    Widgets.ButtonImage(pawnRect, animalIcon.race.uiIcon);
            }
            x = pawnRect.xMax + 4f;

            // Weapon icon
            if (unit.HasWeapon)
            {
                Rect weaponRect = new Rect(x, row.y + (UnitRowHeight - IconSize) / 2f,
                    IconSize, IconSize);
                Widgets.DefIcon(weaponRect, unit.weapons[0].thing, unit.weapons[0].stuff);
                x = weaponRect.xMax + 4f;
            }

            // Unit name
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            Rect nameRect = new Rect(x, row.y, 130f, UnitRowHeight);
            UIUtil.ClampedLabel(nameRect, unit.name);
            x = nameRect.xMax + 4f;

            // Xenotype
            string xenoLabel = unit.xenotype?.label?.CapitalizeFirst();
            if (!string.IsNullOrEmpty(xenoLabel))
            {
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleLeft;
                Rect xenoRect = new Rect(x, row.y, 80f, UnitRowHeight);
                UIUtil.ClampedLabel(xenoRect, xenoLabel);
            }

            // Cost (per-unit and line total) — right-aligned before controls
            float perUnitCost = (float)unit.getTotalCost;
            float lineTotalCost = perUnitCost * count;
            string costText = $"${(int)perUnitCost} ea. / ${(int)lineTotalCost}";

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleRight;
            Rect costRect = new Rect(row.xMax - 246f, row.y, 100f, UnitRowHeight);
            UIUtil.ClampedLabel(costRect, costText);

            // +/- controls
            float btnSize = 24f;
            float btnY = row.y + (UnitRowHeight - btnSize) / 2f;
            float controlX = row.xMax - 141f;

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;

            // [-] button
            if (UIUtil.ClampedButtonText(new Rect(controlX, btnY, btnSize, btnSize), "-", true, true, true))
            {
                DecrementUnit(unit);
            }

            // Count label
            Rect countRect = new Rect(controlX + btnSize + 2f, row.y, 26f, UnitRowHeight);
            UIUtil.ClampedLabel(countRect, count.ToString());

            // [+] button
            if (UIUtil.ClampedButtonText(new Rect(countRect.xMax + 2f, btnY, btnSize, btnSize), "+", true, true, true))
            {
                IncrementUnit(unit);
            }

            // [X] remove-all button
            Rect removeRect = new Rect(row.xMax - 54f, btnY, 22f, 22f);
            if (Widgets.ButtonImage(removeRect, TexLoad.deleteX))
            {
                RemoveAllOfUnit(unit);
            }

            // Gear icon — open unit in editor
            Rect gearRect = new Rect(row.xMax - 28f, btnY, 22f, 22f);
            TooltipHandler.TipRegion(gearRect, "FCEditUnitTooltip".Translate());
            if (Widgets.ButtonImage(gearRect, TexLoad.iconCustomize))
            {
                OpenUnitEditor(unit);
            }

            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;
        }

        private void OpenUnitEditor(MilUnitFC unit)
        {
            Window currentWindow = Find.WindowStack.Windows
                .FirstOrDefault(w => w is FCWindow_Military);
            currentWindow?.Close();

            FactionFC fc = FindFC.FactionComp;
            MilitaryWindow duw = MilitaryWindowRegistry.CreateUnits(fc.military, fc);
            FCWindow_Military newWindow = new FCWindow_Military(
                duw, "FCMilitaryTableButtonCreateUnit".Translate());
            Find.WindowStack.Add(newWindow);
            newWindow.SetActive(unit);
        }

        // --- Bottom Bar ---

        private void DrawBottomBar(Rect rect)
        {
            GameFont fontBefore = Text.Font;
            TextAnchor anchorBefore = Text.Anchor;

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;

            float btnW = (rect.width - margin * 4) / 5f;

            // Add Unit button
            Rect addUnitBtn = new Rect(rect.x, rect.y, btnW, ButtonHeight);
            if (UIUtil.ClampedButtonText(addUnitBtn, "FCAddUnit".Translate()))
            {
                Find.WindowStack.Add(new FCWindow_UnitPicker(mfc, AddUnitToSquad));
            }

            // Set Point Ref button
            Rect pointRefBtn = new Rect(addUnitBtn.xMax + margin, rect.y, btnW, ButtonHeight);
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
                selectedSquad.NewSquad();
                selectedSquad.UpdateEquipmentTotalCost();
                selectedSquad.ChangeTick();
            }

            // Hire button — pays the squad's hire cost and adds an unassigned hired squad
            // to the faction pool. Squad assignment to a settlement happens elsewhere
            // (HireSquadsWindow / settlement window).
            int hireCost = (int)Math.Round((selectedSquad?.GetEquipmentTotalCost() ?? 0) * FCSettings.squadHireCostMultiplier);
            float silver = PaymentUtil.GetSilver();
            bool canAffordHire = silver >= hireCost;
            Rect hireBtn = new Rect(resetBtn.xMax + margin, rect.y, btnW, ButtonHeight);
            Color colorBefore = GUI.color;
            if (!canAffordHire) GUI.color = Color.gray;
            if (UIUtil.ClampedButtonText(hireBtn, "FCHireSquadButton".Translate(hireCost), true, true, canAffordHire))
            {
                mfc.HireSquad(selectedSquad);
            }
            TooltipHandler.TipRegion(hireBtn, "FCHireSquadButtonTip".Translate(hireCost));
            GUI.color = colorBefore;

            // Unit count label
            int totalUnits = selectedSquad.Units.Count(u => !u.isBlank);
            Text.Anchor = TextAnchor.MiddleRight;
            Rect countLabel = new Rect(hireBtn.xMax + margin, rect.y, btnW, ButtonHeight);
            UIUtil.ClampedLabel(countLabel, "FCSquadUnitCount".Translate(totalUnits));

            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;
        }

        // --- Grouping Logic ---

        private struct UnitGroup
        {
            public MilUnitFC unit;
            public int count;
        }

        private List<UnitGroup> BuildUnitGroups(MilSquadFC squad)
        {
            return squad.Units
                .Where(u => !u.isBlank)
                .GroupBy(u => u)
                .Select(g => new UnitGroup { unit = g.Key, count = g.Count() })
                .ToList();
        }

        // --- Unit Mutation ---

        private void AddUnitToSquad(MilUnitFC unit)
        {
            int blankIndex = selectedSquad.FindUnitIndex(u => u.isBlank);
            if (blankIndex == -1)
            {
                Messages.Message("FCSquadFull".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }
            selectedSquad.SetUnit(blankIndex, unit);
        }

        private void IncrementUnit(MilUnitFC unit)
        {
            int blankIndex = selectedSquad.FindUnitIndex(u => u.isBlank);
            if (blankIndex == -1)
            {
                Messages.Message("FCSquadFull".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }
            selectedSquad.SetUnit(blankIndex, unit);
        }

        private void DecrementUnit(MilUnitFC unit)
        {
            int lastIndex = -1;
            for (int i = selectedSquad.Units.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(selectedSquad.Units[i], unit))
                {
                    lastIndex = i;
                    break;
                }
            }
            if (lastIndex == -1) return;

            selectedSquad.SetUnit(lastIndex, mfc.blankUnit);
        }

        private void RemoveAllOfUnit(MilUnitFC unit)
        {
            for (int i = 0; i < selectedSquad.Units.Count; i++)
            {
                if (ReferenceEquals(selectedSquad.Units[i], unit))
                {
                    selectedSquad.SetUnit(i, mfc.blankUnit);
                }
            }
        }
    }
}
