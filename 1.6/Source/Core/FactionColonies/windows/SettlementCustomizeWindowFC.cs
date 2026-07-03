using UnityEngine;
using Verse;

namespace FactionColonies
{
    public class SettlementCustomizeWindowFc : Window
    {
        public new const float StandardMargin = 10;
        private const int StandardHeight = 30;

        private readonly WorldSettlementFC settlement;
        private string name;
        private string shortName;

        public override Vector2 InitialSize => new Vector2(445f, 280f);

        public SettlementCustomizeWindowFc(WorldSettlementFC settlement)
        {
            forcePause = false;
            draggable = true;
            doCloseX = true;
            preventCameraMotion = false;
            this.settlement = settlement;
            name = settlement.Name;
            shortName = settlement.ShortName;
            doCloseButton = true;
            doCloseX = false;
        }

        /// <summary>
        /// Save variables on closing
        /// </summary>
        /// <param name="doCloseSound"></param>
        public override void Close(bool doCloseSound = true)
        {
            base.Close(doCloseSound);
            settlement.SetName(name);
            settlement.SetShortName(shortName);
        }

        public override void DoWindowContents(Rect inRect)
        {
            Rect headerLabelRect = new Rect(inRect.position, new Vector2(inRect.width, StandardHeight));

            Rect fullNameLabelRect = new Rect(StandardMargin, StandardHeight, inRect.width / 4, StandardHeight);
            Rect shortNameLabelRect = new Rect(StandardMargin, StandardMargin + StandardHeight * 2, inRect.width / 4, StandardHeight);

            float x = StandardMargin + inRect.width / 4;
            Rect fullNameInputRect = new Rect(x, StandardHeight, inRect.width - x - StandardMargin - StandardHeight, StandardHeight);
            Rect shortNameInputRect = new Rect(x, StandardMargin + StandardHeight * 2, inRect.width - x - StandardMargin - StandardHeight, StandardHeight);

            Rect resetFullNameButtonRect = new Rect(inRect.width - StandardHeight, StandardHeight, StandardHeight, StandardHeight);
            Rect resetShortNameButtonRect = new Rect(inRect.width - StandardHeight, StandardMargin + StandardHeight * 2, StandardHeight, StandardHeight);

            GameFont fontBefore = Text.Font;
            TextAnchor anchorBefore = Text.Anchor;

            Text.Anchor = TextAnchor.MiddleLeft;
            Text.Font = GameFont.Medium;

            UIUtil.ClampedLabel(headerLabelRect, "FCCustomizeSettlement".Translate());

            Text.Font = GameFont.Small;

            UIUtil.ClampedLabel(fullNameLabelRect, "FCSettlementFullName".Translate());
            name = Widgets.TextField(fullNameInputRect, name);

            UIUtil.ClampedLabel(shortNameLabelRect, "FCSettlementShortName".Translate());
            shortName = Widgets.TextField(shortNameInputRect, shortName);

            if (Widgets.ButtonImage(resetFullNameButtonRect, TexLoad.refreshIcon)) name = settlement.Name;
            if (Widgets.ButtonImage(resetShortNameButtonRect, TexLoad.refreshIcon)) shortName = TextUtil.ToShortName(name);

            Text.Anchor = TextAnchor.MiddleCenter;

            //reset anchor/font
            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;
        }
    }
}
