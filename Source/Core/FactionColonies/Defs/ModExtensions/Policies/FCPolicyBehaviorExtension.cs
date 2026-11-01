using System;
using System.Collections.Generic;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// DefModExtension on FCPolicyDef that provides XML-configurable parameters
    /// and a factory for creating the runtime FCPolicyBehavior instance.
    /// Subclass this to add behavior-specific XML fields.
    /// </summary>
    public class FCPolicyBehaviorExtension : DefModExtension
    {
        /// <summary>
        /// The behavior class to instantiate at runtime.
        /// Must be a subclass of FCPolicyBehavior.
        /// </summary>
        public Type behaviorClass;

        [Unsaved] protected FCPolicyDef parentDef;

        public override void ResolveReferences(Def parentDef)
        {
            base.ResolveReferences(parentDef);
            if (parentDef is FCPolicyDef pd)
                this.parentDef = pd;
            else
                LogUtil.Error($"FCPolicyBehaviorExtension on non-FCPolicyDef: {TextUtil.GetDefModInfo(parentDef)}");
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string err in base.ConfigErrors())
                yield return err;

            if (behaviorClass == null)
                yield return "FCPolicyBehaviorExtension: behaviorClass is null";
            else if (!typeof(FCPolicyBehavior).IsAssignableFrom(behaviorClass))
                yield return "FCPolicyBehaviorExtension: behaviorClass " + behaviorClass.Name
                    + " is not a subclass of FCPolicyBehavior";
        }

        /// <summary>
        /// Creates a new behavior instance and wires it to this extension.
        /// </summary>
        public FCPolicyBehavior CreateBehavior()
        {
            FCPolicyBehavior behavior = (FCPolicyBehavior)Activator.CreateInstance(behaviorClass);
            behavior.extension = this;
            return behavior;
        }
    }
}
