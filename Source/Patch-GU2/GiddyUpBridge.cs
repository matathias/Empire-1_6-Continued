using GiddyUp;
using HarmonyLib;
using GiddyUp.Jobs;
using GiddyUpCore.Core;

namespace FactionColonies.GiddyUpCompat;

/// <summary>
/// Giddy Up 2 implementation of <see cref="IGiddyUpBridge"/>. Only exists in Empire.GiddyUp.dll,
/// which is loaded only when Giddy Up 2 is active (LoadFolders.xml conditional loading).
///
/// The Empire.GiddyUp project publicizes GiddyUpCore (Krafs.Publicizer + IgnoresAccessChecksTo), so
/// this calls GU2's internal MountUtility/StorageUtility directly with typed syntax — no reflection.
/// <see cref="Mount"/> uses GU2's instant mount (GiveJobMethod.Instant), the same path GU2 uses to
/// spawn pre-mounted raiders, so the merc ends up mounted immediately with no walk-up.
/// </summary>
public class GiddyUpBridge : IGiddyUpBridge
{
    public bool IsMountable(PawnKindDef kind)
    {
        if (kind?.race is null) return false;
        // GU2's MountableCache (built from its settings + Mountable/NotMountable mod extensions) is the
        // same gate GU2 uses to decide whether a kind can ever be ridden. AllAnimalKindDefs are already
        // animals, so this cache check is sufficient for filtering the mount picker.
        return ModSettings_GiddyUp.MountableCache.Contains(kind.race.shortHash);
    }

    public void Mount(Pawn rider, Pawn mount)
    {
        if (rider is null || mount is null) return;
        // Skip only when FULLY mounted: GU2's mounted state is set AND the animal is actually running
        // its Mounted job. A draft/undraft faction switch runs ClearMind on the mount, which ends the
        // Mounted job while leaving the mounted state set — fall through so GoMount(Instant) re-issues
        // it (GoMount itself no-ops the job re-issue when it's already present). ResourceBank.JobDefOf
        // is GU2's own [DefOf], so this is typed and guaranteed populated by game start.
        if (rider.IsMounted() && mount.CurJobDef == ResourceBank.JobDefOf.Mounted) return;
        // Instant: set the mounted state directly (no walk-up job), as GU2 does for pre-mounted raiders.
        rider.GoMount(mount, MountUtility.GiveJobMethod.Instant);
        // GU2 draws the rider as a render node on the MOUNT's render tree and suppresses the rider's
        // own draw. The Mount job rebuilds that tree at the end; GoMount(Instant) does NOT. So when we
        // mount a rider whose mount was already drawn (mid-deploy, after pods open), the mount's tree
        // lacks the rider node and the rider goes invisible. Rebuild it (same call the Mount job uses).
        MountedRiderRenderNodeUtility.RefreshMountedAnimalGraphics(mount);
    }
}

/// <summary>
/// Keeps an Empire merc's mount from auto-dismounting when the rider is idle / holding position /
/// wandering, e.g. when the player drafts the merc (job becomes Wait_Combat) or undrafts it back into a
/// lord's wander/defend duty (Wait_Wander / GotoWander). GU2's RiderShouldDismount parks the mount when
/// the rider's current job isn't in its global AllowedJobs set and the rider is near its destination.
/// Adding the jobs to that set permanently would change the behavior for every rider in the game, so
/// the job is added only for the duration of the check, and only when the rider is a merc.
/// Movement (Goto) and forbidden-area dismounts are unaffected.
/// </summary>
[HarmonyPatch(typeof(JobDriver_Mounted), nameof(JobDriver_Mounted.RiderShouldDismount))]
public static class Patch_RiderShouldDismount_MercIdleJobs
{
    private static bool IsMercIdleJob(JobDef def) =>
        def == JobDefOf.Wait_Combat || def == JobDefOf.Wait || def == JobDefOf.Wait_MaintainPosture ||
        def == JobDefOf.Wait_Wander || def == JobDefOf.GotoWander;

    private static void Prefix(JobDriver_Mounted __instance, out JobDef __state)
    {
        __state = null;
        JobDef def = __instance.Rider?.CurJobDef;
        if (def is null || !IsMercIdleJob(def)) return;
        if (JobDriver_Mounted.AllowedJobs.ContainsKey(def)) return;
        // This runs every tick for every mounted rider. The cached merc set answers cheaply; the full
        // squad scan behind it is only worth paying for an Empire-faction rider the set has missed.
        MilitaryFC military = FindFC.Military;
        if (military is null) return;
        Pawn rider = __instance.Rider;
        if (!military.IsKnownMercenaryPawn(rider)
            && (rider.Faction != FindFC.EmpireFaction || !military.IsMercenaryPawn(rider))) return;
        JobDriver_Mounted.AllowedJobs[def] = false;
        __state = def;
    }

    private static Exception Finalizer(Exception __exception, JobDef __state)
    {
        if (__state is not null) JobDriver_Mounted.AllowedJobs.Remove(__state);
        return __exception;
    }
}
