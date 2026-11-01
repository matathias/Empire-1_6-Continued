using System.Linq;
using FactionColonies.util;

namespace FactionColonies
{
    /// <summary>
    /// An <see cref="FCEventHandlerExtension"/> that, when its event fires, starts a situation — and
    /// optionally first resolves an existing one on the same target (a cross-situation transition, e.g.
    /// a crisis referendum that ends the crisis and starts an Autonomy situation on the same settlement).
    ///
    /// The target settlement is the event's first <c>settlementTraitLocations</c> entry, or null for a
    /// faction-scoped situation. Runs from the standard post-processing hook so it lands cleanly after
    /// the event's own resolution.
    /// </summary>
    public class FCEventHandlerExtension_StartSituation : FCEventHandlerExtension
    {
        public FCSituationDef situationToStart;
        public FCSituationDef situationToResolve;

        public override void OnEventTriggered(FCEvent evt)
        {
            FactionFC faction = FindFC.FactionComp;
            if (faction == null) return;

            FCSituationManager manager = faction.situationManager;
            if (manager == null) return;

            WorldSettlementFC target = evt?.settlementTraitLocations?.FirstOrDefault();

            if (situationToResolve != null)
            {
                FCSituation existing = manager.GetByDef(situationToResolve)
                    .FirstOrDefault(s => s.targetSettlement == target);
                if (existing != null) manager.Remove(existing);
            }

            if (situationToStart != null)
                manager.StartSituation(situationToStart, target);
        }
    }
}
