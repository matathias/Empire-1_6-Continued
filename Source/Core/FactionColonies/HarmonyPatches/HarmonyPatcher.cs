using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    [StaticConstructorOnStartup]
    public static class HarmonyPatcher
    {
        static HarmonyPatcher()
        {
            var harmony = new Harmony("com.Matathias.Empire");

            if (SystemInfo.operatingSystemFamily == OperatingSystemFamily.Linux)
            {
                FixLinuxHarmonyCrash(harmony);
            }

            harmony.PatchAll();
            LogUtil.MessageForce("harmony patch complete");
        }

        /* Works around a historical Harmony-on-Mono (Linux/Mac) crash */
        // On Mono, patching an "empty" virtual method -- one whose IL body is absent or is a single `ret`
        //  (opcode 0x2A) -- could throw InvalidProgramException / crash to desktop. Mono's JIT treats these
        //  tiny bodies specially (inlining/dead-code rules), so Harmony can't safely detour them.
        // The fix: before the real PatchAll() runs, apply an empty Harmony patch (no prefix/postfix/transpiler)
        //  to each such method. That forces Mono to JIT-compile the method and build a stable trampoline up
        //  front, so the actual patch attaches cleanly instead of crashing.
        // Below we scope this to only the empty virtual methods Empire itself patches: we read the
        //  [HarmonyPatch] attributes on Empire's own patch classes to find each target method, then prime the
        //  ones matching the crash profile. (Adapted from notfood's "Harmony Fix For Penguins" mod, which
        //  primed every empty virtual in the entire game assembly.)
        // STATUS: this Harmony bug was fixed upstream and the fix is bundled in the Harmony version used with
        //  modern RimWorld (the Penguins mod is deprecated for that reason), so this function is almost
        //  certainly dead weight and safe to delete. It survives only because I have no Linux install to
        //  confirm removal is safe on -- it's Linux/Mac-only and otherwise harmless, so it stays until verified.
        static void FixLinuxHarmonyCrash(Harmony harmony)
        {
            bool WouldCrash(MethodInfo method)
            {
                if (method is null || !method.IsVirtual || method.IsAbstract || method.IsFinal)
                {
                    return false;
                }

                byte[] bytes = method.GetMethodBody()?.GetILAsByteArray();
                if (bytes is null || bytes.Length == 0 || (bytes.Length == 1 && bytes.First() == 0x2A))
                {
                    return true;
                }
                return false;
            }

            var methods = typeof(FactionFC).Assembly.GetTypes().Where(t0 => t0 != null && t0.IsClass && !typeof(Delegate).IsAssignableFrom(t0) && t0.GetCustomAttributes(typeof(HarmonyPatch)).Any()).SelectMany(t1 =>
            {
                Type declaringType = null;
                string methodName = null;
                foreach (HarmonyPatch attr in t1.GetCustomAttributes(typeof(HarmonyPatch), false))
                {
                    if (attr.info.declaringType != null) declaringType = attr.info.declaringType;
                    if (attr.info.methodName != null) methodName = attr.info.methodName;
                }

                MethodInfo[] m = declaringType?.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (m is null) return new List<MethodInfo>();

                return m.Where(met => met.Name == methodName);
            }).Where(WouldCrash);

            foreach (MethodInfo i in methods)
            {
                // An empty patch (no prefix/postfix/transpiler) is enough: it forces Mono to JIT-compile the
                //  method and build its trampoline now, so the real patch in PatchAll() won't crash on it.
                harmony.Patch(i);
            }
            LogUtil.MessageForce($"FixLinuxHarmonyCrash complete");
        }
    }
}