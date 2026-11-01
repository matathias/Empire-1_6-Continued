using System;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    public class FCWindow_Confirm : Window
    {
        public override Vector2 InitialSize => new Vector2(438f, 188f);

        public string stringConfirm;
        private Action onConfirm;

        Rect label_Title = new Rect(0, 20, 400, 30);
        Rect label_Upper = new Rect(0, 50, 400, 70);
        Rect button_Confirm = new Rect(155, 120, 90, 30);

        public FCWindow_Confirm(string confirmText, Action onConfirm)
        {
            this.forcePause = false;
            this.draggable = true;
            this.doCloseX = true;
            this.preventCameraMotion = false;
            this.stringConfirm = confirmText;
            this.onConfirm = onConfirm;
        }

        public override void DoWindowContents(Rect inRect)
        {
            GameFont fontBefore = Text.Font;
            TextAnchor anchorBefore = Text.Anchor;

            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = GameFont.Small;

            UIUtil.ClampedLabel(label_Title, "FCConfirmDecision".Translate());
            UIUtil.ClampedLabel(label_Upper, stringConfirm);

            if (UIUtil.ClampedButtonText(button_Confirm, "FCConfirm".Translate()))
            {
                onConfirm?.Invoke();
                this.Close();
            }

            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;
        }
    }
}
