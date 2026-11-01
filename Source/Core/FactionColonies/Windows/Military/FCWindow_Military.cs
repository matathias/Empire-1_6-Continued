using FactionColonies.util;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    public class FCWindow_Military : Window
    {
        private readonly MilitaryWindow militaryWindow;
        private readonly string title;

        public override Vector2 InitialSize => new Vector2(1050f, 600f);

        public FCWindow_Military(MilitaryWindow militaryWindow, string title)
        {
            this.militaryWindow = militaryWindow;
            this.title = title;

            forcePause = true;
            draggable = true;
            doCloseX = true;
            preventCameraMotion = false;
        }

        public override void PostClose()
        {
            base.PostClose();
            FindFC.Military?.CheckMilitaryUtilForErrors();
        }

        public override void DoWindowContents(Rect inRect)
        {
            GameFont fontBefore = Text.Font;
            TextAnchor anchorBefore = Text.Anchor;

            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            UIUtil.ClampedLabel(new Rect(5f, 5f, inRect.width - 10f, 35f), title);

            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;

            DrawNavigationButtons(inRect);

            militaryWindow.DrawTab(inRect);

            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;
        }

        private void DrawNavigationButtons(Rect inRect)
        {
            GameFont fontBefore = Text.Font;
            TextAnchor anchorBefore = Text.Anchor;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;

            float btnW = 110f;
            float btnH = 28f;
            float gap = 5f;
            float btnY = 5f + (35f - btnH) / 2f;

            string label1, label2, title1, title2;
            MilitaryWindowSlot target1, target2;

            if (militaryWindow.Slot == MilitaryWindowSlot.Units)
            {
                label1 = "FCNavSquads".Translate();
                label2 = "FCNavFireSupport".Translate();
                title1 = "FCMilitaryTableButtonCreateSquad".Translate();
                title2 = "FCMilitaryTableButtonCreateFireSupport".Translate();
                target1 = MilitaryWindowSlot.Squads;
                target2 = MilitaryWindowSlot.FireSupport;
            }
            else if (militaryWindow.Slot == MilitaryWindowSlot.Squads)
            {
                label1 = "FCNavUnits".Translate();
                label2 = "FCNavFireSupport".Translate();
                title1 = "FCMilitaryTableButtonCreateUnit".Translate();
                title2 = "FCMilitaryTableButtonCreateFireSupport".Translate();
                target1 = MilitaryWindowSlot.Units;
                target2 = MilitaryWindowSlot.FireSupport;
            }
            else
            {
                label1 = "FCNavUnits".Translate();
                label2 = "FCNavSquads".Translate();
                title1 = "FCMilitaryTableButtonCreateUnit".Translate();
                title2 = "FCMilitaryTableButtonCreateSquad".Translate();
                target1 = MilitaryWindowSlot.Units;
                target2 = MilitaryWindowSlot.Squads;
            }

            float x2 = inRect.width - gap - btnW;
            float x1 = x2 - gap - btnW;

            if (UIUtil.ClampedButtonText(new Rect(x1, btnY, btnW, btnH), label1))
            {
                NavigateTo(CreateWindow(target1), title1);
            }
            if (UIUtil.ClampedButtonText(new Rect(x2, btnY, btnW, btnH), label2))
            {
                NavigateTo(CreateWindow(target2), title2);
            }

            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;
        }

        private MilitaryWindow CreateWindow(MilitaryWindowSlot slot)
        {
            FactionFC fc = FindFC.FactionComp;
            return MilitaryWindowRegistry.Create(slot, fc.military, fc);
        }

        private void NavigateTo(MilitaryWindow target, string newTitle)
        {
            Close();
            Find.WindowStack.Add(new FCWindow_Military(target, newTitle));
        }

        public void SetActive(IExposable selecting)
        {
            militaryWindow.Select(selecting);
        }

        public MilitaryWindow GetMilitaryWindow() => militaryWindow;
    }
}
