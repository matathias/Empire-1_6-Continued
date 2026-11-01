using FactionColonies.util;
using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Replaces the vanilla xenotype breakdown in the faction tooltip for the PColony faction
    /// with the actual XenotypeFilter weights configured by the player.
    /// </summary>
    [HarmonyPatch(typeof(FactionDef))]
    [HarmonyPatch("Description", MethodType.Getter)]
    class FactionDefDescriptionPatch
    {
        private static string cachedDescription;
        private static bool dirty = true;

        public static void Invalidate()
        {
            dirty = true;
            cachedDescription = null;
        }

        static void Postfix(FactionDef __instance, ref string __result)
        {
            if (__instance != FindFC.EmpireFactionDef)
            {
                return;
            }

            if (!ModsConfig.BiotechActive)
            {
                return;
            }

            FactionFC factionComp = FindFC.FactionComp;
            if (factionComp == null)
            {
                return;
            }

            XenotypeFilter filter = factionComp.xenotypeFilter;
            if (filter == null)
            {
                return;
            }

            if (!dirty && cachedDescription != null)
            {
                __result = cachedDescription;
                return;
            }

            // Strip the vanilla xenotype section from the result.
            string vanillaHeader = ("MemberXenotypeChances".Translate() + ":").AsTipTitle();
            int headerIndex = __result.IndexOf(vanillaHeader);
            string baseDescription;
            if (headerIndex > 0)
            {
                int cutStart = headerIndex;
                while (cutStart > 0 && __result[cutStart - 1] == '\n')
                {
                    cutStart--;
                }
                baseDescription = __result.Substring(0, cutStart);
            }
            else
            {
                baseDescription = __result;
            }

            // Build replacement section from XenotypeFilter.
            StringBuilder sb = new StringBuilder(baseDescription);
            sb.Append("\n\n");
            sb.Append(("MemberXenotypeChances".Translate() + ":").AsTipTitle());
            sb.Append("\n");

            List<KeyValuePair<string, float>> entries = new List<KeyValuePair<string, float>>();

            foreach (KeyValuePair<XenotypeDef, float> kvp in filter.XenotypeWeights)
            {
                if (kvp.Value <= 0f)
                {
                    continue;
                }
                float chance = filter.GetXenotypeChance(kvp.Key);
                if (chance > 0f)
                {
                    entries.Add(new KeyValuePair<string, float>(kvp.Key.LabelCap, chance));
                }
            }

            foreach (KeyValuePair<string, float> kvp in filter.CustomXenotypeWeights)
            {
                if (kvp.Value <= 0f)
                {
                    continue;
                }
                float chance = filter.GetCustomXenotypeChance(kvp.Key);
                if (chance > 0f)
                {
                    entries.Add(new KeyValuePair<string, float>(kvp.Key, chance));
                }
            }

            entries.Sort((a, b) => b.Value.CompareTo(a.Value));

            for (int i = 0; i < entries.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append("\n");
                }
                sb.Append("  - ");
                sb.Append(entries[i].Key);
                sb.Append(": ");
                sb.Append(Mathf.Min(entries[i].Value, 1f).ToStringPercent());
            }

            // If HAR is active with multiple races, show race chances too.
            if (FactionCache.HumanlikeRacesCount > 1 && filter.RaceTotalWeight > 0)
            {
                sb.Append("\n\n");
                sb.Append(("FCEmpireMemberRaceChances".Translate() + ":").AsTipTitle());
                sb.Append("\n");

                List<KeyValuePair<string, float>> raceEntries = new List<KeyValuePair<string, float>>();
                foreach (KeyValuePair<ThingDef, float> kvp in filter.RaceWeights)
                {
                    if (kvp.Value <= 0f)
                    {
                        continue;
                    }
                    float chance = filter.GetRaceChance(kvp.Key);
                    if (chance > 0f)
                    {
                        raceEntries.Add(new KeyValuePair<string, float>(kvp.Key.LabelCap, chance));
                    }
                }

                raceEntries.Sort((a, b) => b.Value.CompareTo(a.Value));

                for (int i = 0; i < raceEntries.Count; i++)
                {
                    if (i > 0)
                    {
                        sb.Append("\n");
                    }
                    sb.Append("  - ");
                    sb.Append(raceEntries[i].Key);
                    sb.Append(": ");
                    sb.Append(Mathf.Min(raceEntries[i].Value, 1f).ToStringPercent());
                }
            }

            cachedDescription = sb.ToString();
            dirty = false;
            __result = cachedDescription;
        }
    }
}
