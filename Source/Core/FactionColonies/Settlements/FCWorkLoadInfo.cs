using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    public enum FCWorkLoad : Byte
    {
        Light, //Adds to overmax
        Medium, //Adds 1 to max
        Heavy, //Adds 2 to max
        DebugLight, //-50 health/day
        DebugHeavy //-100 healty/day
    }

    /* Central lookup table for FCWorkLoad per-value data: health delta, worker
     * contribution, label/explanation translation keys, trend color, and an optional
     * availability predicate. All workload switches and hardcoded picker lists in
     * the prisoner system route through here so adding a new enum value is a
     * single dictionary entry plus the new Keyed translation strings. */
    public static class FCWorkLoadInfo
    {
        private class WorkLoadData
        {
            public int HealthDelta;          // applied per day in AdvanceDailyHealth
            public int WorkerSlots;          // ReturnMaxWorkersFromPrisoners contribution
            public int OverMaxSlots;         // ReturnOverMaxWorkersFromPrisoners contribution
            public string LabelKey;          // translation key, e.g. "FCHeavy"
            public string ExplanationKey;    // translation key, e.g. "FCHeavyExplanation"
            public Color TrendColor;         // used by GetWorkloadPresentation
            public Func<bool> Available;     // null = always available
        }

        private static readonly Dictionary<FCWorkLoad, WorkLoadData> table =
            new Dictionary<FCWorkLoad, WorkLoadData>
        {
            { FCWorkLoad.Heavy,  new WorkLoadData {
                HealthDelta = -4, WorkerSlots = 2, OverMaxSlots = 0,
                LabelKey = "FCHeavy",  ExplanationKey = "FCHeavyExplanation",
                TrendColor = AccentUtil.StatBad } },
            { FCWorkLoad.Medium, new WorkLoadData {
                HealthDelta = -2, WorkerSlots = 1, OverMaxSlots = 0,
                LabelKey = "FCMedium", ExplanationKey = "FCMediumExplanation",
                TrendColor = AccentUtil.StatMedGood } },
            { FCWorkLoad.Light,  new WorkLoadData {
                HealthDelta = +1, WorkerSlots = 0, OverMaxSlots = 1,
                LabelKey = "FCLight",  ExplanationKey = "FCLightExplanation",
                TrendColor = AccentUtil.StatGood } },
            { FCWorkLoad.DebugLight,  new WorkLoadData {
                HealthDelta = -50, WorkerSlots = 1, OverMaxSlots = 1,
                LabelKey = "FCDebugLight",  ExplanationKey = "FCDebugLightExplanation",
                TrendColor = AccentUtil.StatMedGood,
                Available = () => DebugSettings.godMode} },
            { FCWorkLoad.DebugHeavy,  new WorkLoadData {
                HealthDelta = -100, WorkerSlots = 2, OverMaxSlots = 2,
                LabelKey = "FCDebugHeavy",  ExplanationKey = "FCDebugHeavyExplanation",
                TrendColor = AccentUtil.StatBad,
                Available = () => DebugSettings.godMode} },
        };

        private static WorkLoadData Get(FCWorkLoad w)
        {
            WorkLoadData d;
            table.TryGetValue(w, out d);
            return d;
        }

        public static int HealthDelta(FCWorkLoad w)
        {
            WorkLoadData d = Get(w);
            return d is null ? 0 : d.HealthDelta;
        }

        public static int WorkerSlots(FCWorkLoad w)
        {
            WorkLoadData d = Get(w);
            return d is null ? 0 : d.WorkerSlots;
        }

        public static int OverMaxSlots(FCWorkLoad w)
        {
            WorkLoadData d = Get(w);
            return d is null ? 0 : d.OverMaxSlots;
        }

        public static string Label(FCWorkLoad w)
        {
            WorkLoadData d = Get(w);
            if (d is null) return "?";
            return d.LabelKey.Translate().CapitalizeFirst();
        }

        public static string Explanation(FCWorkLoad w)
        {
            WorkLoadData d = Get(w);
            if (d is null) return "";
            return d.ExplanationKey.Translate();
        }

        public static Color TrendColor(FCWorkLoad w)
        {
            WorkLoadData d = Get(w);
            return d is null ? Color.white : d.TrendColor;
        }

        public static string TrendText(FCWorkLoad w)
        {
            int delta = HealthDelta(w);
            string sign = delta > 0 ? "+" : "";
            return "FCPrisonerHealthChangePerDay".Translate(sign + delta);
        }

        public static bool IsAvailable(FCWorkLoad w)
        {
            WorkLoadData d = Get(w);
            if (d is null) return false;
            return d.Available is null || d.Available();
        }

        public static IEnumerable<FCWorkLoad> AvailableValues
        {
            get
            {
                foreach (KeyValuePair<FCWorkLoad, WorkLoadData> kvp in table)
                {
                    if (IsAvailable(kvp.Key)) yield return kvp.Key;
                }
            }
        }

        /* Builds the standard "label - explanation" picker shared by all five workload
         * float menus. Caller-supplied callback receives the chosen value. */
        public static List<FloatMenuOption> BuildSelectionMenu(Action<FCWorkLoad> onSelect)
        {
            List<FloatMenuOption> list = new List<FloatMenuOption>();
            foreach (FCWorkLoad w in AvailableValues)
            {
                FCWorkLoad captured = w;
                list.Add(new FloatMenuOption(
                    Label(captured) + " - " + Explanation(captured),
                    delegate { onSelect(captured); }));
            }
            return list;
        }
    }
}
