namespace FactionColonies
{
    /// <summary>
    /// Allows submods to contribute additive or multiplicative modifiers to the Empire Threat Level (ETL).
    /// Register implementations via <see cref="ThreatScalingRegistry"/>.
    /// </summary>
    public interface IThreatScalingContributor
    {
        /// <summary>
        /// Returns an additive contribution to the ETL (added to the raw score before multiplication).
        /// Return 0 for no effect.
        /// </summary>
        double GetAdditiveContribution(FactionFC faction);

        /// <summary>
        /// Returns a multiplicative contribution to the ETL (multiplied into the final result).
        /// Return 1.0 for no effect.
        /// </summary>
        double GetMultiplicativeContribution(FactionFC faction);
    }
}
