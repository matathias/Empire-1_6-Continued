using System;
using System.Collections.Generic;
using Verse;

namespace FactionColonies
{
    public class MilitaryJobDef : Def
    {
        public Type handlerClass;
        public string statusLabelKey;
        public bool occupiesTarget = true;
        public bool isState;
        public string floatMenuLabelKey;
        public string floatMenuDescKey;
        public string rewardsDesc;
        public bool defaultEnabled = true;

        [Unsaved] private MilitaryJobHandler cachedHandler;

        public MilitaryJobHandler Handler
        {
            get
            {
                if (cachedHandler == null && handlerClass != null)
                {
                    cachedHandler = (MilitaryJobHandler)Activator.CreateInstance(handlerClass);
                    cachedHandler.def = this;
                }
                return cachedHandler;
            }
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string err in base.ConfigErrors())
                yield return err;

            // handlerClass is legitimately null for isState defs (Undefined, Cooldown, ...);
            // only validate that, when set, it can actually be cast to MilitaryJobHandler.
            if (handlerClass is object && !typeof(MilitaryJobHandler).IsAssignableFrom(handlerClass))
                yield return $"MilitaryJobDef {defName}: handlerClass '{handlerClass.FullName}' does not derive from MilitaryJobHandler";
        }
    }
}
