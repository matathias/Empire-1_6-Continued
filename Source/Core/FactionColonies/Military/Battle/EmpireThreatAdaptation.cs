using RimWorld;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Tracks Empire-specific threat adaptation using the active storyteller's curves.
    /// Maintains its own adaptDays value that responds to Empire battle outcomes,
    /// separate from the vanilla StoryWatcher_Adaptation which tracks player colony events.
    /// </summary>
    public class EmpireThreatAdaptation : IExposable
    {
        private float adaptDays;

        /// <summary>Matches StoryWatcher_Adaptation.UpdateInterval (private const).</summary>
        private const int AdaptationUpdateInterval = 30000;

        /// <summary>
        /// Returns the threat multiplier derived from the storyteller's adaptation curve
        /// and the player's difficulty adaptation effect factor.
        /// </summary>
        public double ThreatFactor
        {
            get
            {
                StorytellerDef def = Find.Storyteller.def;
                if (def.pointsFactorFromAdaptDays == null) return 1.0;

                float raw = def.pointsFactorFromAdaptDays.Evaluate(adaptDays);
                float effectFactor = Find.Storyteller.difficulty.adaptationEffectFactor;
                return Mathf.Lerp(1f, raw, effectFactor);
            }
        }

        /// <summary>
        /// Called from FactionFC.Tick(). Uses the storyteller's growth rate curve
        /// to passively increase adaptDays over time.
        /// </summary>
        public void Tick()
        {
            if (Find.TickManager.TicksGame % AdaptationUpdateInterval != 0) return;

            StorytellerDef def = Find.Storyteller.def;
            if (def.adaptDaysGrowthRateCurve == null) return;

            float growth = 0.5f * def.adaptDaysGrowthRateCurve.Evaluate(adaptDays) * GrowthMultiplier;
            if (adaptDays > 0f)
                growth *= Find.Storyteller.difficulty.adaptationGrowthRateFactorOverZero;

            adaptDays += growth;
            adaptDays = Mathf.Clamp(adaptDays, def.adaptDaysMin, def.adaptDaysMax);
        }

        // threatAdaptationGrowthMultiplier (faction-wide) scales how fast threat escalates, on both passive growth and battle wins.
        private static float GrowthMultiplier => (float)(FindFC.FactionComp?.GetStatValue(FCStatDefOf.threatAdaptationGrowthMultiplier) ?? 1);

        public void Notify_BattleWon()
        {
            adaptDays += 2f * GrowthMultiplier;
            adaptDays = Mathf.Min(adaptDays, Find.Storyteller.def.adaptDaysMax);
        }

        public void Notify_BattleLost()
        {
            StorytellerDef def = Find.Storyteller.def;
            if (def.adaptDaysLossFromColonistLostByPostPopulation == null) return;

            int count = FindFC.Settlements.Count;
            float loss = def.adaptDaysLossFromColonistLostByPostPopulation.Evaluate(count);
            adaptDays = Mathf.Max(def.adaptDaysMin, adaptDays - loss);
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref adaptDays, "adaptDays", 0f);
        }
    }
}
