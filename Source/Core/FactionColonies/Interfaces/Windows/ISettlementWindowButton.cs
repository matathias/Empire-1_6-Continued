namespace FactionColonies
{
    /// <summary>
    /// Allows submods to add buttons to the settlement window's left panel.
    /// Buttons are drawn uniformly as text buttons between the built-in buttons and the Delete button.
    /// Register implementations via <see cref="SettlementButtonRegistry"/>.
    /// </summary>
    public interface ISettlementWindowButton
    {
        /// <summary>
        /// Returns the translated label to display on the button.
        /// Called every frame, so dynamic labels (e.g., with counts) are supported.
        /// </summary>
        string Label(WorldSettlementFC settlement);

        /// <summary>
        /// Called when the button is clicked. Open windows, show float menus, etc.
        /// </summary>
        void OnClick(WorldSettlementFC settlement);

        /// <summary>
        /// Returns true if the button should be enabled (clickable). Disabled buttons
        /// are drawn grayed out. Called every frame.
        /// </summary>
        bool IsEnabled(WorldSettlementFC settlement);

        /// <summary>
        /// Returns true if the button should be visible at all for this settlement.
        /// Return false to hide the button entirely (it takes no space).
        /// Called every frame.
        /// </summary>
        bool IsVisible(WorldSettlementFC settlement);
    }
}
