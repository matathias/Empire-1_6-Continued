using UnityEngine;

namespace FactionColonies
{
    /// <summary>
    /// Defines an interface to let classes specify additional tabs to add to the main tab window.
    /// </summary>
    public interface IMainTabWindowOverview
    {
        void PreOpenWindow(FactionFC faction);
        void OnTabSwitch();
        void DrawOverviewTab(Rect boundingBox);
        void PostCloseWindow();
        string TabName();
    }
}
