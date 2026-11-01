using FactionColonies.util;
using RimWorld;
using System;
using UnityEngine;
using Verse;


namespace FactionColonies
{
    public class SettlementUpgradeWindowFc : Window
    {
        public override Vector2 InitialSize => new Vector2(380f, 300f);

        private readonly int yspacing = 30;
        private readonly int yoffset = 90;

        private readonly int length = 335;
        private readonly int xoffset = 0;
        private readonly int height = 200;
        private readonly int settlementUpgradeCost;
        private readonly int maxSettlementLevel;

        private readonly WorldSettlementFC settlement;
        private readonly FactionFC factionfc;

        public string desc;
        public string header;

        public SettlementUpgradeWindowFc(WorldSettlementFC settlement)
        {
            forcePause = false;
            draggable = true;
            doCloseX = true;
            preventCameraMotion = false;
            header = "FCUpgradeSettlement".Translate();
            this.settlement = settlement;
            settlementUpgradeCost = settlement.GetUpgradeCost(Convert.ToInt32(FCSettings.settlementBaseUpgradeCost));
            desc = settlement.Name + " " + "FCCanBeUpgraded".Translate() + " " + settlementUpgradeCost + " " + "FCSilver".Translate().ToLower() + ". " + "FCUpgradeColonyDesc".Translate();
            factionfc = FindFC.FactionComp;
            maxSettlementLevel = FCSettings.settlementMaxLevel;
        }

        /// <summary>
        /// Attempts to create an upgrading <c>FCEvent</c> for a <c>SettlementFC</c>.
        /// </summary>
        /// <returns>A message describing if the process was successful or not, including a reason in case it was not</returns>
        private Message UpgradeSettlement()
        {
            //failure reasons
            if (!FindFC.FactionComp.IsActionAllowed(FCActionType.UpgradeSettlement)) return new Message("FCActionNotAllowed".Translate(), MessageTypeDefOf.RejectInput);
            if (settlement.IsUpgrading) return new Message("FCAlreadyUpgradeSettlement".Translate(), MessageTypeDefOf.RejectInput);
            if (settlement.MilitaryComp?.isUnderAttack == true) return new Message("FCSettlementUnderAttack".Translate(), MessageTypeDefOf.RejectInput);

            //on success (atomic affordability check + payment)
            if (!PaymentUtil.TryPaySilver(settlementUpgradeCost, PaymentUtil.Reason_SettlementUpgrade, settlement))
                return new Message("FCNotEnoughSilverUpgrade".Translate(), MessageTypeDefOf.RejectInput);
            FCEvent tmp = new FCEvent(true)
            {
                def = FCEventDefOf.upgradeSettlement,
                tickStarted = Find.TickManager.TicksGame,
                location = settlement.Tile,
                timeTillTrigger = Find.TickManager.TicksGame + settlement.GetUpgradeTime(factionfc.GetStatValue(FCStatDefOf.buildTimeMultiplier))
            };
            tmp.customDescription = "FCUpgradeEventDesc".Translate(
                settlement.Name,
                settlement.settlementLevel,
                settlement.settlementLevel + 1,
                "FCUpgradeColonyDesc".Translate());
            tmp.hasCustomDescription = true;

            settlement.StartUpgrade(tmp.timeTillTrigger);
            FindFC.EventManager.AddEvent(tmp);

            //Close this window
            Find.WindowStack.TryRemove(this);

            return new Message("FCStartUpgradeSettlement".Translate(), MessageTypeDefOf.NeutralEvent);
        }

        public override void DoWindowContents(Rect inRect)
        {
            //grab before anchor/font
            GameFont fontBefore = Text.Font;
            TextAnchor anchorBefore = Text.Anchor;

            //Settlement Tax Collection Header
            Text.Anchor = TextAnchor.MiddleLeft;
            Text.Font = GameFont.Medium;

            UIUtil.ClampedLabel(new Rect(2, 0, 300, 60), header);

            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Tiny;

            if (settlement.CanUpgrade) //if settlement is not max level
            {
                if (UIUtil.ClampedButtonText(new Rect(xoffset + ((335 - 150) / 2f), height + 10, 150, 40), "FCUpgradeSettlement".Translate() + ": " + settlementUpgradeCost)) Messages.Message(UpgradeSettlement());
            }
            else //if settlement is max level
            {
                desc = "FCCannotBeUpgradedPastMax".Translate() + ": " + maxSettlementLevel;
            }

            Widgets.Label(new Rect(xoffset + 2, yoffset - yspacing + 2, length - 4, height - 4 + yspacing * 2), desc);
            Widgets.DrawBox(new Rect(xoffset, yoffset - yspacing, length, height - yspacing * 2));

            //reset anchor/font
            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;
        }
    }
}