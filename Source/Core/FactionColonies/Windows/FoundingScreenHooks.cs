using System;
using System.Collections.Generic;
using RimWorld.Planet;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Replacement for the Found-screen "Settle" button: a label plus the action to run when clicked.
    /// </summary>
    public class FoundingButtonOverride
    {
        public string Label;
        public Action OnClick;
    }

    /// <summary>
    /// Small extensibility seams on the "Found New Settlement" screen (<see cref="CreateColonyWindowFc"/>).
    /// A submod can replace the Settle button (e.g. to require founding via outposts) and react to the
    /// selected tile/type changing so it can drive its own companion window.
    /// </summary>
    public static class FoundingScreenHooks
    {
        /// <summary>
        /// When set and returns non-null for the current (tile, type), the Found screen draws this
        /// button instead of the normal "Settle" button. Return null to keep the default button.
        /// </summary>
        public static Func<PlanetTile, WorldSettlementDef, FoundingButtonOverride> SettleButtonOverride;

        /// <summary>
        /// Fired whenever the Found screen's selected tile or settlement type changes, and once with
        /// (Invalid, null) when the screen closes. Lets a submod open/refresh/close a companion window.
        /// </summary>
        public static event Action<PlanetTile, WorldSettlementDef> SelectionChanged;

        public static FoundingButtonOverride GetSettleButtonOverride(PlanetTile tile, WorldSettlementDef type)
        {
            return SettleButtonOverride is object ? SettleButtonOverride(tile, type) : null;
        }

        public static void NotifySelectionChanged(PlanetTile tile, WorldSettlementDef type)
        {
            SelectionChanged?.Invoke(tile, type);
        }

        /// <summary>
        /// Lays out every open companion window (<see cref="IFoundingCompanionWindow"/>) in a horizontal
        /// cascade to the left of the main Found screen, so any number of submods coexist without
        /// overlapping. Driven each frame from the main window; companions must not position themselves.
        /// Lower <see cref="IFoundingCompanionWindow.CompanionOrder"/> sits closest to the main window.
        /// </summary>
        public static void ReflowCompanions()
        {
            CreateColonyWindowFc main = Find.WindowStack.WindowOfType<CreateColonyWindowFc>();
            if (main is null) return;

            const float gap = 10f;
            List<Window> companions = new List<Window>();
            foreach (Window w in Find.WindowStack.Windows)
                if (w is IFoundingCompanionWindow) companions.Add(w);

            companions.Sort((a, b) =>
            {
                int c = ((IFoundingCompanionWindow)a).CompanionOrder
                            .CompareTo(((IFoundingCompanionWindow)b).CompanionOrder);
                return c != 0 ? c : string.CompareOrdinal(a.GetType().Name, b.GetType().Name);
            });

            float rightEdge = main.windowRect.x;
            float top = main.windowRect.y;
            foreach (Window w in companions)
            {
                w.windowRect.x = rightEdge - w.windowRect.width - gap;
                w.windowRect.y = top;
                rightEdge = w.windowRect.x;
            }
        }
    }
}
