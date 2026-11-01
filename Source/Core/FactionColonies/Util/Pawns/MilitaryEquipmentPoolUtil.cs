using System.Collections.Generic;
using System.Linq;
using FactionColonies.util;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Session-permanent pools of weapon/apparel ThingDefs that pass the research-agnostic static
    /// filters. The player-facing pickers scan these instead of the whole DefDatabase on every open,
    /// which is what made the unit designer lag with many mods installed (thousands of ThingDefs).
    ///
    /// Contract mirrors <see cref="MilitaryInventoryUtil"/>: cache the static-predicate pool once and
    /// apply the live gates (research via <see cref="CraftUtil.CanCraftItem"/>, race via HAR, worn-set
    /// exclusion) per-open at the call sites. The cache holds no <c>Find.*</c>/research state and the
    /// DefDatabase is immutable at runtime, so it is never invalidated.
    /// </summary>
    public static class MilitaryEquipmentPoolUtil
    {
        private static List<ThingDef> weaponPoolCache;
        private static List<ThingDef> apparelPoolCache;

        /// <summary>Weapons passing the research-agnostic static filters, cached once per session. The
        /// research gate (CanCraftItem) and race gate (HARUtil.CanRaceUseWeapon) are applied live by
        /// callers.</summary>
        public static List<ThingDef> WeaponPool()
        {
            if (weaponPoolCache is object) return weaponPoolCache;

            weaponPoolCache = DefDatabase<ThingDef>.AllDefs
                .Where(t => t.IsWeapon
                    && t.BaseMarketValue != 0f
                    && t.generateAllowChance > 0f // blocks unique weapons
                    && !CraftUtil.WeaponBlockedForMercs(t))
                .ToList();
            return weaponPoolCache;
        }

        /// <summary>Apparel passing the research-agnostic static filters, cached once per session. The
        /// research gate (CanCraftItem), race gate (HARUtil.CanRaceWearApparel), and worn-set exclusion
        /// are applied live by callers.</summary>
        public static List<ThingDef> ApparelPool()
        {
            if (apparelPoolCache is object) return apparelPoolCache;

            apparelPoolCache = DefDatabase<ThingDef>.AllDefs
                .Where(t => t.IsApparel
                    && t.apparel.PawnCanWear(Gender.None, DevelopmentalStage.Adult))
                .ToList();
            return apparelPoolCache;
        }
    }
}
