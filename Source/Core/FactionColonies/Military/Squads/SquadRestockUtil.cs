using FactionColonies.util;
using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Post-battle reconciliation of a squad's carried inventory AND worn gear. After a <em>manual</em>
    /// battle or a deployment (real pawns on a real map who actually fired ammo / used meds / took drugs
    /// and took armor/weapon damage), for each surviving merc:
    /// <list type="bullet">
    ///   <item>consumed carried inventory is diffed against the loadout design, refilled, and billed;</item>
    ///   <item>damaged apparel/weapons are repaired to full HP and billed for the durability lost;</item>
    ///   <item>destroyed/missing design apparel/weapons are re-equipped and billed at full value.</item>
    /// </list>
    /// All of it sums into one "Squad restock" <see cref="BillFC"/> (scaled by
    /// <see cref="FCSettings.squadRestockCostPercentage"/>) with a single letter/message honoring
    /// <see cref="FCSettings.taxNotificationMode"/>.
    ///
    /// <para>Scope: <em>survivors</em> only. Auto-resolved battles never touch pawn inventory/gear (pure
    /// force-crunch), so the caller only invokes this for manual battles and deployments. Dead mercs
    /// (whole-loadout replacement) are a separate concern. Looted (non-design) items are neither billed
    /// nor removed, and a missing design item is treated as needing replacement regardless of whether a
    /// copy was dropped on the battle map (that cleanup is the separate RemoveDroppedEquipment path).</para>
    /// </summary>
    public static class SquadRestockUtil
    {
        /// <summary>One design item the pawn came up short on: what to refill and the per-unit value
        /// used to bill it. Emitted by the pure <see cref="ComputeConsumption"/> seam.</summary>
        public struct ConsumedItem
        {
            public ThingDef thing;
            public ThingDef stuff;
            public QualityCategory quality;
            public int count;
            public float perUnitValue;
        }

        /// <summary>One apparel/weapon currently on the pawn, in the abstract form the pure
        /// <see cref="ComputeEquipmentRestock"/> seam consumes. <see cref="handle"/> is the real
        /// <see cref="Thing"/> (so the orchestrator can repair it) or null in tests.</summary>
        public struct PresentItem
        {
            public ThingDef thing;
            public ThingDef stuff;
            public QualityCategory? quality;
            public int hitPoints;
            public int maxHitPoints;   // 0 when the item has no durability (never "repaired")
            public object handle;
        }

        /// <summary>Result of an apparel-or-weapon diff: the billed <see cref="value"/>, the live items
        /// to repair to full HP (<see cref="repairHandles"/>), and the design items to re-equip
        /// (<see cref="replacements"/>).</summary>
        public class EquipmentRestockResult
        {
            public double value;
            public List<object> repairHandles = new List<object>();
            public List<SavedThing> replacements = new List<SavedThing>();
        }

        // Quality used to key items that carry no quality (meds / ammo / drugs / food). Matches
        // SavedThing.MarketValue's own "quality ?? Normal" convention so design and pawn agree.
        private static ThingQualityTuple Key(ThingDef thing, ThingDef stuff, QualityCategory? quality) =>
            new ThingQualityTuple { thingDef = thing, stuffDef = stuff, quality = quality ?? QualityCategory.Normal };

        /// <summary>Pure diff: for each distinct item in <paramref name="design"/>, compares the wanted
        /// count against <paramref name="currentCount"/> (how many the pawn still carries) and returns
        /// the total market value consumed, appending each shortfall to <paramref name="refillOut"/>.
        /// Items the pawn has enough of contribute nothing; <paramref name="currentCount"/> is only
        /// queried for design items, so looted extras are inherently ignored. No pawn / game state
        /// touched — this is the testable core of <see cref="ReconcileAndBill"/>.</summary>
        public static double ComputeConsumption(
            List<SavedThing> design,
            Func<ThingQualityTuple, int> currentCount,
            List<ConsumedItem> refillOut)
        {
            if (design is null || currentCount is null) return 0;

            // Aggregate the design by item: total wanted count + per-unit market value.
            Dictionary<ThingQualityTuple, int> wanted = new Dictionary<ThingQualityTuple, int>();
            Dictionary<ThingQualityTuple, float> perUnit = new Dictionary<ThingQualityTuple, float>();
            foreach (SavedThing st in design)
            {
                if (st.thing is null) continue;
                ThingQualityTuple key = Key(st.thing, st.stuff, st.quality);
                int prev;
                wanted.TryGetValue(key, out prev);
                wanted[key] = prev + Mathf.Max(1, st.count);
                // SavedThing.MarketValue is count-scaled, so dividing yields the per-unit value.
                perUnit[key] = st.MarketValue / Mathf.Max(1, st.count);
            }

            double value = 0;
            foreach (KeyValuePair<ThingQualityTuple, int> kv in wanted)
            {
                int held = Mathf.Max(0, currentCount(kv.Key));
                int consumed = Mathf.Max(0, kv.Value - held);
                if (consumed <= 0) continue;

                float unit = perUnit[kv.Key];
                value += (double)consumed * unit;
                refillOut?.Add(new ConsumedItem
                {
                    thing = kv.Key.thingDef,
                    stuff = kv.Key.stuffDef,
                    quality = kv.Key.quality,
                    count = consumed,
                    perUnitValue = unit
                });
            }

            return value;
        }

        /// <summary>Pure diff for a single gear category (apparel OR weapons): matches each
        /// <paramref name="present"/> item against the <paramref name="design"/> by (def, stuff, quality),
        /// billing repairs for damaged matches and full value for design items that are missing
        /// (destroyed/lost). Each design entry is one item (count 1). Unmatched present items (looted)
        /// are ignored. No pawn / game state touched — the testable core of the gear path in
        /// <see cref="ReconcileAndBill"/>.</summary>
        public static EquipmentRestockResult ComputeEquipmentRestock(List<SavedThing> design, List<PresentItem> present)
        {
            EquipmentRestockResult result = new EquipmentRestockResult();
            if (design is null) return result;

            // Design as a key -> queue of entries (so each present item claims one, and leftovers
            // are the missing items to replace). Per-entry value is the full item market value.
            Dictionary<ThingQualityTuple, Queue<SavedThing>> wanted = new Dictionary<ThingQualityTuple, Queue<SavedThing>>();
            foreach (SavedThing st in design)
            {
                if (st.thing is null) continue;
                ThingQualityTuple key = Key(st.thing, st.stuff, st.quality);
                Queue<SavedThing> q;
                if (!wanted.TryGetValue(key, out q)) { q = new Queue<SavedThing>(); wanted[key] = q; }
                q.Enqueue(st);
            }

            if (present != null)
            {
                foreach (PresentItem pi in present)
                {
                    if (pi.thing is null) continue;
                    ThingQualityTuple key = Key(pi.thing, pi.stuff, pi.quality);
                    Queue<SavedThing> q;
                    if (!wanted.TryGetValue(key, out q) || q.Count == 0) continue; // looted / over-count: ignore

                    SavedThing st = q.Dequeue();
                    if (pi.maxHitPoints > 0 && pi.hitPoints < pi.maxHitPoints)
                    {
                        float fracLost = 1f - Mathf.Clamp01(pi.hitPoints / (float)pi.maxHitPoints);
                        result.value += st.MarketValue * fracLost;
                        if (pi.handle != null) result.repairHandles.Add(pi.handle);
                    }
                }
            }

            // Leftover design entries were never matched on the pawn -> destroyed / lost -> replace.
            foreach (KeyValuePair<ThingQualityTuple, Queue<SavedThing>> kv in wanted)
            {
                foreach (SavedThing st in kv.Value)
                {
                    result.value += st.MarketValue;
                    result.replacements.Add(st);
                }
            }

            return result;
        }

        /// <summary>Diffs every surviving merc's carried inventory AND worn gear against its design:
        /// refills consumed inventory, repairs damaged apparel/weapons, re-equips destroyed ones, and
        /// creates a single restock bill (plus a letter/message). Returns the silver billed (0 when
        /// nothing was consumed/damaged, the squad is empty, or godMode is on). Only call after a manual
        /// battle or a deployment.</summary>
        public static int ReconcileAndBill(MercenarySquadFC squad)
        {
            if (squad?.mercenaries is null || DebugSettings.godMode) return 0;

            double totalValue = 0;
            List<ConsumedItem> refill = new List<ConsumedItem>();

            foreach (Mercenary merc in squad.mercenaries)
            {
                if (merc is null || merc.IsEmptySlot) continue;
                Pawn pawn = merc.pawn;
                if (pawn is null || pawn.Dead) continue;

                MilUnitFC design = merc.EffectiveLoadout;
                if (design is null) continue;

                bool needsCeUpdate = false;

                // -- Carried inventory (ammo / meds / drugs): refill what was consumed. --
                if (pawn.inventory?.innerContainer != null && design.inventory != null && design.inventory.Count > 0)
                {
                    // Count of each carried item the pawn still has (built once, read by the lambda).
                    Dictionary<ThingQualityTuple, int> have = new Dictionary<ThingQualityTuple, int>();
                    foreach (Thing t in pawn.inventory.innerContainer)
                    {
                        if (t is null) continue;
                        QualityCategory? q = t.TryGetQuality(out QualityCategory qc) ? qc : (QualityCategory?)null;
                        ThingQualityTuple key = Key(t.def, t.Stuff, q);
                        int prev;
                        have.TryGetValue(key, out prev);
                        have[key] = prev + t.stackCount;
                    }

                    refill.Clear();
                    totalValue += ComputeConsumption(
                        design.inventory,
                        key =>
                        {
                            int c;
                            return have.TryGetValue(key, out c) ? c : 0;
                        },
                        refill);

                    if (refill.Count > 0)
                    {
                        foreach (ConsumedItem ci in refill)
                            SquadEquipmentTracker.AddInventoryThings(pawn, ci.thing, ci.stuff, ci.quality, ci.count);
                        needsCeUpdate = true;
                    }
                }

                // -- Worn apparel: repair damaged, re-equip destroyed/lost. --
                if (pawn.apparel != null && design.apparel != null && design.apparel.Count > 0)
                {
                    EquipmentRestockResult r = ComputeEquipmentRestock(design.apparel, BuildPresent(pawn.apparel.WornApparel));
                    totalValue += r.value;
                    ApplyRepairs(r.repairHandles);
                    foreach (SavedThing st in r.replacements)
                        squad.Equipment?.WearApparelItem(merc, st);
                    if (r.replacements.Count > 0) needsCeUpdate = true;
                }

                // -- Equipped weapons: repair damaged, re-equip destroyed/lost. --
                if (pawn.equipment != null && design.weapons != null && design.weapons.Count > 0)
                {
                    EquipmentRestockResult r = ComputeEquipmentRestock(design.weapons, BuildPresent(pawn.equipment.AllEquipmentListForReading));
                    totalValue += r.value;
                    ApplyRepairs(r.repairHandles);
                    foreach (SavedThing st in r.replacements)
                        squad.Equipment?.AddWeaponItem(merc, st);
                    if (r.replacements.Count > 0) needsCeUpdate = true;
                }

                // Let CE re-index ammo after any inventory refill / re-equip (mirrors EquipPawn).
                if (needsCeUpdate) CombatExtendedUtil.UpdateInventory(pawn);
            }

            int cost = (int)Math.Round(totalValue * FCSettings.squadRestockCostPercentage);
            if (cost <= 0) return 0;

            BillFC bill = FindFC.TaxLedger?.CreateRestockCostBill(squad, cost);
            if (bill is object) NotifyRestock(squad, cost);
            return cost;
        }

        /// <summary>Snapshots the pawn-side live items (apparel or weapons) into the abstract
        /// <see cref="PresentItem"/> form the pure seam consumes. <c>maxHitPoints</c> is 0 for items
        /// without durability so they're never billed for "repair".</summary>
        private static List<PresentItem> BuildPresent(IEnumerable<Thing> things)
        {
            List<PresentItem> list = new List<PresentItem>();
            if (things is null) return list;
            foreach (Thing t in things)
            {
                if (t is null) continue;
                QualityCategory? q = t.TryGetQuality(out QualityCategory qc) ? qc : (QualityCategory?)null;
                int maxHp = (t.def != null && t.def.useHitPoints) ? t.MaxHitPoints : 0;
                list.Add(new PresentItem
                {
                    thing = t.def,
                    stuff = t.Stuff,
                    quality = q,
                    hitPoints = t.HitPoints,
                    maxHitPoints = maxHp,
                    handle = t
                });
            }
            return list;
        }

        /// <summary>Repairs each handle (a live <see cref="Thing"/>) to full HP.</summary>
        private static void ApplyRepairs(List<object> handles)
        {
            if (handles is null) return;
            foreach (object h in handles)
            {
                if (h is Thing t && !t.Destroyed && t.def != null && t.def.useHitPoints)
                    t.HitPoints = t.MaxHitPoints;
            }
        }

        /// <summary>Announces the restock charge via letter and/or message per the player's
        /// <see cref="FCSettings.taxNotificationMode"/> (the same setting that gates tax delivery
        /// notifications). Called only when a bill was actually created.</summary>
        private static void NotifyRestock(MercenarySquadFC squad, int cost)
        {
            TaxNotificationMode mode = FCSettings.taxNotificationMode;
            if (mode == TaxNotificationMode.None) return;

            TaggedString text = "FCRestockBillNotification".Translate(
                squad?.DisplayName ?? "", cost, squad?.settlement?.Name ?? "");

            bool showLetter = mode == TaxNotificationMode.All || mode == TaxNotificationMode.LetterOnly;
            bool showMessage = mode == TaxNotificationMode.All || mode == TaxNotificationMode.MessageOnly;

            LookTargets target = squad?.settlement is object
                ? new LookTargets(squad.settlement)
                : null;

            if (showLetter)
            {
                TaggedString label = "FCRestockBillLetterLabel".Translate();
                if (target is object)
                    Find.LetterStack.ReceiveLetter(label, text, LetterDefOf.NeutralEvent, target);
                else
                    Find.LetterStack.ReceiveLetter(label, text, LetterDefOf.NeutralEvent);
            }

            if (showMessage)
            {
                if (target is object)
                    Messages.Message(text, target, MessageTypeDefOf.NeutralEvent);
                else
                    Messages.Message(text, MessageTypeDefOf.NeutralEvent);
            }
        }
    }
}
