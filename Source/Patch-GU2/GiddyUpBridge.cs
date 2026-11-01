using FactionColonies.util;
using GiddyUp;
using GiddyUp.Jobs;
using GiddyUpCore.Core;
using RimWorld;
using Verse;

namespace FactionColonies.GiddyUpCompat
{
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
        private static bool _allowedJobsConfigured;

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
            EnsureMountedIdleJobsAllowed();
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

        /// <summary>
        /// Keeps a mount from auto-dismounting when its rider is idle / holding position / wandering —
        /// e.g. when the player drafts an Empire pawn (job becomes Wait_Combat) or undrafts it back into a
        /// lord's wander/defend duty (Wait_Wander / GotoWander). GU2's RiderShouldDismount dismounts when
        /// the rider's current job isn't in its AllowedJobs set and the rider is near its destination;
        /// <see cref="JobDriver_Mounted.SetAllowedJob"/> is GU2's public hook to extend that set. Movement
        /// (Goto) and forbidden-area dismounts are unaffected — those are handled separately. Done lazily
        /// (first mount) so GU2 has already built its job cache (Setup.BuildAllowedJobsCache) by then; the
        /// cache isn't cleared on rebuild, so a one-time add sticks.
        /// </summary>
        private static void EnsureMountedIdleJobsAllowed()
        {
            if (_allowedJobsConfigured) return;
            _allowedJobsConfigured = true;
            JobDriver_Mounted.SetAllowedJob(JobDefOf.Wait_Combat, false);
            JobDriver_Mounted.SetAllowedJob(JobDefOf.Wait, false);
            JobDriver_Mounted.SetAllowedJob(JobDefOf.Wait_MaintainPosture, false);
            JobDriver_Mounted.SetAllowedJob(JobDefOf.Wait_Wander, false);
            JobDriver_Mounted.SetAllowedJob(JobDefOf.GotoWander, false);
        }
    }
}
