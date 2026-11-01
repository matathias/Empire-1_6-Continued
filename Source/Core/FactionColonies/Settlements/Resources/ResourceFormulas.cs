using System.Collections.Generic;
using System.Linq;

namespace FactionColonies
{
    /// <summary>
    /// Pure calculation methods for resource production and tithe economics.
    /// These methods have zero RimWorld dependencies, making them unit-testable.
    /// </summary>
    public static class ResourceFormulas
    {
        /// <summary>
        /// Calculates the total production base from additive bonuses.
        /// </summary>
        public static double CalculateProductionBase(IEnumerable<double> additiveValues)
        {
            return additiveValues.Sum();
        }

        /// <summary>
        /// Calculates the total production multiplier from multiplier bonuses and tax bonus.
        /// </summary>
        public static double CalculateProductionMult(IEnumerable<double> multiplierValues, double taxBonus)
        {
            double result = 1;
            foreach (double value in multiplierValues)
            {
                result *= value;
            }
            return result * taxBonus;
        }

        /// <summary>
        /// Calculates total production: base × multiplier.
        /// </summary>
        public static double CalculateProduction(double productionBase, double productionMult)
        {
            return productionBase * productionMult;
        }

        /// <summary>
        /// Calculates raw total production: production per worker × assigned workers.
        /// </summary>
        public static double CalculateRawTotalProduction(double production, int assignedWorkers)
        {
            return production * assignedWorkers;
        }

        /// <summary>
        /// Converts raw production to market value.
        /// </summary>
        public static double CalculateMarketValue(double rawTotalProduction, double silverPerResource)
        {
            return rawTotalProduction * silverPerResource;
        }

        /// <summary>
        /// Calculates how many of a thing can be afforded within a budget.
        /// </summary>
        public static int MaxThingCanAfford(double budget, float thingValue)
        {
            return (int)(budget / thingValue);
        }

        /// <summary>
        /// Checks whether a thing amount fits within the available budget.
        /// </summary>
        public static bool CanAffordThingAmount(float thingTotalValue, double availableBudget)
        {
            return thingTotalValue <= availableBudget;
        }
    }
}