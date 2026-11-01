using RimWorld;
using System.Collections.Generic;
using Verse;

namespace FactionColonies
{
    /* Registry of IMercAutoTendProvider implementations. Walked once per off-map merc per
     * hourly heal tick to determine the tending doctor (if any) and the medicine ThingDef.
     * Both consumer methods are self-contained — the medicine pick seeds with the tech-level
     * default internally, so callers only ever invoke PickMedicine, never PickDefaultMedicine. */
    public static class MercAutoTendRegistry
    {
        private static readonly RegistryList<IMercAutoTendProvider> _list = new RegistryList<IMercAutoTendProvider>();

        internal static void Register(IMercAutoTendProvider provider) => _list.Register(provider);
        internal static void Unregister(IMercAutoTendProvider provider) => _list.Unregister(provider);
        internal static void ClearAll() => _list.ClearAll();
        public static IReadOnlyList<IMercAutoTendProvider> Providers => _list.Items;

        /// <summary>
        /// First non-null doctor wins. Returns null if no provider supplies one.
        /// </summary>
        public static Pawn PickDoctor(Mercenary patient, WorldSettlementFC settlement)
        {
            Pawn doctor = null;
            RegistryDispatch.First(_list.Items, t =>
            {
                Pawn d = t.ProvideTendingDoctor(patient, settlement);
                if (d is object) { doctor = d; return true; }
                return false;
            }, nameof(IMercAutoTendProvider.ProvideTendingDoctor));
            return doctor;
        }

        /// <summary>
        /// Seeds with the tech-level default, then chains through every provider's override.
        /// Each provider sees the running choice and may pass it through or replace it.
        /// </summary>
        public static ThingDef PickMedicine(Mercenary patient, WorldSettlementFC settlement)
        {
            ThingDef current = PickDefaultMedicine();
            RegistryDispatch.Each(_list.Items,
                t => current = t.OverrideTendingMedicine(patient, settlement, current),
                nameof(IMercAutoTendProvider.OverrideTendingMedicine));
            return current;
        }

        /* Tech-level -> default medicine. Maps the player colony faction's def techLevel to
         * the abstracted medicine quality used for tending. Only Ultra/Archotech factions get access to
         * glitterworld medicine. */
        private static ThingDef PickDefaultMedicine()
        {
            Faction faction = FindFC.EmpireFaction;
            TechLevel tech = faction?.def?.techLevel ?? TechLevel.Industrial;
            switch (tech)
            {
                case TechLevel.Animal:
                    return null;
                case TechLevel.Neolithic:
                case TechLevel.Medieval:
                    return ThingDefOf.MedicineHerbal;
                case TechLevel.Industrial:
                case TechLevel.Spacer:
                    return ThingDefOf.MedicineIndustrial;
                case TechLevel.Ultra:
                case TechLevel.Archotech:
                    return ThingDefOf.MedicineUltratech;
                default:
                    return ThingDefOf.MedicineIndustrial;
            }
        }
    }
}
