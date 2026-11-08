namespace FactionColonies;

public static class PawnUtil
{
    /// <summary>
    /// Destroys a throwaway pawn (a UI preview or an editing-session pawn) along with its apparel and
    /// equipment, which would otherwise be left behind as orphaned things. A failure is logged with
    /// <paramref name="context"/> and swallowed, so it cannot break the window cleaning up.
    /// </summary>
    public static void DiscardPreviewPawn(Pawn pawn, string context)
    {
        if (pawn is null) return;
        try
        {
            pawn.apparel?.DestroyAll();
            pawn.equipment?.DestroyAllEquipment();
            if (!pawn.Destroyed) pawn.Destroy();
        }
        catch (Exception ex)
        {
            LogUtil.Warning($"{context}: failed to discard preview pawn {pawn.LabelShort}: {ex.Message}");
        }
    }
}
