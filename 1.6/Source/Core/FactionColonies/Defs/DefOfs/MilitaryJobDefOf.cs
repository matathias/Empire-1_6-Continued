using RimWorld;

namespace FactionColonies
{
    [DefOf]
    public static class MilitaryJobDefOf
    {
        public static MilitaryJobDef Undefined;
        public static MilitaryJobDef Cooldown;
        public static MilitaryJobDef Deploy;
        public static MilitaryJobDef RaidEnemySettlement;
        public static MilitaryJobDef EnslaveEnemySettlement;
        public static MilitaryJobDef CaptureEnemySettlement;
        public static MilitaryJobDef RazeEnemySettlement;
        /// <summary>UI-presentation state for a settlement that is foreign-defending another
        /// settlement's defensive battle (lent its squad). The actual op kind is
        /// <see cref="DefendOwnSettlement"/>; this def is just the comp's <c>militaryJob</c>
        /// label for the foreign-defender side.</summary>
        public static MilitaryJobDef DefendFriendlySettlement;
        /// <summary>Op kind for every defensive op (Empire defending one of its settlements or an
        /// external <see cref="IRaidTarget"/>). Handler is <see cref="MilitaryJobHandler_Defend"/>.</summary>
        public static MilitaryJobDef DefendOwnSettlement;
    }
}
