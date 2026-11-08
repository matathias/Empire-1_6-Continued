namespace FactionColonies;

/// <summary>
/// Shared steps for putting a squad's pawns onto a battle or deployment map, used by manual
/// defense, reinforcements, manual offense and deployments.
/// </summary>
public static class SquadSpawnUtil
{
    /// <summary>
    /// Readies <paramref name="squad"/> for a fight: generates any missing pawns, refreshes its
    /// stats for <paramref name="home"/>'s military level (skipped when <paramref name="home"/> is
    /// null), and resets needs.
    /// </summary>
    public static void PrepareSquad(MercenarySquadFC squad, WorldSettlementFC home)
    {
        squad.CheckInitialization();
        if (home is not null)
            squad.UpdateSquadStats(home.settlementMilitaryLevel);
        SquadHealthUtil.ResetNeeds(squad);
    }

    /// <summary>Gives each pawn the combat-efficiency hediff for <paramref name="efficiency"/>.</summary>
    public static void ApplyCombatEfficiency(IEnumerable<Pawn> pawns, double efficiency)
    {
        foreach (Pawn pawn in pawns)
            MilitaryEfficiencyUtil.ApplyCombatEfficiencyHediff(pawn, efficiency);
    }

    /// <summary>
    /// Spawns <paramref name="pawn"/> at <paramref name="cell"/> as a combatant: applies ideology
    /// ritual wounds, gives it a draft controller (so the player can draft it; it stays Empire
    /// faction until drafted), and loads reloadable-weapon magazines from carried ammo. Returns
    /// false, with a warning naming <paramref name="role"/>, when spawning throws (usually a mod
    /// conflict).
    /// </summary>
    public static bool TrySpawnCombatant(Pawn pawn, IntVec3 cell, Map map, string role)
    {
        pawn.ApplyIdeologyRitualWounds();
        try
        {
            GenSpawn.Spawn(pawn, cell, map, new Rot4());
            pawn.drafter = new Pawn_DraftController(pawn);
            ReloadableWeaponUtil.LoadMagazinesFromInventory(pawn);
            return true;
        }
        catch (Exception e)
        {
            LogUtil.Warning($"Failed to spawn {role} {pawn.LabelShort} (likely a mod conflict): {e}");
            return false;
        }
    }
}
