using UnityEngine;

namespace FactionColonies
{
    /// <summary>
    /// Allows DefModExtensions on <see cref="BuildingFCDef"/> to contribute additional sections
    /// to the building detail panel in FCBuildingWindow. Sections render between the Modifiers
    /// block and the Settlement Impact block.
    /// <para>Implement on a <see cref="Verse.DefModExtension"/> attached to a <see cref="BuildingFCDef"/>.
    /// The window discovers implementors via <c>def.modExtensions.OfType&lt;IBuildingDetailSection&gt;()</c>.</para>
    /// </summary>
    public interface IBuildingDetailSection
    {
        /// <summary>Header label for the section (rendered by the caller in standard style).</summary>
        string SectionLabel { get; }

        /// <summary>
        /// Total height needed for section content (excluding the header). Return 0 to hide the section entirely.
        /// </summary>
        float GetSectionHeight(BuildingFCDef def, float width);

        /// <summary>
        /// Draws section content into <paramref name="contentRect"/>. The header is drawn by the caller;
        /// implementors only draw below it.
        /// </summary>
        void DrawSection(BuildingFCDef def, Rect contentRect);

        /// <summary>
        /// Short description appended to building card text in the left panel and tooltips.
        /// Return null or empty to add nothing.
        /// </summary>
        string GetCardDescription(BuildingFCDef def);
    }
}
