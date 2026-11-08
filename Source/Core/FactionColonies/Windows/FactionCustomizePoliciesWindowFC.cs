using UnityEngine;

namespace FactionColonies;

public class FactionCustomizePoliciesWindowFC : FCWindow_PolicySelectionBase
{
    private const float fullwidth = 900f;
    private const float fullheight = 600f;
    private const float iconSize = 30f;
    private const float cardGap = 6f;
    private const float cardPadding = 8f;
    private const float removeButtonSize = 24f;
    public override Vector2 InitialSize => new(fullwidth, fullheight);

    bool traitsChosen;

    List<FCPolicyDef> selectedPolicies = [];

    private Vector2 availableListScroll;
    private Vector2[] cardScrollPositions;

    // Cached list of all core policies from DefDatabase
    private List<FCPolicyDef> allCorePolicies;

    public FactionCustomizePoliciesWindowFC(FactionFC faction)
        : base(faction, "FCPolicySelection".Translate())
    {
        allCorePolicies = FactionCache.GetPoliciesByCategory(FCPolicyCategory.Core);

        cardScrollPositions = new Vector2[FCSettings.maxPolicyCount];

        if (FindFC.PolicyManager.policies.Count != 0)
        {
            foreach (FCPolicy policy in FindFC.PolicyManager.policies)
            {
                selectedPolicies.Add(policy.def);
            }
        }
        // A partial set stays editable: selectedPolicies is a draft, applied only on Confirm.
        traitsChosen = FindFC.PolicyManager.policies.Count == FCSettings.maxPolicyCount;
    }

    protected override void DrawHeaderExtras(Rect inRect)
    {
        // Clear Policies button (top-right, only when policies are locked-in)
        if (traitsChosen)
        {
            int clearCost = PolicyRepickCost.Compute(faction);
            bool canAfford = PaymentUtil.GetSilver() >= clearCost;
            Rect clearBtn = new(inRect.xMax - 240, 5, 230, 30);

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
    }

    protected override void DrawPanels(Rect inRect, float panelTop, float panelBottom)
    {
        // Two-panel layout: cards (left) + picker (right)
        float panelHeight = panelBottom - panelTop;
        float cardsWidth = inRect.width * 0.58f;
        float pickerWidth = inRect.width - cardsWidth - margin;

        Rect cardsPanel = new(inRect.x, panelTop, cardsWidth, panelHeight);
        Rect pickerPanel = new(cardsPanel.xMax + margin, panelTop, pickerWidth, panelHeight);

        // Draw picker first so the hovered policy is set before cards read it
        DrawAvailableList(pickerPanel, "FCAvailablePolicies".Translate(), allCorePolicies, ref availableListScroll,
            selectedPolicies, IsBlockedByIncompatible, !traitsChosen, HandlePolicySelection);
        DrawPolicyCards(cardsPanel);
    }

    protected override string ComputeAlertText()
    {
        if (traitsChosen)
            return "FCTraitsChosen".Translate();
        if (selectedPolicies.Count < FCSettings.maxPolicyCount)
            return "FCSelectTraits0".Translate(FCSettings.maxPolicyCount);
        return "FCSelectTraits2".Translate();
    }

    protected override void ApplySelection()
    {
        if (traitsChosen) return;

        PolicyManager policyManager = FindFC.PolicyManager;
        // Apply the draft as a diff: deselected policies are removed (firing OnRemoved), kept
        // ones are left alone, and new ones are enacted.
        List<FCPolicy> dropped = policyManager.policies.Where(p => !selectedPolicies.Contains(p.def)).ToList();
        policyManager.policies.RemoveAll(dropped.Contains);
        policyManager.RemoveAllPolicies(dropped);
        foreach (FCPolicyDef policy in selectedPolicies)
        {
            if (!policyManager.policies.Any((FCPolicy p) => p.def == policy))
            {
                policyManager.policies.Add(new FCPolicy(policy));
            }
        }
        policyManager.RebuildBehaviorCache();
    }

    private void DrawPolicyCards(Rect inRect)
    {
        float cardHeight = (inRect.height - cardGap) / 2f;
        bool usedPreview = false;

        for (int i = 0; i < FCSettings.maxPolicyCount; i++)
        {
            Rect cardRect = new(inRect.x, inRect.y + i * (cardHeight + cardGap), inRect.width, cardHeight);

            if (i < selectedPolicies.Count)
            {
                DrawSingleCard(cardRect, selectedPolicies[i], i, false);
            }
            else if (!usedPreview && hovered is not null && !selectedPolicies.Contains(hovered))
            {
                DrawSingleCard(cardRect, hovered, i, true);
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
            Rect iconRect = new(inner.x, y + (iconSize - iconSize) / 2f, iconSize, iconSize);
            Widgets.DrawTextureFitted(iconRect, icon, 1f);
        }

        float nameLabelX = inner.x + (icon != null ? iconSize + margin : 0f);
        float nameLabelWidth = inner.width - (icon != null ? iconSize + margin : 0f);

        // Remove button (top-right, only if not preview and not locked in)
        if (!isPreview && !traitsChosen)
        {
            nameLabelWidth -= removeButtonSize + margin;
            Rect removeBtn = new(inner.xMax - removeButtonSize, y + (iconSize - removeButtonSize) / 2f, removeButtonSize, removeButtonSize);

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

        Rect nameRect = new(nameLabelX, y, nameLabelWidth, iconSize);
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
        Rect descRect = new(inner.x, y, inner.width, inner.yMax - y);
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.UpperLeft;
        string desc = PolicyDescription(policy);
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
}
