using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    public class WorldObjectCompProperties_SettlementOpenWindow : WorldObjectCompProperties
    {
        public WorldObjectCompProperties_SettlementOpenWindow()
        {
            compClass = typeof(WorldObjectComp_SettlementOpenWindow);
        }
        public override IEnumerable<string> ConfigErrors(WorldObjectDef parentDef)
        {
            foreach (string item in base.ConfigErrors(parentDef))
            {
                yield return item;
            }
            if (!typeof(MapParent).IsAssignableFrom(parentDef.worldObjectClass))
            {
                yield return parentDef.defName + " has WorldObjectCompProperties_SettlementOpenWindow but it's not MapParent.";
            }
        }
    }

    public class WorldObjectComp_SettlementOpenWindow : WorldObjectComp
    {
        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }
            if (parent is WorldSettlementFC worldsettlement)
            {
                yield return OpenSettlementWindowAction(worldsettlement);
            }
        }
        private Command OpenSettlementWindowAction(WorldSettlementFC worldsettlement)
        {
            Command_Action openWindow = new Command_Action
            {
                defaultLabel = "FCOpenSettlementWindowDefaultLabel".Translate(),
                defaultDesc = "FCOpenSettlementWindowDefaultDesc".Translate(),
                icon = ContentFinder<Texture2D>.Get("UI/Icons/QuestionMark"),
                action = delegate
                {
                    Find.WindowStack.Add(new SettlementWindowFc(worldsettlement));
                }
            };

            return openWindow;
        }
    }
}
