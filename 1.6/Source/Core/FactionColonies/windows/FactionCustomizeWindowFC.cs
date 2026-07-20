using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    public class FactionCustomizeWindowFc : Window
    {
        private const float fullwidth = 500f;
        private const float fullheight = 503f;
        private const float margin = 8f;
        private const float smallMargin = 4f;
        private const float cardGap = 8f;
        private const float cardPadding = 8f;
        private const float swatchSize = 30f;
        private const float colorsRowHeight = 105f;
        private const float rowHeight = 60f;
        private const float bottomRowHeight = 80f;

        public override Vector2 InitialSize => new Vector2(fullwidth, fullheight);

        private FactionFC faction;

        private string tempName;
        private string tempTitle;
        private Texture2D tempFactionIcon;
        private string tempFactionIconPath;

        private Color tempPrimaryColor;
        private bool tempHasPrimaryColor;
        private Color tempSecondaryColor;
        private bool tempHasSecondaryColor;

        public FactionCustomizeWindowFc(FactionFC faction)
        {
            forcePause = false;
            draggable = true;
            doCloseX = true;
            preventCameraMotion = false;
            this.faction = faction;

            tempName = faction.name;
            tempTitle = faction.title;
            tempFactionIcon = faction.factionIcon;
            tempFactionIconPath = faction.factionIconPath;

            tempPrimaryColor = faction.factionColorPrimary;
            tempHasPrimaryColor = faction.hasFactionColor;
            tempSecondaryColor = faction.factionColorSecondary;
            tempHasSecondaryColor = faction.hasFactionColorSecondary;
        }

        private void ApplyChanges()
        {
            if (tempName.NullOrEmpty()) tempName = "FCPlayerFaction".Translate();
            if (tempTitle.NullOrEmpty()) tempTitle = "FCEmpire".Translate();

            faction.title = tempTitle;
            faction.name = tempName;

            // Title/name feed policy-description tokens ({FACTION_TITLE}/{FACTION}); drop the cache so tooltips re-resolve.
            FactionCache.InvalidatePolicyDescs();
            faction.factionIconPath = tempFactionIconPath;
            faction.factionIcon = tempFactionIcon;

            faction.factionColorPrimary = tempPrimaryColor;
            faction.hasFactionColor = tempHasPrimaryColor;
            faction.factionColorSecondary = tempSecondaryColor;
            faction.hasFactionColorSecondary = tempHasSecondaryColor;

            Faction fact = FindFC.EmpireFaction;
            if (fact != null)
            {
                fact.Name = tempName;
                faction.UpdateFactionIcon(ref fact, "FactionIcons/" + tempFactionIconPath);

                if (tempHasPrimaryColor)
                    fact.color = tempPrimaryColor;
                else
                    fact.color = null;
            }
            else
            {
                LogUtil.Error("PlayerColonyFaction is null - cannot sync faction name/icon!");
            }

            if (faction.military?.units != null)
            {
                foreach (MilUnitFC unit in faction.military.units)
                    unit.MarkEquipmentDirty();
            }
        }

        public override void OnAcceptKeyPressed()
        {
            ApplyChanges();
            base.OnAcceptKeyPressed();
        }

        public override void DoWindowContents(Rect inRect)
        {
            GameFont fontBefore = Text.Font;
            TextAnchor anchorBefore = Text.Anchor;

            float y = inRect.y;

            // 1. Header
            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = GameFont.Medium;
            UIUtil.ClampedLabel(new Rect(inRect.x, y, inRect.width, 36f), "FCCustomizeFaction".Translate());
            y += 36f + smallMargin;
            Widgets.DrawLineHorizontal(inRect.x, y, inRect.width);
            y += margin;

            // 2. Name / Title Row
            float halfWidth = (inRect.width - cardGap) / 2f;
            Rect nameCard = new Rect(inRect.x, y, halfWidth, rowHeight);
            Rect titleCard = new Rect(inRect.x + halfWidth + cardGap, y, halfWidth, rowHeight);
            DrawNameCard(nameCard);
            DrawTitleCard(titleCard);
            y += rowHeight + cardGap;

            // 3. Icon + Colors Row
            float iconCardWidth = 80f;
            Rect iconCard = new Rect(inRect.x, y, iconCardWidth, colorsRowHeight);
            Rect colorsCard = new Rect(inRect.x + iconCardWidth + cardGap, y, inRect.width - iconCardWidth - cardGap, colorsRowHeight);
            DrawIconCard(iconCard);
            DrawColorsCard(colorsCard);
            y += colorsRowHeight + cardGap;

            // 4. Xenotypes + Policies Row
            bool showXenoCard = ModsConfig.BiotechActive || FactionCache.HumanlikeRacesCount > 1;
            if (showXenoCard)
            {
                Rect xenoCard = new Rect(inRect.x, y, halfWidth, bottomRowHeight);
                Rect policyCard = new Rect(inRect.x + halfWidth + cardGap, y, halfWidth, bottomRowHeight);
                DrawXenotypesCard(xenoCard);
                DrawPoliciesCard(policyCard);
            }
            else
            {
                Rect policyCard = new Rect(inRect.x, y, inRect.width, bottomRowHeight);
                DrawPoliciesCard(policyCard);
            }
            y += bottomRowHeight + cardGap;

            // 5. Animals & Caravan Types Row
            float halfBottomWidth = (inRect.width - cardGap) / 2f;
            Rect animalCard = new Rect(inRect.x, y, halfBottomWidth, bottomRowHeight);
            Rect caravanCard = new Rect(inRect.x + halfBottomWidth + cardGap, y, halfBottomWidth, bottomRowHeight);
            DrawAnimalsCard(animalCard);
            DrawCaravanTypesCard(caravanCard);
            y += bottomRowHeight + cardGap;

            // 6. Confirm Button
            float confirmWidth = 200f;
            float confirmHeight = 30f;
            Rect confirmRect = new Rect((inRect.width - confirmWidth) / 2f, inRect.yMax - confirmHeight - margin, confirmWidth, confirmHeight);
            Text.Font = GameFont.Small;
            if (UIUtil.ClampedButtonText(confirmRect, "FCConfirmChanges".Translate()))
            {
                ApplyChanges();
                Find.WindowStack.TryRemove(this);
            }

            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;
        }

        private void DrawNameCard(Rect rect)
        {
            DrawCard(rect);
            DrawCardLabel(rect, "FCFactionName".Translate());
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            Rect fieldRect = new Rect(rect.x + cardPadding, rect.y + cardPadding + 18f, rect.width - cardPadding * 2, 28f);
            tempName = Widgets.TextField(fieldRect, tempName);
        }

        private void DrawTitleCard(Rect rect)
        {
            DrawCard(rect);
            DrawCardLabel(rect, "FCFactionTitle".Translate());
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            Rect fieldRect = new Rect(rect.x + cardPadding, rect.y + cardPadding + 18f, rect.width - cardPadding * 2, 28f);
            tempTitle = Widgets.TextField(fieldRect, tempTitle);
            TooltipHandler.TipRegion(fieldRect, "FCFactionTitleDesc".Translate());
        }

        private void DrawIconCard(Rect rect)
        {
            DrawCard(rect);
            DrawCardLabel(rect, "fcInsignia".Translate());
            float btnSize = 36f;
            Rect btnRect = new Rect(rect.x + (rect.width - btnSize) / 2f, rect.y + cardPadding + 20f, btnSize, btnSize);
            if (Widgets.ButtonImage(btnRect, tempFactionIcon))
            {
                List<FloatMenuOption> list = TexLoad.factionIcons.Select(texture => new FloatMenuOption(texture.name, delegate
                {
                    tempFactionIcon = texture;
                    tempFactionIconPath = texture.name;
                }, texture, Color.white)).ToList();

                Find.WindowStack.Add(new FloatMenu(list));
            }
        }

        private void DrawColorsCard(Rect rect)
        {
            DrawCard(rect);
            DrawCardLabel(rect, "fcColors".Translate());

            float contentY = rect.y + cardPadding + 18f;
            float swatchGroupX = rect.x + cardPadding;

            // Primary swatch
            Rect primarySwatchRect = new Rect(swatchGroupX, contentY, swatchSize, swatchSize);
            DrawColorSwatch(primarySwatchRect, tempPrimaryColor, tempHasPrimaryColor);
            if (Widgets.ButtonInvisible(primarySwatchRect))
            {
                OpenFactionColorPicker(true);
            }

            // Primary label + action
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.UpperCenter;
            Rect primaryLabelRect = new Rect(primarySwatchRect.x - 10f, primarySwatchRect.yMax + 2f, swatchSize + 20f, 18f);
            UIUtil.DrawColoredLabel(primaryLabelRect, "fcPrimaryColor".Translate(), ColorUtil.Gray6);

            if (tempHasPrimaryColor)
            {
                Rect clearRect = new Rect(primarySwatchRect.x, primaryLabelRect.yMax, swatchSize, 18f);
                GUI.color = new Color(0.72f, 0.53f, 0.04f);
                if (UIUtil.ClampedButtonText(clearRect, "Clear", drawBackground: false))
                {
                    tempHasPrimaryColor = false;
                    tempPrimaryColor = Color.white;
                    // Clearing primary also clears secondary
                    tempHasSecondaryColor = false;
                    tempSecondaryColor = Color.white;
                }
                GUI.color = Color.white;
            }
            else
            {
                Rect notSetRect = new Rect(primarySwatchRect.x - 8f, primaryLabelRect.yMax, swatchSize + 16f, 18f);
                UIUtil.DrawColoredLabel(notSetRect, "fcColorNotSet".Translate(), ColorUtil.Gray5);
            }

            TooltipHandler.TipRegion(primarySwatchRect, "fcPrimaryColorDesc".Translate());

            // Secondary swatch
            float secondaryX = swatchGroupX + swatchSize + 24f;
            Rect secondarySwatchRect = new Rect(secondaryX, contentY, swatchSize, swatchSize);
            bool secondaryDisabled = !tempHasPrimaryColor;

            DrawColorSwatch(secondarySwatchRect, tempSecondaryColor, tempHasSecondaryColor);
            if (secondaryDisabled)
            {
                // Gray overlay on disabled secondary (drawn after swatch so checkerboard is clean)
                GUI.color = new Color(0.3f, 0.3f, 0.3f, 0.4f);
                GUI.DrawTexture(secondarySwatchRect, BaseContent.WhiteTex);
                GUI.color = Color.white;
            }

            if (!secondaryDisabled && Widgets.ButtonInvisible(secondarySwatchRect))
            {
                OpenFactionColorPicker(false);
            }

            // Secondary label + action
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.UpperCenter;
            Rect secondaryLabelRect = new Rect(secondarySwatchRect.x - 12f, secondarySwatchRect.yMax + 2f, swatchSize + 24f, 18f);
            UIUtil.DrawColoredLabel(secondaryLabelRect, "fcSecondaryColor".Translate(), ColorUtil.Gray6);

            if (tempHasSecondaryColor)
            {
                Rect clearRect = new Rect(secondarySwatchRect.x, secondaryLabelRect.yMax, swatchSize, 18f);
                GUI.color = new Color(0.72f, 0.53f, 0.04f);
                if (UIUtil.ClampedButtonText(clearRect, "Clear", drawBackground: false))
                {
                    tempHasSecondaryColor = false;
                    tempSecondaryColor = Color.white;
                }
                GUI.color = Color.white;
            }
            else
            {
                Rect notSetRect = new Rect(secondarySwatchRect.x - 8f, secondaryLabelRect.yMax, swatchSize + 16f, 18f);
                UIUtil.DrawColoredLabel(notSetRect, "fcColorNotSet".Translate(), ColorUtil.Gray5);
            }

            if (secondaryDisabled)
            {
                TooltipHandler.TipRegion(secondarySwatchRect, "fcSetPrimaryFirst".Translate());
            }
            else
            {
                TooltipHandler.TipRegion(secondarySwatchRect, "fcSecondaryColorDesc".Translate());
            }

            // Info box when both are unset (to the right of swatches)
            if (!tempHasPrimaryColor && !tempHasSecondaryColor)
            {
                float infoX = secondaryX + swatchSize + 16f;
                float infoWidth = rect.xMax - cardPadding - infoX;
                if (infoWidth > 40f)
                {
                    Rect infoRect = new Rect(infoX, contentY, infoWidth, swatchSize + 16f);
                    Widgets.DrawBoxSolid(infoRect, new Color(0.18f, 0.14f, 0.10f, 0.6f));
                    Text.Font = GameFont.Tiny;
                    Text.Anchor = TextAnchor.MiddleLeft;
                    UIUtil.DrawColoredLabel(infoRect.ContractedBy(4f),
                        "fcColorsRandomInfo".Translate(),
                        new Color(0.72f, 0.53f, 0.04f), false);
                }
            }
        }

        private void DrawColorSwatch(Rect rect, Color color, bool isSet)
        {
            if (isSet)
            {
                // Solid color with white border
                Widgets.DrawBoxSolidWithOutline(rect, color, Color.white, 2);

                // Green checkmark badge (top-right)
                float badgeSize = 12f;
                Rect badge = new Rect(rect.xMax - badgeSize + 3f, rect.y - 3f, badgeSize, badgeSize);
                Widgets.DrawBoxSolid(badge, new Color(0.2f, 0.7f, 0.2f));
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = Color.white;
                UIUtil.ClampedLabel(badge, "\u2713");
            }
            else
            {
                // Checkerboard with gray border
                GUI.DrawTexture(rect, TexLoad.checkerboard);
                UIUtil.DrawColoredBox(rect, new Color(0.4f, 0.4f, 0.4f), 2);
            }
        }

        private void DrawXenotypesCard(Rect rect)
        {
            DrawCard(rect);
            DrawCardLabel(rect, "FCAllowedXenotypes".Translate());
            float btnWidth = rect.width - cardPadding * 2;
            float btnHeight = 28f;
            Rect btnRect = new Rect(rect.x + cardPadding, rect.y + (rect.height - btnHeight) / 2f + 8f, btnWidth, btnHeight);
            if (UIUtil.ButtonFlat(btnRect, "fcConfigure".Translate()))
            {
                Find.WindowStack.Add(new FCCustomizeXenotypesWindow());
            }
        }

        private void DrawAnimalsCard(Rect rect)
        {
            DrawCard(rect);
            DrawCardLabel(rect, "FCAllowedAnimals".Translate());
            float btnWidth = rect.width - cardPadding * 2;
            float btnHeight = 28f;
            Rect btnRect = new Rect(rect.x + cardPadding, rect.y + (rect.height - btnHeight) / 2f + 8f, btnWidth, btnHeight);
            if (UIUtil.ButtonFlat(btnRect, "fcConfigure".Translate()))
            {
                Find.WindowStack.Add(new FCWindow_AnimalFilter());
            }
        }

        private void DrawCaravanTypesCard(Rect rect)
        {
            DrawCard(rect);
            DrawCardLabel(rect, "FCCaravanTypes".Translate());
            float btnWidth = rect.width - cardPadding * 2;
            float btnHeight = 28f;
            Rect btnRect = new Rect(rect.x + cardPadding, rect.y + (rect.height - btnHeight) / 2f + 8f, btnWidth, btnHeight);
            if (UIUtil.ButtonFlat(btnRect, "fcConfigure".Translate()))
            {
                Find.WindowStack.Add(new FCWindow_CaravanTypePicker());
            }
        }

        private void DrawPoliciesCard(Rect rect)
        {
            DrawCard(rect);

            bool policiesSelected = FindFC.PolicyManager.policies.Count >= FCSettings.maxPolicyCount;
            float contentY;

            if (policiesSelected)
            {
                // No card label when policies are selected — icons are self-explanatory
                contentY = rect.y + (rect.height - 50f) / 2f;
                // Show selected policy icons
                float totalIconWidth = FindFC.PolicyManager.policies.Count * 32f + (FindFC.PolicyManager.policies.Count - 1) * 5f;
                float startX = rect.x + (rect.width - totalIconWidth) / 2f;

                for (int i = 0; i < FindFC.PolicyManager.policies.Count; i++)
                {
                    FCPolicy policy = FindFC.PolicyManager.policies[i];
                    Rect iconRect = new Rect(startX + i * (32f + 5f), contentY, 32f, 32f);
                    Widgets.DrawBoxSolid(iconRect, new Color(0.15f, 0.15f, 0.15f));

                    Texture2D icon = policy.def.IconLight;
                    if (icon != null)
                    {
                        GUI.DrawTexture(iconRect.ContractedBy(2f), icon);
                    }
                    TooltipHandler.TipRegion(iconRect, policy.def.PolicyText());
                }

                // Combined label below
                string policyNames = string.Join(" \u00b7 ", FindFC.PolicyManager.policies.Select(p => p.def.LabelCap.ToString()));
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.UpperCenter;
                UIUtil.DrawColoredLabel(
                    new Rect(rect.x + cardPadding, contentY + 34f, rect.width - cardPadding * 2, 16f),
                    policyNames,
                    ColorUtil.Gray6);
            }
            else
            {
                DrawCardLabel(rect, "FCSelectPolicies".Translate());
                contentY = rect.y + cardPadding + 18f;

                // Show placeholder slots
                float slotSize = 28f;
                int slotCount = FCSettings.maxPolicyCount;
                float totalSlotWidth = slotCount * slotSize + (slotCount - 1) * 5f;
                float startX = rect.x + (rect.width - totalSlotWidth) / 2f;

                for (int i = 0; i < slotCount; i++)
                {
                    Rect slotRect = new Rect(startX + i * (slotSize + 5f), contentY, slotSize, slotSize);
                    UIUtil.DrawColoredBox(slotRect, ColorUtil.Gray3, 2);
                    Text.Font = GameFont.Small;
                    Text.Anchor = TextAnchor.MiddleCenter;
                    UIUtil.DrawColoredLabel(slotRect, "?", ColorUtil.Gray4);
                }

                // "Click to select" label
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.UpperCenter;
                UIUtil.DrawColoredLabel(
                    new Rect(rect.x + cardPadding, contentY + slotSize + 4f, rect.width - cardPadding * 2, 16f),
                    "fcClickToSelect".Translate(),
                    ColorUtil.Gray5);

                // Clicking anywhere in the card opens the policy window
                if (Widgets.ButtonInvisible(rect))
                {
                    Find.WindowStack.Add(new FactionCustomizePoliciesWindowFC(faction));
                }
            }
        }

        private void OpenFactionColorPicker(bool primary)
        {
            Color current = primary
                ? (tempHasPrimaryColor ? tempPrimaryColor : Color.white)
                : (tempHasSecondaryColor ? tempSecondaryColor : Color.white);

            string header = primary
                ? "fcChoosePrimaryColor".Translate()
                : "fcChooseSecondaryColor".Translate();

            Find.WindowStack.Add(new FCWindow_ColorPicker(
                header,
                current,
                delegate (Color color)
                {
                    if (primary)
                    {
                        tempPrimaryColor = color;
                        tempHasPrimaryColor = true;
                    }
                    else
                    {
                        tempSecondaryColor = color;
                        tempHasSecondaryColor = true;
                    }
                }
            ));
        }

        private static void DrawCard(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
        }

        private static void DrawCardLabel(Rect card, string label)
        {
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.UpperLeft;
            UIUtil.ClampedLabel(new Rect(card.x + cardPadding, card.y + cardPadding, card.width - cardPadding * 2, 16f), label.ToUpper());
        }
    }
}
