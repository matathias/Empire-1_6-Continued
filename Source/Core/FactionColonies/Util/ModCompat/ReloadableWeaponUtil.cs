using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Loads a pawn's reloadable-weapon magazines from the ammo it already carries, at deploy time.
    ///
    /// Ammo mods such as Yayo's Combat attach the vanilla <see cref="CompApparelReloadable"/> to ranged
    /// weapons. A weapon at 0 charges reports <c>Verb.Available() == false</c> and cannot fire. Those mods
    /// set the initial charges during <c>PawnGenerator.GenerateGearFor</c>, but Empire equips mercs AFTER
    /// generation (see <see cref="SquadEquipmentTracker"/>), so a merc's weapon is never seen by that hook
    /// and enters combat empty. This tops the magazine up from the pawn's carried ammo using the vanilla
    /// <see cref="CompApparelReloadable.ReloadFrom"/> primitive, so the supplied ammo is actually used and
    /// the weapon can fire.
    ///
    /// Uses only vanilla members, so it no-ops cleanly when no ammo mod is present (TryGetComp returns null).
    /// Deliberately does not touch runtime reload behaviour — this is a one-time load on deployment.
    /// </summary>
    public static class ReloadableWeaponUtil
    {
        public static void LoadMagazinesFromInventory(Pawn pawn)
        {
            // ReloadFrom plays a sound at the wearer's position, so the pawn must be on a map.
            if (pawn?.equipment is null || !pawn.Spawned)
            {
                return;
            }

            ThingOwner<Thing> inventory = pawn.inventory?.innerContainer;
            if (inventory is null)
            {
                return;
            }

            foreach (ThingWithComps weapon in pawn.equipment.AllEquipmentListForReading)
            {
                CompApparelReloadable comp = weapon.TryGetComp<CompApparelReloadable>();
                if (comp?.AmmoDef is null || !comp.NeedsReload(true))
                {
                    continue;
                }

                // Snapshot: ReloadFrom consumes (SplitOff + Destroy) from the container as it loads.
                foreach (Thing ammo in inventory.ToList())
                {
                    if (ammo is null || ammo.def != comp.AmmoDef)
                    {
                        continue;
                    }

                    comp.ReloadFrom(ammo);
                    if (!comp.NeedsReload(true))
                    {
                        break;
                    }
                }
            }
        }
    }
}
