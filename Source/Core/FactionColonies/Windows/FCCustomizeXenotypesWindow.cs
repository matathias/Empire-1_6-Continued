using FactionColonies.util;
using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    public class FCCustomizeXenotypesWindow : Window
    {
        FactionFC faction;
        List<XenotypeDef> allXenotypes;
        List<CustomXenotype> allCustomXenotypes;
        List<ThingDef> allRaces;
        XenotypeFilter filter;
        private List<string> weightBufXenos = new List<string>();
        private List<string> weightBufCustoms = new List<string>();
        private List<string> weightBufRaces = new List<string>();
        public override Vector2 InitialSize => new Vector2(400f, 600f);

        private Vector2 xenoScrollBar = new Vector2();
        private Vector2 raceScrollBar = new Vector2();

        private const int margin = 5;
        private const int smallMargin = 3;
        private const int rowHeight = 23;
        private const int bigRowHeight = 26;
        private const int scrollSpacing = 16;

        public FCCustomizeXenotypesWindow()
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
                LogUtil.Error("Null FactionFC WorldComponent when opening FCCustomizeXenotypesWindow");
                Close();
                return;
            }
            allXenotypes = FactionCache.XenotypeDefs;
            allCustomXenotypes = FactionCache.CustomXenotypes;
            allRaces = FactionCache.HumanlikeRaces;
            float width = 400f;
            if (allRaces.Count > 1)
            {
                // increase the width of the window, so that we have the xenotype selection on the left, and race selection on the right
                width *= 2;
            }
            float height = 600f;
            windowRect = new Rect(((float)UI.screenWidth - width) / 2f, ((float)UI.screenHeight - height) / 2f, width, height);

            filter = faction.xenotypeFilter;
            filter.ValidateCustomXenotypes();
            for (int i = 0; i < allXenotypes.Count; i++)
            {
                weightBufXenos.Add("");
            }
            for (int i = 0; i < allCustomXenotypes.Count; ++i)
            {
                weightBufCustoms.Add("");
            }
            for (int i = 0; i < allRaces.Count; ++i)
            {
                weightBufRaces.Add("");
            }
        }
        public override void PostClose()
        {
            base.PostClose();
            FactionDefDescriptionPatch.Invalidate();
            filter.CullWeights();
        }

        public override void DoWindowContents(Rect boundingBox)
        {
            GameFont fontBefore = Text.Font;
            TextAnchor anchorBefore = Text.Anchor;

            string titleText;
            if (allRaces.Count > 1)
            {
                titleText = "FCXenotypeRaceSelection".Translate();
            }
            else
            {
                titleText = "FCXenotypeSelection".Translate();
            }

            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            Rect header = new Rect(boundingBox.x, boundingBox.y, boundingBox.width, 35f);
            UIUtil.ClampedLabel(header, titleText);
            Widgets.DrawLineHorizontal(header.x, header.yMax, header.width);

            Text.Font = GameFont.Small;
            Rect subHeader = new Rect(boundingBox.x, header.yMax, boundingBox.width, 30f);
            UIUtil.ClampedLabel(subHeader, FindFC.EmpireFaction.Name);

            float availHeight = boundingBox.yMax - subHeader.yMax - CloseButSize.y - margin;

            if (allRaces.Count > 1)
            {
                Rect xenoBox = new Rect(boundingBox.x, subHeader.yMax, (boundingBox.width - (margin * 2)) / 2, availHeight);
                Rect raceBox = new Rect(xenoBox.xMax + (margin * 2), xenoBox.y, xenoBox.width, availHeight);
                Widgets.DrawLineVertical(xenoBox.xMax + margin, xenoBox.y, xenoBox.height);
                DoXenotypeSelection(xenoBox);
                DoRaceSelection(raceBox);
            }
            else
            {
                Rect xenoBox = new Rect(boundingBox.x, subHeader.yMax, boundingBox.width, availHeight);
                DoXenotypeSelection(xenoBox);
            }


            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;
        }
        private void DoXenotypeSelection(Rect boundingBox)
        {
            Rect header = new Rect(boundingBox.x, boundingBox.y, boundingBox.width, bigRowHeight);
            Rect headerText = new Rect(header.x + smallMargin, header.y, header.width - (smallMargin * 2), header.height);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.DrawHighlight(header);
            UIUtil.ClampedLabel(headerText, "FCXenotypeSelection".Translate());

            float bottomY = boundingBox.yMax;
            if (filter.XenoCompleteWeight == 0)
            {
                Rect errorBox = new Rect(boundingBox.x, boundingBox.yMax - bigRowHeight, boundingBox.width, bigRowHeight);
                Rect errorLabel = new Rect(errorBox.x + smallMargin, errorBox.y + smallMargin, errorBox.width - (smallMargin * 2), errorBox.height - smallMargin);
                TaggedString errorText = "FCXenotypeWeightError".Translate();
                errorText = errorText.Colorize(Color.red);

                Widgets.DrawHighlight(errorBox);
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;
                UIUtil.ClampedLabel(errorLabel, errorText);
                TooltipHandler.TipRegion(errorBox, "FCXenotypeWeightErrorDesc".Translate());

                bottomY -= (errorBox.height + margin);
            }
            if (filter.OnlyNonViolentXenos)
            {
                string noticeText = "FCOnlyNonViolentXenoWarning".Translate();
                float textHeight = Text.CalcHeight(noticeText, boundingBox.width - (smallMargin * 2));
                Rect noticeBox = new Rect(boundingBox.x, bottomY - textHeight, boundingBox.width, textHeight);
                Rect noticeLabel = new Rect(noticeBox.x + smallMargin, noticeBox.y, noticeBox.width - (smallMargin * 2), textHeight);
                noticeText = noticeText.Colorize(Color.yellow);

                Widgets.DrawHighlight(noticeBox);
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(noticeLabel, noticeText);

                bottomY -= (noticeBox.height + margin);
            }
            if (FactionCache.NonViolentXenotypesExist)
            {
                string noticeText = "* " + "FCXenoNonViolentIndicatorDesc".Translate();
                float textHeight = Text.CalcHeight(noticeText, boundingBox.width - (smallMargin * 2));
                Rect noticeBox = new Rect(boundingBox.x, bottomY - textHeight, boundingBox.width, textHeight);
                Rect noticeLabel = new Rect(noticeBox.x + smallMargin, noticeBox.y, noticeBox.width - (smallMargin * 2), textHeight);

                Widgets.DrawHighlight(noticeBox);
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(noticeLabel, noticeText);

                bottomY -= (noticeBox.height + margin);
            }

            Rect enableButton = new Rect(boundingBox.x, bottomY - bigRowHeight, boundingBox.width / 2, bigRowHeight);
            Rect disableButton = new Rect(enableButton.xMax, enableButton.y, enableButton.width, enableButton.height);
            if (UIUtil.ClampedButtonText(enableButton, "FCXenoRaceEnableAll".Translate()))
            {
                filter.ResetToAllXenotypes();
            }
            if (UIUtil.ClampedButtonText(disableButton, "FCXenoDisableNonBaseliner".Translate()))
            {
                filter.ResetToBaselinerXenotypeOnly();
            }
            bottomY -= (enableButton.height + margin);

            float renderHeight = bottomY - header.yMax - margin;
            float totalHeight = rowHeight * (allXenotypes.Count + allCustomXenotypes.Count);
            Rect drawBox = new Rect(boundingBox.x, header.yMax + margin, boundingBox.width, renderHeight);
            Rect selectedListBox = new Rect(drawBox.x + 2, drawBox.y + 2, drawBox.width - 4, drawBox.height - 4);
            Widgets.DrawMenuSection(selectedListBox);
            Rect innerScrollBox = ScrollUtil.BeginScrollView(selectedListBox, ref xenoScrollBar, totalHeight);

            Text.Anchor = TextAnchor.MiddleCenter;
            for (int i = 0; i < allXenotypes.Count + allCustomXenotypes.Count; i++)
            {
                Rect row = new Rect(innerScrollBox.x, innerScrollBox.y + (i * rowHeight), innerScrollBox.width, rowHeight);
                Rect icon = new Rect(row.x + margin, row.y, rowHeight, rowHeight);
                Rect percentLabel = new Rect(row.xMax - 60f, row.y, 60f, rowHeight);
                Rect inputBox = new Rect(percentLabel.x - 80f, row.y + 2, 80f, rowHeight - 4);
                Rect label = new Rect(icon.x + margin, row.y, inputBox.x - icon.xMax, rowHeight);
                if (i % 2 == 0)
                {
                    Widgets.DrawHighlight(row);
                }

                if (i < allXenotypes.Count)
                {
                    XenotypeDef xenotype = allXenotypes[i];
                    Widgets.Label(icon, new GUIContent(xenotype.Icon));
                    if (FactionCache.XenotypeIsNonViolent(xenotype))
                    {
                        UIUtil.ClampedLabel(label, xenotype.LabelCap + "*");
                    }
                    else
                    {
                        UIUtil.ClampedLabel(label, xenotype.LabelCap);
                    }
                    UIUtil.ClampedLabel(percentLabel, Math.Round(filter.GetXenotypeChance(xenotype) * 100, 2).ToString() + "%");
                    TooltipHandler.TipRegion(label, xenotype.description);

                    float weight = filter.GetXenotypeWeight(xenotype);
                    float oldWeight = weight;
                    string buf = weightBufXenos[i];
                    DoWeightField(inputBox, ref weight, ref buf);
                    weight = Math.Clamp(weight, 0f, float.MaxValue);
                    buf = weight.ToString();
                    if (oldWeight != weight)
                    {
                        filter.AddXenotypeWithWeight(xenotype, weight);
                    }
                    weightBufXenos[i] = buf;
                }
                else
                {
                    int customIndex = i - allXenotypes.Count;
                    CustomXenotype xenotype = allCustomXenotypes[customIndex];
                    Texture2D img = xenotype.IconDef?.Icon;
                    if (img != null)
                        Widgets.Label(icon, new GUIContent(xenotype.IconDef.Icon));

                    if (FactionCache.CustomXenotypeIsNonViolent(xenotype))
                    {
                        UIUtil.ClampedLabel(label, xenotype.name + "*");
                    }
                    else
                    {
                        UIUtil.ClampedLabel(label, xenotype.name);
                    }
                    UIUtil.ClampedLabel(percentLabel, Math.Round(filter.GetCustomXenotypeChance(xenotype.name) * 100, 2).ToString() + "%");

                    float weight = filter.GetCustomXenotypeWeight(xenotype.name);
                    float oldWeight = weight;
                    string buf = weightBufCustoms[customIndex];
                    DoWeightField(inputBox, ref weight, ref buf);
                    weight = Math.Clamp(weight, 0f, float.MaxValue);
                    buf = weight.ToString();
                    if (oldWeight != weight)
                    {
                        filter.AddCustomXenotypeWithWeight(xenotype, weight);
                    }
                    weightBufCustoms[customIndex] = buf;
                }
            }

            ScrollUtil.EndScrollView();
        }
        private void DoRaceSelection(Rect boundingBox)
        {
            Rect header = new Rect(boundingBox.x, boundingBox.y, boundingBox.width, bigRowHeight);
            Rect headerText = new Rect(header.x + smallMargin, header.y, header.width - (smallMargin * 2), header.height);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.DrawHighlight(header);
            UIUtil.ClampedLabel(headerText, "FCRaceSelection".Translate());

            float bottomY = boundingBox.yMax;
            if (filter.RaceTotalWeight == 0)
            {
                Rect errorBox = new Rect(boundingBox.x, boundingBox.yMax - bigRowHeight, boundingBox.width, bigRowHeight);
                Rect errorLabel = new Rect(errorBox.x + smallMargin, errorBox.y, errorBox.width - (smallMargin * 2), errorBox.height);
                TaggedString errorText = "FCRaceWeightError".Translate();
                errorText = errorText.Colorize(Color.red);

                Widgets.DrawHighlight(errorBox);
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleRight;
                UIUtil.ClampedLabel(errorLabel, errorText);
                TooltipHandler.TipRegion(errorBox, "FCRaceWeightErrorDesc".Translate());

                bottomY -= (errorBox.height + margin);
            }
            // This warning does break immersion a bit, and since we now assemble a new pawnkind if there are no valid pawnkinds of a given race for a given role,
            //   we should be much less likely to run into humans when the human race is disabled. So disabling this warning for now.
            // The pawnkind construction is pretty iffy though, so I'm leaving this code here in case we want to re-enable it at some point
            /*else if (filter.GetRaceWeight(ThingDefOf.Human) == 0)
            {
                string noticeText = "FCDisabledHumanWarning".Translate();
                float textHeight = Text.CalcHeight(noticeText, boundingBox.width - (smallMargin * 2));
                Rect noticeBox = new Rect(boundingBox.x, bottomY - textHeight - (smallMargin * 2), boundingBox.width, textHeight + (smallMargin * 2));
                Rect noticeLabel = new Rect(noticeBox.x + smallMargin, noticeBox.y, noticeBox.width - (smallMargin * 2), textHeight);

                Widgets.DrawHighlight(noticeBox);
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleRight;
                Widgets.Label(noticeLabel, noticeText.Colorize(Color.yellow));

                bottomY -= (noticeBox.height + margin);
            }*/

            Rect enableButton = new Rect(boundingBox.x, bottomY - bigRowHeight, boundingBox.width / 2, bigRowHeight);
            Rect disableButton = new Rect(enableButton.xMax, enableButton.y, enableButton.width, enableButton.height);
            if (UIUtil.ClampedButtonText(enableButton, "FCXenoRaceEnableAll".Translate()))
            {
                filter.ResetToAllRaces();
            }
            if (UIUtil.ClampedButtonText(disableButton, "FCRaceDisableNonHuman".Translate()))
            {
                filter.ResetToHumanRaceOnly();
            }
            bottomY -= (enableButton.height + margin);

            float renderHeight = bottomY - header.yMax - margin;
            float totalHeight = rowHeight * (allRaces.Count);
            Rect drawBox = new Rect(boundingBox.x, header.yMax + margin, boundingBox.width, renderHeight);
            Rect selectedListBox = new Rect(drawBox.x + 2, drawBox.y + 2, drawBox.width - 4, drawBox.height - 4);
            Widgets.DrawMenuSection(selectedListBox);
            Rect innerScrollBox = ScrollUtil.BeginScrollView(selectedListBox, ref raceScrollBar, totalHeight);

            Text.Anchor = TextAnchor.MiddleCenter;
            for (int i = 0; i < allRaces.Count; i++)
            {
                Rect row = new Rect(innerScrollBox.x, innerScrollBox.y + (i * rowHeight), innerScrollBox.width, rowHeight);
                Rect icon = new Rect(row.x + margin, row.y, rowHeight, rowHeight);
                Rect percentLabel = new Rect(row.xMax - 60f, row.y, 60f, rowHeight);
                Rect inputBox = new Rect(percentLabel.x - 80f, row.y + 2, 80f, rowHeight - 4);
                Rect label = new Rect(icon.x + margin, row.y, inputBox.x - icon.xMax, rowHeight);
                if (i % 2 == 0)
                {
                    Widgets.DrawHighlight(row);
                }

                ThingDef race = allRaces[i];
                //Races don't have icons. But I'm lazy and don't want to remove the icon rect and adjust the math, even though
                //  doing so would've been easier than writing this comment. Hmm. Oh well.
                //Widgets.Label(icon, new GUIContent(race.uiIcon));
                UIUtil.ClampedLabel(label, race.LabelCap);
                UIUtil.ClampedLabel(percentLabel, Math.Round(filter.GetRaceChance(race) * 100, 2).ToString() + "%");
                TooltipHandler.TipRegion(label, race.description);

                float weight = filter.GetRaceWeight(race);
                float oldWeight = weight;
                string buf = weightBufRaces[i];
                DoWeightField(inputBox, ref weight, ref buf);
                weight = Math.Clamp(weight, 0f, float.MaxValue);
                buf = weight.ToString();
                if (oldWeight != weight)
                {
                    filter.AddRaceWithWeight(race, weight);
                }
                weightBufRaces[i] = buf;
            }


            ScrollUtil.EndScrollView();
        }

        private void DoWeightField(Rect boundingBox, ref float value, ref string buffer, float min = 0, float max = float.MaxValue)
        {
            GameFont fontBefore = Text.Font;
            TextAnchor anchorBefore = Text.Anchor;
            float buttonSize = boundingBox.height;
            Rect decButton = new Rect(boundingBox.x, boundingBox.y, buttonSize, buttonSize);
            Rect incButton = new Rect(boundingBox.xMax - buttonSize, boundingBox.y, buttonSize, buttonSize);
            Rect fieldBox = new Rect(decButton.xMax, boundingBox.y, incButton.x - decButton.xMax, boundingBox.height);
            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = GameFont.Tiny;
            if (UIUtil.ClampedButtonText(decButton, "<"))
            {
                value = Math.Max(value - 1f, min);
            }
            if (UIUtil.ClampedButtonText(incButton, ">"))
            {
                value = Math.Min(value + 1f, max);
            }
            Widgets.TextFieldNumeric(fieldBox, ref value, ref buffer, min, max);

            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;
        }
    }
}
