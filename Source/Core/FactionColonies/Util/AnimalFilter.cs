using System.Collections.Generic;
using System.Linq;
using Verse;

namespace FactionColonies
{
    public class AnimalFilter : IExposable
    {
        private HashSet<PawnKindDef> allowedAnimals = new HashSet<PawnKindDef>();
        private bool _initialized;

        private List<PawnKindDef> _cachedAllowed = null;
        private List<PawnKindDef> _cachedAllowedCombat = null;
        private List<PawnKindDef> _cachedAllowedPack = null;

        public bool IsInitialized => _initialized;
        public int AllowedCount => allowedAnimals.Count;

        public List<PawnKindDef> AllowedAnimals
        {
            get
            {
                return _cachedAllowed ?? (_cachedAllowed =
                    FactionCache.AllAnimalKindDefs.Where(k => allowedAnimals.Contains(k)).ToList());
            }
        }

        public List<PawnKindDef> AllowedCombatAnimals
        {
            get
            {
                return _cachedAllowedCombat ?? (_cachedAllowedCombat =
                    FactionCache.AllCombatAnimalKindDefs.Where(k => allowedAnimals.Contains(k)).ToList());
            }
        }

        public List<PawnKindDef> AllowedPackAnimals
        {
            get
            {
                return _cachedAllowedPack ?? (_cachedAllowedPack =
                    FactionCache.AllPackAnimalKinds.Where(k => allowedAnimals.Contains(k)).ToList());
            }
        }

        public bool IsAllowed(PawnKindDef kind)
        {
            return allowedAnimals.Contains(kind);
        }

        public void SetAllowed(PawnKindDef kind, bool allowed)
        {
            if (allowed)
                allowedAnimals.Add(kind);
            else
                allowedAnimals.Remove(kind);
            InvalidateCache();
        }

        public void AllowAll()
        {
            allowedAnimals.Clear();
            allowedAnimals.AddRange(FactionCache.AllAnimalKindDefs);
            InvalidateCache();
        }

        public void DisallowAll()
        {
            allowedAnimals.Clear();
            InvalidateCache();
        }

        public void SetDefaults()
        {
            allowedAnimals.Clear();
            PawnKindDef[] defaults =
            {
                AnimalFilterDefOf.Husky, AnimalFilterDefOf.Wolf_Timber, AnimalFilterDefOf.Wolf_Arctic,
                AnimalFilterDefOf.Lynx, AnimalFilterDefOf.Cougar,
                AnimalFilterDefOf.Muffalo, AnimalFilterDefOf.Dromedary, AnimalFilterDefOf.Horse,
                AnimalFilterDefOf.Donkey, AnimalFilterDefOf.Elephant, AnimalFilterDefOf.Yak
            };
            HashSet<PawnKindDef> valid = new HashSet<PawnKindDef>(FactionCache.AllAnimalKindDefs);
            foreach (PawnKindDef def in defaults)
            {
                if (def is object && valid.Contains(def))
                    allowedAnimals.Add(def);
            }
            InvalidateCache();
        }

        public void FinalizeInit()
        {
            if (allowedAnimals.Count == 0)
            {
                SetDefaults();
            }
            if (AllowedPackAnimals.Count == 0)
            {
                allowedAnimals.AddRange(FactionCache.AllPackAnimalKinds);
            }
            _initialized = true;
            InvalidateCache();
        }

        /// <summary>
        /// Validates the filter state. If no animals are allowed, re-enables all.
        /// </summary>
        public void Validate()
        {
            if (allowedAnimals.Count == 0)
            {
                LogUtil.Warning("AnimalFilter has no allowed animals. Restoring defaults");
                SetDefaults();
                return;
            }

            if (AllowedPackAnimals.Count == 0)
            {
                LogUtil.Warning("AnimalFilter has no allowed pack animals. Re-enabling all pack animals");
                allowedAnimals.AddRange(FactionCache.AllPackAnimalKinds);
                InvalidateCache();
            }
        }

        public void InvalidateCache()
        {
            _cachedAllowed = null;
            _cachedAllowedCombat = null;
            _cachedAllowedPack = null;
        }

        public void ExposeData()
        {
            Scribe_Collections.Look(ref allowedAnimals, "allowedAnimals", LookMode.Def);
        }
    }
}
