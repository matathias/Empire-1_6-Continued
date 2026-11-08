using UnityEngine;

namespace FactionColonies;

public class FCWindow_AnimalFilter : Window
{
    private FactionFC faction;
    private AnimalFilter filter;
    private List<PawnKindDef> allAnimals;
    private List<PawnKindDef> filteredAnimals;
    private string searchTerm = "";
    private int viewFilter; // 0=All, 1=Combat, 2=Pack
    private Vector2 scrollPos;

    public override Vector2 InitialSize => new(450f, 600f);

    private const float RowHeight = 30f;
    private const float SearchBarHeight = 28f;
    private const int margin = 5;
    private const int smallMargin = 3;
    private const int bigRowHeight = 26;

    public FCWindow_AnimalFilter()
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
            LogUtil.Error("Null FactionFC WorldComponent when opening FCWindow_AnimalFilter");
            return;
        }

        filter = faction.animalFilter;
        if (filter is null)
        {
            LogUtil.Error("Null animalFilter when opening FCWindow_AnimalFilter");
            return;
        }

        allAnimals = FactionCache.AllAnimalKindDefs
            .OrderBy(a => a.label ?? a.defName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        filteredAnimals = allAnimals;
    }

    public override void PostClose()
    {
        base.PostClose();
        if (filter is null) return;
        filter.Validate();
        faction.xenotypeFilter?.RefreshPawnGroupMakers();
        FactionDefDescriptionPatch.Invalidate();
    }

    public override void DoWindowContents(Rect inRect)
    {
        // PreOpen can't close the window (it isn't in the stack yet), so close here.
        if (filter is null)
        {
            Close();
            return;
        }

        GameFont fontBefore = Text.Font;
        TextAnchor anchorBefore = Text.Anchor;

        // Biome where trade caravans are delivered (tax spot -> capital -> main colony).
        // Drives pack-badge gray-out and the uncovered-biome warning. May be null pre-game.
        BiomeDef deliveryBiome = AnimalBiomeUtil.GetCaravanDeliveryBiome();

        // Header
        Text.Font = GameFont.Medium;
        Text.Anchor = TextAnchor.MiddleLeft;
        Rect header = new(inRect.x, inRect.y, inRect.width, 35f);
        UIUtil.ClampedLabel(header, "FCAnimalSelection".Translate());
        Widgets.DrawLineHorizontal(header.x, header.yMax, header.width);

        // Sub-header: faction name
        Text.Font = GameFont.Small;
        Rect subHeader = new(inRect.x, header.yMax, inRect.width, 26f);
        UIUtil.ClampedLabel(subHeader, FindFC.EmpireFaction.Name);

        // Search bar
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.MiddleLeft;
        Rect searchRect = new(inRect.x, subHeader.yMax + margin, inRect.width, SearchBarHeight);
        string newSearch = Widgets.TextField(searchRect, searchTerm);
        if (newSearch != searchTerm)
        {
            searchTerm = newSearch;
            RebuildFilteredList();
        }

        // View filter buttons: All | Combat | Pack
        float filterBtnHeight = 24f;
        float filterRowY = searchRect.yMax + margin;
        float filterBtnWidth = (inRect.width - margin * 2) / 3f;

        Rect allBtn = new(inRect.x, filterRowY, filterBtnWidth, filterBtnHeight);
        Rect combatBtn = new(allBtn.xMax + margin, filterRowY, filterBtnWidth, filterBtnHeight);
        Rect packBtn = new(combatBtn.xMax + margin, filterRowY, filterBtnWidth, filterBtnHeight);

        Text.Font = GameFont.Small;
        DrawFilterButton(allBtn, "FCAnimalFilterShowAll".Translate(), 0);
        DrawFilterButton(combatBtn, "FCAnimalFilterShowCombat".Translate(), 1);
        DrawFilterButton(packBtn, "FCAnimalFilterShowPack".Translate(), 2);

        float bottomY = inRect.yMax - CloseButSize.y - margin;

        // Warnings and errors at bottom (drawn bottom-up)
        if (filter.AllowedCount == 0)
        {
            UIUtil.DrawNoticeAbove(inRect.x, inRect.width, ref bottomY, "FCAnimalFilterNoneError".Translate(), Color.red, smallMargin, gap: margin);
        }

        bool noCombat = filter.AllowedCombatAnimals.Count == 0 && filter.AllowedCount > 0;
        if (noCombat)
        {
            UIUtil.DrawNoticeAbove(inRect.x, inRect.width, ref bottomY, "FCAnimalFilterNoCombatWarning".Translate(), Color.yellow, smallMargin, gap: margin);
        }

        bool noPack = filter.AllowedPackAnimals.Count == 0 && filter.AllowedCount > 0;
        if (noPack)
        {
            UIUtil.DrawNoticeAbove(inRect.x, inRect.width, ref bottomY, "FCAnimalFilterNoPackError".Translate(), Color.red, smallMargin, gap: margin);
        }

        // Delivery biome not covered by any allowed pack animal (player has packs, but none reach it).
        // Informational only - the player may keep the selection; they just won't get trade caravans.
        bool biomeUncovered = deliveryBiome is not null
            && filter.AllowedPackAnimals.Count > 0
            && !AnimalBiomeUtil.SelectionCoversBiome(filter, deliveryBiome);
        if (biomeUncovered)
        {
            UIUtil.DrawNoticeAbove(inRect.x, inRect.width, ref bottomY, "FCAnimalFilterBiomeUncoveredWarning".Translate(deliveryBiome.LabelCap), Color.red, smallMargin, gap: margin);
        }

        // Enable All / Disable All / Defaults buttons
        float btnWidth = inRect.width / 3f;
        Rect enableButton = new(inRect.x, bottomY - bigRowHeight, btnWidth, bigRowHeight);
        Rect disableButton = new(enableButton.xMax, enableButton.y, btnWidth, bigRowHeight);
        Rect defaultsButton = new(disableButton.xMax, enableButton.y, btnWidth, bigRowHeight);
        if (UIUtil.ClampedButtonText(enableButton, "FCAnimalEnableAll".Translate()))
        {
            filter.AllowAll();
        }
        if (UIUtil.ClampedButtonText(disableButton, "FCAnimalDisableAll".Translate()))
        {
            filter.DisallowAll();
        }
        if (UIUtil.ClampedButtonText(defaultsButton, "FCAnimalDefaults".Translate()))
        {
            filter.SetDefaults();
        }
        bottomY -= (enableButton.height + margin);

        // Scrollable animal list
        float listTop = filterRowY + filterBtnHeight + margin;
        float listHeight = bottomY - listTop;
        Rect scrollOutRect = new(inRect.x, listTop, inRect.width, listHeight);
        Widgets.DrawMenuSection(scrollOutRect);

        Rect innerRect = new(scrollOutRect.x + 2, scrollOutRect.y + 2, scrollOutRect.width - 4, scrollOutRect.height - 4);
        float viewHeight = filteredAnimals.Count * RowHeight;

        Rect scrollViewRect = ScrollUtil.BeginScrollView(innerRect, ref scrollPos, Math.Max(viewHeight, innerRect.height));

        Text.Font = GameFont.Small;
        for (int i = 0; i < filteredAnimals.Count; i++)
        {
            PawnKindDef animal = filteredAnimals[i];
            Rect row = new(scrollViewRect.x, scrollViewRect.y + (i * RowHeight), scrollViewRect.width, RowHeight);

            if (i % 2 == 0)
                Widgets.DrawHighlight(row);

            // Icon
            Rect iconRect = new(row.x + margin, row.y, RowHeight, RowHeight);
            Widgets.ThingIcon(iconRect, animal.race);

            // Info button
            Rect infoRect = new(iconRect.xMax, row.y + 2, RowHeight - 4, RowHeight - 4);
            Widgets.InfoCardButton(infoRect, animal.race);

            // Checkbox
            float checkboxSize = 24f;
            Rect checkRect = new(row.xMax - margin - checkboxSize, row.y + (RowHeight - checkboxSize) / 2f, checkboxSize, checkboxSize);
            bool allowed = filter.IsAllowed(animal);
            bool prev = allowed;
            Widgets.Checkbox(checkRect.x, checkRect.y, ref allowed, checkboxSize);
            if (allowed != prev)
            {
                filter.SetAllowed(animal, allowed);
            }

            // Tags (combat/pack) - drawn right-to-left before checkbox
            bool isCombat = animal.IsCombatAnimal();
            bool isPack = animal.IsPackAnimal();
            float tagX = checkRect.x - margin;

            if (isPack)
            {
                IReadOnlyList<BiomeDef> supportedBiomes = AnimalBiomeUtil.BiomesForPackAnimal(animal);
                bool packGrayed = deliveryBiome is not null && !deliveryBiome.IsPackAnimalAllowed(animal.race);

                // Inline biome count, e.g. "Pack (12)"
                string packLabel = "FCAnimalTagPack".Translate() + " (" + supportedBiomes.Count.ToString() + ")";
                float tagW = Text.CalcSize(packLabel).x + 8f;
                tagX -= tagW;
                Rect tagRect = new(tagX, row.y + 4f, tagW, RowHeight - 8f);

                Color boxColor = packGrayed
                    ? new Color(0.32f, 0.32f, 0.32f, 0.6f)
                    : new Color(0.2f, 0.4f, 0.55f, 0.6f);
                Widgets.DrawBoxSolid(tagRect, boxColor);

                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleCenter;
                if (packGrayed) GUI.color = new Color(0.7f, 0.7f, 0.7f);
                UIUtil.ClampedLabel(tagRect, packLabel);
                GUI.color = Color.white;

                TooltipHandler.TipRegion(tagRect, BuildPackBiomeTooltip(supportedBiomes, packGrayed ? deliveryBiome : null));
                tagX -= 3f;
            }

            if (isCombat)
            {
                float tagW = Text.CalcSize("FCAnimalTagCombat".Translate()).x + 8f;
                tagX -= tagW;
                Rect tagRect = new(tagX, row.y + 4f, tagW, RowHeight - 8f);
                Widgets.DrawBoxSolid(tagRect, new Color(0.55f, 0.2f, 0.2f, 0.6f));
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleCenter;
                UIUtil.ClampedLabel(tagRect, "FCAnimalTagCombat".Translate());
                tagX -= 3f;
            }

            // Label
            float labelEnd = tagX - margin;
            Rect labelRect = new(infoRect.xMax + margin, row.y, labelEnd - infoRect.xMax - margin, RowHeight);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            UIUtil.ClampedLabel(labelRect, animal.LabelCap);

            // Tooltip with combat disqualification reasons
            string tooltip = animal.race.description ?? "";
            if (!isCombat)
            {
                string reasons = GetNonCombatReasons(animal);
                if (!string.IsNullOrEmpty(reasons))
                {
                    tooltip += "\n\n" + "FCAnimalNotCombatHeader".Translate() + "\n" + reasons;
                }
            }
            TooltipHandler.TipRegion(row, tooltip);
        }

        if (filteredAnimals.Count == 0)
        {
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            UIUtil.ClampedLabel(innerRect, "fcNoAnimalsAvailable".Translate());
        }

        ScrollUtil.EndScrollView();

        Text.Font = fontBefore;
        Text.Anchor = anchorBefore;
    }

    private static string BuildPackBiomeTooltip(IReadOnlyList<BiomeDef> supportedBiomes, BiomeDef unsupportedDeliveryBiome)
    {
        string list = supportedBiomes.Count == 0
            ? (string)"FCAnimalBiomeNone".Translate()
            : string.Join("\n", supportedBiomes.Select(b => b.LabelCap.ToString()).ToArray());

        string body = "FCAnimalTagPackTooltipHeader".Translate() + "\n" + list;

        // When grayed out, lead with why (the current delivery biome isn't supported).
        if (unsupportedDeliveryBiome is not null)
        {
            body = "FCAnimalTagPackUnsupportedNote".Translate(unsupportedDeliveryBiome.LabelCap) + "\n\n" + body;
        }

        return body;
    }

    private static string GetNonCombatReasons(PawnKindDef animal)
    {
        List<string> reasons = [];

        if (animal.RaceProps.trainability is null
            || animal.RaceProps.trainability.intelligenceOrder < TrainabilityDefOf.Intermediate.intelligenceOrder)
        {
            reasons.Add("- " + "FCAnimalNotCombatTrainability".Translate());
        }

        if (animal.combatPower < 50f)
        {
            reasons.Add("- " + "FCAnimalNotCombatPowerLow".Translate(animal.combatPower.ToString("F0")));
        }

        return string.Join("\n", reasons);
    }

    private void DrawFilterButton(Rect rect, string label, int filterValue)
    {
        bool active = viewFilter == filterValue;
        TextAnchor origAnchor = Text.Anchor;
        Text.Anchor = TextAnchor.MiddleCenter;
        if (UIUtil.ButtonFlat(rect, label, highlighted: active))
        {
            viewFilter = filterValue;
            RebuildFilteredList();
        }

        Text.Anchor = origAnchor;
    }

    private void RebuildFilteredList()
    {
        IEnumerable<PawnKindDef> source = allAnimals;

        if (!string.IsNullOrEmpty(searchTerm))
        {
            source = source.Where(a => (a.label ?? a.defName).IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        if (viewFilter == 1)
        {
            source = source.Where(a => a.IsCombatAnimal());
        }
        else if (viewFilter == 2)
        {
            source = source.Where(a => a.IsPackAnimal());
        }

        filteredAnimals = source.ToList();
    }
}
