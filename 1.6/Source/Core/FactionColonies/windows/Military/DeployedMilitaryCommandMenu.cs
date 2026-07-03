using FactionColonies.util;
using LudeonTK;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Sound;


namespace FactionColonies
{
    public class DeployedMilitaryCommandMenu : Window
    {
        readonly FactionFC faction;
        public MercenarySquadFC selectedSquad;
        private List<MercenarySquadFC> squads = new List<MercenarySquadFC>();
        public string squadText;

        private Dictionary<string, string> truncateCache = new Dictionary<string, string>();

        public DeployedMilitaryCommandMenu()
        {
            layer = WindowLayer.Super;
            closeOnClickedOutside = false;
            closeOnAccept = false;
            closeOnCancel = false;
            doCloseX = false;
            draggable = true;
            drawShadow = true;
            doWindowBackground = true;
            preventCameraMotion = false;
            faction = FindFC.FactionComp;

            // Use the op-driven source so the freshly-deployed squad is visible immediately,
            // even while its pawns are still inside drop pods (pawn.Map is null pre-pod-open).
            selectedSquad = faction.military.SquadsInDeployOp.Where(squad => squad.getSettlement != null).RandomElementWithFallback();
        }

        public override Vector2 InitialSize => new Vector2(216f, 300f);

        protected override float Margin => 8f;

        protected override void SetInitialSizeAndPosition()
        {
            windowRect = new Rect(UI.screenWidth - InitialSize.x, 0f, InitialSize.x, InitialSize.y);
        }

        /// <summary>
        /// Lets the user select a squad from the squads active on the map
        /// </summary>
        private void DoSelectSquadCommand()
        {
            List<FloatMenuOption> list = new List<FloatMenuOption>();
            foreach (MercenarySquadFC squad in faction.military.SquadsInDeployOp)
            {
                if (squad.getSettlement != null)
                {
                    list.Add(new FloatMenuOption("FCSelectedDeployedSquad".Translate(squad.getSettlement.Name, squad.DisplayName), () => selectedSquad = squad));
                }
            }
            if (!list.Any())
            {
                //This should never happen
                LogUtil.Error("No deployed squad, but window is still open? Closing..");
                Close();
                list.Add(new FloatMenuOption("FCNoSquadsAvailable".Translate(), null));
            }

            Find.WindowStack.Add(new FloatMenu(list));
        }

        /// <summary>
        /// Makes the currently active squad execute the attack command
        /// </summary>
        private void DoAttackCommand()
        {
            if (selectedSquad != null)
            {
                selectedSquad.Deployment.MilitaryOrder = MilitaryOrder.Hunt;
                Messages.Message("FCAttackSuccess".Translate(selectedSquad.DisplayName), MessageTypeDefOf.NeutralEvent);
            }
        }

        /// <summary>
        /// Makes the currently active squad execute the move command. The move command is actually a point defence command.
        /// </summary>
        private void DoMoveCommand()
        {
            if (selectedSquad != null)
            {
                DebugTool tool;
                IntVec3 Position;
                tool = new DebugTool("FCSelectMilitaryMovePosition".Translate(), delegate ()
                {
                    Position = UI.MouseCell();

                    selectedSquad.Deployment.OrderLocation = Position;
                    selectedSquad.Deployment.MilitaryOrder = MilitaryOrder.DefendPoint;
                    Messages.Message("FCMoveSuccess".Translate(selectedSquad.DisplayName), MessageTypeDefOf.NeutralEvent);

                    DebugTools.curTool = null;
                });
                DebugTools.curTool = tool;
            }
        }

        /// <summary>
        /// Makes the currently selected squad immediately inactive and leave the map
        /// </summary>
        private void DoLeaveCommand()
        {
            if (selectedSquad != null)
            {
                selectedSquad.Deployment.MilitaryOrder = MilitaryOrder.RecoverWoundedAndLeave;
                Messages.Message("FCCommandLeave".Translate(selectedSquad.DisplayName, selectedSquad.dead), MessageTypeDefOf.NeutralEvent);
            }
        }

        /// <summary>
        /// If the dev mode is enabled, despawnes all squads and disables their deployment
        /// </summary>
        private void DoDebugCommand()
        {
            foreach (MercenarySquadFC squad in faction.military.SquadsInDeployOp.ToList())
            {
                DespawnSquad(squad);
            }
        }

        private static void DespawnSquad(MercenarySquadFC squad)
        {
            foreach (Mercenary merc in squad.mercenaries.Concat(squad.AllSubPawns()))
            {
                if (merc?.pawn?.Map != null)
                {
                    merc.pawn.Destroy();
                }
            }

            try
            {
                foreach (Pawn pawn in Find.CurrentMap.mapPawns.SpawnedPawnsInFaction(FindFC.EmpireFaction))
                {
                    pawn.Destroy();
                }
            }
            catch (Exception e)
            {
                LogUtil.Error($"Error when destroying pawns in DespawnSquad: {e}");
            }

            // Resolve the squad's op directly. Skip cooldown — debug action wants the squad
            // immediately freed.
            MilitaryOperation op = squad.Operation;
            if (op is object) FindFC.MilitaryManager?.Unregister(op);
            FindFC.Military?.RegisterSquadInjuries(squad);
        }

        /// <summary>
        /// Draws a flat button with an icon on the left and a text label.
        /// Matches the visual style of UIUtil.ButtonFlat.
        /// </summary>
        private static bool DrawIconButton(Rect rect, string label, Texture2D icon, float iconSize, float iconMargin)
        {
            bool hovered = Mouse.IsOver(rect);
            float bg = hovered ? 0.35f : 0.22f;
            Widgets.DrawBoxSolid(rect, new Color(bg, bg, bg));

            float iconY = rect.y + (rect.height - iconSize) / 2f;
            Rect iconRect = new Rect(rect.x + iconMargin, iconY, iconSize, iconSize);
            GUI.DrawTexture(iconRect, icon);

            float textX = iconRect.xMax + 4f;
            Rect labelRect = new Rect(textX, rect.y, rect.xMax - textX - 4f, rect.height);
            TextAnchor prevAnchor = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleLeft;
            UIUtil.ClampedLabel(labelRect, label);
            Text.Anchor = prevAnchor;

            if (Widgets.ButtonInvisible(rect))
            {
                SoundDefOf.Click.PlayOneShotOnCamera();
                return true;
            }
            return false;
        }

        public override void DoWindowContents(Rect rect)
        {
            if (!faction.military.SquadsInDeployOp.Any())
            {
                Close();
                return;
            }

            if (selectedSquad is null || !MilitaryFC.IsInDeployOp(selectedSquad))
            {
                selectedSquad = faction.military.SquadsInDeployOp.FirstOrDefault();
            }

            GameFont prevFont = Text.Font;
            TextAnchor prevAnchor = Text.Anchor;
            bool prevWordWrap = Text.WordWrap;
            Color prevColor = GUI.color;

            float contentWidth = rect.width;
            float buttonHeight = 36f;
            float infoRowHeight = 24f;
            float iconSize = 20f;
            float iconMargin = 6f;
            float separatorGap = 8f;
            float accentLineThickness = 2f;
            float spacing = 2f;

            float curY = rect.y;

            // --- Header: Select Squad button ---
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;

            Rect selectSquadRect = new Rect(rect.x, curY, contentWidth, buttonHeight);
            squadText = "FCSelectDeployedSquad".Translate();
            if (UIUtil.ClampedButtonText(selectSquadRect, squadText))
            {
                DoSelectSquadCommand();
            }
            curY = selectSquadRect.yMax;

            // --- Red accent separator line ---
            float lineY = curY + (separatorGap / 2f) - (accentLineThickness / 2f);
            Widgets.DrawBoxSolid(new Rect(rect.x + 4f, lineY, contentWidth - 8f, accentLineThickness), AccentUtil.Military);
            curY += separatorGap;

            if (selectedSquad != null)
            {
                // --- Settlement name (dimmer, smaller font, clickable) ---
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleCenter;
                Text.WordWrap = false;

                Rect settlementRect = new Rect(rect.x, curY, contentWidth, infoRowHeight);
                bool settlementHovered = Mouse.IsOver(settlementRect);
                Widgets.DrawHighlight(settlementRect);
                if (settlementHovered) Widgets.DrawHighlight(settlementRect);

                string settlementFullName = selectedSquad.getSettlement?.Name ?? "Unknown";
                string settlementTruncated = settlementFullName.Truncate(contentWidth - 10f, truncateCache);
                UIUtil.DrawColoredLabel(settlementRect, settlementTruncated, settlementHovered ? Color.white : new Color(0.8f, 0.8f, 0.8f));

                if (settlementTruncated != settlementFullName)
                {
                    TooltipHandler.TipRegion(settlementRect, settlementFullName);
                }

                if (selectedSquad.getSettlement is object && Widgets.ButtonInvisible(settlementRect))
                {
                    SoundDefOf.Click.PlayOneShotOnCamera();
                    Find.WindowStack.Add(new SettlementWindowFc(selectedSquad.getSettlement));
                }
                curY = settlementRect.yMax;

                // --- Squad name (normal white, clickable) ---
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleCenter;

                Rect squadNameRect = new Rect(rect.x, curY, contentWidth, infoRowHeight);
                bool squadHovered = Mouse.IsOver(squadNameRect);
                Widgets.DrawHighlight(squadNameRect);
                if (squadHovered) Widgets.DrawHighlight(squadNameRect);

                string squadFullName = selectedSquad.DisplayName;
                string squadTruncated = squadFullName.Truncate(contentWidth - 10f, truncateCache);
                UIUtil.ClampedLabel(squadNameRect, squadTruncated);

                if (squadTruncated != squadFullName)
                {
                    TooltipHandler.TipRegion(squadNameRect, squadFullName);
                }

                if (Widgets.ButtonInvisible(squadNameRect))
                {
                    SoundDefOf.Click.PlayOneShotOnCamera();
                    Pawn pawn = selectedSquad.DeployedMercenaries.FirstOrDefault()?.pawn;
                    if (pawn != null)
                    {
                        CameraJumper.TryJump(new GlobalTargetInfo(pawn));
                    }
                }
                curY = squadNameRect.yMax;

                // --- Faint separator between info and commands ---
                curY += 4f;
                UIUtil.DrawColoredHorizontalLine(rect.x + 8f, curY, contentWidth - 16f, new Color(1f, 1f, 1f, 0.3f));
                curY += 4f;

                // --- Command buttons with icons ---
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleCenter;
                Text.WordWrap = false;

                Rect attackRect = new Rect(rect.x, curY, contentWidth, buttonHeight);
                if (DrawIconButton(attackRect, "FCCommandAttack".Translate(), TexCommand.Attack, iconSize, iconMargin))
                {
                    DoAttackCommand();
                }
                curY = attackRect.yMax + spacing;

                Rect moveRect = new Rect(rect.x, curY, contentWidth, buttonHeight);
                if (DrawIconButton(moveRect, "FCCommandMove".Translate(), TexCommand.Draft, iconSize, iconMargin))
                {
                    DoMoveCommand();
                }
                curY = moveRect.yMax + spacing;

                Rect leaveRect = new Rect(rect.x, curY, contentWidth, buttonHeight);
                if (DrawIconButton(leaveRect, "FCCommandLeave".Translate(), TexCommand.PauseCaravan, iconSize, iconMargin))
                {
                    DoLeaveCommand();
                }
                curY = leaveRect.yMax + spacing;

                if (Prefs.DevMode)
                {
                    Rect debugRect = new Rect(rect.x, curY, contentWidth, buttonHeight);
                    if (UIUtil.ButtonFlat(debugRect, "FCDebugRemoveAllCommand".Translate()))
                    {
                        DoDebugCommand();
                    }
                }
            }

            Text.Font = prevFont;
            Text.Anchor = prevAnchor;
            Text.WordWrap = prevWordWrap;
            GUI.color = prevColor;
        }
    }
}
