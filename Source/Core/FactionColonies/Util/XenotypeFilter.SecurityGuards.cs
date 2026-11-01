using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;

/*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-
 * XenotypeFilter — Security Guards (DORMANT)
 *
 * The "security guard" subsystem auto-assigns guard animals to mercenaries spawned with
 * non-violent xenotypes. It was designed but side-lined due to various issues; it isn't
 * currently active anywhere in the code.
 *
 * Cordoned here so the live XenotypeFilter stays focused. The fields below still
 * serialize (via the partial ExposeSecurityGuardsData hook in the main file's ExposeData)
 * so existing saves continue to round-trip cleanly.
 *
 * To revive:
 *   1. Re-add SetupSecurityGuards(xeno) calls in InitializeXenotypeWeights and
 *      InitializeCustomXenotypeWeights (post-Set, for each enabled xenotype).
 *   2. Re-add SetupAllSecurityGuards() at the top of SetPawnGroupMakers.
 *   3. Re-add the guard-animal injection block in SetPawnGroupMakers that appends
 *      securityGuardsByXenotype[...].List entries to pawnGroupMakers[0].options and
 *      pawnGroupMakers[1].guards.
 *   4. Wire MercenaryPawnFactory.TryAssignSecurityGuard into the mercenary creation
 *      pipeline (it already calls GetSecurityGuardsForXenotype/CustomXenotype).
 *   5. Restore the InvalidateGuardAnimalCache call in AnimalFilter.InvalidateCache.
 *-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*/

namespace FactionColonies.util
{
    public partial class XenotypeFilter
    {
        private Dictionary<XenotypeDef, SecurityGuardList> securityGuardsByXenotype = new Dictionary<XenotypeDef, SecurityGuardList>();
        private Dictionary<string, SecurityGuardList> securityGuardsByCustomXenotype = new Dictionary<string, SecurityGuardList>();

        private List<PawnKindDef> cachedGuardAnimals = null;
        public List<PawnKindDef> GuardAnimals
        {
            get
            {
                if (cachedGuardAnimals is null)
                {
                    List<PawnKindDef> combatPool = factionFc?.animalFilter?.AllowedCombatAnimals ?? FactionCache.AllCombatAnimalKindDefs;
                    cachedGuardAnimals = combatPool.OrderByDescending(def => def.combatPower).Take(3).Distinct().ToList();
                }
                return cachedGuardAnimals;
            }
        }
        public void InvalidateGuardAnimalCache()
        {
            cachedGuardAnimals = null;
        }

        private void SetupSecurityGuards(XenotypeDef xenotype)
        {
            if (!securityGuardsByXenotype.ContainsKey(xenotype))
            {
                securityGuardsByXenotype[xenotype] = new SecurityGuardList();
            }

            if (IsXenotypeNonViolent(xenotype))
            {
                securityGuardsByXenotype[xenotype].SetRange(GuardAnimals);
            }
        }
        private void SetupSecurityGuards(string xenotype)
        {
            if (!securityGuardsByCustomXenotype.ContainsKey(xenotype))
            {
                securityGuardsByCustomXenotype[xenotype] = new SecurityGuardList();
            }

            if (IsCustomXenotypeNonViolent(xenotype))
            {
                securityGuardsByCustomXenotype[xenotype].SetRange(GuardAnimals);
            }
        }
        private void SetupAllSecurityGuards()
        {
            foreach (XenotypeDef xenotype in XenotypeWeights.Keys)
            {
                SetupSecurityGuards(xenotype);
            }
            foreach (string xenoName in CustomXenotypeWeights.Keys)
            {
                SetupSecurityGuards(xenoName);
            }
        }

        /* Returns the union of all configured guard kinds across all xenotypes. */
        public List<PawnKindDef> GetAvailableSecurityGuards()
        {
            List<PawnKindDef> allGuards = new List<PawnKindDef>();
            if (securityGuardsByXenotype.Count > 0)
            {
                foreach (SecurityGuardList guardList in securityGuardsByXenotype.Values)
                {
                    allGuards.AddRange(guardList.List);
                }
            }
            if (securityGuardsByCustomXenotype.Count > 0)
            {
                foreach (SecurityGuardList guardList in securityGuardsByCustomXenotype.Values)
                {
                    allGuards.AddRange(guardList.List);
                }
            }
            return allGuards.Distinct().ToList();
        }
        public List<PawnKindDef> GetSecurityGuardsForXenotype(XenotypeDef xenotype)
        {
            return securityGuardsByXenotype.ContainsKey(xenotype)
                ? securityGuardsByXenotype[xenotype].List
                : new List<PawnKindDef>();
        }
        public List<PawnKindDef> GetSecurityGuardsForCustomXenotype(string xenotypeName)
        {
            return securityGuardsByCustomXenotype.ContainsKey(xenotypeName)
                ? securityGuardsByCustomXenotype[xenotypeName].List
                : new List<PawnKindDef>();
        }

        partial void ExposeSecurityGuardsData()
        {
            Scribe_Collections.Look(ref securityGuardsByXenotype, "securityGuardsByXenotype", LookMode.Def, LookMode.Deep);
            Scribe_Collections.Look(ref securityGuardsByCustomXenotype, "securityGuardsByCustomXenotype", LookMode.Value, LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (securityGuardsByXenotype is null)
                    securityGuardsByXenotype = new Dictionary<XenotypeDef, SecurityGuardList>();
                if (securityGuardsByCustomXenotype is null)
                    securityGuardsByCustomXenotype = new Dictionary<string, SecurityGuardList>();
            }
        }
    }

    /* Scribe_Collections doesn't handle Dictionary<TKey, List<TValue>> directly, so the
     * value side of the guard dicts uses this IExposable wrapper. */
    public class SecurityGuardList : IExposable
    {
        private List<PawnKindDef> list = new List<PawnKindDef>();

        public int Count => list.Count;
        public bool Contains(PawnKindDef def) => list.Contains(def);
        public void Clear() => list.Clear();
        public List<PawnKindDef> List => list;

        public void InitList()
        {
            list = new List<PawnKindDef>();
        }
        public bool Add(PawnKindDef def)
        {
            if (Contains(def))
            {
                return false;
            }
            list.Add(def);
            return true;
        }
        public bool Remove(PawnKindDef def)
        {
            if (Contains(def))
            {
                list.Remove(def);
                return true;
            }
            return false;
        }
        public void AddRange(List<PawnKindDef> deflist)
        {
            foreach (PawnKindDef def in deflist)
            {
                Add(def);
            }
        }
        public void RemoveRange(List<PawnKindDef> deflist)
        {
            foreach (PawnKindDef def in deflist)
            {
                Remove(def);
            }
        }
        public void SetRange(List<PawnKindDef> deflist)
        {
            Clear();
            AddRange(deflist);
        }
        public void ExposeData()
        {
            Scribe_Collections.Look(ref list, "guardKindList", LookMode.Def);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (list is null)
                {
                    InitList();
                }
            }
        }
    }
}
