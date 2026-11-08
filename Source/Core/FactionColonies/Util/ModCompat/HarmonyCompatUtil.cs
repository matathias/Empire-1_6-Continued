using System.Reflection;
using HarmonyLib;

namespace FactionColonies;

/// <summary>
/// Harmony patching for compatibility modules, which patch another mod's code that can change
/// under them.
/// </summary>
public static class HarmonyCompatUtil
{
    /// <summary>
    /// Applies every [HarmonyPatch] class in <paramref name="assembly"/>, one class at a time.
    /// Unlike <see cref="Harmony.PatchAll(Assembly)"/>, a class whose target no longer resolves
    /// (a renamed method or parameter in the other mod) is logged and skipped, and the remaining
    /// classes still apply. The same goes for a class whose attribute names a type the other mod
    /// removed. Returns the number of classes that failed.
    /// </summary>
    public static int PatchEachClass(this Harmony harmony, Assembly assembly, Action<string> logError = null)
    {
        logError ??= message => LogUtil.Error(message);
        int failed = 0;
        foreach (Type type in AccessTools.GetTypesFromAssembly(assembly))
        {
            try
            {
                // Inside the try: reading the attributes resolves their typeof() arguments, which
                // throws when the other mod removed or renamed a type a patch class names.
                if (!type.HasHarmonyAttribute()) continue;
                harmony.CreateClassProcessor(type).Patch();
            }
            catch (Exception e)
            {
                failed++;
                logError($"Harmony patch class {type.FullName} ({harmony.Id}) failed and was skipped: {e}");
            }
        }
        return failed;
    }
}
