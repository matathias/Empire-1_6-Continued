using FactionColonies.util;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Text;
using Verse;

namespace FactionColonies
{
    public class SettlementTypeExtension_Orbital : SettlementTypeExtension
    {
        public int constructionDays = 8;

        private static string[] spaceLocations;
        private static string[] spaceKeywords;

        private static string[] GetSpaceLocations()
        {
            if (spaceLocations == null)
            {
                spaceLocations = new string[]
                {
                    "FCOrbitalLocation1".Translate(),
                    "FCOrbitalLocation2".Translate(),
                    "FCOrbitalLocation3".Translate(),
                    "FCOrbitalLocation4".Translate(),
                    "FCOrbitalLocation5".Translate(),
                    "FCOrbitalLocation6".Translate(),
                    "FCOrbitalLocation7".Translate()
                };
            }
            return spaceLocations;
        }

        private static string[] GetSpaceKeywords()
        {
            if (spaceKeywords == null)
            {
                spaceKeywords = new string[]
                {
                    "FCOrbitalKeyword1".Translate(),
                    "FCOrbitalKeyword2".Translate(),
                    "FCOrbitalKeyword3".Translate(),
                    "FCOrbitalKeyword4".Translate(),
                    "FCOrbitalKeyword5".Translate(),
                    "FCOrbitalKeyword6".Translate(),
                    "FCOrbitalKeyword7".Translate(),
                    "FCOrbitalKeyword8".Translate(),
                    "FCOrbitalKeyword9".Translate(),
                    "FCOrbitalKeyword10".Translate()
                };
            }
            return spaceKeywords;
        }

        public static void InvalidateCache()
        {
            spaceLocations = null;
            spaceKeywords = null;
        }

        public override string GetSettlementName(string fallback = "Settlement")
        {
            string[] keywords = GetSpaceKeywords();

            // Get the base name using the same logic as regular settlements
            string baseName = base.GetSettlementName("Orbital");

            // Get a random space keyword
            string spaceKeyword = keywords[Rand.Range(0, keywords.Length)];

            // Combine base name with space keyword
            return $"{baseName} {spaceKeyword}";
        }
        public override int GetCreationCost()
        {
            int baseCost = 5000;

            /* The four orbital types are horizontal specializations off the base station (each gated behind its own
             * research, each adding two exclusive buildings), so cost is base-cheapest, Glitter highest, with Advanced
             * nudged just above Logistics for its permanent build-time-cutting shipyard. */
            switch (parentDef.defName)
            {
                case "WorldSettlementDef_Orbital":
                    return baseCost;
                case "WorldSettlementDef_Orbital_Logistics":
                    return 7000;
                case "WorldSettlementDef_Orbital_Advanced":
                    return 7500;
                case "WorldSettlementDef_Orbital_Glitter":
                    return 9000;
                default:
                    return baseCost;
            }
        }
        public override bool TileIsValidForSettlement(PlanetTile tile, StringBuilder reason = null)
        {
            if (Find.WorldObjects.AnyWorldObjectAt(tile))
            {
                reason?.Append("FCOrbitalTileOccupied".Translate());
                return false;
            }
            return true;
        }
        /// <summary>
        /// Takes a tile, and then returns the correct corresponding orbital tile.
        /// </summary>
        /// <param name="tile"></param>
        /// <returns></returns>
        public override PlanetTile GetTileForSettlement(PlanetTile tile)
        {
            var worldGrid = Find.WorldGrid;
            if (tile.Layer == worldGrid.Orbit)
            {
                return tile;
            }
            else
            {
                return new PlanetTile(tile.tileId, worldGrid.Orbit);
            }
        }
        public override int GetCreationTime(PlanetTile destination)
        {
            int baseDays = constructionDays;

            // Build time scales with tier to mirror creation cost (Advanced just above Logistics, Glitter highest).
            switch (parentDef.defName)
            {
                case "WorldSettlementDef_Orbital":
                    return baseDays * GenDate.TicksPerDay;        // 8 days
                case "WorldSettlementDef_Orbital_Logistics":
                    return (baseDays + 5) * GenDate.TicksPerDay;  // 13 days
                case "WorldSettlementDef_Orbital_Advanced":
                    return (baseDays + 6) * GenDate.TicksPerDay;  // 14 days
                case "WorldSettlementDef_Orbital_Glitter":
                    return (baseDays + 8) * GenDate.TicksPerDay;  // 16 days
                default:
                    return baseDays * GenDate.TicksPerDay;
            }
        }
        public override string GetLocationText(WorldSettlementFC settlement)
        {
            string[] locations = GetSpaceLocations();
            // Use the settlement's tileid to deterministically select a location text
            int locationIndex = Math.Abs(settlement.Tile.tileId) % locations.Length;
            return locations[locationIndex];
        }
        public override TaxDeliveryMode GetTaxDeliveryMode(bool canUseShuttle, PlanetTile sourceTile)
        {
            // Force drop pods or shuttles for orbital platform settlements
            if (sourceTile != PlanetTile.Invalid)
            {
                if (ModsConfig.RoyaltyActive && canUseShuttle)
                {
                    return TaxDeliveryMode.Shuttle;
                }
                return TaxDeliveryMode.DropPod;
            }

            return base.GetTaxDeliveryMode(canUseShuttle, sourceTile);
        }

        public override bool CanBeRaidedByFaction(Faction attackingFaction)
        {
            return attackingFaction?.def?.techLevel >= TechLevel.Spacer;
        }
    }
}
