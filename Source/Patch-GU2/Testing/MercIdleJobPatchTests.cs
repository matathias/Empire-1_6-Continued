using System.Reflection;
using GiddyUp.Jobs;
using HarmonyLib;
using Verse.AI;

namespace FactionColonies.GiddyUpCompat;

/// <summary>
/// The mounted-idle-job patch: while GU2 decides whether a rider should dismount, an Empire merc's
/// idle job (waiting, holding position, wandering) counts as an allowed mounted job, and only for
/// that one check. GU2's allowed-job table is shared by every rider, so anything the patch adds
/// must be gone again afterwards, and non-merc riders must never see a change.
/// The patch methods are private Harmony members, so they are invoked by reflection.
/// </summary>
public static class MercIdleJobPatchTests
{
    private const string Category = "GU2 Mounted Idle Jobs";

    private static readonly MethodInfo IsMercIdleJobMethod =
        AccessTools.Method(typeof(Patch_RiderShouldDismount_MercIdleJobs), "IsMercIdleJob");
    private static readonly MethodInfo PrefixMethod =
        AccessTools.Method(typeof(Patch_RiderShouldDismount_MercIdleJobs), "Prefix");
    private static readonly MethodInfo FinalizerMethod =
        AccessTools.Method(typeof(Patch_RiderShouldDismount_MercIdleJobs), "Finalizer");

    private static bool IsMercIdleJob(JobDef def) =>
        (bool)TestAssert.NotNull(IsMercIdleJobMethod, "IsMercIdleJob not found").Invoke(null, [def]);

    /* Runs the prefix and returns the JobDef it handed to the finalizer (its __state). */
    private static JobDef RunPrefix(JobDriver_Mounted driver)
    {
        object[] args = [driver, null];
        TestAssert.NotNull(PrefixMethod, "Prefix not found").Invoke(null, args);
        return (JobDef)args[1];
    }

    private static Exception RunFinalizer(Exception exception, JobDef state) =>
        (Exception)TestAssert.NotNull(FinalizerMethod, "Finalizer not found").Invoke(null, [exception, state]);

    private static JobDef MakeScratchJobDef(string name) => new() { defName = name, driverClass = typeof(JobDriver_Wait) };

    [EmpireTest(Category, GameState = TestGameState.Any)]
    public static void IsMercIdleJob_MatchesIdleJobsOnly()
    {
        TestAssert.IsTrue(IsMercIdleJob(RimWorld.JobDefOf.Wait), "Wait");
        TestAssert.IsTrue(IsMercIdleJob(RimWorld.JobDefOf.Wait_Combat), "Wait_Combat (drafted)");
        TestAssert.IsTrue(IsMercIdleJob(RimWorld.JobDefOf.Wait_MaintainPosture), "Wait_MaintainPosture");
        TestAssert.IsTrue(IsMercIdleJob(RimWorld.JobDefOf.Wait_Wander), "Wait_Wander (lord wander duty)");
        TestAssert.IsTrue(IsMercIdleJob(RimWorld.JobDefOf.GotoWander), "GotoWander (lord wander duty)");

        TestAssert.IsFalse(IsMercIdleJob(RimWorld.JobDefOf.Goto), "Plain movement keeps GU2's normal dismount rules");
        TestAssert.IsFalse(IsMercIdleJob(RimWorld.JobDefOf.AttackMelee), "Non-idle jobs are not affected");
        TestAssert.IsFalse(IsMercIdleJob(null), "No job is not an idle job");
    }

    [EmpireTest(Category, GameState = TestGameState.Any)]
    public static void Finalizer_RemovesOnlyTheJobThePrefixAdded()
    {
        JobDef added = MakeScratchJobDef("EmpireTest_MountedAdded");
        JobDef other = MakeScratchJobDef("EmpireTest_MountedOther");
        try
        {
            JobDriver_Mounted.AllowedJobs[added] = false;
            JobDriver_Mounted.AllowedJobs[other] = false;

            TestAssert.IsNull(RunFinalizer(null, added), "No exception in, none out");
            TestAssert.IsFalse(JobDriver_Mounted.AllowedJobs.ContainsKey(added), "The job the prefix added is removed");
            TestAssert.IsTrue(JobDriver_Mounted.AllowedJobs.ContainsKey(other), "Other entries are left alone");

            RunFinalizer(null, null);
            TestAssert.IsTrue(JobDriver_Mounted.AllowedJobs.ContainsKey(other), "With nothing added, nothing is removed");

            JobDriver_Mounted.AllowedJobs[added] = false;
            var thrown = new InvalidOperationException("test");
            TestAssert.IsTrue(ReferenceEquals(thrown, RunFinalizer(thrown, added)), "An exception from GU2 is passed through");
            TestAssert.IsFalse(JobDriver_Mounted.AllowedJobs.ContainsKey(added), "Cleanup still happens when GU2 threw");
        }
        finally
        {
            JobDriver_Mounted.AllowedJobs.Remove(added);
            JobDriver_Mounted.AllowedJobs.Remove(other);
        }
    }

    [EmpireTest(Category)]
    public static void Prefix_NoRider_ChangesNothing()
    {
        int before = JobDriver_Mounted.AllowedJobs.Count;
        JobDef state = RunPrefix(new JobDriver_Mounted());
        TestAssert.IsNull(state, "Nothing to clean up without a rider");
        TestAssert.AreEqual(before, JobDriver_Mounted.AllowedJobs.Count, "The shared table is unchanged");
    }

    [EmpireDestructiveTest(Category)]
    public static void Prefix_NonMercRider_ChangesNothing()
    {
        Pawn pawn = DestructiveTestUtil.GenerateWorldPawn();
        TestAssert.IsFalse(FindFC.Military?.IsMercenaryPawn(pawn) == true, "A freshly generated empire pawn is not a merc");
        RunIdleJobCheck(pawn, RimWorld.JobDefOf.Wait_Combat, expectAllowedDuringCheck: false);
    }

    [EmpireDestructiveTest(Category)]
    public static void Prefix_MercRiderOnIdleJob_AllowedForTheCheckOnly()
    {
        MercenarySquadFC squad = DestructiveTestUtil.EnsureHiredSquad();
        Pawn merc = TestAssert.NotNull(squad.EquippedMercenaryPawns.FirstOrDefault(), "A hired squad should have a merc pawn");
        TestAssert.IsTrue(FindFC.Military.IsMercenaryPawn(merc), "A hired squad's pawn is a merc");

        RunIdleJobCheck(merc, RimWorld.JobDefOf.Wait_Combat, expectAllowedDuringCheck: true);
        RunIdleJobCheck(merc, RimWorld.JobDefOf.Wait_Wander, expectAllowedDuringCheck: true);
        RunIdleJobCheck(merc, RimWorld.JobDefOf.Goto, expectAllowedDuringCheck: false);
    }

    /* Puts the rider on a job of jobDef, runs prefix then finalizer around a mounted driver, and
     * checks the shared table during and after. A job GU2 itself already allows is left as GU2 set
     * it, so the patch must not add or remove it. */
    private static void RunIdleJobCheck(Pawn rider, JobDef jobDef, bool expectAllowedDuringCheck)
    {
        TestAssert.NotNull(rider.jobs, "The rider needs a job tracker");
        bool gu2Allows = JobDriver_Mounted.AllowedJobs.ContainsKey(jobDef);
        Job originalJob = rider.jobs.curJob;
        rider.jobs.curJob = JobMaker.MakeJob(jobDef);
        try
        {
            var driver = new JobDriver_Mounted { Rider = rider };
            JobDef state = RunPrefix(driver);

            if (gu2Allows)
            {
                TestAssert.IsNull(state, $"{jobDef.defName} is already allowed by GU2; the patch leaves it alone");
            }
            else
            {
                TestAssert.AreEqual(expectAllowedDuringCheck, JobDriver_Mounted.AllowedJobs.ContainsKey(jobDef),
                    $"{jobDef.defName} counts as allowed during the check: {expectAllowedDuringCheck}");
                TestAssert.AreEqual(expectAllowedDuringCheck, state == jobDef, "The prefix hands what it added to the finalizer");
            }

            RunFinalizer(null, state);
            TestAssert.AreEqual(gu2Allows, JobDriver_Mounted.AllowedJobs.ContainsKey(jobDef),
                $"After the check the table holds {jobDef.defName} only if GU2 itself allows it");
        }
        finally
        {
            if (!gu2Allows) JobDriver_Mounted.AllowedJobs.Remove(jobDef);
            rider.jobs.curJob = originalJob;
        }
    }
}
