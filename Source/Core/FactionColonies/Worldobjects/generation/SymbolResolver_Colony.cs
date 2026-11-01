using RimWorld;
using RimWorld.BaseGen;
using Verse;

namespace FactionColonies
{
    public class SymbolResolver_Colony : SymbolResolver
    {
        public override void Resolve(ResolveParams rp)
        {
            int dist = 0;
            if (rp.edgeDefenseWidth.HasValue)
                dist = rp.edgeDefenseWidth.Value;
            else if (rp.rect.Width >= 20 && rp.rect.Height >= 20 &&
                     (rp.faction.def.techLevel >= TechLevel.Industrial || Rand.Bool))
                dist = Rand.Bool ? 2 : 4;

            float emptyNodes = (float)rp.rect.Area / 144f * 0.17f;
            BaseGen.globalSettings.minEmptyNodes = emptyNodes < 1f ? 0 : GenMath.RoundRandom(emptyNodes);

            if (rp.faction.def.techLevel >= TechLevel.Industrial)
            {
                BaseGen.symbolStack.Push("outdoorLighting", rp);
                int num = Rand.Chance(0.75f) ? GenMath.RoundRandom(rp.rect.Area / 400f) : 0;
                for (int index = 0; index < num; ++index)
                {
                    ResolveParams resolveParams2 = rp;
                    resolveParams2.faction = rp.faction;
                    BaseGen.symbolStack.Push("firefoamPopper", resolveParams2);
                }
            }

            if (dist > 0)
            {
                ResolveParams resolveParams2 = rp;
                resolveParams2.faction = rp.faction;
                resolveParams2.edgeDefenseWidth = dist;
                resolveParams2.edgeThingMustReachMapEdge = rp.edgeThingMustReachMapEdge ?? true;
                BaseGen.symbolStack.Push("edgeDefense", resolveParams2);
            }

            ResolveParams resolveParams3 = rp;
            resolveParams3.rect = rp.rect.ContractedBy(dist);
            resolveParams3.faction = rp.faction;
            BaseGen.symbolStack.Push("ensureCanReachMapEdge", resolveParams3);
            ResolveParams resolveParams4 = rp;
            resolveParams4.rect = rp.rect.ContractedBy(dist);
            resolveParams4.faction = rp.faction;
            resolveParams4.floorOnlyIfTerrainSupports = rp.floorOnlyIfTerrainSupports ?? true;
            BaseGen.symbolStack.Push("basePart_outdoors", resolveParams4);
            ResolveParams resolveParams5 = rp;
            resolveParams5.floorDef = TerrainDefOf.Bridge;
            resolveParams5.floorOnlyIfTerrainSupports = rp.floorOnlyIfTerrainSupports ?? true;
            resolveParams5.allowBridgeOnAnyImpassableTerrain = rp.allowBridgeOnAnyImpassableTerrain ?? true;
            BaseGen.symbolStack.Push("floor", resolveParams5);
            BaseGen.symbolStack.Push("removeDangerousTerrain", rp);
            if (ModsConfig.BiotechActive)
            {
                ResolveParams resolveParams6 = rp;
                resolveParams6.rect = rp.rect.ExpandedBy(Rand.Range(1, 4));
                resolveParams6.edgeUnpolluteChance = 0.5f;
                BaseGen.symbolStack.Push("unpollute", resolveParams6);
            }
        }
    }
}
