using System;
using Verse;

namespace FactionColonies
{
    public class FCPolicy : IExposable
    {
        public FCPolicy()
        {
        }

        public FCPolicy(FCPolicyDef def)
        {
            FactionFC faction = FindFC.FactionComp;
            this.def = def;
            timeEnacted = Find.TickManager.TicksGame;

            // Create behavior instance if this policy has a behavior extension
            FCPolicyBehaviorExtension ext = def.BehaviorExtension;
            if (ext != null)
            {
                behavior = ext.CreateBehavior();
                behavior.policy = this;
                behavior.PostInitialize();
                try
                {
                    behavior.OnEnacted(faction);
                }
                catch (Exception e)
                {
                    LogUtil.Error($"FCPolicyBehavior.OnEnacted error for '{def.defName}': {e}");
                }
            }

        }

        public FCPolicyDef def;
        public int timeEnacted;
        public FCPolicyBehavior behavior;

        public bool IsFullyActive => def.enactDuration <= 0
            || Find.TickManager.TicksGame - timeEnacted >= def.enactDuration;

        public void ExposeData()
        {
            Scribe_Defs.Look(ref def, "def");
            Scribe_Values.Look(ref timeEnacted, "timeEnacted");
            Scribe_Deep.Look(ref behavior, "behavior");

            // Wire unsaved back-references every load phase so they're available
            // as early as possible. WorldObjects load before WorldComponents in
            // World.ExposeComponents, so settlement comps' PostExposeData can
            // trigger behavior hooks before FactionFC reaches PostLoadInit.
            if (Scribe.mode != LoadSaveMode.Saving && behavior != null)
            {
                behavior.policy = this;
                if (def?.BehaviorExtension != null)
                    behavior.extension = def.BehaviorExtension;
            }

            if (Scribe.mode == LoadSaveMode.PostLoadInit && behavior != null)
            {
                // If the def failed to resolve (removed content) the behavior's extension was
                // never wired; PostInitialize would deref a null extension. Drop the orphan so
                // it isn't initialized or folded into the faction behavior/action caches.
                if (def is null || behavior.extension is null)
                    behavior = null;
                else
                    behavior.PostInitialize();
            }
        }
    }
}
