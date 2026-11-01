using RimWorld;
using System.Collections.Generic;
using System.Text;
using Verse;

namespace FactionColonies
{
    // All: every listed project must be finished. An empty list counts as satisfied (nothing required).
    // Any: at least one listed project must be finished. An empty list counts as unsatisfied (no way to satisfy).
    public enum TechBarrierMode
    {
        All,
        Any
    }

    public class TechProgressionDef : Def
    {
        public List<TechLevelBarrier> barriers = new List<TechLevelBarrier>();
    }

    public class TechLevelBarrier
    {
        public TechLevel techLevel = TechLevel.Undefined;
        public List<ResearchProjectDef> researchProjects = new List<ResearchProjectDef>();
        public TechBarrierMode mode = TechBarrierMode.All;

        public bool IsSatisfied()
        {
            // Empty list: All mode is satisfied (nothing required), Any mode is not (nothing can satisfy it).
            if (researchProjects.NullOrEmpty())
                return mode == TechBarrierMode.All;

            if (mode == TechBarrierMode.All)
            {
                foreach (ResearchProjectDef rp in researchProjects)
                {
                    if (rp is null) continue;
                    if (!rp.IsFinished) return false;
                }
                return true;
            }
            else
            {
                foreach (ResearchProjectDef rp in researchProjects)
                {
                    if (rp is null) continue;
                    if (rp.IsFinished) return true;
                }
                return false;
            }
        }

        // For UI display (FCBuildingWindow tech-barrier labels). Returns null when list is empty.
        public string DisplayLabel
        {
            get
            {
                if (researchProjects.NullOrEmpty()) return null;
                if (researchProjects.Count == 1) return researchProjects[0]?.label;
                string sep = mode == TechBarrierMode.All ? " + " : " / ";
                StringBuilder sb = new StringBuilder();
                bool first = true;
                foreach (ResearchProjectDef rp in researchProjects)
                {
                    if (rp is null) continue;
                    if (!first) sb.Append(sep);
                    sb.Append(rp.label);
                    first = false;
                }
                return sb.Length == 0 ? null : sb.ToString();
            }
        }
    }
}
