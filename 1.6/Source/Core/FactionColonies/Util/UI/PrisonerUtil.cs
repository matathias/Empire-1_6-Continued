using FactionColonies.util;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    /* Shared renderer + action helpers for FCPrisoner displays.
       Used by FCPrisonerMenu (single-settlement window, rich row) and
       MainTabWindow_Colony.DrawPrisonersTab (faction-wide tab, compact row). */
    public static class PrisonerUtil
    {
        public const float RowHeight = 95f;
        public const float CompactRowHeight = 46f;
        public const float AccentWidth = 4f;

        private const float portraitW = 70f;
        private const float compactPortraitSz = 38f;
        private const float infoBtnSz = 18f;
        private const float gap = 4f;
        private const float rightColW = 128f;
        private const float pad = 4f;

        private static readonly Color healthBarBg = new Color(0.15f, 0.15f, 0.15f);

        // Captive-type badge colors: amber for slaves, cool gray for prisoners.
        private static readonly Color slaveBadgeColor = new Color(0.90f, 0.62f, 0.20f);
        private static readonly Color prisonerBadgeColor = new Color(0.70f, 0.72f, 0.78f);

        private static string CaptiveTypeLabel(FCPrisoner p)
            => (p.isSlave ? "FCCaptiveTypeSlave" : "FCCaptiveTypePrisoner").Translate();

        private static Color CaptiveTypeColor(FCPrisoner p)
            => p.isSlave ? slaveBadgeColor : prisonerBadgeColor;

        public static int CullNullPrisoners(FactionFC faction)
        {
            if (faction?.settlements is null) return 0;
            int total = 0;
            for (int i = 0; i < faction.settlements.Count; i++)
            {
                total += faction.settlements[i]?.PrisonerComp?.CullNullPrisoners() ?? 0;
            }
            return total;
        }

        public static bool HasCapturedPawns(Caravan caravan)
        {
            if (caravan?.PawnsListForReading is null) return false;
            List<Pawn> pawns = caravan.PawnsListForReading;
            for (int i = 0; i < pawns.Count; i++)
            {
                if (pawns[i].IsPrisonerOfColony || pawns[i].IsSlaveOfColony) return true;
            }
            return false;
        }

        public static string WorkloadLabel(FCWorkLoad w) => FCWorkLoadInfo.Label(w);

        /* Faction-scope: sets FactionFC.defaultPrisonerWorkload. Settlements with their
         * own override are unaffected. */
        public static void OpenFactionDefaultWorkloadFloatMenu(FactionFC faction)
        {
            if (faction is null) return;
            Find.WindowStack.Add(new FloatMenu(
                FCWorkLoadInfo.BuildSelectionMenu(delegate (FCWorkLoad w) { faction.defaultPrisonerWorkload = w; })));
        }

        /* Faction-scope: bulk-applies the chosen workload to every prisoner in every
         * settlement. Routes through each settlement's SetWorkload so per-settlement
         * stat caches dirty correctly. */
        public static void OpenFactionBulkSetWorkloadFloatMenu(FactionFC faction)
        {
            if (faction?.settlements is null) return;
            Find.WindowStack.Add(new FloatMenu(
                FCWorkLoadInfo.BuildSelectionMenu(delegate (FCWorkLoad w) { BulkSetAllSettlements(faction, w); })));
        }

        private static void BulkSetAllSettlements(FactionFC faction, FCWorkLoad w)
        {
            for (int i = 0; i < faction.settlements.Count; i++)
            {
                faction.settlements[i]?.PrisonerComp?.BulkSetWorkload(w);
            }
        }

        private static void GetWorkloadPresentation(FCWorkLoad workload, out string label, out string trend, out Color trendColor)
        {
            label = FCWorkLoadInfo.Label(workload);
            trend = FCWorkLoadInfo.TrendText(workload);
            trendColor = FCWorkLoadInfo.TrendColor(workload);
        }

        /* "Jonathan, Novelist" — title segment colorized via SubtleGrayColor.
           Falls back to just the name when the pawn has no backstory title. */
        private static string BuildNameWithTitle(Pawn pawn)
        {
            if (pawn is null) return "unknown";
            string name = pawn.Name?.ToStringShort ?? "unknown";
            string title = pawn.story?.TitleShortCap;
            if (string.IsNullOrEmpty(title)) return name;
            return name + (", " + title).Colorize(ColoredText.SubtleGrayColor);
        }

        /* "Male, age 63 (115) of New Arrivals" — vanilla pawn descriptor. */
        private static string BuildSubtitle(Pawn pawn)
        {
            if (pawn is null) return "";
            try { return pawn.MainDesc(writeFaction: true); }
            catch { return ""; }
        }

        /* Just the prisoner's home faction name (e.g. "New Arrivals"). Used by the
         * compact row where the full MainDesc string is too wide for a 2-column card. */
        private static string BuildFactionLabel(Pawn pawn)
        {
            Faction f = pawn?.Faction;
            if (f is null || f.Hidden) return "";
            return f.Name;
        }

        /* "Male, age 63 (115)" — pawn descriptor without the faction segment. The faction
         * gets rendered separately on the compact card so it can wear an icon and a
         * relation-derived color. Caller appends " of " when a faction follows. */
        private static string BuildSubtitlePrefix(Pawn pawn)
        {
            if (pawn is null) return "";
            string gender = pawn.GetGenderLabel().CapitalizeFirst();
            string age = "FCAge".Translate();
            int bio = pawn.ageTracker?.AgeBiologicalYears ?? 0;
            int chrono = pawn.ageTracker?.AgeChronologicalYears ?? bio;
            if (chrono != bio)
                return gender + ", " + age + " " + bio + " (" + chrono + ")";
            return gender + ", " + age + " " + bio;
        }

        /* Faction-name color in the prisoner card subtitle, keyed off relation kind with
         * the player faction. Delegates to vanilla FactionRelationKindUtility.GetColor for
         * the actual palette; only adds the null/hidden/player short-circuits. */
        private static Color FactionRelationColor(Faction f)
        {
            if (f is null || f.Hidden || f == Faction.OfPlayer) return ColoredText.SubtleGrayColor;
            return f.RelationKindWith(Faction.OfPlayer).GetColor();
        }

        /* Rich 95px prisoner row (settlement window).
           Three content rows next to the portrait:
             Row 1: Name, TitleShort (gray) ............... [info]      | $value
             Row 2: Male, age 63 (115) of New Arrivals                  | [Actions]
             Row 3: [== health 100 ===== +4/tick ====]                  | [Workload] */
        public static void DrawPrisonerRow(Rect box, FCPrisoner prisoner, WorldSettlementFC settlement, int altIndex, Action onRemoved)
        {
            GameFont fontBefore = Text.Font;
            TextAnchor anchorBefore = Text.Anchor;
            Color origColor = GUI.color;

            Widgets.DrawMenuSection(box);
            if (altIndex % 2 == 0)
            {
                Widgets.DrawHighlight(box);
            }

            // Faction-of-origin accent strip (left edge)
            Color accentColor = prisoner.prisoner?.Faction?.Color ?? Color.gray;
            Widgets.DrawBoxSolid(new Rect(box.x, box.y, AccentWidth, box.height), accentColor);

            float contentStartX = box.x + AccentWidth;

            // Portrait
            Rect portraitRect = new Rect(contentStartX + pad, box.y + 6f, portraitW, 78f);
            if (prisoner.prisoner is object)
            {
                UIUtil.DrawPawnPortrait(portraitRect, prisoner.prisoner, 1.2f);
            }

            // Center + right column geometry
            float centerX = portraitRect.xMax + gap + pad;
            float rightX = box.xMax - rightColW - pad;
            float centerW = rightX - centerX - gap;

            const float row1H = 24f;
            const float row2H = 22f;
            const float row3H = 22f;
            const float rowGap = 2f;

            float row1Y = box.y + pad;
            float row2Y = row1Y + row1H + rowGap;
            float row3Y = row2Y + row2H + rowGap;

            /* ROW 1: name + title + captive-type badge + info button (center), value (right) */
            Rect infoRect = new Rect(centerX + centerW - infoBtnSz, row1Y + (row1H - infoBtnSz) / 2f, infoBtnSz, infoBtnSz);

            // Captive-type badge (Slave / Prisoner), anchored just left of the info button.
            string badgeText = CaptiveTypeLabel(prisoner);
            Text.Font = GameFont.Tiny;
            float badgeW = Text.CalcSize(badgeText).x + 6f;
            Rect badgeRect = new Rect(infoRect.x - badgeW - 4f, row1Y, badgeW, row1H);
            Text.Anchor = TextAnchor.MiddleRight;
            UIUtil.DrawColoredLabel(badgeRect, badgeText, CaptiveTypeColor(prisoner), clamp: false);

            float nameW = Mathf.Max(0f, badgeRect.x - centerX - 4f);
            Rect nameRect = new Rect(centerX, row1Y, nameW, row1H);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(nameRect, BuildNameWithTitle(prisoner.prisoner));

            if (prisoner.prisoner is object)
            {
                UIUtil.InfoCardThing(infoRect, prisoner.prisoner);
            }

            Rect valueRect = new Rect(rightX, row1Y, rightColW, row1H);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleRight;
            UIUtil.ClampedLabel(valueRect, "$" + (int)(prisoner.prisoner?.MarketValue ?? 0));

            /* ROW 2: subtitle (gender, age, faction) | Actions */
            Rect subtitleRect = new Rect(centerX, row2Y, centerW, row2H);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = ColoredText.SubtleGrayColor;
            UIUtil.ClampedLabel(subtitleRect, BuildSubtitle(prisoner.prisoner));
            GUI.color = origColor;

            Rect actionsRect = new Rect(rightX, row2Y, rightColW, row2H);
            if (UIUtil.ButtonFlat(actionsRect, "FCActions".Translate()))
            {
                settlement.PrisonerComp?.DoActionsMenu(prisoner, onRemoved);
            }

            /* ROW 3: health bar with embedded trend | Workload */
            float healthBarH = 16f;
            float healthBarY = row3Y + (row3H - healthBarH) / 2f;
            const float trendW = 60f;
            Rect healthBarRect = new Rect(centerX, healthBarY, centerW - trendW - 5f, healthBarH);
            float healthFrac = prisoner.health / 100f;
            Color healthColor = AccentUtil.GetStatColor(prisoner.health, false);
            UIUtil.DrawProgressBarColors(healthBarRect, healthFrac, healthBarBg, healthColor);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            UIUtil.ClampedLabel(healthBarRect, "Health".Translate().CapitalizeFirst() + ": " + (int)prisoner.health);

            GetWorkloadPresentation(prisoner.workload, out string wlLabel, out string wlTrend, out Color wlTrendColor);

            // Trend indicator anchored to the right end of the bar
            Rect trendRect = new Rect(healthBarRect.xMax + 4f, healthBarRect.y, trendW, healthBarRect.height);
            Text.Anchor = TextAnchor.MiddleRight;
            GUI.color = wlTrendColor;
            UIUtil.ClampedLabel(trendRect, wlTrend);
            GUI.color = origColor;

            Rect workloadRect = new Rect(rightX, row3Y, rightColW, row3H);
            if (UIUtil.ButtonFlat(workloadRect, "FCWorkload".Translate().CapitalizeFirst() + ": " + wlLabel))
            {
                settlement.PrisonerComp?.OpenWorkloadFloatMenu(prisoner);
            }

            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;
            GUI.color = origColor;
        }

        /* Compact ~46px prisoner card (faction-wide tab, 2 cards per row).
           Two rows next to a 38×38 portrait:
             Top:    [i] Name, Title ... ... ... ... New Arrivals   $value
             Bottom: Health: 100/100 ... +1/day  [Light]  [Actions] */
        public static void DrawPrisonerRowCompact(Rect box, FCPrisoner prisoner, WorldSettlementFC settlement, int altIndex, Action onRemoved)
        {
            GameFont fontBefore = Text.Font;
            TextAnchor anchorBefore = Text.Anchor;
            Color origColor = GUI.color;

            bool isHighlighted = true;
            Widgets.DrawBoxSolid(box, ColorUtil.Gray2);

            Color accentColor = prisoner.prisoner?.Faction?.Color ?? Color.gray;
            Widgets.DrawBoxSolid(new Rect(box.x, box.y, AccentWidth, box.height), accentColor);

            // Secondary accent: vertical health bar, bottom-filled, color-graded.
            float healthBarX = box.x + AccentWidth + 1f;
            Widgets.DrawBoxSolid(new Rect(healthBarX, box.y, AccentWidth, box.height), healthBarBg);
            float healthFrac = Mathf.Clamp01(prisoner.health / 100f);
            float fillH = box.height * healthFrac;
            Widgets.DrawBoxSolid(
                new Rect(healthBarX, box.y + box.height - fillH, AccentWidth, fillH),
                AccentUtil.GetStatColor(prisoner.health, false));

            float contentStartX = box.x + AccentWidth * 2f + 1f;

            // Portrait — vertically centered in the row
            float portraitY = box.y + (box.height - compactPortraitSz) / 2f;
            Rect portraitRect = new Rect(contentStartX + pad, portraitY, compactPortraitSz, compactPortraitSz);
            if (prisoner.prisoner is object)
            {
                UIUtil.DrawPawnPortrait(portraitRect, prisoner.prisoner, 1.2f);
            }

            const float topRowH = 22f;
            const float botRowH = 20f;
            const float rowGap = 0f;

            float topY = box.y + pad;
            float botY = topY + topRowH + rowGap;

            // Center column starts right of portrait
            float centerX = portraitRect.xMax + (pad * 2);
            float rightEdge = box.xMax - pad;

            /* Button geometry (computed up front so the top-row faction label can
               right-align to the trend's right edge on the row below). Narrower than
               the pre-2-column layout: workload label drops its "Workload: " prefix. */
            const float actionsW = 70f;
            const float workloadW = 100f;
            const float btnGap = 4f;
            const float trendW = 45f;

            float actionsX = rightEdge - actionsW;
            float workloadX = actionsX - btnGap - workloadW;
            float trendRightEdge = workloadX - btnGap;
            float trendLeftEdge = trendRightEdge - trendW;

            /* TOP ROW */
            // Far right: $value (narrow column — $1430 is ~35px in Tiny, 45 leaves padding)
            const float valueW = 45f;
            Rect valueRect = new Rect(rightEdge - valueW, topY, valueW, topRowH);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleRight;
            UIUtil.ClampedLabel(valueRect, "$" + (int)(prisoner.prisoner?.MarketValue ?? 0));

            /* Subtitle slot — three right-aligned segments:
             *   "Male, age 39 (116) of "   [factionIcon]   FactionName
             * Faction name is colored by relation kind; prefix stays gray. */
            const float subtitleW = 240f;
            float subtitleRight = valueRect.x - 4f;
            float subtitleLeft = subtitleRight - subtitleW;
            Text.Font = GameFont.Tiny;

            Faction homeFaction = prisoner.prisoner?.Faction;
            string factionName = BuildFactionLabel(prisoner.prisoner);
            Texture2D factionIcon = (homeFaction is object && !homeFaction.Hidden) ? homeFaction.def?.FactionIcon : null;
            string subtitlePrefix = BuildSubtitlePrefix(prisoner.prisoner);
            // Only append the " of " connector when a faction segment will actually follow.
            if (!string.IsNullOrEmpty(subtitlePrefix) && !string.IsNullOrEmpty(factionName))
                subtitlePrefix += " " + "FCOf".Translate() + " ";

            float factionNameW = string.IsNullOrEmpty(factionName) ? 0f : Text.CalcSize(factionName).x + 2f;
            const float factionIconSz = 16f;
            float factionIconW = factionIcon != null ? factionIconSz + 2f : 0f;

            // Right-most: faction name
            if (!string.IsNullOrEmpty(factionName))
            {
                Rect factionNameRect = new Rect(subtitleRight - factionNameW, topY, factionNameW, topRowH);
                Text.Anchor = TextAnchor.MiddleRight;
                UIUtil.DrawColoredLabel(factionNameRect, factionName, FactionRelationColor(homeFaction));
            }
            // Next-right: faction icon tinted with the faction's own Color
            if (factionIcon != null)
            {
                float iconX = subtitleRight - factionNameW - factionIconSz;
                Rect iconRect = new Rect(iconX, topY + (topRowH - factionIconSz) / 2f, factionIconSz, factionIconSz);
                GUI.color = homeFaction.Color;
                GUI.DrawTexture(iconRect, factionIcon);
                GUI.color = origColor;
            }
            // Left-most: gray "Male, age N (M) of "
            float prefixRight = subtitleRight - factionNameW - factionIconW;
            Rect prefixRect = new Rect(subtitleLeft, topY, prefixRight - subtitleLeft, topRowH);
            Text.Anchor = TextAnchor.MiddleRight;
            GUI.color = ColoredText.SubtleGrayColor;
            UIUtil.ClampedLabel(prefixRect, subtitlePrefix);
            GUI.color = origColor;

            // Left of center: info button + captive-type badge + name+title
            Rect infoRect = new Rect(centerX, topY + (topRowH - infoBtnSz) / 2f, infoBtnSz, infoBtnSz);
            if (prisoner.prisoner is object)
            {
                UIUtil.InfoCardThing(infoRect, prisoner.prisoner);
            }

            string badgeTextC = CaptiveTypeLabel(prisoner);
            Text.Font = GameFont.Tiny;
            float badgeWC = Text.CalcSize(badgeTextC).x + 6f;
            Rect badgeRectC = new Rect(centerX + infoBtnSz + 4f, topY, badgeWC, topRowH);
            Text.Anchor = TextAnchor.MiddleLeft;
            UIUtil.DrawColoredLabel(badgeRectC, badgeTextC, CaptiveTypeColor(prisoner), clamp: false);

            float nameX = badgeRectC.xMax + 4f;
            float nameW = Mathf.Max(0f, subtitleLeft - nameX - 4f);
            Rect nameRect = new Rect(nameX, topY, nameW, topRowH);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(nameRect, BuildNameWithTitle(prisoner.prisoner));

            /* BOTTOM ROW */
            // Buttons render in Tiny font, with row-alt highlight tracking
            Text.Font = GameFont.Tiny;

            Rect actionsRect = new Rect(actionsX, botY, actionsW, botRowH);
            if (UIUtil.ButtonFlat(actionsRect, "FCActions".Translate(), highlighted: isHighlighted))
            {
                settlement.PrisonerComp?.DoActionsMenu(prisoner, onRemoved);
            }

            GetWorkloadPresentation(prisoner.workload, out string wlLabel, out string wlTrend, out Color wlTrendColor);
            Rect workloadRect = new Rect(workloadX, botY, workloadW, botRowH);
            if (UIUtil.ButtonFlat(workloadRect, wlLabel, highlighted: isHighlighted))
            {
                settlement.PrisonerComp?.OpenWorkloadFloatMenu(prisoner);
            }

            // Left of buttons: trend label (right-aligned, in trend color)
            Rect trendRect = new Rect(trendLeftEdge, botY, trendW, botRowH);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleRight;
            GUI.color = wlTrendColor;
            UIUtil.ClampedLabel(trendRect, wlTrend);
            GUI.color = origColor;

            /* Optional "Downed: No Work" badge between health text and trend. The badge
             * eats some of the health text's right edge when present, but never overlaps
             * the trend label. */
            bool downed = prisoner.prisoner is object && prisoner.prisoner.Downed;
            const float badgeW = 110f;
            const float badgeGap = 6f;
            float healthRightEdge = trendRect.x - 4f;
            if (downed)
            {
                Rect badgeRect = new Rect(trendRect.x - badgeGap - badgeW, botY, badgeW, botRowH);
                Text.Anchor = TextAnchor.MiddleRight;
                UIUtil.DrawColoredLabel(badgeRect, "FCPrisonerDownedNoWork".Translate(), AccentUtil.StatBad);
                healthRightEdge = badgeRect.x - 4f;
            }

            // Health text fills the remaining left side, color-graded by health value
            Rect healthRect = new Rect(centerX, botY, healthRightEdge - centerX, botRowH);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            UIUtil.DrawColoredLabel(healthRect, "Health".Translate().CapitalizeFirst() + ": " + (int)prisoner.health + "/100", AccentUtil.GetStatColor(prisoner.health, false));

            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;
            GUI.color = origColor;
        }
    }
}
