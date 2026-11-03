using UnityEngine;

namespace FactionColonies;

/* PrivateFieldUtil's UnsafeAccessors bind on first call, so a RimWorld field rename only shows up at
   runtime. These run each accessor against a fresh, unregistered def (no shared game state) and check
   that the ref aliases the real field: a value written through the accessor must come back out of the
   public getter that reads that field. */
public static class PrivateFieldUtilTests
{
    [EmpireTest("Util")]
    public static void FactionIconCache_AliasesFactionIconField()
    {
        var def = new FactionDef();
        PrivateFieldUtil.FactionIconCache(def) = BaseContent.WhiteTex;
        TestAssert.IsTrue(def.FactionIcon == BaseContent.WhiteTex, "FactionIcon returns the texture written through the accessor");

        PrivateFieldUtil.FactionIconCache(def) = null;
        TestAssert.IsTrue(def.FactionIcon == BaseContent.BadTex, "after nulling the cache, the getter reloads (empty path -> BadTex)");
    }

    [EmpireTest("Util")]
    public static void ExpandingIconCache_AliasesExpandingIconTextureField()
    {
        var def = new WorldObjectDef();
        PrivateFieldUtil.ExpandingIconCache(def) = BaseContent.WhiteTex;
        TestAssert.IsTrue(def.ExpandingIconTexture == BaseContent.WhiteTex, "ExpandingIconTexture returns the texture written through the accessor");
    }

    [EmpireTest("Util")]
    public static void MaterialCache_AliasesMaterialField()
    {
        // The Material getter returns null when texture is empty, before it reads the cache.
        var def = new WorldObjectDef { texture = "World/WorldObjects/DefaultSettlement" };
        PrivateFieldUtil.MaterialCache(def) = BaseContent.WhiteMat;
        TestAssert.IsTrue(def.Material == BaseContent.WhiteMat, "Material returns the material written through the accessor");
    }
}
