using UnityEngine;

namespace FactionColonies
{
    /// <summary>
    /// Defines an interface that WorldObjectComps can implement in order to add a new overview tab to the settlement window.
    /// <para>This must be implemented by a WorldObjectComp. It will not be invoked otherwise.</para>
    /// </summary>
    public interface ISettlementWindowOverview
    {
        void PreOpenWindow(WorldSettlementFC settlement);
        void OnTabSwitch();
        void DrawOverviewTab(Rect boundingBox);
        void PostCloseWindow();
        string OverviewTabName();

        /// <summary>
        /// Whether this overview's tab should be shown for the given settlement. Evaluated once when the
        /// settlement window opens; return false to hide the tab entirely (e.g. when the providing feature
        /// is disabled). Implementers that always want the tab shown should return true.
        /// </summary>
        bool ShouldShowOverviewTab(WorldSettlementFC settlement);
    }
}
