using FactionColonies.util;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Offensive squad picker. Lists every squad in the faction as a card with billet, power,
    /// efficiency, travel time, predicted win chance (vs the target's defender bounds), and
    /// status. The raid type itself is selectable inside the dialog. Confirm dispatches the
    /// chosen squad through <see cref="MilitaryOperationManager.CreateOffensiveOp"/>.
    /// <para>The dialog is also reused for "pick a squad" flows that don't need raid-type switching
    /// (caravan defense routing, etc.) — instantiate via the override constructor with a single job,
    /// a custom <c>onConfirm</c> callback, and a <c>headerOverride</c>.</para>
    /// <para>Card layout, scroll plumbing, sort/filter, and status helpers live on
    /// <see cref="Dialog_SquadPicker"/>.</para>
    /// </summary>
    public class Dialog_AttackSettlement : Dialog_SquadPicker
    {
        private readonly WorldObject target;
        private readonly Faction enemy;
        private readonly List<MilitaryJobDef> validJobs; // null in override mode
        private readonly Action<MercenarySquadFC> onConfirm;
        private readonly string headerOverride;

        /* Drives the picker: dropdown selection in default mode, pinned job in override mode.
           Defender bounds and per-row attacker modifiers all depend on this through
           BattleForceContext.kind, so any change must rebuild both. */
        private MilitaryJobDef currentJob;

        /* Defender power: the registry entry plus pre-built min/max bounds with
         * BattleModifierRegistry already applied. Computed at ctor and rebuilt whenever the
         * dropdown picks a new job — stable across redraws within a single job selection.
         * The bounds frame the actual battle force the player will face: the engagement-time
         * roll in MilitaryOperation.BeginEngagement lands somewhere in
         * [defenderForceMin, defenderForceMax]. */
        private EnemyPower defenderPower;
        private MilitaryForce defenderForceMin;
        private MilitaryForce defenderForceMax;

        /// <summary>Default mode: the dialog presents a switcher across the supplied job list.</summary>
        public Dialog_AttackSettlement(WorldObject target, Faction enemy, List<MilitaryJobDef> jobs)
            : this(target, enemy, jobs, jobs?.FirstOrDefault()) { }

        /// <summary>Default mode with an explicit initial job (must be present in <paramref name="jobs"/>;
        /// falls back to the first entry otherwise).</summary>
        public Dialog_AttackSettlement(WorldObject target, Faction enemy, List<MilitaryJobDef> jobs, MilitaryJobDef initialJob)
        {
            this.target = target;
            this.enemy = enemy;
            this.validJobs = jobs;
            this.onConfirm = null;
            this.headerOverride = null;
            this.currentJob = (initialJob is object && jobs is object && jobs.Contains(initialJob))
                ? initialJob
                : jobs?.FirstOrDefault();

            doCloseX = true;
            forcePause = true;
            absorbInputAroundWindow = false;
            closeOnClickedOutside = false;
            draggable = true;

            BuildDefenderRange();
        }

        /// <summary>Override mode (caravan defense and similar): caller pins a single
        /// <paramref name="job"/>, supplies their own <paramref name="onConfirm"/>, and an explicit
        /// header title. The raid-type switcher is suppressed.</summary>
        public Dialog_AttackSettlement(WorldObject target, MilitaryJobDef job, Faction enemy,
            Action<MercenarySquadFC> onConfirm, string headerOverride)
        {
            this.target = target;
            this.enemy = enemy;
            this.validJobs = null;
            this.onConfirm = onConfirm;
            this.headerOverride = headerOverride;
            this.currentJob = job;

            doCloseX = true;
            forcePause = false;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;

            BuildDefenderRange();
        }

        /* Resolves the cached defender entry, builds min/max MilitaryForce instances at the
         * variance bounds, and lets the worldcomp run battle modifiers against each so the
         * displayed range matches what the engagement-time roll will hit (modulo same-tick
         * cache state). Probe context's aggressor is left null because settlement-/faction-
         * level modifiers run at cache time; battle modifiers that read aggressor are rare
         * and would need a per-row recompute path. */
        private void BuildDefenderRange()
        {
            defenderPower = null;
            defenderForceMin = null;
            defenderForceMax = null;

            Settlement targetSettlement = target as Settlement;
            if (targetSettlement is null || enemy is null) return;

            WorldComponent_EnemyPower registry = FindFC.EnemyPower;
            if (registry is null) return;

            defenderPower = registry.GetOrCompute(targetSettlement);

            BattleForceContext probeCtx = new BattleForceContext
            {
                kind = currentJob,
                targetTile = targetSettlement.Tile,
                targetObject = targetSettlement,
                aggressor = null
            };
            var bounds = registry.ResolveDefenderBounds(probeCtx);
            defenderForceMin = bounds.min;
            defenderForceMax = bounds.max;
        }

        /* Header: full-width title banner, then a two-column body — left = target row + defender
           power line; right = "Operation:" dropdown + description + rewards. In override mode the
           dialog has a pinned job, so the right column is suppressed and the body reverts to the
           single-column layout from before this change. Returns the y just below the body. */
        protected override float DrawHeader(Rect inRect)
        {
            // Title banner (full width)
            Rect titleRect = new Rect(0, 0, inRect.width, TitleH);
            Widgets.DrawHighlight(titleRect);

            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            string titleText = headerOverride.NullOrEmpty()
                ? (string)"FCSquadPickerTitle".Translate()
                : headerOverride;
            Widgets.Label(new Rect(8f, 0, inRect.width - 16f, TitleH), titleText);

            // Divider under title
            UIUtil.DrawColoredHorizontalLine(0, TitleH, inRect.width, Color.gray);

            float bodyTop = TitleH + 6f;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;

            bool twoColumn = headerOverride.NullOrEmpty() && validJobs is object && validJobs.Count > 0;

            if (!twoColumn)
            {
                // Single-column override mode: defender power line only, target row suppressed
                // (the override title already names the engagement context).
                float y = bodyTop;
                if (defenderForceMin is object && defenderForceMax is object)
                {
                    float defRowH = 22f;
                    Widgets.Label(new Rect(8f, y, inRect.width - 16f, defRowH), DefenderLineText());
                    y += defRowH;
                }
                return y;
            }

            // Two-column body
            float colW = (inRect.width - HeaderColGap) * 0.5f;
            Rect leftCol = new Rect(0, bodyTop, colW, 0);
            Rect rightCol = new Rect(colW + HeaderColGap, bodyTop, colW, 0);

            float leftBottom = DrawHeaderLeftColumn(leftCol);
            float rightBottom = DrawHeaderRightColumn(rightCol);

            // Vertical divider between columns, sized to the taller column
            float bodyBottom = Math.Max(leftBottom, rightBottom);
            Widgets.DrawBoxSolid(
                new Rect(colW + HeaderColGap * 0.5f - 0.5f, bodyTop, 1f, bodyBottom - bodyTop),
                new Color(0.5f, 0.5f, 0.5f));

            return bodyBottom;
        }

        /* Left column: a centered, highlighted target box (Target / faction icon + name /
           settlement name) followed by the defender power line. */
        private float DrawHeaderLeftColumn(Rect col)
        {
            float y = col.y;

            const float TargetFactionRowH = 24f;
            const float TargetSettlementH = 28f;
            const float TargetIconSize = 22f;
            const float TargetIconGap = 6f;

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;

            // Target header row
            Rect targetHeader = new Rect(col.x + margin, y, col.width - (margin * 2), SubHeaderH);
            Rect targetHeaderLabel = new Rect(targetHeader.x + smallMargin, targetHeader.y, targetHeader.width - (smallMargin * 2), targetHeader.height);
            Widgets.Label(targetHeaderLabel, "FCSquadPickerTarget".Translate());
            TexLoad.DrawHorizontalPeakGradientLine(targetHeader.x, targetHeader.yMax, targetHeader.width, Color.gray);
            y += targetHeader.height + margin;

            Color relationsColor = enemy?.PlayerRelationKind.GetColor() ?? Color.white;

            // faction icon (faction color) + faction name (relations color), centered as a unit
            Text.Anchor = TextAnchor.MiddleCenter;
            string factionName = enemy?.Name ?? "?";
            Vector2 nameSize = Text.CalcSize(factionName);
            float groupW = (enemy?.def?.FactionIcon != null ? TargetIconSize + TargetIconGap : 0f) + nameSize.x;
            float groupX = col.x + (col.width - groupW) * 0.5f;

            if (enemy?.def?.FactionIcon != null)
            {
                Color colorBefore = GUI.color;
                GUI.color = enemy.Color;
                GUI.DrawTexture(new Rect(groupX, y + (TargetFactionRowH - TargetIconSize) / 2f,
                    TargetIconSize, TargetIconSize), enemy.def.FactionIcon);
                GUI.color = colorBefore;
                groupX += TargetIconSize + TargetIconGap;
            }

            UIUtil.DrawColoredLabel(new Rect(groupX, y, nameSize.x + 4f, TargetFactionRowH), factionName, relationsColor);
            y += TargetFactionRowH;

            // settlement name, Medium font, centered, relations color
            Text.Font = GameFont.Medium;
            UIUtil.DrawColoredLabel(new Rect(col.x, y, col.width, TargetSettlementH), target?.LabelCap ?? "?", relationsColor);

            Text.Font = GameFont.Small;
            y += TargetSettlementH;

            // Defender power line — sits below the target box
            if (defenderForceMin is object && defenderForceMax is object)
            {
                float defRowH = 22f;
                y += 4f;
                Widgets.Label(new Rect(col.x + 8f, y, col.width - 16f, defRowH), DefenderLineText());
                y += defRowH;
            }

            return y;
        }

        /* Right column: "Operation:" label + dropdown button, then the wrapped description and
           rewards lines. Description comes from Verse Def.description (auto-translated via
           DefInjections); rewards comes from the optional rewardsDescKey. */
        private float DrawHeaderRightColumn(Rect col)
        {
            float y = col.y;
            float innerX = col.x + 8f;
            float innerW = col.width - 16f;

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;

            // Operation header row
            Rect opHeader = new Rect(col.x + margin, y, col.width - (margin * 2), SubHeaderH);
            Rect opHeaderLabel = new Rect(opHeader.x + smallMargin, opHeader.y, opHeader.width - (smallMargin * 2), opHeader.height);
            Widgets.Label(opHeaderLabel, "FCSquadPickerOperation".Translate());
            TexLoad.DrawHorizontalPeakGradientLine(opHeader.x, opHeader.yMax, opHeader.width, Color.gray);
            y += opHeader.height + margin;

            // Operation dropdown row
            float buttonMargin = margin * 3;
            Rect opButton = new Rect(col.x + buttonMargin, y, col.width - (buttonMargin * 2), SubHeaderH - 4f);
            string btnLabel = currentJob is null ? "?" : currentJob.LabelCap.ToString();
            if (Widgets.ButtonText(opButton, btnLabel))
            {
                List<FloatMenuOption> opts = new List<FloatMenuOption>();
                foreach (MilitaryJobDef job in validJobs)
                {
                    MilitaryJobDef captured = job;
                    opts.Add(new FloatMenuOption(captured.LabelCap, () => SetCurrentJob(captured)));
                }
                Find.WindowStack.Add(new FloatMenu(opts));
            }
            y += opButton.height + margin;

            // Description
            Text.Anchor = TextAnchor.UpperLeft;
            string description = currentJob?.description;
            if (!description.NullOrEmpty())
            {
                float h = Text.CalcHeight(description, innerW);
                Widgets.Label(new Rect(innerX, y, innerW, h), description);
                y += h + 4f;
            }

            // Rewards line
            string rewards = currentJob?.rewardsDesc;
            if (!rewards.NullOrEmpty())
            {
                string rewardsLine = (string)"FCSquadPickerRewards".Translate() + ": " + rewards;
                float h = Text.CalcHeight(rewardsLine, innerW);
                Widgets.Label(new Rect(innerX, y, innerW, h), rewardsLine);
                y += h;
            }

            Text.Anchor = TextAnchor.MiddleLeft;
            return y;
        }

        /* Switching jobs invalidates defender bounds (BattleForceContext.kind drives them) and
           per-row attacker modifiers (same), so we rebuild both. */
        private void SetCurrentJob(MilitaryJobDef job)
        {
            if (job == currentJob) return;
            currentJob = job;
            BuildDefenderRange();
            rowsDirty = true;
        }

        /* Builds the defender-power summary line, collapsing min/max ranges to a single value
           when min == max (e.g. when defender variance is zero). */
        private string DefenderLineText()
        {
            string forceText = TextUtil.FormatRange(defenderForceMin.forceRemaining, defenderForceMax.forceRemaining, "0");
            string effText = TextUtil.FormatRange(defenderForceMin.militaryEfficiency, defenderForceMax.militaryEfficiency, "0.##");
            return "FCSquadPickerEstimatedDefender".Translate(forceText, effText).ToString();
        }

        protected override bool CanConfirm() =>
            selected is object && selected.IsAvailable && currentJob is object;

        protected override void Confirm()
        {
            if (selected is null) return;
            if (!selected.IsAvailable) return;
            if (currentJob is null) return;

            /* Morale lockout: refuse the raid before any deployment-cost bill is created. */
            if (selected.settlement is object && selected.settlement.TryGetSquadDeploymentBlock(out string lockReason))
            {
                Messages.Message(lockReason, MessageTypeDefOf.RejectInput);
                return;
            }

            if (onConfirm is object)
            {
                try { onConfirm(selected); }
                catch (Exception e) { LogUtil.Error($"Dialog_AttackSettlement.onConfirm threw: {e}"); }
                Close();
                return;
            }

            // Default behavior: create offensive op via manager.
            int travel = selected.settlement is object && target is object
                ? TravelUtil.ReturnTicksToArrive(selected.settlement.Tile, target.Tile)
                : GenDate.TicksPerDay;
            MilitaryOperationManager manager = FindFC.MilitaryManager;
            if (manager is null)
            {
                LogUtil.Error("Dialog_AttackSettlement.Confirm: MilitaryManager unavailable.");
                Close();
                return;
            }
            RelationsUtilFC.AttackFaction(enemy);
            FindFC.TaxLedger.CreateDeploymentCostBill(selected);
            manager.CreateOffensiveOp(selected, target, currentJob, enemy, travel);
            Close();
        }

        protected override void RebuildRows()
        {
            rows.Clear();
            FactionFC fc = FindFC.FactionComp;
            List<MercenarySquadFC> pool = fc?.military?.mercenarySquads;
            if (pool is null) { rowsDirty = false; return; }

            int now = Find.TickManager.TicksGame;
            foreach (MercenarySquadFC squad in pool)
            {
                if (squad is null) continue;
                bool available = squad.IsAvailable;
                /* Morale lockout: a squad whose home settlement has collapsed morale cannot raid.
                 * Treat it as unavailable so the row greys and the availableOnly filter hides it. */
                bool moraleLocked = squad.settlement is object && squad.settlement.SquadDeploymentLocked;
                if (moraleLocked) available = false;
                if (availableOnly && !available) continue;

                int travelTicks = 0;
                if (squad.settlement is object && target is object)
                    travelTicks = TravelUtil.ReturnTicksToArrive(squad.settlement.Tile, target.Tile);

                // Attacker force is computed once per row so we can show power+efficiency on busy/cooldown
                // squads too (they're still informative to compare). Win chance only meaningful when ready.
                MilitaryForce attackerForce = MilitaryForce.CreateMilitaryForceFromSquad(squad, isAttacking: true);
                double attackerPower = SquadPowerRegistry.Resolve(squad).militaryLevel;
                double attackerEfficiency = attackerForce?.militaryEfficiency ?? 0;
                bool hasAttackerForce = attackerForce is object;

                /* Apply attacker-side modifiers per-row so the win chance reflects how the
                 * specific squad would actually perform after BattleModifierRegistry runs at
                 * engagement. Defender-side bounds were already pre-modified once at ctor. */
                if (hasAttackerForce && target is object)
                {
                    BattleForceContext rowCtx = new BattleForceContext
                    {
                        kind = currentJob,
                        targetTile = target.Tile,
                        targetObject = target,
                        aggressor = new MilitaryOperationParticipant
                        {
                            faction = FindFC.EmpireFaction,
                            squad = squad,
                            force = attackerForce,
                            homeSettlement = squad.settlement
                        },
                        defender = new MilitaryOperationParticipant { faction = enemy }
                    };
                    FindFC.EnemyPower?.ApplyBattleModifiers(rowCtx, attackerForce, isAttacker: true);
                    attackerEfficiency = attackerForce.militaryEfficiency;
                }

                /* Win chance against MAX defender = lower bound; against MIN defender = upper bound.
                 * (Stronger defender => lower attacker win chance, and vice versa.) Computed for
                 * unavailable squads too — the value still reads as "if this squad were ready,
                 * here's how it would do" and drives the win-chance-tinted card highlights. */
                double winChanceMin = 0;
                double winChanceMax = 0;
                if (hasAttackerForce && defenderForceMin is object && defenderForceMax is object)
                {
                    winChanceMin = SimulateBattleFc.CalculateAttackerWinChance(attackerForce, defenderForceMax);
                    winChanceMax = SimulateBattleFc.CalculateAttackerWinChance(attackerForce, defenderForceMin);
                }

                string status;
                Color statusColor;
                bool isReady;
                ComputeStatus(squad, now, out status, out statusColor, out isReady);

                int injuredCount = SquadHealthUtil.CountInjuredMercs(squad);
                if (isReady && injuredCount > 0)
                {
                    status = "FCSquadStatusInjured".Translate(injuredCount);
                    statusColor = AccentUtil.MilActiveMission;
                }
                /* Surface the morale lockout in the status column (takes precedence — it's blocking). */
                if (moraleLocked)
                {
                    status = "FCSquadStatusMoraleLocked".Translate();
                    statusColor = AccentUtil.MilUnderAttack;
                }

                string unavailableReason = null;
                if (!available)
                {
                    List<string> reasons = new List<string>();
                    SquadStatusUtil.AppendIntrinsicUnavailReasons(squad, reasons);
                    if (moraleLocked && squad.settlement.TryGetSquadDeploymentBlock(out string mr)) reasons.Add(mr);
                    unavailableReason = SquadStatusUtil.FormatUnavailTooltip(reasons);
                }

                rows.Add(new RowData
                {
                    squad = squad,
                    travelTicks = travelTicks,
                    winChanceMin = winChanceMin,
                    winChanceMax = winChanceMax,
                    ourPower = attackerPower,
                    ourEfficiency = attackerEfficiency,
                    hasOurForce = hasAttackerForce,
                    status = status,
                    statusColor = statusColor,
                    available = available,
                    deploymentCost = squad.DeploymentCost(),
                    injuredCount = injuredCount,
                    unavailableReason = unavailableReason
                });
            }

            ApplySort();
            rowsDirty = false;
        }
    }
}
