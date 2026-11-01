using UnityEngine;

namespace FactionColonies
{
    /// <summary>
    /// Interface for tabs in the <see cref="CodexWindow"/>. Each tab controls
    /// what gets drawn in the three-pane layout (left, center, right).
    /// </summary>
    public interface ICodexTab
    {
        /// <summary>
        /// The label shown on the tab button.
        /// </summary>
        string TabLabel { get; }

        /// <summary>
        /// Called when this tab becomes the active tab.
        /// </summary>
        void OnTabSelected();

        /// <summary>
        /// Called when this tab is no longer the active tab.
        /// </summary>
        void OnTabDeselected();

        /// <summary>
        /// Draws the left navigation pane (category tree, entry list, etc.).
        /// </summary>
        void DrawLeftPane(Rect rect);

        /// <summary>
        /// Draws the center content pane (entry details, descriptions, etc.).
        /// </summary>
        void DrawCenterPane(Rect rect);

        /// <summary>
        /// Draws the right info pane (banner, dynamic content, metadata, etc.).
        /// Only called if <see cref="HasRightPane"/> returns true.
        /// </summary>
        void DrawRightPane(Rect rect);

        /// <summary>
        /// Whether this tab uses the right pane. If false, the center pane
        /// expands to fill the remaining width.
        /// </summary>
        bool HasRightPane { get; }
    }
}
