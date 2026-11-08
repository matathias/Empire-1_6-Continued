using UnityEngine;

namespace FactionColonies;

public class WorldObjectCompProperties_SettlementOpenWindow : MapParentCompProperties
{
    public WorldObjectCompProperties_SettlementOpenWindow()
    {
        compClass = typeof(WorldObjectComp_SettlementOpenWindow);
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
        Command_Action openWindow = new()
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
