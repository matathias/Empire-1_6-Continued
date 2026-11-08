namespace FactionColonies;

public static class BuildingDemolitionUtil
{
    /// <summary>
    /// Orders building slots for demolition so that a building is always demolished before any
    /// building in the list it requires. Among the slots that can go next, the one with the most
    /// requirements goes first. Slots in a requirement cycle, or with an unresolved def, keep a
    /// stable order at the end.
    /// </summary>
    public static List<int> DependentsFirst(WorldObjectComp_SettlementBuildings comp, List<int> slots)
    {
        List<int> remaining = [.. slots];
        List<int> ordered = new(slots.Count);
        while (remaining.Count > 0)
        {
            int pick = -1;
            int pickReqCount = -1;
            foreach (int slot in remaining)
            {
                BuildingFCDef def = comp.GetBuildingInSlot(slot);
                if (def is null) continue;
                // Another remaining building still requires this one: it has to wait.
                if (IsRequiredByAnyOther(comp, def, slot, remaining)) continue;
                int reqCount = def.requiredBuildings?.Count ?? 0;
                if (reqCount > pickReqCount)
                {
                    pick = slot;
                    pickReqCount = reqCount;
                }
            }
            if (pick < 0)
            {
                ordered.AddRange(remaining);
                break;
            }
            ordered.Add(pick);
            remaining.Remove(pick);
        }
        return ordered;
    }

    private static bool IsRequiredByAnyOther(WorldObjectComp_SettlementBuildings comp, BuildingFCDef def, int slot, List<int> remaining)
    {
        foreach (int other in remaining)
        {
            if (other == slot) continue;
            BuildingFCDef otherDef = comp.GetBuildingInSlot(other);
            if (otherDef is not null && FactionCache.SatisfiesAnyRequirement(def, otherDef.requiredBuildings))
                return true;
        }
        return false;
    }
}
