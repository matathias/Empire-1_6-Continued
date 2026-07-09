using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Shared loadout-upgrade math used by BOTH the per-pawn Upgrade button
    /// (<see cref="Dialog_SquadInspection"/>) and the bulk Upgrade All path
    /// (<see cref="SquadUpgradeUtil.UpgradeToTemplate"/>).
    /// Keeping the cost and equivalence logic in one place is load-bearing: the two callers
    /// must agree on what "needs upgrading" and "costs what" or the UI desyncs (the bug this
    /// class was extracted to fix).
    /// </summary>
    public static class LoadoutUpgradeUtil
    {
        /// <summary>Equipment-only market value for a loadout (apparel + weapons). Race base
        /// cost is excluded so a pawnKind mismatch between assigned and equipped snapshots
        /// doesn't leak a phantom race-cost diff (the pawn doesn't change race on an upgrade).</summary>
        public static double SumEquipmentCost(MilUnitFC unit)
        {
            if (unit is null) return 0;
            double total = 0;
            if (unit.apparel != null)
                foreach (SavedThing a in unit.apparel) total += a.MarketValue;
            if (unit.weapons != null)
                foreach (SavedThing w in unit.weapons) total += w.MarketValue;
            if (unit.inventory != null)
                foreach (SavedThing inv in unit.inventory) total += inv.MarketValue;
            if (unit.implants != null)
                foreach (SavedImplant im in unit.implants) total += MilUnitFC.ImplantCost(im);
            // Psycasts: psylink-level cost (owned by the active psycast system) + any explicitly-chosen
            // psycasts. Base game charges per psylink level; VPE charges per chosen psycast instead.
            total += PsycastSystemRegistry.Active?.PsylinkCost(unit.psylinkLevel) ?? 0;
            if (unit.psycasts != null)
                foreach (SavedPsycast a in unit.psycasts) total += MilUnitFC.PsycastCost(a);
            // Companion animals + mount: market value times count, mirroring the design-side cost in
            // MilUnitFC.UpdateEquipmentTotalCost so the upgrade diff is non-zero when animals/mount change.
            if (unit.animals != null)
                foreach (SavedAnimal a in unit.animals)
                    if (a.kind?.race != null)
                        total += Math.Floor(a.kind.race.BaseMarketValue * FCSettings.militaryAnimalCostMultiplier) * Math.Max(1, a.count);
            if (unit.mount?.race != null)
                total += Math.Floor(unit.mount.race.BaseMarketValue * FCSettings.militaryAnimalCostMultiplier);
            // Mechanitor: flat mechlink surcharge + each bonded mech's market value. Matches the
            // design-side cost in MilUnitFC.UpdateEquipmentTotalCost so the upgrade diff stays in sync.
            if (ModsConfig.BiotechActive && unit.IsMechanitorDesign)
            {
                total += FCSettings.militaryMechlinkCost;
                if (unit.mechs != null)
                    foreach (SavedMech m in unit.mechs)
                        if (m.kind?.race != null)
                            total += Math.Floor(m.kind.race.BaseMarketValue * FCSettings.militaryMechCostMultiplier) * Math.Max(1, m.count);
            }
            return total;
        }

        /// <summary>Unscaled, unrounded positive equipment-cost difference between a target
        /// loadout and the currently-equipped one. Zero when the target costs the same or
        /// less (the upgrade still applies — it's just free). Callers that need the player-
        /// facing silver amount use <see cref="UpgradeCostDiff"/>; the bulk planner sums the
        /// raw diff and scales once at the end to keep its total round-of-sum.</summary>
        public static double RawEquipmentDiff(MilUnitFC target, MilUnitFC current)
        {
            if (target is null) return 0;
            return Math.Max(0, SumEquipmentCost(target) - SumEquipmentCost(current));
        }

        /// <summary>Silver cost to bring <paramref name="current"/> in line with
        /// <paramref name="target"/> — <see cref="RawEquipmentDiff"/> scaled by
        /// <see cref="FCSettings.squadUpgradeCostMultiplier"/> and rounded.</summary>
        public static int UpgradeCostDiff(MilUnitFC target, MilUnitFC current)
        {
            return (int)Math.Round(RawEquipmentDiff(target, current) * FCSettings.squadUpgradeCostMultiplier);
        }

        /// <summary>True when <paramref name="target"/> differs from <paramref name="current"/>
        /// in any applied way (companion animals, mount, apparel set/stuff/quality/color, weapon). A
        /// null target means "nothing assigned" -> false; a null current with a real target -> true.</summary>
        public static bool LoadoutsDiffer(MilUnitFC target, MilUnitFC current)
        {
            if (target is null) return false;
            if (current is null) return true;
            if (target.mount != current.mount) return true;
            if (!AnimalsEquivalent(target.animals, current.animals)) return true;
            if (!ApparelEquivalent(target.apparel, current.apparel)) return true;
            if (!WeaponsEquivalent(target.weapons, current.weapons)) return true;
            if (!InventoryEquivalent(target.inventory, current.inventory)) return true;
            if (!ImplantsEquivalent(target.implants, current.implants)) return true;
            if (!PsycastsEquivalent(target, current)) return true;
            if (MechanitorChanged(target, current)) return true;
            return false;
        }

        /// <summary>True when the mechanitor flag or the assigned-mech set differs between
        /// <paramref name="target"/> and <paramref name="current"/>. The upgrade paths run an
        /// in-place mech reconcile (<see cref="SquadEquipmentTracker.ReconcileMechs"/>) when this is
        /// true — destroying and rebuilding the merc's bonded mechs.</summary>
        public static bool MechanitorChanged(MilUnitFC target, MilUnitFC current)
        {
            if (target is null) return false;
            if (current is null) return target.IsMechanitorDesign;
            if (target.isMechanitor != current.isMechanitor) return true;
            if (!GroupWorkModesEquivalent(target.mechGroupWorkModes, current.mechGroupWorkModes)) return true;
            return !MechsEquivalent(target.mechs, current.mechs);
        }

        /* Order-independent equality over companion-animal rows (kind + count). */
        public static bool AnimalsEquivalent(List<SavedAnimal> a, List<SavedAnimal> b)
        {
            int an = a == null ? 0 : a.Count(x => x.kind != null);
            int bn = b == null ? 0 : b.Count(x => x.kind != null);
            if (an != bn) return false;
            if (an == 0) return true;
            List<string> sa = a.Where(x => x.kind != null).Select(x => x.kind.defName + "|" + Math.Max(1, x.count)).OrderBy(s => s).ToList();
            List<string> sb = b.Where(x => x.kind != null).Select(x => x.kind.defName + "|" + Math.Max(1, x.count)).OrderBy(s => s).ToList();
            for (int i = 0; i < an; i++)
                if (sa[i] != sb[i]) return false;
            return true;
        }

        /* Order-independent equality over mech rows (kind + count + group). */
        public static bool MechsEquivalent(List<SavedMech> a, List<SavedMech> b)
        {
            int an = a == null ? 0 : a.Count(x => x.kind != null);
            int bn = b == null ? 0 : b.Count(x => x.kind != null);
            if (an != bn) return false;
            if (an == 0) return true;
            List<string> sa = a.Where(x => x.kind != null).Select(x => x.kind.defName + "|" + Math.Max(1, x.count) + "|" + x.group).OrderBy(s => s).ToList();
            List<string> sb = b.Where(x => x.kind != null).Select(x => x.kind.defName + "|" + Math.Max(1, x.count) + "|" + x.group).OrderBy(s => s).ToList();
            for (int i = 0; i < an; i++)
                if (sa[i] != sb[i]) return false;
            return true;
        }

        /* Per-group work mode equality. Trailing entries that resolve to the default (Escort/null)
         * don't count as a difference, so a longer-but-equivalent list still matches. */
        private static bool GroupWorkModesEquivalent(List<MechWorkModeDef> a, List<MechWorkModeDef> b)
        {
            int n = Math.Max(a?.Count ?? 0, b?.Count ?? 0);
            for (int i = 0; i < n; i++)
            {
                MechWorkModeDef ma = (a != null && i < a.Count) ? a[i] : null;
                MechWorkModeDef mb = (b != null && i < b.Count) ? b[i] : null;
                if (ma != mb) return false;
            }
            return true;
        }

        /// <summary>True when the psylink level or chosen-psycast set differs between
        /// <paramref name="target"/> and <paramref name="current"/>. Like <see cref="ImplantsChanged"/>,
        /// the upgrade paths run an in-place psycast reconcile (<see cref="MilUnitFC.ReconcilePsycastsOnPawn"/>)
        /// when this is true — no pawn regeneration, identity preserved.</summary>
        public static bool PsycastsChanged(MilUnitFC target, MilUnitFC current)
        {
            if (target is null) return false;
            if (current is null) return true;
            return !PsycastsEquivalent(target, current);
        }

        /* Equal when both the psylink level and the (order-independent) chosen-psycast set match.
         * Base-game units store no psycasts, so for them this reduces to a psylink-level comparison. */
        public static bool PsycastsEquivalent(MilUnitFC a, MilUnitFC b)
        {
            int al = a?.psylinkLevel ?? 0;
            int bl = b?.psylinkLevel ?? 0;
            if (al != bl) return false;
            return PsycastsEquivalent(a?.psycasts, b?.psycasts);
        }

        private static bool PsycastsEquivalent(List<SavedPsycast> a, List<SavedPsycast> b)
        {
            int an = a?.Count ?? 0;
            int bn = b?.Count ?? 0;
            if (an != bn) return false;
            if (an == 0) return true;
            // Key on every meaningful field so a changed focus or stat-point count (not just a
            // changed psycast) registers as a difference and offers an upgrade.
            List<string> sa = a.Select(x => x.systemKey + "|" + x.kind + "|" + x.psycastDef + "|" + x.count).OrderBy(s => s).ToList();
            List<string> sb = b.Select(x => x.systemKey + "|" + x.kind + "|" + x.psycastDef + "|" + x.count).OrderBy(s => s).ToList();
            for (int i = 0; i < an; i++)
                if (sa[i] != sb[i]) return false;
            return true;
        }

        /// <summary>True when the implant set differs between <paramref name="target"/> and
        /// <paramref name="current"/>. Re-equipping (apparel/weapons/inventory) doesn't touch
        /// surgically-applied implants, so the upgrade paths additionally run an in-place implant
        /// reconcile (<see cref="MilUnitFC.ReconcileImplantsOnPawn"/>) when this is true — no pawn
        /// regeneration, identity preserved.</summary>
        public static bool ImplantsChanged(MilUnitFC target, MilUnitFC current)
        {
            if (target is null) return false;
            if (current is null) return true;
            return !ImplantsEquivalent(target.implants, current.implants);
        }

        /* Order-independent equality over inventory rows (thing + stuff + quality + count). */
        public static bool InventoryEquivalent(List<SavedThing> a, List<SavedThing> b)
        {
            int an = a == null ? 0 : a.Count(x => x.thing != null);
            int bn = b == null ? 0 : b.Count(x => x.thing != null);
            if (an != bn) return false;
            if (an == 0) return true;
            // Sort on a fully-discriminating key (every field SavedThingEquivalent inspects, incl. count),
            // so same-def rows differing only in stuff/quality/count still land in matching positions and
            // the positional pairing below is valid.
            List<SavedThing> sa = a.Where(x => x.thing != null).OrderBy(SavedThingSortKey, StringComparer.Ordinal).ToList();
            List<SavedThing> sb = b.Where(x => x.thing != null).OrderBy(SavedThingSortKey, StringComparer.Ordinal).ToList();
            for (int i = 0; i < an; i++)
            {
                if (!SavedThingEquivalent(sa[i], sb[i])) return false;
                if (sa[i].count != sb[i].count) return false;
            }
            return true;
        }

        /* Order-independent equality over implants (install source + body part + occurrence index).
         * Covers both surgery (recipe) and self-install (selfInstallThing) entries — a leveled
         * self-install implant appears once per level, so count matters and is captured by the key. */
        public static bool ImplantsEquivalent(List<SavedImplant> a, List<SavedImplant> b)
        {
            int an = a == null ? 0 : a.Count(x => x.IsValid);
            int bn = b == null ? 0 : b.Count(x => x.IsValid);
            if (an != bn) return false;
            if (an == 0) return true;
            List<string> sa = a.Where(x => x.IsValid).Select(ImplantKey).OrderBy(s => s).ToList();
            List<string> sb = b.Where(x => x.IsValid).Select(ImplantKey).OrderBy(s => s).ToList();
            for (int i = 0; i < an; i++)
                if (sa[i] != sb[i]) return false;
            return true;
        }

        private static string ImplantKey(SavedImplant im)
        {
            string src = im.recipe?.defName ?? im.selfInstallThing?.defName ?? "";
            return src + "|" + (im.bodyPart?.defName ?? "") + "|" + im.bodyPartIndex;
        }

        public static bool ApparelEquivalent(List<SavedThing> a, List<SavedThing> b)
        {
            int an = a == null ? 0 : a.Count(x => x.thing != null);
            int bn = b == null ? 0 : b.Count(x => x.thing != null);
            if (an != bn) return false;
            if (an == 0) return true;
            // Fully-discriminating sort key (see InventoryEquivalent): same-def apparel differing only in
            // stuff/quality/color must sort into matching positions for the positional pairing to hold.
            List<SavedThing> sa = a.Where(x => x.thing != null).OrderBy(SavedThingSortKey, StringComparer.Ordinal).ToList();
            List<SavedThing> sb = b.Where(x => x.thing != null).OrderBy(SavedThingSortKey, StringComparer.Ordinal).ToList();
            for (int i = 0; i < an; i++)
            {
                if (!SavedThingEquivalent(sa[i], sb[i])) return false;
            }
            return true;
        }

        /* Compares only the first non-null weapon in each list — matches the rest of the
         * military system, which treats a unit as single-weapon. Kept deliberately as-is so
         * the per-pawn and bulk paths share identical semantics. */
        public static bool WeaponsEquivalent(List<SavedThing> a, List<SavedThing> b)
        {
            SavedThing? wa = a == null ? (SavedThing?)null : a.Where(x => x.thing != null).Select(x => (SavedThing?)x).FirstOrDefault();
            SavedThing? wb = b == null ? (SavedThing?)null : b.Where(x => x.thing != null).Select(x => (SavedThing?)x).FirstOrDefault();
            if (wa.HasValue != wb.HasValue) return false;
            if (!wa.HasValue) return true;
            return SavedThingEquivalent(wa.Value, wb.Value);
        }

        /* Canonical string key over every field SavedThingEquivalent compares, so an OrderBy on it
         * yields identical ordering for equal multisets. Color is only included when hasColor, matching
         * SavedThingEquivalent's color check. */
        private static string SavedThingSortKey(SavedThing t)
        {
            string q = t.quality.HasValue ? ((int)t.quality.Value).ToString() : "-1";
            string col = t.hasColor ? ("|" + t.color.r + "," + t.color.g + "," + t.color.b + "," + t.color.a) : "|-";
            return (t.thing?.defName ?? "") + "|" + (t.stuff?.defName ?? "") + "|" + q + "|"
                + (t.hasColor ? "1" : "0") + col + "|" + t.count;
        }

        public static bool SavedThingEquivalent(SavedThing a, SavedThing b)
        {
            if (a.thing != b.thing) return false;
            if (a.stuff != b.stuff) return false;
            if (a.quality != b.quality) return false;
            if (a.hasColor != b.hasColor) return false;
            if (a.hasColor && a.color != b.color) return false;
            return true;
        }
    }
}
