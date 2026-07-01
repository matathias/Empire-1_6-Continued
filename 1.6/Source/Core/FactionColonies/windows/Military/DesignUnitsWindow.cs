using FactionColonies.util;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    public class DesignUnitsWindow : MilitaryWindow
    {
        public override MilitaryWindowSlot Slot => MilitaryWindowSlot.Units;

        protected readonly MilitaryFC mfc;
        protected readonly FactionFC faction;
        protected MilUnitFC selectedUnit;

        private Vector2 unitListScrollPos;
        private string unitSearchTerm = "";
        private Vector2 apparelListScrollPos;
        private Vector2 inventoryListScrollPos;
        private Vector2 implantListScrollPos;
        private Vector2 animalListScrollPos;
        private Vector2 psycastListScrollPos;
        private Vector2 mechListScrollPos;
        private LoadoutTab activeTab = LoadoutTab.Apparel;

        // Layout sizing constants
        private const float SidebarWidth = 250f;
        private const float GearWidth = 310f;
        private const float RowHeight = 30f;
        private const float SearchBarHeight = 28f;
        private const float IconSize = 24f;
        private const float margin = 5f;
        private const float ButtonHeight = 30f;

        public DesignUnitsWindow(MilitaryFC mfc, FactionFC faction)
        {
            this.mfc = mfc;
            this.faction = faction;

            selectedText = "Select A Unit";

            mfc.CheckMilitaryUtilForErrors();
        }

        public override void Select(IExposable selecting)
        {
            MilUnitFC unit = (MilUnitFC)selecting;
            selectedUnit = unit;
            selectedText = unit.name;
        }

        public override void DrawTab(Rect rect)
        {
            Widgets.DrawLineHorizontal(rect.x, rect.y + 45, rect.width);

            GameFont fontBefore = Text.Font;
            TextAnchor anchorBefore = Text.Anchor;

            float contentTop = rect.y + 45f + margin;
            float rightEdge = rect.xMax - margin;

            // Layout Y metrics
            float belowHighlight = contentTop + 35f + margin;
            float gearTop = belowHighlight + 61f + margin;
            float gearBottom = gearTop + 305f;
            float contentBottom = rect.yMax - margin;

            // Left sidebar: search + unit list + action buttons
            Rect sidebarRect = new Rect(rect.x + margin, contentTop,
                SidebarWidth, contentBottom - contentTop);
            DrawSidebar(sidebarRect);

            // Content area starts after sidebar + gap
            float contentLeft = sidebarRect.xMax + 10f;
            Rect gearRect = new Rect(contentLeft, gearTop, GearWidth, 305f);

            if (selectedUnit != null)
            {
                Rect headerRect = new Rect(contentLeft, contentTop,
                    rightEdge - contentLeft, 85f);
                DrawUnitHeader(headerRect);

                Rect buttonsRect = new Rect(contentLeft + GearWidth + 10f, belowHighlight,
                    rightEdge - contentLeft - GearWidth - 10f, 61f);
                DrawActionButtons(buttonsRect);

                DrawGearPanel(gearRect);

                // The gear panel (left) has fixed-height content, but the loadout panel (the tabbed
                // equipment list) should fill the rest of the window height — otherwise there's dead
                // space below it.
                Rect loadoutRect = new Rect(gearRect.xMax + 10f, gearRect.y,
                    rightEdge - gearRect.xMax - 10f, contentBottom - gearRect.y);
                DrawLoadoutPanel(loadoutRect, selectedUnit);
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
            unitSearchTerm = Widgets.TextField(searchRect, unitSearchTerm);

            // Unit list (fills space between search bar and buttons)
            float buttonsHeight = ButtonHeight * 2 + margin;
            float listHeight = rect.yMax - searchRect.yMax - margin - buttonsHeight - margin;
            Rect listOutRect = new Rect(rect.x, searchRect.yMax + margin, rect.width, listHeight);
            Widgets.DrawMenuSection(listOutRect);

            List<MilUnitFC> filteredUnits = string.IsNullOrEmpty(unitSearchTerm)
                ? mfc.units
                : mfc.units.Where(u => (u.name ?? "").IndexOf(unitSearchTerm, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            float viewHeight = filteredUnits.Count * RowHeight;
            Rect scrollViewRect = ScrollUtil.BeginScrollView(listOutRect, ref unitListScrollPos, viewHeight);

            for (int i = 0; i < filteredUnits.Count; i++)
            {
                MilUnitFC unit = filteredUnits[i];
                Rect row = new Rect(scrollViewRect.x, scrollViewRect.y + i * RowHeight, scrollViewRect.width, RowHeight);

                if (unit == selectedUnit)
                    Widgets.DrawHighlightSelected(row);
                else if (i % 2 == 0)
                    Widgets.DrawHighlight(row);

                // Weapon icon
                Rect iconRect = new Rect(row.x + 2f, row.y + 3f, IconSize, IconSize);
                if (unit.HasWeapon)
                    Widgets.DefIcon(iconRect, unit.weapons[0].thing, unit.weapons[0].stuff);
                // Name label
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;
                Rect labelRect = new Rect(iconRect.xMax + 4f, row.y, row.xMax - iconRect.xMax - 6f, RowHeight);
                UIUtil.ClampedLabel(labelRect, unit.name);

                if (Widgets.ButtonInvisible(row))
                {
                    selectedUnit = unit;
                    selectedText = unit.name;
                }
            }

            ScrollUtil.EndScrollView();

            // Action buttons (2x2 grid)
            float btnY = listOutRect.yMax + margin;
            float buttonW = (rect.width - margin) / 2f;

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;

            Rect createBtn = new Rect(rect.x, btnY, buttonW, ButtonHeight);
            Rect importBtn = new Rect(rect.x + buttonW + margin, btnY, buttonW, ButtonHeight);
            Rect deleteBtn = new Rect(rect.x, btnY + ButtonHeight + margin, buttonW, ButtonHeight);
            Rect exportBtn = new Rect(rect.x + buttonW + margin, btnY + ButtonHeight + margin, buttonW, ButtonHeight);

            if (UIUtil.ClampedButtonText(createBtn, "FCCreateNewUnit".Translate()))
            {
                MilUnitFC newUnit = MilTemplateFactory.CreateUnit(false);
                newUnit.name = $"New Unit {mfc.units.Count + 1}";
                selectedText = newUnit.name;
                selectedUnit = newUnit;
                mfc.units.Add(newUnit);
            }

            if (UIUtil.ClampedButtonText(importBtn, "FCImportUnit".Translate()))
            {
                Find.WindowStack.Add(new Dialog_ManageUnitExportsFC(
                    FactionColoniesMilitary.SavedUnits.ToList()));
            }

            if (selectedUnit != null)
            {
                if (UIUtil.ClampedButtonText(deleteBtn, "FCDeleteUnitButton".Translate()))
                {
                    MilUnitFC unitToDelete = selectedUnit;
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                        "FCConfirmDeleteUnit".Translate((NamedArgument)unitToDelete.name),
                        delegate
                        {
                            // Routes through DeleteUnit so any merc referencing the unit
                            // snapshots into ownedLoadout (gear preserved).
                            mfc.DeleteUnit(unitToDelete);
                            mfc.CheckMilitaryUtilForErrors();
                            if (selectedUnit == unitToDelete)
                            {
                                selectedUnit = null;
                                selectedText = "FCSelectAUnitButton".Translate();
                            }
                        }));
                }

                if (UIUtil.ClampedButtonText(exportBtn, "FCExportUnitButton".Translate()))
                {
                    FactionColoniesMilitary.SaveUnit(selectedUnit.ToSavedUnit());
                    Messages.Message("FCExportUnit".Translate(), MessageTypeDefOf.TaskCompletion);
                }
            }

            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;
        }

        // --- Unit Header ---

        private void DrawUnitHeader(Rect rect)
        {
            GameFont fontBefore = Text.Font;
            TextAnchor anchorBefore = Text.Anchor;

            // Highlight banner behind unit name
            Rect highlightBar = new Rect(rect.x, rect.y, rect.width, 35f);
            Widgets.DrawHighlight(highlightBar);

            // Unit name (large label)
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            Rect nameRect = new Rect(rect.x + margin, rect.y, 400f, 30f);
            UIUtil.ClampedLabel(nameRect, selectedUnit.name);

            // Pencil icon to trigger rename
            float nameTextWidth = Text.CalcSize(selectedUnit.name).x;
            Rect pencilRect = new Rect(rect.x + Mathf.Min(nameTextWidth + 8f + margin, rect.width - 22f), rect.y + 4f, 22f, 22f);
            if (Widgets.ButtonImage(pencilRect, TexButton.Rename))
            {
                Find.WindowStack.Add(new FCWindow_Rename(selectedUnit.name, "FCRenameUnit", name => selectedUnit.name = name));
            }

            // Race / Xeno info line
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            Rect infoRect = new Rect(rect.x, highlightBar.yMax + margin, rect.width, 20f);
            string raceName = selectedUnit.pawnKind?.race?.label?.CapitalizeFirst() ?? "Unknown";
            if (ModsConfig.BiotechActive)
            {
                string xenoName = selectedUnit.GetXenotypeLabel();
                UIUtil.ClampedLabel(infoRect, "Race".Translate() + ": " + raceName + "   ·   " + "Xenotype".Translate() + ": " + xenoName);
            }
            else
            {
                UIUtil.ClampedLabel(infoRect, "Race".Translate() + ": " + raceName);
            }

            // Equipment cost
            float totalCost = (float)selectedUnit.getTotalCost;
            Rect costRect = new Rect(rect.x, infoRect.yMax + margin, rect.width, 20f);
            UIUtil.ClampedLabel(costRect, "FCTotalEquipmentCostLabel".Translate() + totalCost.ToString("F0"));

            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;
        }

        // --- Action Buttons ---

        private void DrawActionButtons(Rect rect)
        {
            float btnH = 28f;
            float gap = 5f;
            float btnW = (rect.width - gap) / 2f;

            GameFont fontBefore = Text.Font;
            TextAnchor anchorBefore = Text.Anchor;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;

            float raceButtonWidth = ModsConfig.BiotechActive ? btnW : (2 * btnW) + gap;

            if (UIUtil.ClampedButtonText(new Rect(rect.x, rect.y, raceButtonWidth, btnH), "FCChangeUnitRaceButton".Translate(), true, true))
            {
                Find.WindowStack.Add(new FCWindow_RacePicker(selectedUnit, faction));
            }

            if (ModsConfig.BiotechActive &&
                UIUtil.ClampedButtonText(new Rect(rect.x + btnW + gap, rect.y, btnW, btnH), "FCChangeUnitXenoButton".Translate(), true, true))
            {
                Find.WindowStack.Add(new FCWindow_XenoPicker(selectedUnit));
            }

            float y2 = rect.y + btnH + gap;

            if (UIUtil.ClampedButtonText(new Rect(rect.x, y2, btnW, btnH), "FCRollANewUnitButton".Translate(), true, true))
            {
                selectedUnit.RerollPreviewPawn();
            }

            if (UIUtil.ClampedButtonText(new Rect(rect.x + btnW + gap, y2, btnW, btnH), "FCResetUnitToDefaultButton".Translate(), true, true))
            {
                selectedUnit.ClearAllEquipment();
            }

            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;
        }

        // --- Gear Panel ---

        private void DrawGearPanel(Rect gearArea)
        {
            const float pawnWidth = 100f;
            const float pawnHeight = 130f;
            const float slotSize = 50f;
            const float slotGap = 15f;

            // Pawn preview centered near the top of the gear area
            Rect unitIcon = new Rect(
                gearArea.x + (gearArea.width - pawnWidth) / 2f,
                gearArea.y + 5f,
                pawnWidth, pawnHeight);

            // Weapon slot, plus a Mount slot when Giddy Up 2 is active. Without GU2 the weapon slot is
            // centered alone and companion animals live entirely in the Animals tab.
            bool showMount = FactionCompat.GiddyUp2Active;
            float slotsY = unitIcon.yMax + slotGap + 15f; // +15 for label above
            float slotsWidth = showMount ? slotSize * 2 + 20f : slotSize;
            float slotsStartX = gearArea.x + (gearArea.width - slotsWidth) / 2f;

            Rect MountSlot = new Rect(slotsStartX, slotsY, slotSize, slotSize);
            Rect EquipmentWeapon = showMount
                ? new Rect(slotsStartX + slotSize + 20f, slotsY, slotSize, slotSize)
                : new Rect(slotsStartX, slotsY, slotSize, slotSize);

            // --- Always drawn: slot backgrounds and labels ---
            GameFont fontBefore = Text.Font;
            TextAnchor anchorBefore = Text.Anchor;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.UpperCenter;

            if (showMount)
            {
                UIUtil.ClampedLabel(new Rect(MountSlot.x, MountSlot.y - 15f, MountSlot.width, 18f), "fcLabelMount".Translate());
                Widgets.DrawMenuSection(MountSlot);
            }
            UIUtil.ClampedLabel(new Rect(EquipmentWeapon.x, EquipmentWeapon.y - 15f, EquipmentWeapon.width, 18f), "fcLabelWeapon".Translate());
            Widgets.DrawMenuSection(EquipmentWeapon);

            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;

            // --- Unit-selected content ---
            if (selectedUnit == null) return;

            // Draw Pawn Preview
            Pawn preview = selectedUnit.PreviewPawn;
            if (preview != null)
            {
                UIUtil.DrawPawnPortrait(unitIcon, preview, 1.2f);
            }

            // --- Mount Slot (Giddy Up 2 only) ---
            if (showMount && Widgets.ButtonInvisible(MountSlot))
            {
                Find.WindowStack.Add(new FCWindow_MountPicker(selectedUnit));
            }

            // --- Weapon Slot ---
            if (Widgets.ButtonInvisible(EquipmentWeapon))
            {
                List<ThingDef> weaponDefs = DefDatabase<ThingDef>.AllDefs
                    .Where(t => t.IsWeapon && t.BaseMarketValue != 0
                        && !CraftUtil.WeaponBlockedForMercs(t)
                        && t.generateAllowChance > 0f // blocks unique weapons
                        && CraftUtil.CanCraftItem(t)
                        && HARUtil.CanRaceUseWeapon(selectedUnit.pawnKind?.race, t))
                    .OrderBy(t => t.label)
                    .ToList();

                SavedThing? currentWeapon = selectedUnit.HasWeapon ? selectedUnit.weapons[0] : (SavedThing?)null;
                Find.WindowStack.Add(new FCWindow_ItemStuffPicker(
                    weaponDefs,
                    onConfirm: (item, stuff, quality) => selectedUnit.SetWeapon(item, stuff, quality),
                    onUnequip: () => selectedUnit.ClearWeapon(),
                    titleKey: "fcPickWeapon",
                    initialItem: currentWeapon?.thing,
                    initialStuff: currentWeapon?.stuff,
                    initialQuality: currentWeapon?.quality
                ));
            }

            // Mount icon
            if (showMount && selectedUnit.mount != null)
            {
                Widgets.ButtonImage(MountSlot, selectedUnit.mount.race.uiIcon);
            }

            // Weapon icon
            if (selectedUnit.HasWeapon)
            {
                Widgets.ButtonImage(EquipmentWeapon, selectedUnit.weapons[0].thing.uiIcon);
            }

            // --- Gender control (below the slots) ---
            float genderY = EquipmentWeapon.yMax + 25f;
            Rect genderLabelRect = new Rect(slotsStartX-10f, genderY, slotsWidth+20f, 16f);
            Rect genderBtnRect = new Rect(slotsStartX-10f, genderLabelRect.yMax, slotsWidth+20f, 28f);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.UpperCenter;
            UIUtil.ClampedLabel(genderLabelRect, "fcUnitGender".Translate());
            Text.Anchor = anchorBefore;
            Text.Font = GameFont.Small;
            if (UIUtil.ClampedButtonText(genderBtnRect, GenderLabel(selectedUnit.forcedGender)))
            {
                MilUnitFC captured = selectedUnit;
                List<FloatMenuOption> opts = new List<FloatMenuOption>
                {
                    new FloatMenuOption("fcGenderAny".Translate(), () => captured.SetForcedGender(null)),
                    new FloatMenuOption("fcGenderMale".Translate(), () => captured.SetForcedGender(Gender.Male)),
                    new FloatMenuOption("fcGenderFemale".Translate(), () => captured.SetForcedGender(Gender.Female)),
                };
                Find.WindowStack.Add(new FloatMenu(opts));
            }
            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;
        }

        private static string GenderLabel(Gender? g)
        {
            if (!g.HasValue || g.Value == Gender.None) return "fcGenderAny".Translate();
            return g.Value == Gender.Male ? "fcGenderMale".Translate() : "fcGenderFemale".Translate();
        }

        // --- Loadout Panel (tabbed: Apparel / Inventory / Implants) ---

        private void DrawLoadoutPanel(Rect rect, MilUnitFC unit)
        {
            Rect content;
            // Only show the Psycasts tab when a psycast system is actually available
            // (Royalty, VPE, or another provider) — otherwise it's an empty, useless tab.
            bool showPsycasts = PsycastSystemRegistry.Active != null;
            // Mechs tab requires Biotech (mechanitors/mechlinks).
            bool showMechs = ModsConfig.BiotechActive;
            activeTab = LoadoutTabStrip.Draw(rect, activeTab, out content, includePsycasts: showPsycasts, includeMechs: showMechs);
            content = content.ContractedBy(4f);

            if (activeTab == LoadoutTab.Apparel)
            {
                ApparelListWidget.Draw(content, unit, ref apparelListScrollPos, new ApparelListWidget.Options
                {
                    canEdit = true,
                    showHeaderButtons = true,
                    getEditTarget = () => unit,
                });
            }
            else if (activeTab == LoadoutTab.Inventory)
            {
                InventoryListWidget.Draw(content, unit, ref inventoryListScrollPos, new InventoryListWidget.Options
                {
                    canEdit = true,
                    showHeaderButtons = true,
                    getEditTarget = () => unit,
                });
            }
            else if (activeTab == LoadoutTab.Implants)
            {
                ImplantListWidget.Draw(content, unit, ref implantListScrollPos, new ImplantListWidget.Options
                {
                    canEdit = true,
                    showHeaderButtons = true,
                    getEditTarget = () => unit,
                    getDisplayUnit = () => unit,
                });
            }
            else if (activeTab == LoadoutTab.Animals)
            {
                AnimalListWidget.Draw(content, unit, ref animalListScrollPos, new AnimalListWidget.Options
                {
                    canEdit = true,
                    showHeaderButtons = true,
                    getEditTarget = () => unit,
                    getDisplayUnit = () => unit,
                });
            }
            else if (activeTab == LoadoutTab.Psycasts)
            {
                PsycastListWidget.Draw(content, unit, ref psycastListScrollPos, new PsycastListWidget.Options
                {
                    canEdit = true,
                    showHeaderButtons = true,
                    getEditTarget = () => unit,
                    getDisplayUnit = () => unit,
                });
            }
            else
            {
                MechListWidget.Draw(content, unit, ref mechListScrollPos, new MechListWidget.Options
                {
                    canEdit = true,
                    showHeaderButtons = true,
                    getEditTarget = () => unit,
                    getDisplayUnit = () => unit,
                });
            }
        }
    }
}
