using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    public class MainTabWindow_EmpireExtensions : MainTabWindow
    {
        private string curTab;
        private List<TabRecord> tabs = new List<TabRecord>();
        private Dictionary<string, Action<Rect>> tabFuncs = new Dictionary<string, Action<Rect>>();

        public override Vector2 InitialSize => new Vector2(1060f, 640f);

        public override void PreOpen()
        {
            base.PreOpen();
            FactionFC faction = FindFC.FactionComp;
            if (faction == null) return;

            tabs.Clear();
            tabFuncs.Clear();
            curTab = null;

            foreach (IMainTabWindowOverview itab in MainTableRegistry.Tabs)
            {
                try
                {
                    itab.PreOpenWindow(faction);
                    string name = itab.TabName();
                    tabs.Add(new TabRecord(name, delegate
                    {
                        curTab = name;
                        itab.OnTabSwitch();
                    }, () => curTab == name));
                    tabFuncs.Add(name, itab.DrawOverviewTab);
                    if (curTab == null) curTab = name;
                }
                catch (Exception e)
                {
                    LogUtil.Error($"IMainTabWindowOverview {itab.GetType().Name} threw during registration: {e}");
                }
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (tabs.Count == 0 || curTab == null) return;

            GameFont fontBefore = Text.Font;
            TextAnchor anchorBefore = Text.Anchor;

            Text.Font = GameFont.Small;
            float maxLabelWidth = 0f;
            foreach (TabRecord tab in tabs)
            {
                float w = Text.CalcSize(tab.label).x;
                if (w > maxLabelWidth) maxLabelWidth = w;
            }
            float minTabWidth = maxLabelWidth + 16f;

            float tabHeight = TabDrawer.GetOverflowTabHeight(inRect, tabs, minTabWidth, 200f);
            Rect contentRect = new Rect(inRect.x, inRect.y + tabHeight, inRect.width, inRect.height - tabHeight);
            Widgets.DrawMenuSection(contentRect);
            TabDrawer.DrawTabsOverflow(inRect, tabs, minTabWidth, 200f);

            try
            {
                tabFuncs[curTab](contentRect);
            }
            catch (Exception e)
            {
                LogUtil.Error($"Error drawing extension tab '{curTab}': {e}");
                if (tabs.Count > 0) curTab = tabs[0].label;
            }

            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;
        }

        public override void PostClose()
        {
            base.PostClose();
            MainTableRegistry.InvokePostCloseWindow();
        }
    }
}
