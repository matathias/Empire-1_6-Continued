namespace FactionColonies;

[DefOf]
public class BiomeResourceDefOf
{
    public static BiomeResourceDef defaultBiome;
    static BiomeResourceDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(BiomeResourceDefOf));
    }
}
