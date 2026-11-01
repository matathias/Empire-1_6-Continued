using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace FactionColonies.util
{
    /// <summary>
    /// Caches, per concrete Type, whether it overrides a given virtual method declared on a base type.
    /// Used to skip no-op virtual dispatch in hot tick loops: a comp/behavior whose class never overrides
    /// its tick method only inherits an empty base body, so calling it every tick is pure overhead.
    /// One reflection lookup per (type, method) pair, ever; cached thereafter.
    /// </summary>
    internal static class TickOverrideUtil
    {
        private static readonly Dictionary<string, bool> cache = new Dictionary<string, bool>();

        /// <param name="type">Concrete runtime type to test.</param>
        /// <param name="methodName">Instance method to look for.</param>
        /// <param name="baseType">Type that declares the empty/base virtual; an override is anything declared elsewhere.</param>
        /// <param name="paramTypes">Parameter types of the method signature (empty for parameterless methods).</param>
        internal static bool Overrides(Type type, string methodName, Type baseType, params Type[] paramTypes)
        {
            // Method name disambiguates per type (a type may be tested for more than one tick method).
            string key = type.FullName + "|" + methodName;
            if (cache.TryGetValue(key, out bool result)) return result;

            // AccessTools.Method walks base types, so the base's own declaration is always found
            // (never null here); DeclaringType != baseType is what tells us a subclass actually overrode it.
            // Must be Method (inheritance-walking), not DeclaredMethod, so a C : B : A chain where B
            // overrides is still detected as ticking.
            MethodInfo m = AccessTools.Method(type, methodName, paramTypes);
            result = m is object && m.DeclaringType != baseType;

            cache[key] = result;
            return result;
        }
    }
}
