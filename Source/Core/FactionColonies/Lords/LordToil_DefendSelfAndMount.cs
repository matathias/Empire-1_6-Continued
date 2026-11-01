using FactionColonies.util;
using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace FactionColonies
{
    public class LordToil_DefendSelfAndMount : LordToil
    {
        private readonly Dictionary<Pawn, Pawn> mounts;

        public LordToil_DefendSelfAndMount(Dictionary<Pawn, Pawn> mounts)
        {
            this.mounts = mounts;
        }

        public override void UpdateAllDuties()
        {
            // Pass 1: give EVERY pawn a duty BEFORE mounting anyone. Mounting a rider hands its mount the
            // "Mounted" job, which runs the mount's constant think tree — and that logs
            // "ThinkNode_DutyConstant with no duty" if the mount hasn't been given a duty yet. Setting all
            // duties up front (riders + their mounts + other defenders) guarantees the mount has one when
            // it's mounted in pass 2. Mounts are values in the dict (not keys), so they take the Defend
            // branch here; the Mounted job, not the duty, actually drives their behavior.
            foreach (Pawn pawn in lord.ownedPawns)
            {
                if (mounts.ContainsKey(pawn))
                {
                    pawn.mindState.duty = new PawnDuty(DutyDefOf.WanderClose, pawn);
                }
                else
                {
                    pawn.mindState.duty = new PawnDuty(DutyDefOf.Defend, pawn.Position, -1f)
                    {
                        focusSecond = pawn.Position,
                        radius = (pawn.kindDef.defendPointRadius >= 0f) ? pawn.kindDef.defendPointRadius : 28f
                    };
                }
            }

            // Pass 2: now that every pawn has a duty, mount the riders.
            foreach (KeyValuePair<Pawn, Pawn> pair in mounts)
            {
                Pawn rider = pair.Key;
                if (rider is null) continue;
                if (rider.jobs == null) rider.jobs = new Pawn_JobTracker(rider);
                GiddyUpUtil.Mount(rider, pair.Value);
            }
        }
    }
}