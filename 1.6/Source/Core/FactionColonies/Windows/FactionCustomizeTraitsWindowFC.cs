using FactionColonies.util;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    public class FactionCustomizeTraitsWindowFC : Window
    {
        private const float fullwidth = 800f;
        private const float fullheight = 500f;
        private const float margin = 5f;
        private const float smallMargin = 3f;
        private const float traitRowHeight = 30f;
        public override Vector2 InitialSize => new Vector2(fullwidth, fullheight);

        private FactionFC faction;
        public string header;
        string alertText = "";

        List<FCPolicyDef> selectedTraits = new List<FCPolicyDef>();
        private FCPolicyDef hoveredTrait;

        private Vector2 traitListScroll;
        static List<Vector2> traitScrollBars = new List<Vector2>();

        private const int slotCount = 5;

        public FactionCustomizeTraitsWindowFC(FactionFC faction)
        {
            forcePause = false;
            draggable = true;
            doCloseX = true;
            preventCameraMotion = false;
            this.faction = faction;
            header = "FCTraitSelection".Translate();

            traitScrollBars.Clear();
            for (int i = 0; i < slotCount; i++)
            {
                traitScrollBars.Add(new Vector2());
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            GameFont fontBefore = Text.Font;
            TextAnchor anchorBefore = Text.Anchor;

            hoveredTrait = null;

            // Header
            Rect labelHeader = new Rect(0, 0, 200, 40);
            float headerHeight = labelHeader.yMax + (margin * 4);

            Text.Anchor = TextAnchor.MiddleLeft;
            Text.Font = GameFont.Medium;
            UIUtil.ClampedLabel(labelHeader, header);
            Widgets.DrawLineHorizontal(labelHeader.x, labelHeader.yMax + margin, fullwidth - (Margin * 2));

            Text.Font = GameFont.Small;

            // Confirm button
            Rect buttonConfirm = new Rect((inRect.xMax - 200) / 2f, inRect.yMax - 50, 200, 30);

            // Alert text
            Rect alertRect = new Rect(inRect.x + margin, buttonConfirm.y - 25, inRect.width - (margin * 2), 20);

            // Left panel
            float traitWidth = inRect.width * 0.25f;
            Rect leftPanel = new Rect(inRect.x, headerHeight, traitWidth, alertRect.y - headerHeight - margin);

            // Right panel
            Rect centerPanel = new Rect(leftPanel.xMax + margin, headerHeight, inRect.width - (traitWidth * 2) - (margin * 2), alertRect.y - headerHeight - margin);

            Rect rightPanel = new Rect(centerPanel.xMax + margin, headerHeight, traitWidth, leftPanel.height);

            DrawTraitSlots(leftPanel);
            DrawAvailableTraitsList(rightPanel);
            DrawTraitDescription(centerPanel);

            // Alert text
            int openSlots = CountOpenSlots();
            int remaining = openSlots - selectedTraits.Count;
            if (openSlots == 0)
            {
                alertText = "FCAllTraitSlotsFilled".Translate();
            }
            else if (remaining > 0)
            {
                alertText = "FCSelectTraitsPrompt".Translate(remaining);
            }
            else
            {
                alertText = "FCTraitSelectionReady".Translate();
            }

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            UIUtil.ClampedLabel(alertRect, alertText);

            Text.Font = GameFont.Small;
            if (UIUtil.ClampedButtonText(buttonConfirm, "FCConfirmChanges".Translate()))
            {
                if (selectedTraits.Count > 0)
                {
                    int traitIndex = 0;
                    for (int slot = 0; slot < slotCount && traitIndex < selectedTraits.Count; slot++)
                    {
                        bool isLocked = faction.factionLevel < (slot + 1);
                        bool isAssigned = FindFC.PolicyManager.factionTraits[slot].def != FCPolicyDefOf.empty;
                        if (!isLocked && !isAssigned)
                        {
                            FindFC.PolicyManager.factionTraits[slot] = new FCPolicy(selectedTraits[traitIndex]);
                            traitIndex++;
                        }
                    }
                    FindFC.PolicyManager.RebuildBehaviorCache();
                }

                Find.WindowStack.TryRemove(this);
            }

            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;
        }

        private void DrawTraitSlots(Rect inRect)
        {
            // Section A: Current slot status
            Rect slotHeader = new Rect(inRect.x, inRect.y, inRect.width, 22f);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.DrawHighlight(slotHeader);
            UIUtil.ClampedLabel(new Rect(slotHeader.x + margin, slotHeader.y, slotHeader.width - margin, slotHeader.height),
                          "FCTraitSlots".Translate());

            float y = slotHeader.yMax + smallMargin;

            int newTraitIndex = 0;
            for (int slot = 0; slot < slotCount; slot++)
            {
                Rect row = new Rect(inRect.x, y, inRect.width, traitRowHeight);
                if (slot % 2 == 0) Widgets.DrawHighlight(row);

                FCPolicy existing = FindFC.PolicyManager.factionTraits[slot];
                bool isLocked = faction.factionLevel < (slot + 1);
                bool isAssigned = existing.def != FCPolicyDefOf.empty;

                string label;
                Color labelColor;

                if (isAssigned)
                {
                    label = (slot + 1) + ". " + existing.def.LabelCap;
                    labelColor = Color.gray;
                    TooltipHandler.TipRegion(row, existing.def.PolicyText());
                }
                else if (isLocked)
                {
                    label = (slot + 1) + ". " + "FCTraitLockedUntilLevel".Translate(slot + 1);
                    labelColor = Color.gray;
                }
                else if (newTraitIndex < selectedTraits.Count)
                {
                    label = (slot + 1) + ". " + "FCTraitSelectedPreview".Translate(selectedTraits[newTraitIndex].LabelCap);
                    labelColor = Color.green;
                    TooltipHandler.TipRegion(row, selectedTraits[newTraitIndex].PolicyText());
                    newTraitIndex++;
                }
                else
                {
                    label = (slot + 1) + ". " + "FCSelectANewTrait".Translate();
                    labelColor = Color.white;
                }

                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;
                UIUtil.DrawColoredLabel(
                    new Rect(row.x + margin, row.y, row.width - (margin * 2), row.height),
                    label,
                    labelColor);

                y += traitRowHeight + smallMargin;
            }
        }

        private void DrawAvailableTraitsList(Rect inRect)
        {
            Rect listHeader = new Rect(inRect.x, inRect.y, inRect.width, 22f);
            Widgets.DrawHighlight(listHeader);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            UIUtil.ClampedLabel(new Rect(listHeader.x + margin, listHeader.y, listHeader.width - margin, listHeader.height),
                          "FCAvailableTraits".Translate());

            List<FCPolicyDef> available = GetAvailableTraits();

            float listY = listHeader.yMax + smallMargin;
            float listHeight = inRect.yMax - listY;
            float contentHeight = available.Count * (traitRowHeight + smallMargin);

            Rect viewRect = new Rect(inRect.x, listY, inRect.width, listHeight);

            Rect scrollRect = ScrollUtil.BeginScrollView(viewRect, ref traitListScroll, Mathf.Max(contentHeight, listHeight));

            for (int i = 0; i < available.Count; i++)
            {
                FCPolicyDef trait = available[i];
                bool isSelected = selectedTraits.Contains(trait);
                Rect row = new Rect(scrollRect.x, scrollRect.y + i * (traitRowHeight + smallMargin), scrollRect.width, traitRowHeight);

                string buttonLabel = isSelected ? ">> " + trait.LabelCap + " <<" : trait.LabelCap;

                if (Widgets.ButtonTextSubtle(row, buttonLabel, highlight: isSelected))
                {
                    HandleTraitSelection(trait);
                }

                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;

                if (Mouse.IsOver(row))
                {
                    hoveredTrait = trait;
                }
            }

            ScrollUtil.EndScrollView();
        }

        private void HandleTraitSelection(FCPolicyDef trait)
        {
            bool alreadySelected = selectedTraits.Contains(trait);
            int openSlots = CountOpenSlots();

            if (alreadySelected)
            {
                selectedTraits.Remove(trait);
            }
            else if (selectedTraits.Count < openSlots)
            {
                selectedTraits.Add(trait);
            }
            else
            {
                Messages.Message("FCNoOpenTraitSlots".Translate(), MessageTypeDefOf.RejectInput);
            }
        }

        private void DrawTraitDescription(Rect inRect)
        {
            Widgets.DrawMenuSection(inRect);

            FCPolicyDef displayTrait = hoveredTrait;
            if (displayTrait == null && selectedTraits.Count > 0)
            {
                displayTrait = selectedTraits[selectedTraits.Count - 1];
            }

            if (displayTrait == null || displayTrait == FCPolicyDefOf.empty)
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleCenter;
                UIUtil.ClampedLabel(inRect, "FCHoverTraitForDetails".Translate());
                return;
            }

            float innerMargin = margin * 2;
            Rect inner = new Rect(inRect.x + innerMargin, inRect.y + innerMargin,
                                  inRect.width - (innerMargin * 2), inRect.height - (innerMargin * 2));

            // Trait name
            Rect nameRect = new Rect(inner.x, inner.y, inner.width, 30f);
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            UIUtil.ClampedLabel(nameRect, displayTrait.LabelCap);

            // Description
            Rect descRect = new Rect(inner.x, nameRect.yMax + margin, inner.width, inner.yMax - nameRect.yMax - margin);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;

            string desc = returnTraitText(displayTrait);
            float textHeight = Text.CalcHeight(desc.StripTags(), descRect.width);

            if (textHeight > descRect.height)
            {
                textHeight = Text.CalcHeight(desc.StripTags(), descRect.width - 16f);
                Vector2 scrollBar = traitScrollBars.Count > 0 ? traitScrollBars[0] : new Vector2();
                Rect scrollContent = ScrollUtil.BeginScrollView(descRect, ref scrollBar, textHeight);
                Widgets.Label(scrollContent, desc);
                ScrollUtil.EndScrollView();
                if (traitScrollBars.Count > 0) traitScrollBars[0] = scrollBar;
            }
            else
            {
                Widgets.Label(descRect, desc);
            }
        }

        private int CountOpenSlots()
        {
            int count = 0;
            for (int slot = 0; slot < slotCount; slot++)
            {
                bool isLocked = faction.factionLevel < (slot + 1);
                bool isAssigned = FindFC.PolicyManager.factionTraits[slot].def != FCPolicyDefOf.empty;
                if (!isLocked && !isAssigned) count++;
            }
            return count;
        }

        private List<FCPolicyDef> GetAvailableTraits()
        {
            return FactionCache.GetPoliciesByCategory(FCPolicyCategory.Trait)
                .Where(d => !FindFC.PolicyManager.HasTrait(d) && MeetsTraitPrerequisites(d))
                .ToList();
        }

        private bool MeetsTraitPrerequisites(FCPolicyDef def)
        {
            if (def.requiredPolicies.NullOrEmpty()) return true;

            if (def.requirementMode == FCRequirementMode.Any)
            {
                foreach (FCPolicyDef req in def.requiredPolicies)
                {
                    if (FindFC.PolicyManager.HasTrait(req) || selectedTraits.Contains(req)
                        || FindFC.PolicyManager.HasPolicy(req) || FindFC.PolicyManager.HasEdict(req))
                        return true;
                }
                return false;
            }

            foreach (FCPolicyDef req in def.requiredPolicies)
            {
                if (!FindFC.PolicyManager.HasTrait(req) && !selectedTraits.Contains(req)
                    && !FindFC.PolicyManager.HasPolicy(req) && !FindFC.PolicyManager.HasEdict(req))
                    return false;
            }
            return true;
        }

        string returnTraitText(FCPolicyDef def)
        {
            return FactionCache.FCPolicyDescs?[def] ?? def.PolicyDesc();
        }
    }
}
