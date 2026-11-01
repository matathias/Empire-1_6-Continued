namespace FactionColonies
{
    /// <summary>Squad-projected combat power. <see cref="militaryLevel"/> is on the same
    /// scale as <see cref="WorldSettlementFC.settlementMilitaryLevel"/>.
    /// <see cref="militaryEfficiency"/> is a multiplier (typical range 0.5-1.5, dampened
    /// by <see cref="FCSettings.efficiencyDamping"/> in <see cref="SimulateBattleFc"/>).</summary>
    public struct SquadPower
    {
        public double militaryLevel;
        public double militaryEfficiency;
        public SquadPower(double level, double efficiency)
        {
            militaryLevel = level;
            militaryEfficiency = efficiency;
        }
    }
}
