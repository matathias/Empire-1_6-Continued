namespace FactionColonies;

/// <summary>Base for comp properties whose comp only works on a <see cref="MapParent"/> world
/// object: reports a config error when attached to any other world object class.</summary>
public abstract class MapParentCompProperties : WorldObjectCompProperties
{
    public override IEnumerable<string> ConfigErrors(WorldObjectDef parentDef)
    {
        foreach (string item in base.ConfigErrors(parentDef))
        {
            yield return item;
        }
        if (!typeof(MapParent).IsAssignableFrom(parentDef.worldObjectClass))
        {
            yield return $"{parentDef.defName} has {GetType().Name} but it's not MapParent.";
        }
    }
}

/// <summary>Base for comps attached to a <see cref="WorldSettlementFC"/>: exposes the parent as a
/// cached <see cref="WorldSettlement"/>, logging once if the parent is some other world object.</summary>
public abstract class WorldSettlementCompBase : WorldObjectComp
{
    private WorldSettlementFC cachedWorldSettlementParent = null;
    public WorldSettlementFC WorldSettlement
    {
        get
        {
            if (cachedWorldSettlementParent is not null)
            {
                return cachedWorldSettlementParent;
            }
            if (parent is WorldSettlementFC ws)
            {
                cachedWorldSettlementParent = ws;
            }
            else
            {
                cachedWorldSettlementParent = null;
                LogUtil.ErrorOnce($"{GetType().Name} has a non-WorldSettlementFC parent: {parent?.Label}");
            }
            return cachedWorldSettlementParent;
        }
    }
}
