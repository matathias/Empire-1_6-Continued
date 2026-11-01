using RimWorld;
using System.Collections.Generic;
using Verse;

namespace FactionColonies
{
    public class Mercenary : IExposable, ILoadReferenceable
    {
        //init variables
        /* Unit template pointer for display ("Marksman" template tag). Mutable from the
         * outside via DesignUnitsWindow — do NOT trust this for cost/gear queries.
         * Goes null only on unit template deletion. */
        public MilUnitFC loadout;
        /* "This pawn has been personalized" marker. Cloned from currentLoadout on
         * direct edit (Dialog_PawnLoadout) or unit template deletion. Cleared on per-pawn
         * template swap, bulk Upgrade All claim, or "Reset to unit template" action. */
        public MilUnitFC ownedLoadout;
        /* Truth of equipped gear right now. Always populated when the pawn is alive.
         * Re-cloned on every gear-touching event (hire / fill / upgrade / per-pawn
         * edit / reset). All cost / gear queries should read this, not loadout.
         * Assigning it re-seeds this merc's design-sourced stat modifiers from the new
         * loadout (unit scope) — the natural copy point at hire/fill/upgrade/edit. */
        private MilUnitFC _currentLoadout;
        public MilUnitFC currentLoadout
        {
            get => _currentLoadout;
            set { _currentLoadout = value; SyncDesignStatModifiers(value); }
        }
        public MercenarySquadFC squad;
        public WorldSettlementFC settlement;
        public Mercenary handler;
        /* Instance sub-pawns deep-owned by this merc. animals holds the merc's companion animals AND
         * its single rideable mount (distinguished by subPawnType: Animal vs Mount); mechs holds the
         * bonded mechanoids of a mechanitor merc. A wrapper with subPawnType != None and pawn == null
         * is "assigned but absent" (dead, awaiting paid replacement). See handler for the owning-merc
         * back-reference. */
        public List<Mercenary> animals = new List<Mercenary>();
        public List<Mercenary> mechs = new List<Mercenary>();
        public Pawn pawn;
        public int loadID;

        /// <summary>Discriminates a sub-pawn wrapper. None for top-level mercs. Mount is a companion
        /// animal the merc rides (Giddy Up 2); it lives in the animals list like a companion but is
        /// ridden instead of fighting on foot.</summary>
        public enum SubPawnType { None, Animal, Mech, Mount }
        /* Persisted sub-pawn identity. Lets a placeholder wrapper (pawn == null after death)
         * be recreated without reading a live pawn. */
        public SubPawnType subPawnType = SubPawnType.None;
        public PawnKindDef subPawnKind;          // animal/mount race OR mech kind
        public int subPawnMechGroup;             // mech control group (animals/mounts: 0)
        public MechWorkModeDef subPawnWorkMode;  // mech work mode (animals/mounts: null)

        /// <summary>A sub-pawn that needs paid replacement: it's assigned but its pawn is gone —
        /// never created, dead, or destroyed. Distinct from a downed/injured sub-pawn, which heals.
        /// Always false for a top-level merc (handled by the empty-slot path instead).</summary>
        public bool IsMissingSubPawn =>
            subPawnType != SubPawnType.None && (pawn is null || pawn.Dead || pawn.Destroyed);

        /// <summary>Live and placeholder sub-pawn wrappers owned by this merc (animals first, then mechs).</summary>
        public IEnumerable<Mercenary> SubPawns()
        {
            if (animals != null)
                foreach (Mercenary a in animals) if (a is object) yield return a;
            if (mechs != null)
                foreach (Mercenary m in mechs) if (m is object) yield return m;
        }

        /// <summary>Source tag for design modifiers copied from the loadout template.</summary>
        public const string DesignModifierSource = "__design";

        /// <summary>
        /// Effective per-unit stat modifiers read in combat/healing (unit scope). Holds a flattened copy of
        /// the loadout design's modifiers (sourceId == DesignModifierSource) plus any earned accolades.
        /// </summary>
        public List<PermanentStatModifier> statModifiers = new List<PermanentStatModifier>();

        public void AddStatModifier(PermanentStatModifier mod) { statModifiers.Add(mod); }
        public void RemoveStatModifiersBySource(string sourceId) { statModifiers.RemoveAll(m => m.sourceId == sourceId); }

        /// <summary>
        /// Re-seed design-sourced modifiers from the given loadout, preserving non-design (e.g. accolades, upgrades)
        /// entries. A null source keeps the existing copy.
        /// </summary>
        private void SyncDesignStatModifiers(MilUnitFC source)
        {
            if (source is null) return;
            if (statModifiers is null) statModifiers = new List<PermanentStatModifier>();
            statModifiers.RemoveAll(m => m.sourceId == DesignModifierSource);
            if (source.statModifiers != null)
            {
                foreach (PermanentStatModifier m in source.statModifiers)
                {
                    PermanentStatModifier copy = m.Clone();
                    copy.sourceId = DesignModifierSource;
                    statModifiers.Add(copy);
                }
            }
        }

        /// <summary>True when this merc has a real (non-blank) loadout assigned and is
        /// therefore eligible to be sent on a deployment. Derived — there is no field to
        /// scribe and no setter to forget. Returns false during early init when
        /// <c>blankUnit</c> isn't available yet (matches the legacy default).</summary>
        public bool deployable
        {
            get
            {
                MilUnitFC blank = FindFC.Military?.blankUnit;
                return blank != null && loadout != null && loadout != blank;
            }
        }

        /// <summary>Equipped truth — the gear actually on the pawn right now.
        /// Falls back to <see cref="BlueprintLoadout"/> if <see cref="currentLoadout"/>
        /// has not yet been populated (intermediate save shape, freshly created merc,
        /// empty slot).</summary>
        public MilUnitFC EffectiveLoadout => currentLoadout ?? BlueprintLoadout;

        /// <summary>Blueprint of last record — what gear the merc *should* have.
        /// <see cref="ownedLoadout"/> (personalization snapshot) supersedes
        /// <see cref="loadout"/> (pool reference). Use this for "what would Fill /
        /// Upgrade equip", not for "what's worn right now".</summary>
        public MilUnitFC BlueprintLoadout => ownedLoadout ?? loadout;
        /* True when the slot has no pawn — alive in the list as a placeholder for Fill. */
        public bool IsEmptySlot => pawn is null;
        // True when the pawn has another deep owner at save time (Map.mapPawns or
        // WorldPawns). Falls back to Scribe_References to avoid duplicate-id load
        // errors. Scribed under the legacy "isOnMap" key for back-compat with
        // older saves; defaults to false so old saves keep using Scribe_Deep.
        private bool isExternallyOwned = false;

        /// <summary>
        /// Extensible data dictionary for submods. Keyed by submod namespace to avoid collisions.
        /// Use <see cref="GetCustomData{T}"/>, <see cref="SetCustomData"/>, <see cref="RemoveCustomData"/>.
        /// </summary>
        private Dictionary<string, IExposable> customData;

        public Mercenary()
        {

        }

        public Mercenary(bool blank)
        {
            // Defensive: FindFC.Military can be null if this is constructed mid-load before
            // FactionFC's legacy-military migration has populated it. Fall back to 0 rather than NRE.
            MilitaryFC mil = FindFC.Military;
            loadID = mil is object ? mil.NextMercenaryId() : 0;
        }

        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                isExternallyOwned = pawn is object &&
                    (pawn.Map is object
                     || pawn.SpawnedOrAnyParentSpawned
                     || (Find.WorldPawns is object && Find.WorldPawns.Contains(pawn)));
            }

            Scribe_Values.Look(ref isExternallyOwned, "isOnMap", false);
            Scribe_References.Look(ref loadout, "loadout");
            Scribe_Deep.Look(ref ownedLoadout, "ownedLoadout");
            Scribe_Deep.Look(ref _currentLoadout, "currentLoadout");
            Scribe_Collections.Look(ref statModifiers, "statModifiers", LookMode.Deep);
            Scribe_References.Look(ref squad, "squad");
            Scribe_References.Look(ref settlement, "settlement");
            Scribe_References.Look(ref handler, "handler");
            // Sub-pawns are now deep-owned by the merc (was a Scribe_References "animal" pointer
            // into the squad's animal list; that legacy data is migrated by MercenarySquadFC).
            Scribe_Collections.Look(ref animals, "animals", LookMode.Deep);
            Scribe_Collections.Look(ref mechs, "mechs", LookMode.Deep);
            Scribe_Values.Look(ref subPawnType, "subPawnType", SubPawnType.None);
            Scribe_Defs.Look(ref subPawnKind, "subPawnKind");
            Scribe_Values.Look(ref subPawnMechGroup, "subPawnMechGroup", 0);
            Scribe_Defs.Look(ref subPawnWorkMode, "subPawnWorkMode");

            if (isExternallyOwned)
            {
                Scribe_References.Look(ref pawn, "pawn");
            }
            else
            {
                Scribe_Deep.Look(ref pawn, "pawn");
            }

            Scribe_Values.Look(ref loadID, "loadID");

            // Custom data — only expose if non-empty (backwards compatible with older saves)
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                bool hasData = customData != null && customData.Count > 0;
                Scribe_Values.Look(ref hasData, "hasCustomData", false);
                if (hasData)
                    Scribe_Collections.Look(ref customData, "customData", LookMode.Value, LookMode.Deep);
            }
            else
            {
                bool hasData = false;
                Scribe_Values.Look(ref hasData, "hasCustomData", false);
                if (hasData)
                    Scribe_Collections.Look(ref customData, "customData", LookMode.Value, LookMode.Deep);
            }

            if (Scribe.mode == LoadSaveMode.PostLoadInit && pawn != null && pawn.kindDef == null)
            {
                pawn.kindDef = PawnKindDefOf.Colonist;
                LogUtil.Warning($"Mercenary pawn {pawn.LabelShort} had null kindDef on load, reset to Colonist.");
            }

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (statModifiers is null) statModifiers = new List<PermanentStatModifier>();
                if (animals is null) animals = new List<Mercenary>();
                if (mechs is null) mechs = new List<Mercenary>();

                /* Identity backfill for legacy sub-pawns (saved before subPawnType existed). The only
                 * sub-pawn type that ever shipped is the companion animal, so any pre-existing wrapper
                 * with a handler is an animal; capture its kind so it can survive a future death as a
                 * placeholder. (Mechanitor handling never went live, so there are no legacy mechs.) */
                if (subPawnType == SubPawnType.None && handler is object)
                {
                    subPawnType = SubPawnType.Animal;
                    if (subPawnKind is null && pawn is object) subPawnKind = pawn.kindDef;
                }
                /* currentLoadout migration. Three save shapes:
                 *   1. Pre-refactor saves: only loadout (ref) populated.
                 *   2. Intermediate-refactor saves: loadout + ownedLoadout, no currentLoadout.
                 *   3. Post-refactor saves: currentLoadout already populated.
                 * Adopt ownedLoadout when present (it was the truth in shape 2), else
                 * clone the unit template for shape 1. Mercs without a pawn or loadout
                 * stay null (empty slot or fresh-created). */
                if (currentLoadout is null)
                {
                    if (ownedLoadout != null)
                        currentLoadout = ownedLoadout;
                    else if (loadout != null)
                        currentLoadout = loadout.Clone();
                }
            }
        }

        public T GetCustomData<T>(string key) where T : class, IExposable
        {
            if (customData != null && customData.TryGetValue(key, out IExposable val))
                return val as T;
            return null;
        }

        public void SetCustomData(string key, IExposable data)
        {
            if (customData is null) customData = new Dictionary<string, IExposable>();
            customData[key] = data;
        }

        public void RemoveCustomData(string key)
        {
            customData?.Remove(key);
        }

        public string GetUniqueLoadID()
        {
            return "Mercenary_" + loadID;
        }
    }
}