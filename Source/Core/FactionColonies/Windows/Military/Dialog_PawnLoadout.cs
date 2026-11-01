using FactionColonies.util;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Per-pawn loadout editor with buffered edits and a live preview pawn.
    ///
    /// All edits go to <see cref="workingLoadout"/> (a clone of <c>merc.ownedLoadout</c> taken
    /// at open time). Nothing is committed to <c>merc.ownedLoadout</c> until the player clicks
    /// Apply; closing the window without Apply discards all changes.
    ///
    /// The preview pawn shown on the left is a deep clone of <c>merc.pawn</c> (via
    /// <see cref="GameComponent_PawnDuplicator"/>) and is re-equipped on the fly from
    /// <see cref="DisplayLoadout"/>. The clone is destroyed in <see cref="PostClose"/>.
    ///
    /// Apply only writes <c>merc.ownedLoadout</c>. The squad inspection's per-pawn Upgrade
    /// button does the assigned -> equipped transition (paying the silver cost diff).
    ///
    /// Reuses <see cref="FCWindow_ItemStuffPicker"/> for weapon and apparel pickers (same UX
    /// as the unit designer). Pawn identity (kindDef / xenotype) is preserved.
    /// </summary>
    public class Dialog_PawnLoadout : Window
    {
        public override Vector2 InitialSize => new Vector2(640f, 680f);

        private readonly MercenarySquadFC squad;
        private readonly Mercenary merc;
        private Vector2 apparelScroll;
        private Vector2 inventoryScroll;
        private Vector2 implantScroll;
        private Vector2 animalScroll;
        private Vector2 psycastScroll;
        private Vector2 mechScroll;
        private LoadoutTab activeTab = LoadoutTab.Apparel;

        /* Buffered edits. null = "inherits from squad template" (same semantics as
         * Mercenary.ownedLoadout being null). Apply writes this onto merc.ownedLoadout. */
        private MilUnitFC workingLoadout;
        private bool dirty;

        /* Live preview pawn — deep clone of merc.pawn, re-equipped from DisplayLoadout.
         * Created lazily on first DoWindowContents and destroyed in PostClose. */
        private Pawn previewPawn;
        private MilUnitFC lastEquippedLoadoutRef;
        private int lastEquippedVersion = int.MinValue;

        public Dialog_PawnLoadout(MercenarySquadFC squad, Mercenary merc)
        {
            this.squad = squad;
            this.merc = merc;
            this.workingLoadout = merc?.ownedLoadout?.Clone();
            doCloseX = true;
            forcePause = false;
            absorbInputAroundWindow = true;
            draggable = true;
        }

        /* The loadout the dialog displays. Mirrors Mercenary.BlueprintLoadout's
         * fallback chain but reads the working buffer rather than ownedLoadout. */
        private MilUnitFC DisplayLoadout =>
            workingLoadout ?? merc?.loadout ?? merc?.currentLoadout;

        /* Returns the working buffer, lazily allocating it on first edit. Picker
         * confirm callbacks call this so that opening + cancelling a picker doesn't
         * break inheritance from the squad template. Always marks dirty — this is
         * only invoked from edit-confirmation paths. */
        private MilUnitFC EnsureWorkingLoadout()
        {
            if (merc is null) return null;
            dirty = true;
            if (workingLoadout != null) return workingLoadout;

            MilUnitFC source = merc.loadout ?? merc.currentLoadout;
            if (source != null)
            {
                workingLoadout = source.Clone();
            }
            else
            {
                workingLoadout = MilTemplateFactory.CreateUnit(false);
                workingLoadout.name = (string)"FCSquadInspectionPersonalLoadoutDefaultName".Translate();
            }
            return workingLoadout;
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (merc is null) { Close(); return; }

            EnsurePreviewPawn();
            RefreshPreviewIfStale();

            GameFont fontBefore = Text.Font;
            TextAnchor anchorBefore = Text.Anchor;

            MilUnitFC current = DisplayLoadout;

            // Header bar (full-width highlight behind the title; right-inset
            // leaves room for the close X which overlaps inRect's top-right).
            Rect headerBar = new Rect(inRect.x, inRect.y, inRect.width - 26f, 35f);
            Widgets.DrawHighlight(headerBar);

            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            string title = merc.pawn != null
                ? (string)"FCDialogPawnLoadoutTitle".Translate(merc.pawn.LabelShortCap)
                : (string)"FCDialogPawnLoadoutTitleEmpty".Translate();
            UIUtil.ClampedLabel(new Rect(headerBar.x + 5f, headerBar.y, headerBar.width - 10f, headerBar.height), title);

            // Subtitle: template association state (based on the working buffer)
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            string sub = workingLoadout != null
                ? (string)"FCDialogPawnLoadoutDivergedSubtitle".Translate()
                : (string)"FCDialogPawnLoadoutInheritedSubtitle".Translate(merc.loadout?.name ?? (string)"FCNone".Translate());
            UIUtil.ClampedLabel(new Rect(inRect.x, headerBar.yMax + 4f, inRect.width, 18f), sub);

            // Info: race + xenotype (read-only — pawn identity is preserved here)
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            string raceName = current?.pawnKind?.race?.label?.CapitalizeFirst() ?? "Unknown";
            string infoLine;
            if (ModsConfig.BiotechActive)
            {
                string xenoName = current?.GetXenotypeLabel() ?? "";
                infoLine = "Race".Translate() + ": " + raceName + "   ·   " + "Xenotype".Translate() + ": " + xenoName;
            }
            else
            {
                infoLine = "Race".Translate() + ": " + raceName;
            }
            UIUtil.ClampedLabel(new Rect(inRect.x, headerBar.yMax + 26f, inRect.width, 20f), infoLine);

            // Total equipment cost (read-only — Upgrade pays cost diff on equip)
            float totalCost = current != null ? (float)current.getTotalCost : 0f;
            UIUtil.ClampedLabel(new Rect(inRect.x, headerBar.yMax + 48f, inRect.width, 20f),
                "FCTotalEquipmentCostLabel".Translate() + totalCost.ToString("F0"));

            // Layout: portrait + slots on the left, apparel list on the right
            float topY = inRect.y + 105f;
            float bottomBtnH = 36f;

            float leftW = 220f;
            Rect leftPanel = new Rect(inRect.x, topY, leftW, inRect.height - (topY - inRect.y) - bottomBtnH - 8f);
            // Pull the right edge in a few px so the tab box's right border isn't clipped by the window frame.
            Rect rightPanel = new Rect(inRect.x + leftW + 10f, topY, inRect.width - leftW - 10f - 3f, leftPanel.height);

            DrawLeftPanel(leftPanel);
            DrawLoadoutPanel(rightPanel);

            // Bottom: [Pick] [Reset]               [Apply] [Close]
            Rect bottomRect = new Rect(inRect.x, inRect.yMax - bottomBtnH, inRect.width, bottomBtnH);
            float gap = 8f;
            float pickW = 200f;
            float resetW = 180f;
            float rightBtnW = 90f;

            Rect pickRect = new Rect(bottomRect.x, bottomRect.y, pickW, bottomRect.height);
            if (UIUtil.ClampedButtonText(pickRect, "FCDialogPawnLoadoutPickFromPool".Translate()))
            {
                OpenPickFromPoolMenu();
            }

            // Reset: clear workingLoadout so DisplayLoadout falls back to the squad
            // template (loadout). Buffered — only Apply commits.
            bool canReset = workingLoadout != null && merc.loadout != null;
            Color colorBefore = GUI.color;
            if (!canReset) GUI.color = Color.gray;
            Rect resetRect = new Rect(pickRect.xMax + gap, bottomRect.y, resetW, bottomRect.height);
            if (UIUtil.ClampedButtonText(resetRect, "FCDialogPawnLoadoutResetToPool".Translate(), true, true, canReset))
            {
                workingLoadout = null;
                dirty = true;
            }
            GUI.color = colorBefore;

            Rect closeRect = new Rect(bottomRect.xMax - rightBtnW, bottomRect.y, rightBtnW, bottomRect.height);
            Rect applyRect = new Rect(closeRect.x - gap - rightBtnW, bottomRect.y, rightBtnW, bottomRect.height);

            if (!dirty) GUI.color = Color.gray;
            if (UIUtil.ClampedButtonText(applyRect, "FCDialogPawnLoadoutApply".Translate(), true, true, dirty))
            {
                ApplyChanges();
            }
            GUI.color = colorBefore;

            if (UIUtil.ClampedButtonText(closeRect, "FCDialogPawnLoadoutClose".Translate())) Close();

            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;
        }

        public override void PostClose()
        {
            base.PostClose();
            DestroyPreviewPawn();
            workingLoadout = null;
        }

        // --- Apply ---

        /* Commits workingLoadout onto merc.ownedLoadout. Clones so the dialog can
         * keep buffering further edits without aliasing the assigned loadout. */
        private void ApplyChanges()
        {
            if (merc is null) return;
            merc.ownedLoadout = workingLoadout?.Clone();
            dirty = false;
        }

        // --- Left panel: pawn portrait + weapon/animal slots ---

        private void DrawLeftPanel(Rect rect)
        {
            const float portraitH = 130f;
            const float slotSize = 50f;
            const float gap = 14f;

            Rect portraitRect = new Rect(rect.x + (rect.width - 100f) / 2f, rect.y, 100f, portraitH);
            if (previewPawn != null)
                UIUtil.DrawPawnPortrait(portraitRect, previewPawn);
            else
                Widgets.DrawMenuSection(portraitRect);

            // The companion-animal slot is repurposed as a Mount slot when Giddy Up 2 is active;
            // otherwise it's hidden (companions are edited only in the Animals tab) and the weapon slot
            // sits centered on its own.
            bool showMount = FactionCompat.GiddyUp2Active;
            float slotsY = portraitRect.yMax + gap + 18f;
            float slotsTotalW = showMount ? slotSize * 2 + 16f : slotSize;
            float slotsX = rect.x + (rect.width - slotsTotalW) / 2f;
            Rect mountSlot = new Rect(slotsX, slotsY, slotSize, slotSize);
            Rect weaponSlot = showMount
                ? new Rect(slotsX + slotSize + 16f, slotsY, slotSize, slotSize)
                : new Rect(slotsX, slotsY, slotSize, slotSize);

            GameFont fontBefore = Text.Font;
            TextAnchor anchorBefore = Text.Anchor;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.UpperCenter;
            if (showMount)
            {
                UIUtil.ClampedLabel(new Rect(mountSlot.x - 15f, mountSlot.y - 18f, mountSlot.width + 30f, 18f), "fcLabelMount".Translate());
                Widgets.DrawMenuSection(mountSlot);
            }
            UIUtil.ClampedLabel(new Rect(weaponSlot.x - 15f, weaponSlot.y - 18f, weaponSlot.width + 30f, 18f), "fcLabelWeapon".Translate());
            Widgets.DrawMenuSection(weaponSlot);
            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;

            // Display the working buffer (or template fallback) — what the player is editing.
            MilUnitFC current = DisplayLoadout;
            // Use non-interactive draws so ButtonInvisible below handles all clicks.
            if (current?.HasWeapon == true)
                Widgets.DrawTextureFitted(weaponSlot, current.weapons[0].thing.uiIcon, 1f);
            if (showMount && current?.mount != null)
                Widgets.DrawTextureFitted(mountSlot, current.mount.race.uiIcon, 1f);

            if (Widgets.ButtonInvisible(weaponSlot))
            {
                OpenWeaponPicker();
            }
            if (showMount && Widgets.ButtonInvisible(mountSlot))
            {
                OpenMountPicker();
            }
        }

        // --- Right panel: tabbed loadout (Apparel / Inventory / Implants) ---

        /* Implant note: edits go to the buffered workingLoadout and apply on the next real
         * outfit pass. The duplicator-clone preview portrait re-equips apparel/weapons live but
         * does NOT live-rebuild health, so implant graphics changes are not reflected here (most
         * implants are invisible anyway). */
        private void DrawLoadoutPanel(Rect rect)
        {
            Rect content;
            // Show the Psycasts tab only when a psycast system is available (Royalty / VPE / other),
            // matching the unit designer. Edits buffer into workingLoadout like every other tab; the
            // live pawn is reconciled later by the squad inspection's per-pawn Upgrade.
            bool showPsycasts = PsycastSystemRegistry.Active != null;
            bool showMechs = ModsConfig.BiotechActive;
            activeTab = LoadoutTabStrip.Draw(rect, activeTab, out content, includePsycasts: showPsycasts, includeMechs: showMechs);
            content = content.ContractedBy(4f);

            if (activeTab == LoadoutTab.Apparel)
            {
                ApparelListWidget.Draw(content, DisplayLoadout, ref apparelScroll, new ApparelListWidget.Options
                {
                    canEdit = true,
                    showHeaderButtons = true,
                    getEditTarget = EnsureWorkingLoadout,
                });
            }
            else if (activeTab == LoadoutTab.Inventory)
            {
                InventoryListWidget.Draw(content, DisplayLoadout, ref inventoryScroll, new InventoryListWidget.Options
                {
                    canEdit = true,
                    showHeaderButtons = true,
                    getEditTarget = EnsureWorkingLoadout,
                });
            }
            else if (activeTab == LoadoutTab.Implants)
            {
                ImplantListWidget.Draw(content, DisplayLoadout, ref implantScroll, new ImplantListWidget.Options
                {
                    canEdit = true,
                    showHeaderButtons = true,
                    getEditTarget = EnsureWorkingLoadout,
                    getDisplayUnit = () => DisplayLoadout,
                });
            }
            else if (activeTab == LoadoutTab.Animals)
            {
                AnimalListWidget.Draw(content, DisplayLoadout, ref animalScroll, new AnimalListWidget.Options
                {
                    canEdit = true,
                    showHeaderButtons = true,
                    getEditTarget = EnsureWorkingLoadout,
                    getDisplayUnit = () => DisplayLoadout,
                });
            }
            else if (activeTab == LoadoutTab.Psycasts)
            {
                PsycastListWidget.Draw(content, DisplayLoadout, ref psycastScroll, new PsycastListWidget.Options
                {
                    canEdit = true,
                    showHeaderButtons = true,
                    getEditTarget = EnsureWorkingLoadout,
                    getDisplayUnit = () => DisplayLoadout,
                });
            }
            else
            {
                MechListWidget.Draw(content, DisplayLoadout, ref mechScroll, new MechListWidget.Options
                {
                    canEdit = true,
                    showHeaderButtons = true,
                    getEditTarget = EnsureWorkingLoadout,
                    getDisplayUnit = () => DisplayLoadout,
                });
            }
        }

        // --- Pickers ---

        /* All pickers defer EnsureWorkingLoadout into their confirm callbacks so that
         * opening and cancelling does not break the template association. Display
         * filters read from DisplayLoadout (which falls back to the squad template). */

        private void OpenWeaponPicker()
        {
            MilUnitFC source = DisplayLoadout;
            ThingDef raceDef = source?.pawnKind?.race;
            List<ThingDef> weaponDefs = DefDatabase<ThingDef>.AllDefs
                .Where(t => t.IsWeapon && t.BaseMarketValue != 0
                    && !CraftUtil.WeaponBlockedForMercs(t)
                    && t.generateAllowChance > 0f
                    && CraftUtil.CanCraftItem(t)
                    && HARUtil.CanRaceUseWeapon(raceDef, t))
                .OrderBy(t => t.label)
                .ToList();

            SavedThing? currentWeapon = source?.HasWeapon == true ? source.weapons[0] : (SavedThing?)null;
            Find.WindowStack.Add(new FCWindow_ItemStuffPicker(
                weaponDefs,
                onConfirm: (item, stuff, quality) =>
                {
                    MilUnitFC target = EnsureWorkingLoadout();
                    if (target != null) target.SetWeapon(item, stuff, quality);
                },
                onUnequip: () =>
                {
                    MilUnitFC target = EnsureWorkingLoadout();
                    if (target != null) target.ClearWeapon();
                },
                titleKey: "fcPickWeapon",
                initialItem: currentWeapon?.thing,
                initialStuff: currentWeapon?.stuff,
                initialQuality: currentWeapon?.quality
            ));
        }

        private void OpenMountPicker()
        {
            MilUnitFC source = DisplayLoadout;
            Find.WindowStack.Add(new FCWindow_MountPicker(
                initialMount: source?.mount,
                onConfirm: picked =>
                {
                    MilUnitFC target = EnsureWorkingLoadout();
                    if (target is null) return;
                    target.SetMount(picked);
                },
                onUnequip: () =>
                {
                    MilUnitFC target = EnsureWorkingLoadout();
                    if (target is null) return;
                    target.SetMount(null);
                }
            ));
        }

        // --- Pick from unit template ---

        private void OpenPickFromPoolMenu()
        {
            MilitaryFC mfc = FindFC.Military;
            if (mfc?.units is null) return;
            List<FloatMenuOption> options = new List<FloatMenuOption>();
            foreach (MilUnitFC unit in mfc.units)
            {
                MilUnitFC captured = unit;
                options.Add(new FloatMenuOption(captured.name, delegate
                {
                    // Personalize this merc to use the picked unit template's gear as their
                    // working loadout. Buffered — only Apply commits to ownedLoadout.
                    workingLoadout = captured.Clone();
                    dirty = true;
                }));
            }
            if (options.Count == 0)
                options.Add(new FloatMenuOption("FCNoUnitAvailable".Translate(), null));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        // --- Preview pawn ---

        /* Lazy-clones merc.pawn into previewPawn on first call. Uses the base game's
         * GameComponent_PawnDuplicator which deep-copies identity, appearance, genes,
         * traits, skills, hediffs, abilities, and arrives with no gear (forceNoGear).
         *
         * Anomaly side effect: Duplicate writes pawn.duplicate.duplicateOf on the
         * source (used by the duplicate-sickness mechanic). Snapshot/restore around
         * the call so opening this dialog does not silently mark merc.pawn as a
         * duplicate. The clone's own duplicate state is irrelevant — we destroy it
         * in PostClose. */
        private void EnsurePreviewPawn()
        {
            if (previewPawn != null) return;
            if (merc?.pawn is null) return;

            int savedDuplicateOf = int.MinValue;
            bool hadDup = ModsConfig.AnomalyActive && merc.pawn.duplicate != null;
            if (hadDup) savedDuplicateOf = merc.pawn.duplicate.duplicateOf;

            try
            {
                GameComponent_PawnDuplicator dup = Current.Game?.GetComponent<GameComponent_PawnDuplicator>();
                if (dup is null) return;
                previewPawn = dup.Duplicate(merc.pawn);
            }
            catch (System.Exception ex)
            {
                LogUtil.Warning($"Dialog_PawnLoadout: failed to clone preview pawn: {ex.Message}");
                previewPawn = null;
            }
            finally
            {
                if (hadDup) merc.pawn.duplicate.duplicateOf = savedDuplicateOf;
            }
        }

        /* Re-equips previewPawn from DisplayLoadout when either the buffer reference
         * or its editVersion differs from the last equip pass. */
        private void RefreshPreviewIfStale()
        {
            if (previewPawn is null) return;
            MilUnitFC display = DisplayLoadout;
            int version = display?.editVersion ?? int.MinValue;
            if (display == lastEquippedLoadoutRef && version == lastEquippedVersion) return;

            MilUnitFC.ApplyEquipmentToPawn(previewPawn, display);
            previewPawn.Drawer?.renderer?.SetAllGraphicsDirty();
            PortraitsCache.SetDirty(previewPawn);

            lastEquippedLoadoutRef = display;
            lastEquippedVersion = version;
        }

        private void DestroyPreviewPawn()
        {
            if (previewPawn is null) return;
            try
            {
                previewPawn.apparel?.DestroyAll();
                previewPawn.equipment?.DestroyAllEquipment();
                if (!previewPawn.Destroyed) previewPawn.Destroy();
            }
            catch (System.Exception ex)
            {
                LogUtil.Warning($"Dialog_PawnLoadout: failed to destroy preview pawn: {ex.Message}");
            }
            finally
            {
                previewPawn = null;
                lastEquippedLoadoutRef = null;
                lastEquippedVersion = int.MinValue;
            }
        }
    }
}
