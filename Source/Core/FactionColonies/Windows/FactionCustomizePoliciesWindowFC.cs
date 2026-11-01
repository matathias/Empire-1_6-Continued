using FactionColonies.util;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    public class FactionCustomizePoliciesWindowFC : Window
    {
        private const float fullwidth = 900f;
        private const float fullheight = 600f;
        private const float margin = 5f;
        private const float smallMargin = 3f;
        private const float policyRowHeight = 30f;
        private const float iconSize = 30f;
        private const float cardGap = 6f;
        private const float cardPadding = 8f;
        private const float removeButtonSize = 24f;
        public override Vector2 InitialSize => new Vector2(fullwidth, fullheight);

        private FactionFC faction;
        public string header;
        string alertText = "";

        bool traitsChosen;

        List<FCPolicyDef> selectedPolicies = new List<FCPolicyDef>();
        private FCPolicyDef hoveredPolicy;

        private Vector2 availableListScroll;
        private Vector2[] cardScrollPositions;

        // Cached list of all core policies from DefDatabase
        private List<FCPolicyDef> allCorePolicies;

        public FactionCustomizePoliciesWindowFC(FactionFC faction)
        {
            forcePause = false;
            draggable = true;
            doCloseX = true;
            preventCameraMotion = false;
            this.faction = faction;
            header = "FCPolicySelection".Translate();

            allCorePolicies = FactionCache.GetPoliciesByCategory(FCPolicyCategory.Core);

            cardScrollPositions = new Vector2[FCSettings.maxPolicyCount];

            if (FindFC.PolicyManager.policies.Count != 0)
            {
                foreach (FCPolicy policy in FindFC.PolicyManager.policies)
                {
                    selectedPolicies.Add(policy.def);
                }
            }
            if (FindFC.PolicyManager.policies.Count == FCSettings.maxPolicyCount)
            {
                traitsChosen = true;
            }
            else
            {
                traitsChosen = false;
                FindFC.PolicyManager.RemoveAllPolicies(FindFC.PolicyManager.policies);
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            GameFont fontBefore = Text.Font;
            TextAnchor anchorBefore = Text.Anchor;

            hoveredPolicy = null;

            // Header
            Rect labelHeader = new Rect(0, 0, 200, 40);
            float headerHeight = labelHeader.yMax + (margin * 4);

            Text.Anchor = TextAnchor.MiddleLeft;
            Text.Font = GameFont.Medium;
            UIUtil.ClampedLabel(labelHeader, header);
            Widgets.DrawLineHorizontal(labelHeader.x, labelHeader.yMax + margin, fullwidth - (Margin * 2));

            Text.Font = GameFont.Small;

            // Clear Policies button (top-right, only when policies are locked-in)
            if (traitsChosen)
            {
                int clearCost = PolicyRepickCost.Compute(faction);
                bool canAfford = PaymentUtil.GetSilver() >= clearCost;
                Rect clearBtn = new Rect(inRect.xMax - 240, 5, 230, 30);

                Color savedColor = GUI.color;
                if (!canAfford) GUI.color = new Color(1f, 1f, 1f, 0.5f);

                Text.Anchor = TextAnchor.MiddleCenter;
                if (UIUtil.ClampedButtonText(clearBtn, "FCClearPolicies".Translate(clearCost)) && canAfford)
                {
                    Find.WindowStack.Add(new FCWindow_Confirm(
                        "FCClearPoliciesConfirm".Translate(clearCost),
                        () => DoClearPolicies(clearCost)));
                }

                GUI.color = savedColor;
                TooltipHandler.TipRegion(clearBtn, "FCClearPoliciesTooltip".Translate(FindFC.EmpireName));
            }

            // Confirm button
            Rect buttonConfirm = new Rect((inRect.xMax - 200) / 2f, inRect.yMax - 50, 200, 30);

            // Alert text
            Rect alertRect = new Rect(inRect.x + margin, buttonConfirm.y - 25, inRect.width - (margin * 2), 20);

            // Two-panel layout: cards (left) + picker (right)
            float panelTop = headerHeight;
            float panelBottom = alertRect.y - margin;
            float panelHeight = panelBottom - panelTop;
            float cardsWidth = inRect.width * 0.58f;
            float pickerWidth = inRect.width - cardsWidth - margin;

            Rect cardsPanel = new Rect(inRect.x, panelTop, cardsWidth, panelHeight);
            Rect pickerPanel = new Rect(cardsPanel.xMax + margin, panelTop, pickerWidth, panelHeight);

            // Draw picker first so hoveredPolicy is set before cards read it
            DrawAvailablePolicies(pickerPanel);
            DrawPolicyCards(cardsPanel);

            // Alert text
            if (traitsChosen)
            {
                alertText = "FCTraitsChosen".Translate();
            }
            else if (selectedPolicies.Count < FCSettings.maxPolicyCount)
            {
                alertText = "FCSelectTraits0".Translate(FCSettings.maxPolicyCount);
            }
            else
            {
                alertText = "FCSelectTraits2".Translate();
            }

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            UIUtil.ClampedLabel(alertRect, alertText);

            // Confirm button
            Text.Font = GameFont.Small;
            if (UIUtil.ClampedButtonText(buttonConfirm, "FCConfirmChanges".Translate()))
            {
                if (!traitsChosen)
                {
                    foreach (FCPolicyDef policy in selectedPolicies)
                    {
                        if (!FindFC.PolicyManager.policies.Any((FCPolicy p) => p.def == policy))
                        {
                            FindFC.PolicyManager.policies.Add(new FCPolicy(policy));
                        }
                    }
                    FindFC.PolicyManager.RebuildBehaviorCache();
                }

                Find.WindowStack.TryRemove(this);
            }

            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;
        }

        private void DrawPolicyCards(Rect inRect)
        {
            float cardHeight = (inRect.height - cardGap) / 2f;
            bool usedPreview = false;

            for (int i = 0; i < FCSettings.maxPolicyCount; i++)
            {
                Rect cardRect = new Rect(inRect.x, inRect.y + i * (cardHeight + cardGap), inRect.width, cardHeight);

                if (i < selectedPolicies.Count)
                {
                    DrawSingleCard(cardRect, selectedPolicies[i], i, false);
                }
                else if (!usedPreview && hoveredPolicy != null && !selectedPolicies.Contains(hoveredPolicy))
                {
                    DrawSingleCard(cardRect, hoveredPolicy, i, true);
                    usedPreview = true;
                }
                else
                {
                    DrawEmptyCard(cardRect, i);
                }
            }
        }

        private void DrawSingleCard(Rect cardRect, FCPolicyDef policy, int slotIndex, bool isPreview)
        {
            Widgets.DrawMenuSection(cardRect);

            if (isPreview)
                GUI.color = new Color(1f, 1f, 1f, 0.45f);

            Rect inner = cardRect.ContractedBy(cardPadding);
            float y = inner.y;

            // Header row: icon + name + remove button
            Texture2D icon = policy.IconLight;
            if (icon != null)
            {
                Rect iconRect = new Rect(inner.x, y + (iconSize - iconSize) / 2f, iconSize, iconSize);
                Widgets.DrawTextureFitted(iconRect, icon, 1f);
            }

            float nameLabelX = inner.x + (icon != null ? iconSize + margin : 0f);
            float nameLabelWidth = inner.width - (icon != null ? iconSize + margin : 0f);

            // Remove button (top-right, only if not preview and not locked in)
            if (!isPreview && !traitsChosen)
            {
                nameLabelWidth -= removeButtonSize + margin;
                Rect removeBtn = new Rect(inner.xMax - removeButtonSize, y + (iconSize - removeButtonSize) / 2f, removeButtonSize, removeButtonSize);

                // Restore color briefly for the button
                Color savedColor = GUI.color;
                GUI.color = Color.white;
                if (UIUtil.ClampedButtonText(removeBtn, "X"))
                {
                    selectedPolicies.RemoveAt(slotIndex);
                    ResetCardScrollPositions();
                    GUI.color = Color.white;
                    return;
                }
                GUI.color = savedColor;
            }

            Rect nameRect = new Rect(nameLabelX, y, nameLabelWidth, iconSize);
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            UIUtil.ClampedLabel(nameRect, policy.LabelCap);
            y += iconSize + smallMargin;

            // Conflicts warning
            if (!policy.incompatiblePolicies.NullOrEmpty())
            {
                string names = string.Join(", ", policy.incompatiblePolicies.Select(p => p.LabelCap.ToString()));
                string conflictText = "FCConflictsWith".Translate(names);
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.UpperLeft;
                UIUtil.DrawColoredLabel(new Rect(inner.x, y, inner.width, 18f), conflictText, isPreview ? new Color(1f, 1f, 0f, 0.45f) : Color.yellow);
                y += 20f;
            }

            // Policy effects description
            Rect descRect = new Rect(inner.x, y, inner.width, inner.yMax - y);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            string desc = returnPolicyDesc(policy);
            float textHeight = Text.CalcHeight(desc, descRect.width);

            if (textHeight > descRect.height)
            {
                textHeight = Text.CalcHeight(desc, descRect.width - 16f);
                Rect scrollContent = ScrollUtil.BeginScrollView(descRect, ref cardScrollPositions[slotIndex], textHeight);
                Widgets.Label(scrollContent, desc);
                ScrollUtil.EndScrollView();
            }
            else
            {
                Widgets.Label(descRect, desc);
            }

            GUI.color = Color.white;
        }

        private void DrawEmptyCard(Rect cardRect, int slotIndex)
        {
            Widgets.DrawMenuSection(cardRect);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            UIUtil.DrawColoredLabel(cardRect,
                (slotIndex + 1) + ". " + "FCSelectANewTrait".Translate(),
                Color.gray);
        }

        private void DrawAvailablePolicies(Rect inRect)
        {
            Rect listHeader = new Rect(inRect.x, inRect.y, inRect.width, 22f);
            Widgets.DrawHighlight(listHeader);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            UIUtil.ClampedLabel(new Rect(listHeader.x + margin, listHeader.y, listHeader.width - margin, listHeader.height),
                "FCAvailablePolicies".Translate());

            float listY = listHeader.yMax + smallMargin;
            float listHeight = inRect.yMax - listY;
            float contentHeight = allCorePolicies.Count * (policyRowHeight + smallMargin);

            Rect viewRect = new Rect(inRect.x, listY, inRect.width, listHeight);

            Rect scrollRect = ScrollUtil.BeginScrollView(viewRect, ref availableListScroll, Mathf.Max(contentHeight, listHeight));

            for (int i = 0; i < allCorePolicies.Count; i++)
            {
                FCPolicyDef policy = allCorePolicies[i];
                bool isSelected = selectedPolicies.Contains(policy);
                bool isBlocked = IsBlockedByIncompatible(policy);
                Rect row = new Rect(scrollRect.x, scrollRect.y + i * (policyRowHeight + smallMargin), scrollRect.width, policyRowHeight);

                // Determine label style
                string buttonLabel;
                if (isSelected)
                    buttonLabel = ">> " + policy.LabelCap + " <<";
                else if (isBlocked)
                    buttonLabel = policy.LabelCap + " (!)";
                else
                    buttonLabel = policy.LabelCap;

                if (isBlocked && !isSelected)
                    GUI.color = new Color(1f, 1f, 1f, 0.5f);

                if (Widgets.ButtonTextSubtle(row, buttonLabel, highlight: isSelected) && !traitsChosen)
                {
                    HandlePolicySelection(policy);
                }

                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;

                if (Mouse.IsOver(row))
                    hoveredPolicy = policy;
            }

            ScrollUtil.EndScrollView();
        }

        /// <summary>
        /// Returns true if the given policy is blocked because an incompatible policy is already selected.
        /// </summary>
        private bool IsBlockedByIncompatible(FCPolicyDef policy)
        {
            if (policy.incompatiblePolicies.NullOrEmpty()) return false;
            foreach (FCPolicyDef incompatible in policy.incompatiblePolicies)
            {
                if (selectedPolicies.Contains(incompatible))
                    return true;
            }
            return false;
        }

        private void HandlePolicySelection(FCPolicyDef policy)
        {
            bool selected = selectedPolicies.Contains(policy);
            if (selectedPolicies.Count < FCSettings.maxPolicyCount || selected)
            {
                if (selected)
                {
                    selectedPolicies.Remove(policy);
                    ResetCardScrollPositions();
                }
                else if (IsBlockedByIncompatible(policy))
                {
                    Messages.Message("FCConflictingTraits".Translate(), MessageTypeDefOf.RejectInput);
                }
                else
                {
                    selectedPolicies.Add(policy);
                    ResetCardScrollPositions();
                }
            }
            else
            {
                Messages.Message("FCUnselectTrait".Translate(), MessageTypeDefOf.RejectInput);
            }
        }

        private void ResetCardScrollPositions()
        {
            for (int i = 0; i < cardScrollPositions.Length; i++)
                cardScrollPositions[i] = Vector2.zero;
        }

        private void DoClearPolicies(int cost)
        {
            if (!PaymentUtil.TryPaySilver(cost, PaymentUtil.Reason_PolicyRepick))
            {
                Messages.Message("FCClearPoliciesInsufficientSilver".Translate(cost), MessageTypeDefOf.RejectInput);
                return;
            }
            FindFC.PolicyManager.RemoveAllPolicies(FindFC.PolicyManager.policies);
            FindFC.PolicyManager.RebuildBehaviorCache();
            selectedPolicies.Clear();
            traitsChosen = false;
            ResetCardScrollPositions();
        }

        string returnPolicyDesc(FCPolicyDef def)
        {
            return FactionCache.FCPolicyDescs?[def] ?? def.PolicyDesc();
        }
    }
}
