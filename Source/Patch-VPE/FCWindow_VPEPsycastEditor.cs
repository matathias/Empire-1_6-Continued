using System;
using System.Collections.Generic;
using System.Linq;
using FactionColonies.util;
using RimWorld;
using UnityEngine;
using VanillaPsycastsExpanded;
using VanillaPsycastsExpanded.UI;
using VEF.Abilities;
using Verse;
using AbilityDef = VEF.Abilities.AbilityDef;

namespace FactionColonies.VPE
{
    /// <summary>
    /// Routes psycast picking through VPE's own UI. A throwaway "session" pawn is generated from the
    /// unit template and given psylink at the unit's level (so VPE attaches its tracker + points); the
    /// path/ability tree is then drawn and unlocked with VPE's own helpers and validation
    /// (<see cref="PsycastsUIUtility"/>, <c>UnlockPath</c>, <c>GiveAbility</c>, prerequisite checks).
    /// On close the pawn's learned VPE psycasts are snapshotted back into the template and the pawn is
    /// discarded. The session pawn is owned by this window — decoupled from the shared preview pawn.
    /// </summary>
    public class FCWindow_VPEPsycastEditor : Window
    {
        private readonly MilUnitFC unit;
        private readonly Action onClosed;

        private Pawn sessionPawn;
        private Hediff_PsycastAbilities hediff;
        private CompAbilities compAbilities;
        private bool setupFailed;

        private readonly Dictionary<string, List<PsycasterPathDef>> pathsByTab;
        private readonly List<TabRecord> tabs;
        private string curTab;
        private Vector2 pathsScrollPos;
        private float lastPathsHeight;
        private int pathsPerRow = 3;
        private readonly Dictionary<AbilityDef, Vector2> abilityPos = new Dictionary<AbilityDef, Vector2>();
        private bool useAltBackgrounds;

        // Meditation foci the player can buy with points, plus a local tally of psycaster-stat points
        // spent (Hediff_PsycastAbilities.statPoints is private, so we track our own spends here and
        // replay/snapshot from it). Foci and stat points consume points exactly like path abilities.
        private readonly List<MeditationFocusDef> foci;
        private int sessionStatPoints;

        // The authoritative, ORDERED list of the player's picks for this editing session. Each pick
        // (psycast, focus, or a single stat purchase) is appended in order, so trimming on a psylink
        // decrease removes the most recent picks regardless of kind. Initialized from the unit's saved
        // selections and only written back to the unit on Apply (Cancel/X discards it).
        private readonly List<SavedPsycast> selections = new List<SavedPsycast>();

        // Budget/spent are OUR authoritative numbers, derived from the psylink level and the live session
        // pawn state — never from VPE's mutable hediff.points (which desyncs across generate/replay).
        // Spent counts the same things VPE charges a point for: unlocked paths, learned psycasts,
        // unlocked meditation foci, and stat points.
        private int Budget => VPEPointMath.Budget(unit?.psylinkLevel ?? 0);

        private int Spent
        {
            get
            {
                int s = sessionStatPoints;
                if (hediff != null) s += hediff.unlockedPaths.Count + hediff.unlockedMeditationFoci.Count;
                if (compAbilities != null)
                    s += compAbilities.LearnedAbilities.Count(a => a.def != null && a.def.Psycast() != null);
                return s;
            }
        }

        private int Available => Budget - Spent;

        public override Vector2 InitialSize => new Vector2(1000f, 720f);

        public FCWindow_VPEPsycastEditor(MilUnitFC unit, Action onClosed)
        {
            this.unit = unit;
            this.onClosed = onClosed;
            forcePause = false;
            draggable = true;
            doCloseX = true;
            resizeable = true;
            absorbInputAroundWindow = true;

            pathsByTab = DefDatabase<PsycasterPathDef>.AllDefs
                .GroupBy(d => d.tab)
                .ToDictionary(g => g.Key, g => g.ToList());
            tabs = pathsByTab.Select(kv => new TabRecord(kv.Key, () => curTab = kv.Key, () => curTab == kv.Key)).ToList();
            curTab = pathsByTab.Keys.FirstOrDefault();

            foci = DefDatabase<MeditationFocusDef>.AllDefs
                .OrderByDescending(d => d.modContentPack.IsOfficialMod)
                .ThenByDescending(d => d.label)
                .ToList();

            // Working copy of the unit's saved picks, in stored order. Edited locally; persisted only on Apply.
            selections.AddRange(unit.psycasts.Where(a => a.systemKey == VPEPsycastProvider.ProviderKey));

            SetupSessionPawn();
        }

        private void SetupSessionPawn()
        {
            setupFailed = false;
            sessionStatPoints = 0;
            try
            {
                sessionPawn = FCPawnGenerator.GenerateWithForcedXenotype(FCPawnGenerator.WorkerOrMilitaryRequestForUnit(unit));
                if (sessionPawn is null) { setupFailed = true; return; }

                Faction empire = FindFC.EmpireFaction;
                if (sessionPawn.Faction is null && empire != null)
                    sessionPawn.SetFaction(empire);

                new VPEPsycastProvider().ApplyPsylink(sessionPawn, unit.psylinkLevel);

                hediff = sessionPawn.Psycasts();
                compAbilities = sessionPawn.GetComp<CompAbilities>();
                if (hediff is null || compAbilities is null) { setupFailed = true; return; }

                // Replay the working selection list onto the session pawn so the tree shows them as
                // owned. We do NOT touch VPE's point counter — our own Budget/Spent are authoritative.
                foreach (SavedPsycast saved in selections.Where(a => a.systemKey == VPEPsycastProvider.ProviderKey))
                {
                    if (saved.kind == VPEPsycastProvider.KindStatUpgrade)
                    {
                        int n = saved.count > 0 ? saved.count : 1;
                        hediff.ImproveStats(n);
                        sessionStatPoints += n;
                        continue;
                    }

                    if (saved.kind == VPEPsycastProvider.KindMeditationFocus)
                    {
                        MeditationFocusDef focus = DefDatabase<MeditationFocusDef>.GetNamedSilentFail(saved.psycastDef);
                        if (focus is null || hediff.unlockedMeditationFoci.Contains(focus)) continue;
                        hediff.UnlockMeditationFocus(focus);
                        continue;
                    }

                    AbilityDef def = DefDatabase<AbilityDef>.GetNamedSilentFail(saved.psycastDef);
                    if (def is null || compAbilities.HasAbility(def)) continue;
                    AbilityExtension_Psycast psycast = def.Psycast();
                    if (psycast != null && psycast.path != null && !hediff.unlockedPaths.Contains(psycast.path))
                        hediff.UnlockPath(psycast.path);
                    compAbilities.GiveAbility(def);
                }
            }
            catch (Exception ex)
            {
                setupFailed = true;
                LogUtil.Error($"VPE psycast editor: failed to set up session pawn for '{unit?.name}': {ex.Message}");
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            GameFont fontBefore = Text.Font;
            TextAnchor anchorBefore = Text.Anchor;

            if (setupFailed || sessionPawn is null || hediff is null || compAbilities is null)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(inRect, "fcVPEEditorUnavailable".Translate());
                Text.Anchor = anchorBefore;
                return;
            }

            // VPE's drawing helpers read these statics.
            PsycastsUIUtility.Hediff = hediff;
            PsycastsUIUtility.CompAbilities = compAbilities;

            // Reserve a bottom strip for the Clear button; the panels fill the rest.
            Rect bottomBar = new Rect(inRect.x, inRect.yMax - 34f, inRect.width, 34f);
            Rect body = inRect;
            body.height -= 40f;

            // --- Left info + stats/foci column (mirrors VPE's own psycast tab) ---
            Rect left = body.LeftPartPixels(300f);
            var listing = new Listing_Standard();
            listing.Begin(left);
            Text.Font = GameFont.Medium;
            listing.Label("fcVPEEditorTitle".Translate());
            Text.Font = GameFont.Small;
            listing.Gap(6f);
            listing.Label("fcPsylinkLevel".Translate() + ": " + hediff.level);
            listing.Label("fcVPEPointsAvailable".Translate(Available, Budget));
            listing.Gap(8f);
            Text.Font = GameFont.Tiny;
            listing.Label("fcVPEEditorHint".Translate());
            listing.Gap(8f);

            // Psycaster stat upgrade — spend points to raise neural heat limit, recovery, sensitivity, etc.
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            if (listing.ButtonTextLabeled("VPE.PsycasterStats".Translate(), "VPE.Upgrade".Translate()))
            {
                int num = GenUI.CurrentAdjustmentMultiplier();
                if (Available >= num)
                {
                    hediff.ImproveStats(num);
                    sessionStatPoints += num;
                    selections.Add(new SavedPsycast(VPEPsycastProvider.ProviderKey, "", VPEPsycastProvider.KindStatUpgrade, num));
                }
                else
                {
                    Messages.Message("VPE.NotEnoughPoints".Translate(), MessageTypeDefOf.RejectInput, false);
                }
            }
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.UpperLeft;
            listing.Label("fcVPEStatPointsSpent".Translate(sessionStatPoints));
            listing.Gap(8f);

            // Meditation focus types — buy with points (mirrors ITab_Pawn_Psycasts focus grid).
            Text.Font = GameFont.Small;
            listing.Label("VPE.FocusTypes".Translate());
            float fx = left.x;
            Rect fociRow = listing.GetRect(48f);
            foreach (MeditationFocusDef def in foci)
            {
                if (fx + 50f >= left.xMax)
                {
                    fx = left.x;
                    listing.Gap(3f);
                    fociRow = listing.GetRect(48f);
                }
                DoFocus(new Rect(fx, fociRow.y, 48f, 48f), def);
                fx += 50f;
            }
            listing.Gap(8f);

            listing.CheckboxLabeled("VPE.UseAltBackground".Translate(), ref useAltBackgrounds);
            listing.End();

            // --- Right path panel (tabs + scrolling tree) ---
            Rect right = body;
            right.xMin = left.xMax + 10f;

            if (pathsByTab.NullOrEmpty())
            {
                Widgets.DrawMenuSection(right);
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(right, "No Paths");
            }
            else
            {
                pathsPerRow = Mathf.Max(1, Mathf.FloorToInt(right.width / 200f));
                Rect tabArea = new Rect(right.x, right.y + 40f, right.width, right.height - 40f);
                TabDrawer.DrawTabs(tabArea, tabs);
                Rect panel = tabArea;
                Widgets.DrawMenuSection(panel);
                Rect viewRect = new Rect(0, 0, panel.width - 20f, lastPathsHeight);
                Widgets.BeginScrollView(panel.ContractedBy(2f), ref pathsScrollPos, viewRect);
                DoPaths(viewRect);
                Widgets.EndScrollView();
            }

            // --- Bottom bar: Clear (left), Cancel + Apply (right). Nothing persists until Apply. ---
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            const float btnW = 120f, btnH = 30f, btnGap = 8f;
            if (Widgets.ButtonText(new Rect(bottomBar.x, bottomBar.y, btnW, btnH), "fcClearPsycasts".Translate()))
                ClearSelections();
            Rect applyRect = new Rect(bottomBar.xMax - btnW, bottomBar.y, btnW, btnH);
            if (Widgets.ButtonText(applyRect, "fcApplyPsycasts".Translate()))
                ApplyAndClose();
            Rect cancelRect = new Rect(applyRect.x - btnGap - btnW, bottomBar.y, btnW, btnH);
            if (Widgets.ButtonText(cancelRect, "fcCancelPsycasts".Translate()))
                Close();

            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;
        }

        /* Empties the working selection list and rebuilds a clean session pawn. Leaves the unit's psylink
         * level untouched and does NOT persist — the player must still click Apply. */
        private void ClearSelections()
        {
            selections.Clear();
            DestroySessionPawn();
            SetupSessionPawn();
        }

        /* Writes the working selections back to the unit, then closes. The only path that persists edits. */
        private void ApplyAndClose()
        {
            unit.SetPsycastsForSystem(VPEPsycastProvider.ProviderKey, selections);
            Close();
        }

        /* Mirrors VanillaPsycastsExpanded.UI.ITab_Pawn_Psycasts.DoPaths, operating on the session pawn:
         * unlocked paths render their ability tree (clickable via DoAbility); locked paths show an
         * Unlock button gated by VPE's own CanPawnUnlock + point checks. */
        private void DoPaths(Rect inRect)
        {
            Vector2 curPos = inRect.position + Vector2.one * 10f;
            float widthPerPath = (inRect.width - (pathsPerRow + 1) * 10f) / pathsPerRow;
            float maxHeight = 0f;
            int remaining = pathsPerRow;

            foreach (PsycasterPathDef def in pathsByTab[curTab]
                .OrderByDescending(p => hediff.unlockedPaths.Contains(p))
                .ThenBy(p => p.order)
                .ThenBy(p => p.label))
            {
                Texture2D texture = useAltBackgrounds ? def.backgroundImage : def.altBackgroundImage;
                float height = widthPerPath / texture.width * texture.height + 30f;
                Rect rect = new Rect(curPos, new Vector2(widthPerPath, height));
                PsycastsUIUtility.DrawPathBackground(ref rect, def, useAltBackgrounds);

                if (hediff.unlockedPaths.Contains(def))
                {
                    if (def.HasAbilities)
                        PsycastsUIUtility.DoPathAbilities(rect, def, abilityPos, DoAbility);
                }
                else
                {
                    Widgets.DrawRectFast(rect, new Color(0f, 0f, 0f, useAltBackgrounds ? 0.7f : 0.55f));
                    if (Available >= 1)
                    {
                        Rect centerRect = new Rect(rect.center.x - 70f, rect.center.y - 15f, 140f, 30f);
                        if (def.CanPawnUnlock(sessionPawn))
                        {
                            if (Widgets.ButtonText(centerRect, "VPE.Unlock".Translate()))
                            {
                                hediff.UnlockPath(def);
                            }
                        }
                        else
                        {
                            GUI.color = Color.grey;
                            string label = "VPE.Locked".Translate().Resolve() + ": " + def.lockedReason;
                            centerRect.width = Mathf.Max(centerRect.width, Text.CalcSize(label).x + 10f);
                            Widgets.ButtonText(centerRect, label, active: false);
                            GUI.color = Color.white;
                        }
                    }

                    TooltipHandler.TipRegion(
                        rect,
                        () => def.tooltip + "\n\n" + "VPE.AbilitiesList".Translate() + "\n" + def.abilities.Select(ab => ab.label).ToLineList("  ", true),
                        def.GetHashCode());
                }

                maxHeight = Mathf.Max(maxHeight, height + 10f);
                curPos.x += widthPerPath + 10f;
                remaining--;
                if (remaining == 0)
                {
                    curPos.x = inRect.x + 10f;
                    curPos.y += maxHeight;
                    remaining = pathsPerRow;
                    maxHeight = 0f;
                }
            }

            lastPathsHeight = curPos.y + maxHeight;
        }

        /* Mirrors ITab_Pawn_Psycasts.DoAbility: an ability is unlockable when its prerequisites are met
         * and a point is available; clicking spends a point and grants it via VPE's own CompAbilities. */
        private void DoAbility(Rect inRect, AbilityDef ability)
        {
            bool unlockable = false;
            bool locked = false;
            if (!compAbilities.HasAbility(ability))
            {
                if (ability.Psycast().PrereqsCompleted(compAbilities) && Available >= 1)
                    unlockable = true;
                else
                    locked = true;
            }

            if (unlockable) Widgets.DrawStrongHighlight(inRect.ExpandedBy(12f));
            PsycastsUIUtility.DrawAbility(inRect, ability);
            if (locked) Widgets.DrawRectFast(inRect, new Color(0f, 0f, 0f, 0.6f));

            TooltipHandler.TipRegion(
                inRect,
                () => $"{ability.LabelCap}\n\n{ability.description}{(unlockable ? "\n\n" + "VPE.ClickToUnlock".Translate().Resolve().ToUpper() : "")}",
                ability.GetHashCode());

            if (unlockable && Widgets.ButtonInvisible(inRect))
            {
                compAbilities.GiveAbility(ability);
                selections.Add(new SavedPsycast(VPEPsycastProvider.ProviderKey, ability.defName));
            }
        }

        /* Mirrors ITab_Pawn_Psycasts.DoFocus: a meditation focus the player can buy with a point.
         * VPE's own CanPawnUse (already usable) and CanUnlock (eligibility + reason) gate the button. */
        private void DoFocus(Rect inRect, MeditationFocusDef def)
        {
            Widgets.DrawBox(inRect, 3, Texture2D.grayTexture);
            bool unlocked = def.CanPawnUse(sessionPawn);
            string lockedReason;
            bool canUnlock = def.CanUnlock(sessionPawn, out lockedReason);

            GUI.color = unlocked ? Color.white : Color.gray;
            GUI.DrawTexture(inRect.ContractedBy(5f), def.Icon());
            GUI.color = Color.white;

            TooltipHandler.TipRegion(inRect, def.LabelCap + (def.description.NullOrEmpty() ? "" : "\n\n") +
                                             def.description + (canUnlock ? "" : "\n\n" + lockedReason));
            Widgets.DrawHighlightIfMouseover(inRect);

            if (Available >= 1 && !unlocked && canUnlock)
            {
                if (Widgets.ButtonText(new Rect(inRect.xMax - 13f, inRect.yMax - 13f, 12f, 12f), "▲"))
                {
                    hediff.UnlockMeditationFocus(def);
                    selections.Add(new SavedPsycast(VPEPsycastProvider.ProviderKey, def.defName, VPEPsycastProvider.KindMeditationFocus, 1));
                }
            }
        }

        // Close just tears down the session pawn — it never persists. Edits are written only by
        // ApplyAndClose(), so closing via Cancel, the X, or Esc discards the working selections.
        public override void PreClose()
        {
            base.PreClose();
            PsycastsUIUtility.Hediff = null;
            PsycastsUIUtility.CompAbilities = null;
            DestroySessionPawn();
            onClosed?.Invoke();
        }

        private void DestroySessionPawn()
        {
            if (sessionPawn is null) return;
            try
            {
                sessionPawn.apparel?.DestroyAll();
                sessionPawn.equipment?.DestroyAllEquipment();
                if (!sessionPawn.Destroyed) sessionPawn.Destroy();
            }
            catch (Exception ex)
            {
                LogUtil.Warning($"VPE psycast editor: failed to discard session pawn: {ex.Message}");
            }
            sessionPawn = null;
        }
    }
}
