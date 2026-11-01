using UnityEngine;

namespace FactionColonies
{
    /// <summary>
    /// Allows submods to add sections to the squad inspection window (below the per-pawn rows).
    /// Register implementations via <see cref="SquadInspectionRegistry"/>.
    /// </summary>
    public interface ISquadInspectionSection
    {
        /// <summary>Header label for the section.</summary>
        string SectionLabel { get; }

        /// <summary>Total height needed for the section (excluding the header drawn by the caller).
        /// Return 0 to hide the section entirely.</summary>
        float GetSectionHeight(MercenarySquadFC squad, float width);

        /// <summary>Draws the section content into <paramref name="contentRect"/>. The header is
        /// drawn by the caller — only draw below it.</summary>
        void DrawSection(MercenarySquadFC squad, Rect contentRect);

        /// <summary>Lower values render earlier; ties broken by registration order.</summary>
        int Order { get; }
    }
}
