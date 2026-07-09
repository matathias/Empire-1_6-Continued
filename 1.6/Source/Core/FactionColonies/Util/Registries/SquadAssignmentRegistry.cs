using System.Collections.Generic;

namespace FactionColonies
{
    public static class SquadAssignmentRegistry
    {
        private static readonly RegistryList<ISquadAssignmentValidator> _list = new RegistryList<ISquadAssignmentValidator>();

        internal static void Register(ISquadAssignmentValidator validator) => _list.Register(validator);
        internal static void Unregister(ISquadAssignmentValidator validator) => _list.Unregister(validator);
        internal static void ClearAll() => _list.ClearAll();
        public static IReadOnlyList<ISquadAssignmentValidator> Validators => _list.Items;

        /// <summary>
        /// Returns true if all registered validators allow the assignment.
        /// On first rejection, outputs the reason string.
        /// <para>Fail-open: a validator that throws is logged and skipped, never blocking the
        /// assignment — a buggy submod validator cannot brick base squad assignment.</para>
        /// <para>The <c>out</c> parameter on <see cref="ISquadAssignmentValidator.CanAssign"/>
        /// can't flow through an <c>Action&lt;T&gt;</c> closure cleanly, so the rejection
        /// reason is captured to a local and copied out at the end.</para>
        /// </summary>
        public static bool CanAssign(WorldSettlementFC settlement, MercenarySquadFC squad, out string reason)
        {
            string captured = null;
            bool allowed = RegistryDispatch.All(_list.Items, v =>
            {
                bool ok = v.CanAssign(settlement, squad, out string r);
                if (!ok) captured = r;
                return ok;
            }, nameof(ISquadAssignmentValidator.CanAssign));
            reason = captured;
            return allowed;
        }
    }
}
