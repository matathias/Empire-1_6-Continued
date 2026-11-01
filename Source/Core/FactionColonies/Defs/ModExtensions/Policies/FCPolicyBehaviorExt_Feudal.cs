using RimWorld;

namespace FactionColonies
{
    public class FCPolicyBehaviorExt_Feudal : FCPolicyBehaviorExtension
    {
        public int mercenaryCooldownTicks = GenDate.TicksPerSeason;
        public float relationChanceFactor = 20f;
        public string readyLetterKey = "FCActionAvailable";
        public string cooldownMessageKey = "FCActionMercenaryOnCooldown";
    }
}
