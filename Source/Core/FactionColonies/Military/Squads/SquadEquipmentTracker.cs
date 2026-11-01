using FactionColonies.util;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Per-squad equipment tracker. Owns the lists of weapons / apparel currently
    /// in use by the squad's mercenaries (the post-battle cleanup uses these to
    /// destroy gear that fell off non-player pawns) and the equip / strip / outfit
    /// methods that mutate them. Equip-time list snapshotting is internal: callers
    /// just call EquipPawn and the tracker appends the worn items.
    /// </summary>
    public class SquadEquipmentTracker : IExposable
    {
        protected MercenarySquadFC squad;

        public List<ThingWithComps> UsedWeaponList;
        public List<Apparel> UsedApparelList;

        public SquadEquipmentTracker() { }

        public SquadEquipmentTracker(MercenarySquadFC squad)
        {
            this.squad = squad;
            UsedWeaponList = new List<ThingWithComps>();
            UsedApparelList = new List<Apparel>();
        }

        public void ExposeData()
        {
            Scribe_Collections.Look(ref UsedWeaponList, "UsedWeaponList", LookMode.Reference);
            Scribe_Collections.Look(ref UsedApparelList, "UsedApparelList", LookMode.Reference);
        }

        /// <summary>Re-equips every mercenary slot from <paramref name="outfit"/>'s units,
        /// generating fresh pawns for empty / mismatched slots and stripping/re-applying gear
        /// on existing pawns. Called only from explicit player actions: <see cref="MercenarySquadFC.InitiateSquad"/>
        /// at hire, <see cref="SquadUpgradeUtil.UpgradeToTemplate"/>, <see cref="MercenarySquadFC.FillEmptySlots"/>,
        /// and the per-pawn editor in <c>Dialog_PawnLoadout</c>.</summary>
        public virtual void OutfitSquad(MilSquadFC outfit)
        {
            if (squad is null) return;
            int count = 0;
            squad.outfit = outfit;
            UsedWeaponList = new List<ThingWithComps>();
            UsedApparelList = new List<Apparel>();
            foreach (MilUnitFC loadout in outfit.Units)
            {
                try
                {
                    if (loadout == null)
                    {
                        count++;
                        continue;
                    }

                    // Ensure we have enough mercenaries in the list
                    while (squad.mercenaries.Count <= count)
                    {
                        Mercenary newMerc = new Mercenary(true);
                        MercenaryPawnFactory.CreateNewPawn(squad, ref newMerc, loadout?.pawnKind, loadout?.xenotype, loadout?.customXenotypeName, loadout);
                        if (newMerc?.pawn != null)
                        {
                            squad.mercenaries.Add(newMerc);
                        }
                        else
                        {
                            LogUtil.Warning($"Could not create mercenary for slot {count}.");
                            break;
                        }
                    }

                    // Skip if we still don't have enough mercenaries
                    if (count >= squad.mercenaries.Count || squad.mercenaries[count]?.pawn == null)
                    {
                        LogUtil.Warning($"Skipping outfit slot {count} - no valid mercenary available.");
                        count++;
                        continue;
                    }

                    if (squad.mercenaries[count].pawn.kindDef != loadout.pawnKind || squad.mercenaries[count].pawn.Dead)
                    {
                        Mercenary pawn = new Mercenary(true);
                        MercenaryPawnFactory.CreateNewPawn(squad, ref pawn, loadout.pawnKind, loadout.xenotype, loadout.customXenotypeName, loadout);
                        // Only replace if new pawn was successfully created
                        if (pawn?.pawn != null)
                        {
                            squad.mercenaries.Replace(squad.mercenaries[count], pawn);
                        }
                        else
                        {
                            LogUtil.Warning($"Failed to create replacement pawn for slot {count}.");
                        }
                    }

                    // Skip operations if pawn is null
                    if (squad.mercenaries[count]?.pawn == null)
                    {
                        count++;
                        continue;
                    }

                    StripPawn(squad.mercenaries[count]);
                    if (loadout != null)
                    {
                        EquipPawn(squad.mercenaries[count], loadout);
                        // Fresh outfit pass: clear any sub-pawns a reused merc still carries.
                        ClearSubPawns(squad.mercenaries[count]);
                        // Companion animals (+ mount, when Giddy Up 2 is active).
                        SpawnAnimalsFor(squad.mercenaries[count], loadout);

                        // Mechanitor: spawn and bond the unit's assigned mechs to this merc (which already
                        // had the mechlink applied in CreateNewPawn, so pawn.mechanitor exists).
                        SpawnMechsFor(squad.mercenaries[count], loadout);

                        squad.mercenaries[count].loadout = loadout;
                        // Sync currentLoadout with what we just equipped — clear any prior
                        // divergence since this is a fresh outfit pass.
                        squad.mercenaries[count].ownedLoadout = null;
                        squad.mercenaries[count].currentLoadout = loadout.Clone();
                    }
                }
                catch (Exception e)
                {
                    LogUtil.Error($"Something went wrong when outfitting a squad (slot {count}): {e}");
                    if (!squad.mercenaries.NullOrEmpty())
                    {
                        LogUtil.Error($"Number of Mercs: {squad.mercenaries.Count}, Any null pawn: {squad.mercenaries.Any(m => m?.pawn == null)}");
                    }
                }
                count++;
            }
            FindFC.Military?.RebuildMercenaryPawnSet();
        }

        /// <summary>Applies <paramref name="loadout"/>'s apparel + weapons to <paramref name="merc"/>
        /// and snapshots the worn items into <see cref="UsedWeaponList"/> / <see cref="UsedApparelList"/>
        /// so post-battle cleanup can identify and destroy dropped gear.</summary>
        public virtual void EquipPawn(Mercenary merc, MilUnitFC loadout)
        {
            if (merc?.pawn == null || loadout == null) return;

            // Ensure the snapshot lists exist even if this loadout has no apparel/weapons; the
            // per-item helpers below append the worn items as they go (see RemoveDroppedEquipment).
            if (UsedWeaponList == null) UsedWeaponList = new List<ThingWithComps>();
            if (UsedApparelList == null) UsedApparelList = new List<Apparel>();

            if (merc.pawn.apparel != null)
            {
                foreach (SavedThing apparelDef in loadout.apparel)
                    WearApparelItem(merc, apparelDef);
            }

            // Carried inventory — the unit's player-chosen loadout (including any ammo). Counts
            // above the item's stack limit are split across multiple stacks.
            if (merc.pawn.inventory?.innerContainer != null && loadout.inventory != null)
            {
                foreach (SavedThing invDef in loadout.inventory)
                    AddInventoryThings(merc.pawn, invDef.thing, invDef.stuff, invDef.quality, invDef.count);
            }

            if (merc.pawn.equipment != null)
            {
                foreach (SavedThing weaponDef in loadout.weapons)
                    AddWeaponItem(merc, weaponDef);
            }

            // Let CE re-index the pawn's inventory/ammo after we populated it.
            CombatExtendedUtil.UpdateInventory(merc.pawn);
        }

        /// <summary>Creates one apparel item from <paramref name="apparelDef"/> and wears it on
        /// <paramref name="merc"/> — resolving faction color, biocoding when antiExploit is on, and
        /// appending the worn item to <see cref="UsedApparelList"/>. Shared by <see cref="EquipPawn"/>
        /// (initial outfit) and <see cref="SquadRestockUtil"/> (post-battle replacement of destroyed
        /// apparel). No-op on a null pawn / apparel tracker or a non-apparel def.</summary>
        public void WearApparelItem(Mercenary merc, SavedThing apparelDef)
        {
            if (merc?.pawn?.apparel == null) return;
            Thing thing = apparelDef.CreateThing();
            if (!(thing is Apparel ap)) return;

            Color resolved = FindFC.FactionComp?.ResolveApparelColor(apparelDef) ?? Color.white;
            ap.SetColor(resolved, reportFailure: false);
            merc.pawn.apparel.Wear(ap);
            // Anti-exploit: biocode worn apparel to the merc so looted gear is unusable.
            // No-op on apparel without CompBiocodable.
            if (FCSettings.antiExploit)
                ap.TryGetComp<CompBiocodable>()?.CodeFor(merc.pawn);

            if (UsedApparelList == null) UsedApparelList = new List<Apparel>();
            UsedApparelList.Add(ap);
        }

        /// <summary>Creates one weapon from <paramref name="weaponDef"/> and equips it on
        /// <paramref name="merc"/> — biocoding when antiExploit is on, and appending it to
        /// <see cref="UsedWeaponList"/>. Shared by <see cref="EquipPawn"/> (initial outfit) and
        /// <see cref="SquadRestockUtil"/> (post-battle replacement of a destroyed weapon). No-op on a
        /// null pawn / equipment tracker or a non-ThingWithComps def.</summary>
        public void AddWeaponItem(Mercenary merc, SavedThing weaponDef)
        {
            if (merc?.pawn?.equipment == null) return;
            Thing weaponThing = weaponDef.CreateThing();
            if (!(weaponThing is ThingWithComps twc)) return;

            // Free the primary slot if something else holds it (e.g. a weapon looted mid-battle, when
            // re-equipping the merc's destroyed issued weapon) so AddEquipment doesn't error. In the
            // EquipPawn path StripPawn already cleared equipment, so this is a no-op there.
            if (twc.def.equipmentType == EquipmentType.Primary && merc.pawn.equipment.Primary is object)
                merc.pawn.equipment.DestroyEquipment(merc.pawn.equipment.Primary);

            merc.pawn.equipment.AddEquipment(twc);
            // Anti-exploit: biocode equipped weapons to the merc so looted gear is useless to
            // other pawns. No-op on weapons without CompBiocodable.
            if (FCSettings.antiExploit)
                twc.TryGetComp<CompBiocodable>()?.CodeFor(merc.pawn);

            if (UsedWeaponList == null) UsedWeaponList = new List<ThingWithComps>();
            UsedWeaponList.Add(twc);
        }

        /// <summary>Adds <paramref name="count"/> of (<paramref name="thing"/>, <paramref name="stuff"/>,
        /// <paramref name="quality"/>) to <paramref name="pawn"/>'s carried inventory, splitting counts above
        /// the item's stack limit across multiple stacks. Shared by <see cref="EquipPawn"/> (initial outfit)
        /// and <see cref="SquadRestockUtil"/> (post-battle refill of consumed items). No-op on a null thing /
        /// pawn / inventory container or non-positive count.</summary>
        public static void AddInventoryThings(Pawn pawn, ThingDef thing, ThingDef stuff, QualityCategory? quality, int count)
        {
            if (thing == null || pawn?.inventory?.innerContainer == null) return;
            int remaining = Mathf.Max(1, count);
            int stackLimit = Mathf.Max(1, thing.stackLimit);
            while (remaining > 0)
            {
                int take = Mathf.Min(remaining, stackLimit);
                Thing invThing = new SavedThing(thing, stuff, take, quality).CreateThing();
                if (invThing == null) break;
                pawn.inventory.innerContainer.TryAdd(invThing, true);
                remaining -= take;
            }
        }

        public virtual void StripPawn(Mercenary merc)
        {
            if (merc?.pawn == null) return;

            try
            {
                merc.pawn.apparel?.DestroyAll();
                merc.pawn.equipment?.DestroyAllEquipment();
                merc.pawn.inventory?.innerContainer?.ClearAndDestroyContents();
            }
            catch (Exception e)
            {
                LogUtil.Error($"Error stripping pawn equipment (mod conflict likely): {e}");
            }
            CombatExtendedUtil.UpdateInventory(merc.pawn);
        }

        public void StripSquad()
        {
            if (squad?.mercenaries is null) return;
            for (int count = 0; count < squad.mercenaries.Count && count < MilSquadFC.MaxSquadSize; count++)
            {
                if (squad.mercenaries[count]?.pawn != null)
                {
                    StripPawn(squad.mercenaries[count]);
                }
            }
        }

        /// <summary>Creates <paramref name="loadout"/>'s companion animals — and, when Giddy Up 2 is
        /// active, its single rideable mount — as sub-pawns of <paramref name="merc"/>. Mirrors
        /// <see cref="SpawnMechsFor"/>; assumes prior sub-pawns were already cleared (fresh outfit pass).
        /// The mount lives in the same animals list, tagged <see cref="Mercenary.SubPawnType.Mount"/>.</summary>
        public void SpawnAnimalsFor(Mercenary merc, MilUnitFC loadout)
        {
            if (squad is null || merc is null || loadout is null) return;
            if (merc.animals is null) merc.animals = new List<Mercenary>();

            if (loadout.animals != null)
            {
                foreach (SavedAnimal sa in loadout.animals)
                {
                    if (sa.kind is null) continue;
                    for (int n = 0; n < Mathf.Max(1, sa.count); n++)
                    {
                        Mercenary animal = new Mercenary(true);
                        MercenaryPawnFactory.CreateNewAnimal(squad, ref animal, sa.kind);
                        animal.handler = merc;
                        merc.animals.Add(animal);
                    }
                }
            }

            if (FactionCompat.GiddyUp2Active && loadout.mount is object)
            {
                Mercenary mount = new Mercenary(true);
                MercenaryPawnFactory.CreateNewAnimal(squad, ref mount, loadout.mount);
                mount.subPawnType = Mercenary.SubPawnType.Mount;   // CreateNewAnimal tags it Animal; override.
                mount.handler = merc;
                merc.animals.Add(mount);
            }
        }

        /// <summary>Syncs a merc's companion animals AND mount to <paramref name="target"/>'s design:
        /// keeps still-wanted live sub-pawns, destroys extras/wrong ones, and creates any missing —
        /// keeping <see cref="Mercenary.animals"/> consistent (no orphaned entries). Preserving matching
        /// live sub-pawns (rather than clear-and-rebuild like <see cref="ReconcileMechs"/>) avoids killing
        /// a healthy companion on an unrelated loadout change. Shared by the per-pawn Upgrade path and the
        /// bulk <see cref="SquadUpgradeUtil.UpgradeToTemplate"/>.</summary>
        public void ReconcileAnimal(Mercenary merc, MilUnitFC target)
        {
            if (merc is null || squad is null) return;
            if (merc.animals is null) merc.animals = new List<Mercenary>();

            ReconcileCompanions(merc, target);
            ReconcileMount(merc, target);
        }

        /* Reconciles the (kind -> count) multiset of companion animals (subPawnType == Animal). */
        private void ReconcileCompanions(Mercenary merc, MilUnitFC target)
        {
            Dictionary<PawnKindDef, int> wanted = new Dictionary<PawnKindDef, int>();
            if (target?.animals != null)
            {
                foreach (SavedAnimal a in target.animals)
                {
                    if (a.kind is null) continue;
                    int cur;
                    wanted.TryGetValue(a.kind, out cur);
                    wanted[a.kind] = cur + Mathf.Max(1, a.count);
                }
            }

            // Keep each live, still-wanted companion (decrementing its tally); destroy the rest.
            for (int i = merc.animals.Count - 1; i >= 0; i--)
            {
                Mercenary sub = merc.animals[i];
                if (sub is null || sub.subPawnType != Mercenary.SubPawnType.Animal) continue;

                PawnKindDef kind = sub.subPawnKind;
                int remaining;
                if (kind != null && sub.pawn != null && !sub.pawn.Destroyed
                    && wanted.TryGetValue(kind, out remaining) && remaining > 0)
                {
                    wanted[kind] = remaining - 1;
                }
                else
                {
                    if (sub.pawn != null && !sub.pawn.Destroyed) sub.pawn.Destroy();
                    merc.animals.RemoveAt(i);
                }
            }

            // Create any still-missing companions.
            foreach (KeyValuePair<PawnKindDef, int> kv in wanted)
            {
                for (int n = 0; n < kv.Value; n++)
                {
                    Mercenary animal = new Mercenary(true);
                    MercenaryPawnFactory.CreateNewAnimal(squad, ref animal, kv.Key);
                    animal.handler = merc;
                    merc.animals.Add(animal);
                }
            }
        }

        /* Reconciles the single mount (subPawnType == Mount). Only wanted when Giddy Up 2 is active. */
        private void ReconcileMount(Mercenary merc, MilUnitFC target)
        {
            PawnKindDef wanted = (FactionCompat.GiddyUp2Active && target != null) ? target.mount : null;

            Mercenary existing = null;
            for (int i = 0; i < merc.animals.Count; i++)
            {
                if (merc.animals[i] != null && merc.animals[i].subPawnType == Mercenary.SubPawnType.Mount)
                {
                    existing = merc.animals[i];
                    break;
                }
            }

            if (wanted is null)
            {
                if (existing != null)
                {
                    if (existing.pawn != null && !existing.pawn.Destroyed) existing.pawn.Destroy();
                    merc.animals.Remove(existing);
                }
                return;
            }

            // Correct mount already present and alive — leave it.
            if (existing != null && existing.pawn != null && existing.subPawnKind == wanted) return;

            if (existing != null)
            {
                if (existing.pawn != null && !existing.pawn.Destroyed) existing.pawn.Destroy();
                merc.animals.Remove(existing);
            }

            Mercenary mount = new Mercenary(true);
            MercenaryPawnFactory.CreateNewAnimal(squad, ref mount, wanted);
            mount.subPawnType = Mercenary.SubPawnType.Mount;
            mount.handler = merc;
            merc.animals.Add(mount);
        }

        /// <summary>Destroys and clears every sub-pawn (animals + mechs) of <paramref name="merc"/>.</summary>
        public void ClearSubPawns(Mercenary merc)
        {
            if (merc is null) return;
            if (merc.animals != null)
            {
                foreach (Mercenary a in merc.animals)
                    if (a?.pawn != null && !a.pawn.Destroyed) a.pawn.Destroy();
                merc.animals.Clear();
            }
            squad?.RemoveMechsFor(merc);
        }

        /// <summary>Creates and bonds every mech in <paramref name="loadout"/>'s mech list to
        /// <paramref name="mechanitor"/>, adding them to <see cref="Mercenary.mechs"/>. Assumes
        /// the mechanitor pawn already has its mechlink (applied in CreateNewPawn). No-op when Biotech
        /// is absent, the loadout isn't a mechanitor design, or the pawn isn't a mechanitor.</summary>
        public void SpawnMechsFor(Mercenary mechanitor, MilUnitFC loadout)
        {
            if (squad is null || mechanitor?.pawn is null || loadout is null) return;
            if (!ModsConfig.BiotechActive || !loadout.IsMechanitorDesign) return;
            // Ensure the mechlink is present (idempotent) so pawn.mechanitor exists even for a reused
            // pawn that predates this loadout becoming a mechanitor design.
            MilUnitFC.ApplyMechanitorToPawn(mechanitor.pawn, loadout);
            if (mechanitor.pawn.mechanitor is null) return;
            if (mechanitor.mechs is null) mechanitor.mechs = new List<Mercenary>();
            if (loadout.mechs is null) return;

            foreach (SavedMech sm in loadout.mechs)
            {
                if (sm.kind is null) continue;
                int group = Mathf.Max(0, sm.group);
                MechWorkModeDef workMode = loadout.GetGroupWorkMode(group);
                for (int n = 0; n < Mathf.Max(1, sm.count); n++)
                {
                    Mercenary mech = new Mercenary(true);
                    MercenaryPawnFactory.CreateNewMech(squad, ref mech, sm.kind, mechanitor.pawn, group, workMode);
                    if (mech.pawn != null)
                    {
                        mech.handler = mechanitor;
                        mechanitor.mechs.Add(mech);
                    }
                }
            }
        }

        /// <summary>Syncs a mechanitor merc's bonded mechs to <paramref name="target"/>'s mech design:
        /// destroys this merc's existing mechs and rebuilds from the design. Unlike <see cref="ReconcileAnimal"/>
        /// (a single companion), a mechanitor owns N mechs, so this clear-and-rebuilds — simple and correct
        /// since mech pawns are disposable battle pawns. Shared by the per-pawn Upgrade path and the bulk
        /// <see cref="SquadUpgradeUtil.UpgradeToTemplate"/>.</summary>
        public void ReconcileMechs(Mercenary merc, MilUnitFC target)
        {
            if (merc is null || squad is null) return;
            squad.RemoveMechsFor(merc);
            if (target != null && ModsConfig.BiotechActive && target.IsMechanitorDesign)
                SpawnMechsFor(merc, target);
        }

        public void RemoveDroppedEquipment()
        {
            if (UsedApparelList != null)
            {
                for (int i = UsedApparelList.Count - 1; i >= 0; i--)
                {
                    Apparel apparel = UsedApparelList[i];
                    // A reference-mode Scribe load can leave null entries (an apparel that wasn't saved /
                    // couldn't resolve). Drop them rather than deref ParentHolder on null.
                    if (apparel is null) { UsedApparelList.RemoveAt(i); continue; }
                    if (apparel.ParentHolder is Pawn_ApparelTracker tracker && tracker.pawn is object)
                    {
                        Pawn pawn = tracker.pawn;
                        if ((pawn.Faction == FindFC.EmpireFaction ||
                             pawn.Faction == Find.FactionManager.OfPlayer) && !pawn.Dead)
                            continue;
                    }
                    UsedApparelList.RemoveAt(i);
                    if (!apparel.Destroyed)
                        apparel.Destroy();
                }
            }

            if (UsedWeaponList != null)
            {
                for (int i = UsedWeaponList.Count - 1; i >= 0; i--)
                {
                    ThingWithComps weapon = UsedWeaponList[i];
                    if (weapon is null) { UsedWeaponList.RemoveAt(i); continue; }
                    if (weapon.ParentHolder is Pawn_EquipmentTracker tracker && tracker.pawn is object)
                    {
                        Pawn pawn = tracker.pawn;
                        if ((pawn.Faction == FindFC.EmpireFaction ||
                             pawn.Faction == Find.FactionManager.OfPlayer) && !pawn.Dead)
                            continue;
                    }
                    UsedWeaponList.RemoveAt(i);
                    if (!weapon.Destroyed)
                        weapon.Destroy();
                }
            }
        }

        /// <summary>Migration drain — adopts top-level UsedWeaponList / UsedApparelList from a
        /// pre-refactor save into this tracker. Called by <see cref="MercenarySquadFC.ExposeData"/>
        /// in PostLoadInit. Always-drains: passing nulls leaves the tracker's existing (empty) lists alone.</summary>
        internal void AdoptLegacyLists(List<ThingWithComps> weapons, List<Apparel> apparel)
        {
            if (weapons != null) UsedWeaponList = weapons;
            if (apparel != null) UsedApparelList = apparel;
        }
    }
}
