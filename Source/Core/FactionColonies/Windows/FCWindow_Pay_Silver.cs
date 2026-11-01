using UnityEngine;
using Verse;

namespace FactionColonies
{
    public class FCWindow_Pay_Silver : Window
    {

        public override Vector2 InitialSize
        {
            get
            {
                return new Vector2(438f, 238f);
            }
        }

        public int silverCount;
        public int selectedSilver;
        public WorldSettlementFC settlement;

        public string stringEffect;

        Rect label_Title = new Rect(0, 20, 400, 30);
        Rect label_Upper = new Rect(0, 50, 400, 30);

        Rect slider = new Rect(50, 70, 300, 30);

        Rect label_Lower = new Rect(0, 90, 400, 30);
        Rect button_Confirm = new Rect(155, 120, 90, 30);



        public FCWindow_Pay_Silver(WorldSettlementFC settlement)
        {
            this.forcePause = false;
            this.draggable = true;
            this.doCloseX = true;
            this.preventCameraMotion = false;
            this.silverCount = PaymentUtil.GetSilver();
            this.settlement = settlement;
            this.selectedSilver = 0;
        }

        public virtual float ReturnValue(int silver)
        {
            return silver / 100f;
        }

        public virtual void UseValue(float value)
        {

        }

        public override void DoWindowContents(Rect inRect)
        {
            //grab before anchor/font
            GameFont fontBefore = Text.Font;
            TextAnchor anchorBefore = Text.Anchor;

            //Settlement Tax Collection Header
            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = GameFont.Small;

            UIUtil.ClampedLabel(label_Title, "FCSendSilverToColony".Translate());
            UIUtil.ClampedLabel(label_Upper, "FCSendingXSilver".Translate(selectedSilver));


            selectedSilver = (int)Widgets.HorizontalSlider(slider, selectedSilver, 0, silverCount, roundTo: 1);

            UIUtil.ClampedLabel(label_Lower, stringEffect.Translate(ReturnValue(selectedSilver)));

            if (UIUtil.ClampedButtonText(button_Confirm, "FCConfirm".Translate()))
            {
                // Atomic: only apply the effect if the silver was actually paid (guards against the
                // balance dropping after the window's snapshot bounded the slider).
                if (PaymentUtil.TryPaySilver(selectedSilver, PaymentUtil.Reason_SilverPayment, settlement))
                {
                    this.UseValue(selectedSilver);
                    this.Close();
                }
            }

            //reset anchor/font
            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;

        }
    }
}

