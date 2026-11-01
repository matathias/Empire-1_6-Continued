using FactionColonies.util;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    /* Shared carried-inventory panel used by DesignUnitsWindow (per-template) and
     * Dialog_PawnLoadout (per-pawn). Reads from displayUnit; routes mutations through
     * opts.getEditTarget so the per-pawn editor can defer cloning a squad template into
     * ownedLoadout. The 70% carry-weight cap is enforced inside MilUnitFC.AddInventory /
     * SetInventoryCount, not here. */
    public static class InventoryListWidget
    {
        public struct Options
        {
            public bool canEdit;
            public bool showHeaderButtons;
            public Func<MilUnitFC> getEditTarget;
        }

        private const float headerHeight = 25f;
        private const float rowHeight = 28f;
        private const float removeButtonSize = 20f;
        private const float IconSize = 24f;

        public static void Draw(Rect rect, MilUnitFC displayUnit, ref Vector2 scrollPos, Options opts)
        {
            GameFont fontBefore = Text.Font;
            TextAnchor anchorBefore = Text.Anchor;

            // Top row: mass usage (left) + Add button (right). No title — the tab labels the panel.
            float btnY = rect.y;
            if (displayUnit != null)
            {
                float cur = displayUnit.CurrentInventoryMass;
                float cap = displayUnit.CarryCapacity;
                Rect massRect = new Rect(rect.x, btnY, rect.width, headerHeight);
                Color colorBefore = GUI.color;
                if (cur > cap + 0.0001f) GUI.color = Color.red;
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleLeft;
                UIUtil.ClampedLabel(massRect, "fcInventoryMass".Translate(cur.ToString("F1"), cap.ToString("F1")));
                GUI.color = colorBefore;

                if (opts.canEdit && opts.showHeaderButtons)
                {
                    float addW = 110f;
                    Rect addBtnRect = new Rect(rect.xMax - addW, btnY, addW, headerHeight);
                    Text.Font = GameFont.Tiny;
                    Text.Anchor = TextAnchor.MiddleCenter;
                    if (UIUtil.ClampedButtonText(addBtnRect, "fcAddInventoryItem".Translate()))
                        OpenInventoryPicker(displayUnit, opts);
                }
            }

            // List
            Rect listOutRect = new Rect(rect.x, btnY + headerHeight + 2f, rect.width, rect.height - headerHeight - 4f);

            List<SavedThing> weapons = displayUnit?.weapons ?? new List<SavedThing>();
            List<SavedThing> items = displayUnit?.inventory ?? new List<SavedThing>();

            int weaponRows = 0;
            foreach (SavedThing w in weapons) if (w.thing != null) weaponRows++;

            float viewHeight = (weaponRows + items.Count) * rowHeight;
            Rect scrollViewRect = ScrollUtil.BeginScrollView(listOutRect, ref scrollPos, viewHeight);

            int drawn = 0;

            // Equipped weapon(s): shown here so their carried weight is visible; highlighted and
            // read-only (managed via the weapon slot, not editable from this list).
            foreach (SavedThing w in weapons)
            {
                if (w.thing == null) continue;
                Rect row = new Rect(scrollViewRect.x, scrollViewRect.y + drawn * rowHeight, scrollViewRect.width, rowHeight);
                Widgets.DrawHighlightSelected(row);
                DrawItemRowCommon(row, w, "fcInventoryWeaponSuffix".Translate());
                drawn++;
            }

            // Carried inventory: editable.
            for (int i = 0; i < items.Count; i++)
            {
                SavedThing item = items[i];
                if (item.thing == null) continue;
                int index = i;
                Rect row = new Rect(scrollViewRect.x, scrollViewRect.y + drawn * rowHeight, scrollViewRect.width, rowHeight);
                if (drawn % 2 == 0) Widgets.DrawHighlight(row);
                drawn++;

                // Remove button (far right)
                Rect removeRect = Rect.zero;
                if (opts.canEdit)
                {
                    removeRect = new Rect(row.xMax - removeButtonSize - 2f, row.y + (rowHeight - removeButtonSize) / 2f, removeButtonSize, removeButtonSize);
                    Text.Font = GameFont.Small;
                    Text.Anchor = TextAnchor.MiddleCenter;
                    if (UIUtil.ClampedButtonText(removeRect, "X"))
                    {
                        MilUnitFC target = opts.getEditTarget?.Invoke();
                        if (target != null) target.RemoveInventory(index);
                    }
                }

                // Count controls: [ - ] N [ + ]
                float rightEdge = opts.canEdit ? removeRect.x - 4f : row.xMax - 4f;
                float countAreaW = opts.canEdit ? 86f : 30f;
                Rect countArea = new Rect(rightEdge - countAreaW, row.y, countAreaW, rowHeight);
                Text.Font = GameFont.Tiny;
                if (opts.canEdit)
                {
                    Rect minus = new Rect(countArea.x, countArea.y + 4f, 20f, rowHeight - 8f);
                    if (UIUtil.ClampedButtonText(minus, "-"))
                    {
                        MilUnitFC target = opts.getEditTarget?.Invoke();
                        if (target != null) target.SetInventoryCount(index, Mathf.Max(0, item.count - 1));
                    }
                    Rect num = new Rect(countArea.x + 22f, countArea.y, 40f, rowHeight);
                    Text.Anchor = TextAnchor.MiddleCenter;
                    UIUtil.ClampedLabel(num, item.count.ToString());
                    Rect plus = new Rect(countArea.x + 64f, countArea.y + 4f, 20f, rowHeight - 8f);
                    if (UIUtil.ClampedButtonText(plus, "+"))
                    {
                        MilUnitFC target = opts.getEditTarget?.Invoke();
                        if (target != null) target.SetInventoryCount(index, item.count + 1);
                    }
                }
                else
                {
                    Text.Anchor = TextAnchor.MiddleRight;
                    UIUtil.ClampedLabel(countArea, "x" + item.count);
                }

                DrawItemRowCommon(new Rect(row.x, row.y, countArea.x - row.x, row.height), item, null);
            }

            ScrollUtil.EndScrollView();

            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;
        }

        /* Draws icon + info button + label + (mass / value) into the given rect. The mass/value
         * block hugs the right edge of the rect, so callers reserve space on the right (e.g. for
         * count/remove controls) by passing a narrowed rect. */
        private static void DrawItemRowCommon(Rect row, SavedThing item, string suffix)
        {
            Rect iconRect = new Rect(row.x + 2f, row.y + 2f, IconSize, IconSize);
            Widgets.ThingIcon(iconRect, item.thing, item.stuff);

            const float infoBtnSize = 24f;
            Rect infoRect = new Rect(iconRect.xMax + 2f, row.y + (row.height - infoBtnSize) / 2f, infoBtnSize, infoBtnSize);
            Widgets.InfoCardButton(infoRect.x, infoRect.y, item.thing, item.stuff);

            float massVal = item.thing.GetStatValueAbstract(StatDefOf.Mass, item.stuff) * Mathf.Max(1, item.count);
            Rect valueRect = new Rect(row.xMax - 100f, row.y, 96f, row.height);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleRight;
            UIUtil.ClampedLabel(valueRect, massVal.ToString("F1") + " kg  $" + item.MarketValue.ToString("F0"));

            Rect labelRect = new Rect(infoRect.xMax + 4f, row.y, valueRect.x - infoRect.xMax - 8f, row.height);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            string label = item.stuff != null
                ? (string)(item.thing.LabelCap + " (" + item.stuff.LabelCap + ")")
                : item.thing.LabelCap.ToString();
            if (item.quality.HasValue)
                label = item.quality.Value.GetLabel().CapitalizeFirst() + " " + label;
            if (!string.IsNullOrEmpty(suffix)) label = label + "  " + suffix;
            string shown = Text.ClampTextWithEllipsis(labelRect, label);
            UIUtil.ClampedLabel(labelRect, shown);
            if (shown != label) TooltipHandler.TipRegion(labelRect, label);
        }

        private static void OpenInventoryPicker(MilUnitFC displayUnit, Options opts)
        {
            // Def-driven whitelist (weapons/food/medicine/drugs/ammo), gated by research.
            List<ThingDef> defs = MilitaryInventoryUtil.AvailableItems();

            Find.WindowStack.Add(new FCWindow_ItemStuffPicker(
                defs,
                onConfirm: null,
                titleKey: "fcPickInventoryItem",
                showCount: true,
                initialCount: 1,
                onConfirmWithCount: (item, stuff, count, quality) =>
                {
                    MilUnitFC target = opts.getEditTarget?.Invoke();
                    if (target != null) target.AddInventory(item, stuff, count, quality);
                }
            ));
        }
    }
}
