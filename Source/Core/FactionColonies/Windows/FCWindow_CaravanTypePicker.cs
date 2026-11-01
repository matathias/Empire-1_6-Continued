using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    public class FCWindow_CaravanTypePicker : Window
    {
        private FactionFC faction;
        private List<CaravanTypeEntry> entries;
        private Vector2 scrollPos;

        public override Vector2 InitialSize => new Vector2(450f, 520f);

        private const float RowHeight = 30f;
        private const int margin = 5;
        private const int bigRowHeight = 26;

        public FCWindow_CaravanTypePicker()
        {
            forcePause = false;
            draggable = true;
            doCloseX = true;
            preventCameraMotion = false;
            resizeable = true;
            doCloseButton = true;
        }

        public override void PreOpen()
        {
            base.PreOpen();

            faction = FindFC.FactionComp;
            if (faction is null)
            {
                LogUtil.Error("Null FactionFC when opening FCWindow_CaravanTypePicker");
                Close();
                return;
            }

            BuildEntries();
        }

        public override void PostClose()
        {
            base.PostClose();
            if (faction is null)
                return;

            // Persist selections back to faction
            faction.enabledCaravanTypes.Clear();
            foreach (CaravanTypeEntry entry in entries)
            {
                if (entry.enabled)
                    faction.enabledCaravanTypes.Add(entry.typeId);
            }

            // Rebuild caravanTraderKinds immediately
            faction.RebuildCaravanTraderKinds();
        }

        public override void DoWindowContents(Rect inRect)
        {
            GameFont fontBefore = Text.Font;
            TextAnchor anchorBefore = Text.Anchor;

            // Header
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            Rect header = new Rect(inRect.x, inRect.y, inRect.width, 35f);
            UIUtil.ClampedLabel(header, "FCCaravanTypes".Translate());
            Widgets.DrawLineHorizontal(header.x, header.yMax, header.width);

            // Sub-header
            Text.Font = GameFont.Tiny;
            Rect subHeader = new Rect(inRect.x, header.yMax, inRect.width, 20f);
            UIUtil.ClampedLabel(subHeader, "FCCaravanTypesDesc".Translate(FindFC.EmpireName));

            float bottomY = inRect.yMax - CloseButSize.y - margin;

            // Validation error
            int enabledCount = entries.Count(e => e.enabled);
            if (enabledCount == 0)
            {
                string errorText = "FCCaravanNoneError".Translate();
                float textHeight = Text.CalcHeight(errorText, inRect.width - (margin * 2));
                Rect errorBox = new Rect(inRect.x, bottomY - textHeight - (margin * 2), inRect.width,
                    textHeight + (margin * 2));
                Widgets.DrawHighlight(errorBox);
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(errorBox.x + margin, errorBox.y + margin, errorBox.width - margin * 2, textHeight),
                    errorText.Colorize(Color.red));
                bottomY -= (errorBox.height + margin);
            }

            // Enable All / Disable All buttons
            Rect enableButton = new Rect(inRect.x, bottomY - bigRowHeight, inRect.width / 2f, bigRowHeight);
            Rect disableButton = new Rect(enableButton.xMax, enableButton.y, enableButton.width, enableButton.height);
            if (UIUtil.ClampedButtonText(enableButton, "FCAnimalEnableAll".Translate()))
            {
                foreach (CaravanTypeEntry entry in entries)
                {
                    if (!entry.locked)
                        entry.enabled = true;
                }
            }
            if (UIUtil.ClampedButtonText(disableButton, "FCAnimalDisableAll".Translate()))
            {
                foreach (CaravanTypeEntry entry in entries)
                    entry.enabled = false;
            }
            bottomY -= (enableButton.height + margin);

            // Scrollable list
            float listTop = subHeader.yMax + margin;
            float listHeight = bottomY - listTop;
            Rect scrollOutRect = new Rect(inRect.x, listTop, inRect.width, listHeight);
            Widgets.DrawMenuSection(scrollOutRect);

            Rect innerRect = new Rect(scrollOutRect.x + 2, scrollOutRect.y + 2, scrollOutRect.width - 4,
                scrollOutRect.height - 4);
            float viewHeight = entries.Count * RowHeight;

            Rect scrollViewRect = ScrollUtil.BeginScrollView(innerRect, ref scrollPos, Mathf.Max(viewHeight, innerRect.height));

            Text.Font = GameFont.Small;
            for (int i = 0; i < entries.Count; i++)
            {
                CaravanTypeEntry entry = entries[i];
                Rect row = new Rect(scrollViewRect.x, scrollViewRect.y + (i * RowHeight), scrollViewRect.width,
                    RowHeight);

                if (i % 2 == 0)
                    Widgets.DrawHighlight(row);

                // Icon
                Rect iconRect = new Rect(row.x + margin, row.y, RowHeight, RowHeight);
                if (entry.icon is object)
                    GUI.DrawTexture(iconRect, entry.icon);

                // Checkbox
                float checkboxSize = 24f;
                Rect checkRect = new Rect(row.xMax - margin - checkboxSize,
                    row.y + (RowHeight - checkboxSize) / 2f, checkboxSize, checkboxSize);

                if (entry.locked)
                {
                    // Draw greyed-out checkbox
                    GUI.color = new Color(1f, 1f, 1f, 0.3f);
                    Widgets.CheckboxDraw(checkRect.x, checkRect.y, false, false, checkboxSize);
                    GUI.color = Color.white;
                }
                else
                {
                    bool prev = entry.enabled;
                    Widgets.Checkbox(checkRect.x, checkRect.y, ref entry.enabled, checkboxSize);
                }

                // Label
                Rect labelRect = new Rect(iconRect.xMax + margin, row.y, checkRect.x - iconRect.xMax - margin * 2,
                    RowHeight);
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;

                if (entry.locked)
                {
                    UIUtil.ClampedLabel(labelRect, entry.label.Colorize(Color.gray));
                }
                else
                {
                    UIUtil.ClampedLabel(labelRect, entry.label);
                }

                // Tooltip
                if (!string.IsNullOrEmpty(entry.tooltip))
                    TooltipHandler.TipRegion(row, entry.tooltip);
            }

            ScrollUtil.EndScrollView();

            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;
        }

        private void BuildEntries()
        {
            entries = new List<CaravanTypeEntry>();

            // Resource-based entries
            TechLevel tech = faction.techLevel;
            foreach (ResourceTypeDef rtd in DefDatabase<ResourceTypeDef>.AllDefs.OrderBy(r => r.label))
            {
                if (rtd.isPoolResource || !rtd.CanTithe)
                    continue;

                bool techAllowed = rtd.ResourceTypeAllowedByTech(tech);
                entries.Add(new CaravanTypeEntry
                {
                    typeId = rtd.defName,
                    label = rtd.LabelCap,
                    icon = ContentFinder<Texture2D>.Get(rtd.iconPath, false),
                    enabled = faction.enabledCaravanTypes.Contains(rtd.defName) && techAllowed,
                    locked = !techAllowed,
                    tooltip = techAllowed
                        ? "FCCaravanResourceTooltip".Translate(rtd.LabelCap, FindFC.EmpireTitle)
                        : "FCCaravanResourceTechLocked".Translate(rtd.LabelCap)
                });
            }

            // Exotic
            bool exoticUnlocked = faction.factionLevel >= 4 || FindFC.PolicyManager.HasTrait(FCPolicyDefOf.mercantile);
            entries.Add(new CaravanTypeEntry
            {
                typeId = "Exotic",
                label = "FCCaravanExotic".Translate(),
                icon = null,
                enabled = faction.enabledCaravanTypes.Contains("Exotic") && exoticUnlocked,
                locked = !exoticUnlocked,
                tooltip = exoticUnlocked
                    ? "FCCaravanExoticTooltip".Translate()
                    : "FCCaravanExoticLocked".Translate()
            });

            // Slaver
            bool slaverBlocked = FindFC.PolicyManager.HasPolicy(FCPolicyDefOf.pacifist)
                || FindFC.PolicyManager.HasTrait(FCPolicyDefOf.pacifist)
                || FindFC.PolicyManager.HasPolicy(FCPolicyDefOf.egalitarian)
                || FindFC.PolicyManager.HasTrait(FCPolicyDefOf.egalitarian);
            entries.Add(new CaravanTypeEntry
            {
                typeId = "Slaver",
                label = "FCCaravanSlaver".Translate(),
                icon = null,
                enabled = faction.enabledCaravanTypes.Contains("Slaver") && !slaverBlocked,
                locked = slaverBlocked,
                tooltip = slaverBlocked
                    ? "FCCaravanSlaverBlocked".Translate()
                    : "FCCaravanSlaverTooltip".Translate()
            });
        }

        private class CaravanTypeEntry
        {
            public string typeId;
            public string label;
            public Texture2D icon;
            public bool enabled;
            public bool locked;
            public string tooltip;
        }
    }
}
