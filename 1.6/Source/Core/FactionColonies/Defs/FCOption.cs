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
    public enum FCRequirementMode : byte
    {
        All = 0,
        Any = 1
    }

    public class FCOptionDef : Def
    {
        public FCOptionDef()
        {
        }

        public float baseChanceOfSuccess;
        public int silverCost = 0;
        public FCEventDef parentEvent;
        public FCEventDef successEvent = null;
        public FCEventDef failEvent = null;
        public List<FCPolicyDef> requiredPolicies = new List<FCPolicyDef>();
        public FCRequirementMode requirementMode = FCRequirementMode.All;

        /* Ideology meme gate, parallel to requiredPolicies. Stored as MemeDef defName strings
         * (not List<MemeDef>) because MemeDefs only exist when the Ideology DLC is loaded; strings
         * have no cross-reference to resolve, so meme-gated option defs load error-free even
         * without Ideology. When Ideology is off, FCOptionWindow hides any option with a meme gate.
         * Resolved to MemeDef at runtime via MemeUtilFC only when Ideology is active. */
        public List<string> requiredMemes = new List<string>();
        public FCRequirementMode memeRequirementMode = FCRequirementMode.All;

        public int EffectiveSilverCost
        {
            get { return Math.Max(0, (int)Math.Round(silverCost * FCSettings.eventSilverCostMultiplier, MidpointRounding.AwayFromZero)); }
        }

        /* Event-aware cost lookup. Returns the snapshotted value on the FCEvent when present
         * (set by FCOptionWindow at first construction so the displayed price matches the
         * payment). Falls back to a fresh compute for callers without a snapshot — and to the
         * parameterless EffectiveSilverCost when there's no event context at all. */
        public int GetEffectiveSilverCost(FCEvent evt)
        {
            if (evt is null) return EffectiveSilverCost;
            if (evt.optionCostSnapshots is object
                && evt.optionCostSnapshots.TryGetValue(this.defName, out int snapped))
                return snapped;
            return FCOptionCostUtil.ComputeScaledCost(this, evt);
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string err in base.ConfigErrors())
                yield return err;

            /* Validate meme defNames only when Ideology is active. Without the DLC the MemeDefs
             * legitimately don't exist, so an unresolved name is expected, not an error. */
            if (ModsConfig.IdeologyActive && requiredMemes != null)
            {
                foreach (string memeName in requiredMemes)
                {
                    if (memeName.NullOrEmpty())
                        yield return $"{defName}: requiredMemes contains an empty entry";
                    else if (DefDatabase<MemeDef>.GetNamedSilentFail(memeName) is null)
                        yield return $"{defName}: requiredMemes contains unknown meme '{memeName}'";
                }
            }
        }
    }

    public class FCOptionWindow : Window
    {
        // Layout constants
        private const float WindowWidth = 520f;
        private const float Padding = 10f;
        private const float AccentBarHeight = 4f;
        private const float OptionSpacing = 8f;
        private const float OptionInnerPadding = 8f;
        private const float MinOptionHeight = 64f;
        private const float MetadataRowHeight = 20f;
        private const float StripeWidth = 3f;
        private const float SilverIconSize = 16f;
        private const float SettlementButtonHeight = 24f;
        private const float SettlementButtonSpacing = 4f;
        private const float MaxWindowHeight = 700f;

        public List<FCOptionDef> options = new List<FCOptionDef>();
        public string header;
        public string desc;
        public FCEvent parentEvent;

        // Set when the constructor finds no visible options; PostOpen closes the window at once.
        private bool closeImmediately;

        private Color categoryColor;
        private List<WorldSettlementFC> affectedSettlements;
        private float cachedWindowHeight;
        private float cachedTitleHeight;
        private float cachedDescHeight;
        private float[] cachedOptionLabelHeights;
        private string[] cachedEffectPreviews;
        private float[] cachedEffectPreviewHeights;
        private const float EffectPreviewSpacing = 4f;
        private Vector2 scrollPosition;
        private float cachedHeaderHeight;
        private float cachedTotalOptionsHeight;
        private float openedAtRealTime;
        private bool OptionsEnabled => Time.realtimeSinceStartup - openedAtRealTime >= FCSettings.eventOptionDelaySeconds;

        public override Vector2 InitialSize
        {
            get { return new Vector2(WindowWidth, cachedWindowHeight > 0f ? cachedWindowHeight : 400f); }
        }

        public FCOptionWindow(FCEventDef evt, FCEvent parentEvent)
        {
            this.forcePause = true;
            this.draggable = true;
            this.doCloseX = false;
            this.preventCameraMotion = false;
            this.closeOnAccept = false;
            this.closeOnCancel = false;
            this.closeOnClickedOutside = false;
            this.preventSave = true;

            this.header = evt.label;

            /* Hide meme-gated options entirely when the Ideology DLC is off (the only behavioral
             * difference from policy gating, which greys unmet options instead). Build a filtered
             * copy — never mutate the def's list. */
            if (!ModsConfig.IdeologyActive)
            {
                this.options = evt.options
                    .Where(o => o.requiredMemes == null || o.requiredMemes.Count == 0)
                    .ToList();
            }
            else
            {
                this.options = evt.options;
            }

            /* An event with options must always present at least one pickable (free, ungated)
             * choice — enforced by FCEventDef.ConfigErrors. If filtering still leaves nothing to
             * show, the def is misconfigured (e.g. every option meme-gated while Ideology is off).
             * Don't soldier on with an empty, undismissable window: log and close immediately. */
            if (this.options.Count == 0)
            {
                LogUtil.Error($"FCOptionWindow for event '{evt.defName}' has no visible options"
                    + (!ModsConfig.IdeologyActive ? " (all options are meme-gated and Ideology is disabled)" : "")
                    + "; closing. Every event with options must have at least one free, non-gated option.");
                closeImmediately = true;
            }
            this.desc = (evt.optionDescription.NullOrEmpty() ? evt.desc : evt.optionDescription).Format();
            this.parentEvent = parentEvent;

            // Category color
            if (parentEvent != null)
            {
                this.categoryColor = AccentUtil.GetEventCategoryColor(parentEvent);
            }
            else
            {
                FCEventCategoryDef cat = evt.category ?? FCEventCategoryDefOf.EC_Other;
                this.categoryColor = cat.color;
            }

            // Affected settlements
            this.affectedSettlements = new List<WorldSettlementFC>();
            if (parentEvent != null && parentEvent.settlementTraitLocations != null)
            {
                foreach (WorldSettlementFC s in parentEvent.settlementTraitLocations)
                {
                    if (s != null)
                    {
                        affectedSettlements.Add(s);
                    }
                }
            }

            /* Snapshot per-option scaled costs onto the parent FCEvent the first time the window
             * opens. Locks the price shown == price paid, survives save/reload. Subsequent reopens
             * reuse the snapshot. Must happen before MeasureLayout — cost width feeds layout. */
            if (parentEvent != null && parentEvent.optionCostSnapshots is null && this.options != null)
            {
                parentEvent.optionCostSnapshots = new Dictionary<string, int>();
                foreach (FCOptionDef opt in this.options)
                {
                    if (opt is null) continue;
                    parentEvent.optionCostSnapshots[opt.defName] = FCOptionCostUtil.ComputeScaledCost(opt, parentEvent);
                }
            }

            // Measure layout
            MeasureLayout();
        }

        private void MeasureLayout()
        {
            float contentWidth = WindowWidth - (Margin * 2);
            float textWidth = contentWidth - (Padding * 2);

            Text.Font = GameFont.Medium;
            cachedTitleHeight = Text.CalcHeight(header, textWidth);

            Text.Font = GameFont.Small;
            cachedDescHeight = Text.CalcHeight(desc, textWidth);

            cachedOptionLabelHeights = new float[options.Count];
            float labelWidth = contentWidth - StripeWidth - (OptionInnerPadding * 2);
            Text.Font = GameFont.Small;
            FCEventHandlerExtension handler = parentEvent?.def?.GetModExtension<FCEventHandlerExtension>();
            for (int i = 0; i < options.Count; i++)
            {
                string measureLabel = options[i].label;
                if (handler != null)
                {
                    string dynLabel = handler.GetDynamicOptionLabel(options[i], parentEvent);
                    if (dynLabel != null) measureLabel = dynLabel;
                }
                cachedOptionLabelHeights[i] = Text.CalcHeight(measureLabel.Format(), labelWidth);
            }

            cachedEffectPreviews = new string[options.Count];
            cachedEffectPreviewHeights = new float[options.Count];
            Text.Font = GameFont.Tiny;
            for (int i = 0; i < options.Count; i++)
            {
                cachedEffectPreviews[i] = GetEffectPreview(options[i], parentEvent);
                if (cachedEffectPreviews[i] != null)
                {
                    cachedEffectPreviewHeights[i] = Text.CalcHeight(cachedEffectPreviews[i], labelWidth);
                }
            }

            // Header height (everything above the options)
            float headerH = AccentBarHeight + Padding;  // accent bar + top padding
            headerH += cachedTitleHeight;                // title
            headerH += Padding;                          // gap
            headerH += cachedDescHeight;                  // description

            if (affectedSettlements.Count > 0)
            {
                headerH += 8f;                           // gap
                headerH += 16f;                          // "Affecting:" label
                headerH += 2f;                           // gap
                headerH += SettlementButtonHeight;        // settlement buttons row
            }

            headerH += Padding;                          // gap before separator
            headerH += 1f;                               // separator
            headerH += Padding;                          // gap after separator
            cachedHeaderHeight = headerH;

            // Total options height (all option cards + spacing + bottom padding)
            float optH = 0f;
            for (int i = 0; i < options.Count; i++)
            {
                if (i > 0) optH += OptionSpacing;
                float cardH = OptionInnerPadding + cachedOptionLabelHeights[i] + 6f + MetadataRowHeight + OptionInnerPadding;
                if (cachedEffectPreviews[i] != null)
                {
                    cardH += EffectPreviewSpacing + cachedEffectPreviewHeights[i];
                }
                optH += Math.Max(cardH, MinOptionHeight);
            }
            optH += Padding;                             // bottom margin
            cachedTotalOptionsHeight = optH;

            cachedWindowHeight = Math.Min(cachedHeaderHeight + cachedTotalOptionsHeight + (Margin * 2), MaxWindowHeight);
        }

        public override void PreOpen()
        {
            base.PreOpen();
            openedAtRealTime = Time.realtimeSinceStartup;
            windowRect = new Rect(
                (UI.screenWidth - WindowWidth) / 2f,
                (UI.screenHeight - cachedWindowHeight) / 2f,
                WindowWidth,
                cachedWindowHeight
            );
        }

        public override void PostOpen()
        {
            base.PostOpen();
            // Constructor flagged an empty option list — close before the first frame is drawn.
            if (closeImmediately) Close(false);
        }

        public override void DoWindowContents(Rect inRect)
        {
            GameFont fontBefore = Text.Font;
            TextAnchor anchorBefore = Text.Anchor;
            Color colorBefore = GUI.color;

            float contentWidth = inRect.width;
            float textWidth = contentWidth - (Padding * 2);
            float curY = inRect.y;

            // === Accent bar ===
            Widgets.DrawBoxSolid(new Rect(inRect.x, curY, contentWidth, AccentBarHeight), categoryColor);
            curY += AccentBarHeight + Padding;

            // === Title ===
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.UpperLeft;
            Widgets.Label(new Rect(inRect.x + Padding, curY, textWidth, cachedTitleHeight), header);
            curY += cachedTitleHeight + Padding;

            // === Description ===
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            UIUtil.DrawColoredLabel(new Rect(inRect.x + Padding, curY, textWidth, cachedDescHeight), desc, new Color(0.85f, 0.85f, 0.85f), clamp: false);
            curY += cachedDescHeight;

            // === Affected settlements (clickable buttons) ===
            if (affectedSettlements.Count > 0)
            {
                curY += 8f;

                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleLeft;
                UIUtil.DrawColoredLabel(new Rect(inRect.x + Padding, curY, 60f, 16f), "FCEventAffecting".Translate(), new Color(0.6f, 0.6f, 0.6f));
                curY += 16f + 2f;

                Text.Font = GameFont.Tiny;
                if (affectedSettlements.Count > 3)
                {
                    // Collapse to a single summary chip with a hover tooltip listing every settlement.
                    int total = FindFC.FactionComp?.settlements?.Count ?? 0;
                    bool trulyAll = total > 0 && affectedSettlements.Count >= total;
                    string label = trulyAll
                        ? "FCEventAllSettlements".Translate().ToString()
                        : "FCEventNumSettlements".Translate(affectedSettlements.Count).ToString();

                    float btnWidth = Text.CalcSize(label).x + 16f;
                    Rect btnRect = new Rect(inRect.x + Padding, curY, btnWidth, SettlementButtonHeight);
                    UIUtil.ButtonFlat(btnRect, label, categoryColor);
                    TooltipHandler.TipRegion(btnRect, string.Join("\n", affectedSettlements.Select(s => s.Name)));
                }
                else
                {
                    float btnX = inRect.x + Padding;
                    for (int i = 0; i < affectedSettlements.Count; i++)
                    {
                        WorldSettlementFC settlement = affectedSettlements[i];
                        float btnWidth = Text.CalcSize(settlement.Name).x + 16f;
                        Rect btnRect = new Rect(btnX, curY, btnWidth, SettlementButtonHeight);

                        if (UIUtil.ButtonFlat(btnRect, settlement.Name, categoryColor))
                        {
                            Find.WindowStack.Add(new SettlementWindowFc(settlement));
                        }

                        btnX += btnWidth + SettlementButtonSpacing;
                    }
                }
                curY += SettlementButtonHeight;
            }

            // === Separator line ===
            curY += Padding;
            Color dimCategoryColor = new Color(categoryColor.r, categoryColor.g, categoryColor.b, 0.3f);
            Widgets.DrawBoxSolid(new Rect(inRect.x + Padding, curY, textWidth, 1f), dimCategoryColor);
            curY += 1f + Padding;

            // === Option cards (scrollable) ===
            int currentSilver = PaymentUtil.GetSilver();
            FCEventHandlerExtension optHandler = parentEvent?.def?.GetModExtension<FCEventHandlerExtension>();

            float scrollOuterHeight = inRect.yMax - curY;
            Rect scrollOuterRect = new Rect(inRect.x, curY, contentWidth, scrollOuterHeight);

            Rect scrollInnerRect = ScrollUtil.BeginScrollView(scrollOuterRect, ref scrollPosition, cachedTotalOptionsHeight);

            float optY = 0f;

            for (int i = 0; i < options.Count; i++)
            {
                if (i > 0) optY += OptionSpacing;

                FCOptionDef opt = options[i];
                int effectiveCost = opt.GetEffectiveSilverCost(parentEvent);
                bool affordable = currentSilver >= effectiveCost;
                bool isFree = effectiveCost <= 0;
                bool meetsPolicy = MeetsPolicyRequirements(opt, out string policyFail);
                bool meetsMeme = MeetsMemeRequirements(opt, out string memeFail);
                bool meetsRequirements = meetsPolicy && meetsMeme;
                string requirementFailReason = policyFail ?? memeFail;
                string handlerUnavailableReason = null;
                bool handlerAvailable = optHandler == null ||
                    optHandler.IsOptionAvailable(opt, parentEvent, out handlerUnavailableReason);
                bool available = affordable && meetsRequirements && handlerAvailable && OptionsEnabled;
                if (!handlerAvailable && requirementFailReason == null)
                    requirementFailReason = handlerUnavailableReason;

                // Card height
                float cardH = OptionInnerPadding + cachedOptionLabelHeights[i] + 6f + MetadataRowHeight + OptionInnerPadding;
                if (cachedEffectPreviews[i] != null)
                {
                    cardH += EffectPreviewSpacing + cachedEffectPreviewHeights[i];
                }
                cardH = Math.Max(cardH, MinOptionHeight);

                Rect cardRect = new Rect(0f, optY, scrollInnerRect.width, cardH);

                // Card background
                float bgVal = available ? 0.18f : 0.12f;
                Widgets.DrawBoxSolid(cardRect, new Color(bgVal, bgVal, bgVal));

                // Left accent stripe
                Color stripeColor = available
                    ? categoryColor
                    : new Color(categoryColor.r * 0.4f, categoryColor.g * 0.4f, categoryColor.b * 0.4f);
                Widgets.DrawBoxSolid(new Rect(cardRect.x, cardRect.y, StripeWidth, cardRect.height), stripeColor);

                // Inner content
                float innerX = cardRect.x + StripeWidth + OptionInnerPadding;
                float innerW = cardRect.width - StripeWidth - (OptionInnerPadding * 2);
                float innerY = cardRect.y + OptionInnerPadding;

                // Option label text
                string displayLabel = opt.label;
                if (optHandler != null)
                {
                    string dynLabel = optHandler.GetDynamicOptionLabel(opt, parentEvent);
                    if (dynLabel != null) displayLabel = dynLabel;
                }
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                UIUtil.DrawColoredLabel(new Rect(innerX, innerY, innerW, cachedOptionLabelHeights[i]), displayLabel.Format(), available ? Color.white : new Color(0.5f, 0.5f, 0.5f), clamp: false);
                innerY += cachedOptionLabelHeights[i] + 6f;

                // Metadata row: success hint (left) + cost (right)
                Rect metaRect = new Rect(innerX, innerY, innerW, MetadataRowHeight);

                // Success hint
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleLeft;
                string successLabel;
                Color successColor;
                float displayChance = opt.baseChanceOfSuccess;
                if (optHandler != null)
                {
                    float dynChance = optHandler.GetDynamicOptionSuccessChance(opt, parentEvent);
                    if (dynChance >= 0f) displayChance = dynChance;
                }
                GetSuccessHint(displayChance, out successLabel, out successColor);
                if (!available) successColor = new Color(successColor.r * 0.5f, successColor.g * 0.5f, successColor.b * 0.5f);
                UIUtil.DrawColoredLabel(new Rect(metaRect.x, metaRect.y, metaRect.width * 0.6f, metaRect.height), successLabel, successColor);

                // Requirement tag (policy and/or meme; always visible)
                bool hasPolicyReq = opt.requiredPolicies != null && opt.requiredPolicies.Count > 0;
                bool hasMemeReq = opt.requiredMemes != null && opt.requiredMemes.Count > 0;
                if (hasPolicyReq || hasMemeReq)
                {
                    List<string> tagParts = new List<string>();
                    if (hasPolicyReq)
                    {
                        string sep = opt.requirementMode == FCRequirementMode.Any ? " / " : ", ";
                        tagParts.Add(string.Join(sep, opt.requiredPolicies.Select(p => p.LabelCap.ToString())));
                    }
                    if (hasMemeReq)
                    {
                        string sep = opt.memeRequirementMode == FCRequirementMode.Any ? " / " : ", ";
                        tagParts.Add(string.Join(sep, opt.requiredMemes.Select(m => MemeUtilFC.EmpireMemeLabel(m))));
                    }
                    string policyTag = string.Join(", ", tagParts);

                    // Measure cost area so the tag gets all remaining space
                    float costAreaWidth;
                    if (isFree)
                    {
                        Text.Font = GameFont.Small;
                        costAreaWidth = Text.CalcSize("FCEventOptionFree".Translate()).x;
                    }
                    else
                    {
                        Text.Font = GameFont.Small;
                        costAreaWidth = Text.CalcSize(effectiveCost.ToString()).x + SilverIconSize + 2f;
                    }

                    Text.Font = GameFont.Tiny;
                    float successWidth = Text.CalcSize(successLabel).x;
                    float tagX = metaRect.x + successWidth + 6f;
                    float tagW = metaRect.width - successWidth - 6f - costAreaWidth - 8f;
                    Rect tagRect = new Rect(tagX, metaRect.y, tagW, metaRect.height);

                    string fullTagText = "[" + policyTag + "]";
                    string clampedTag = Text.ClampTextWithEllipsis(tagRect, fullTagText);

                    UIUtil.DrawColoredLabel(tagRect, clampedTag, available ? new Color(0.6f, 0.75f, 0.9f) : new Color(0.4f, 0.4f, 0.4f));

                    TooltipHandler.TipRegion(tagRect, fullTagText);
                }

                // Silver cost (right-aligned)
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleRight;
                if (isFree)
                {
                    UIUtil.DrawColoredLabel(metaRect, "FCEventOptionFree".Translate(), available ? AccentUtil.Income : new Color(0.3f, 0.5f, 0.3f));
                }
                else
                {
                    // Draw silver icon + cost text
                    GUI.color = available ? Color.white : AccentUtil.Expense;
                    string costStr = effectiveCost.ToString();
                    float costTextW = Text.CalcSize(costStr).x;
                    Rect costTextRect = new Rect(metaRect.xMax - costTextW, metaRect.y, costTextW, metaRect.height);
                    UIUtil.ClampedLabel(costTextRect, costStr);

                    Rect iconRect = new Rect(
                        costTextRect.x - SilverIconSize - 2f,
                        metaRect.y + (metaRect.height - SilverIconSize) / 2f,
                        SilverIconSize, SilverIconSize
                    );
                    GUI.color = available ? Color.white : new Color(0.5f, 0.5f, 0.5f);
                    GUI.DrawTexture(iconRect, ThingDefOf.Silver.uiIcon);
                    GUI.color = colorBefore;

                    /* Show a per-contribution breakdown on hover when the cost was actually scaled
                     * (multi-settlement multiplier or extension axes). BuildCostBreakdown returns
                     * null for the unscaled base-only case, so this is a no-op then. */
                    string costBreakdown = FCOptionCostUtil.BuildCostBreakdown(opt, parentEvent);
                    if (costBreakdown != null)
                    {
                        Rect costAreaRect = new Rect(iconRect.x, metaRect.y, costTextRect.xMax - iconRect.x, metaRect.height);
                        TooltipHandler.TipRegion(costAreaRect, costBreakdown);
                    }

                    if (!affordable)
                    {
                        TooltipHandler.TipRegion(cardRect, "FCNotEnoughSilverOption".Translate());
                    }
                }

                if (!meetsRequirements)
                {
                    TooltipHandler.TipRegion(cardRect, requirementFailReason);
                }

                // Effect preview (guaranteed options only)
                if (cachedEffectPreviews[i] != null)
                {
                    innerY += MetadataRowHeight + EffectPreviewSpacing;
                    Text.Font = GameFont.Tiny;
                    Text.Anchor = TextAnchor.UpperLeft;
                    UIUtil.DrawColoredLabel(new Rect(innerX, innerY, innerW, cachedEffectPreviewHeights[i]), cachedEffectPreviews[i], available ? new Color(0.7f, 0.7f, 0.7f) : new Color(0.4f, 0.4f, 0.4f), clamp: false);
                }

                // Hover effect
                if (Mouse.IsOver(cardRect) && available)
                {
                    Widgets.DrawBoxSolid(cardRect, new Color(1f, 1f, 1f, 0.04f));
                    Widgets.DrawBox(cardRect);
                }

                // Click handler
                if (Widgets.ButtonInvisible(cardRect) && OptionsEnabled)
                {
                    if (available)
                    {
                        SoundDefOf.Click.PlayOneShotOnCamera();
                        // Atomic: only resolve the option if the silver was actually paid.
                        if (PaymentUtil.TryPaySilver(effectiveCost, PaymentUtil.Reason_EventOption))
                        {
                            FCEventMaker.CalculateSuccess(opt, parentEvent);
                            Find.WindowStack.TryRemove(this);
                        }
                        else
                        {
                            Messages.Message("FCNotEnoughSilverOption".Translate(), MessageTypeDefOf.RejectInput);
                        }
                    }
                    else if (!meetsRequirements)
                    {
                        Messages.Message(requirementFailReason, MessageTypeDefOf.RejectInput);
                    }
                    else
                    {
                        Messages.Message("FCNotEnoughSilverOption".Translate(), MessageTypeDefOf.RejectInput);
                    }
                }

                optY += cardH;
            }

            ScrollUtil.EndScrollView();

            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;
            GUI.color = colorBefore;
        }

        private static string GetEffectPreview(FCOptionDef opt, FCEvent parentEvent)
        {
            if (opt.baseChanceOfSuccess < 100f) return null;

            FCEventDef resultEvent = opt.successEvent;
            if (resultEvent == null) return null;

            List<string> tempParts = new List<string>();
            List<string> permParts = new List<string>();

            // Temporary stat modifiers
            if (resultEvent.statModifiers != null && resultEvent.statModifiers.Count > 0)
            {
                TaggedString statDesc = FCStatModifier.GetDescription(resultEvent.statModifiers);
                if (!statDesc.NullOrEmpty())
                {
                    string[] lines = statDesc.ToString().Split('\n');
                    foreach (string line in lines)
                    {
                        string trimmed = line.Trim();
                        if (!trimmed.NullOrEmpty()) tempParts.Add(trimmed);
                    }
                }
            }

            // Permanent stat modifiers
            if (resultEvent.permanentStatModifiers != null && resultEvent.permanentStatModifiers.Count > 0)
            {
                TaggedString permDesc = FCStatModifier.GetDescription(resultEvent.permanentStatModifiers);
                if (!permDesc.NullOrEmpty())
                {
                    string[] lines = permDesc.ToString().Split('\n');
                    foreach (string line in lines)
                    {
                        string trimmed = line.Trim();
                        if (!trimmed.NullOrEmpty()) permParts.Add(trimmed + " (permanent)");
                    }
                }
            }

            // Item rewards (delivered, not permanent). When the success event will inherit the
            // parent's settlement targets, show the scaled value the player will actually receive;
            // otherwise we can't know the future event's target count and fall back to the base.
            if (resultEvent.randomThingValue > 0 && resultEvent.randomThingRewardDef != null)
            {
                int previewValue = resultEvent.randomThingValue;
                if (resultEvent.settlementsCarryOver && parentEvent != null)
                {
                    previewValue *= FCEventScalingUtil.CountAffectedSettlements(parentEvent);
                }
                tempParts.Add("FCEffectPreviewReward".Translate(previewValue));
            }

            if (tempParts.Count == 0 && permParts.Count == 0) return null;

            // Duration context (skip near-instant deliveries)
            string durationStr = "";
            if (resultEvent.timeTillTrigger > 1500)
            {
                int minDays = (int)(resultEvent.timeTillTrigger / (double)GenDate.TicksPerDay);
                if (resultEvent.HasVariableDuration)
                {
                    int maxDays = (int)(resultEvent.timeTillTriggerMax / (double)GenDate.TicksPerDay);
                    if (minDays > 0 && maxDays > minDays)
                    {
                        durationStr = " " + "FCEffectPreviewDurationRange".Translate(minDays, maxDays);
                    }
                    else if (minDays > 0)
                    {
                        durationStr = " " + "FCEffectPreviewDuration".Translate(minDays);
                    }
                }
                else if (minDays > 0)
                {
                    durationStr = " " + "FCEffectPreviewDuration".Translate(minDays);
                }
            }

            // Attach duration only to temporary modifiers; permanent listed after
            string result = "";
            if (tempParts.Count > 0)
            {
                result = string.Join(", ", tempParts) + durationStr;
            }
            if (permParts.Count > 0)
            {
                if (result.Length > 0) result += ", ";
                result += string.Join(", ", permParts);
            }

            // Follow-up indicator: note when the success event chains into a further event.
            // One-hop lookahead only. Possible = split branch may be null (chain may terminate);
            // certain covers both unsplit and split-with-both-branches-defined.
            if (resultEvent.HasFollowUp)
            {
                bool hasBranch1 = resultEvent.followingEvent is object;
                bool hasBranch2 = resultEvent.followingEvent2 is object;
                if (resultEvent.splitEventFollows && (!hasBranch1 || !hasBranch2))
                {
                    if (hasBranch1 || hasBranch2)
                    {
                        if (result.Length > 0) result += "\n";
                        result += "FCOption_Followup_Possible".Translate().Resolve();
                    }
                }
                else if (hasBranch1)
                {
                    if (result.Length > 0) result += "\n";
                    result += "FCOption_Followup_Certain".Translate().Resolve();
                }
            }

            return result.Length > 0 ? result : null;
        }

        private static void GetSuccessHint(float chance, out string label, out Color color)
        {
            if (chance >= 100f)
            {
                label = "FCEventSuccessGuaranteed".Translate();
                color = AccentUtil.StatGood;
            }
            else if (chance >= 75f)
            {
                label = "FCEventSuccessLikely".Translate();
                color = AccentUtil.StatGood;
            }
            else if (chance >= 40f)
            {
                label = "FCEventSuccessUncertain".Translate();
                color = AccentUtil.StatMedBad;
            }
            else
            {
                label = "FCEventSuccessRisky".Translate();
                color = AccentUtil.StatBad;
            }
        }

        private static bool HasPolicyOrTrait(FactionFC faction, FCPolicyDef def)
        {
            foreach (FCPolicy p in FindFC.PolicyManager.policies)
            {
                if (p.def == def) return true;
            }
            foreach (FCPolicy t in FindFC.PolicyManager.factionTraits)
            {
                if (t.def == def) return true;
            }
            return false;
        }

        private static bool MeetsPolicyRequirements(FCOptionDef opt, out string failReason)
        {
            failReason = null;
            if (opt.requiredPolicies == null || opt.requiredPolicies.Count == 0)
                return true;

            FactionFC faction = FindFC.FactionComp;

            if (opt.requirementMode == FCRequirementMode.Any)
            {
                foreach (FCPolicyDef required in opt.requiredPolicies)
                {
                    if (HasPolicyOrTrait(faction, required))
                        return true;
                }
                string allNames = string.Join(", ", opt.requiredPolicies.Select(p => p.label));
                failReason = "FCOptionRequiresPolicyAny".Translate(allNames);
                return false;
            }
            else
            {
                List<string> missing = new List<string>();
                foreach (FCPolicyDef required in opt.requiredPolicies)
                {
                    if (!HasPolicyOrTrait(faction, required))
                        missing.Add(required.label);
                }
                if (missing.Count > 0)
                {
                    failReason = "FCOptionRequiresPolicy".Translate(string.Join(", ", missing));
                    return false;
                }
                return true;
            }
        }

        /* Meme sibling of MeetsPolicyRequirements. Checks the empire's primary ideo via
         * MemeUtilFC. Meme-gated options are only ever drawn when Ideology is active (they're
         * filtered out otherwise), so this runs only in that context. */
        private static bool MeetsMemeRequirements(FCOptionDef opt, out string failReason)
        {
            failReason = null;
            if (opt.requiredMemes == null || opt.requiredMemes.Count == 0)
                return true;

            if (opt.memeRequirementMode == FCRequirementMode.Any)
            {
                foreach (string required in opt.requiredMemes)
                {
                    if (MemeUtilFC.EmpireHasMeme(required))
                        return true;
                }
                string allNames = string.Join(", ", opt.requiredMemes.Select(m => MemeUtilFC.EmpireMemeLabel(m)));
                failReason = "FCOptionRequiresMemeAny".Translate(allNames);
                return false;
            }
            else
            {
                List<string> missing = new List<string>();
                foreach (string required in opt.requiredMemes)
                {
                    if (!MemeUtilFC.EmpireHasMeme(required))
                        missing.Add(MemeUtilFC.EmpireMemeLabel(required));
                }
                if (missing.Count > 0)
                {
                    failReason = "FCOptionRequiresMeme".Translate(string.Join(", ", missing));
                    return false;
                }
                return true;
            }
        }
    }
}
