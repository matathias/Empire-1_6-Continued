using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    public class CompPowerEmpire : CompPowerPlant
    {


        protected override float DesiredPowerOutput
        {
            get
            {
                FactionFC faction = FindFC.FactionComp;
                if (faction.powerOutput == null || faction.powerOutput.DestroyedOrNull() || faction.powerOutput == this.parent)
                {
                    faction.powerOutput = this.parent;
                    return (float)faction.GetResourcePoolValue(ResourceTypeDefOf.RTD_Power);
                }
                else
                {
                    return 0f;
                }
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }
            if (this.parent.Faction == Faction.OfPlayer)
            {
                yield return new Command_Action
                {
                    action = delegate ()
                    {
                        FindFC.FactionComp.powerOutput = this.parent;
                        Messages.Message("FCSetAsOutputSuccess".Translate(), MessageTypeDefOf.NeutralEvent);
                    },
                    defaultDesc = "FCSetAsEmpirePowerOutput".Translate(FindFC.EmpireTitle.CapitalizeFirst()),
                    icon = ContentFinder<Texture2D>.Get("UI/Commands/TryReconnect", true),
                    defaultLabel = "FCSetAsOutput".Translate()
                };
            }
        }


    }
}
