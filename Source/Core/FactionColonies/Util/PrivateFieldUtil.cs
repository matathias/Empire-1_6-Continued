using System.Runtime.CompilerServices;
using UnityEngine;

namespace FactionColonies;

/// <summary>
/// Typed by-ref access to private RimWorld fields that Empire must overwrite. Each accessor is bound
/// by the runtime on first call; if RimWorld renames or removes the field, that call throws
/// <see cref="MissingFieldException"/> instead of silently doing nothing.
/// </summary>
internal static class PrivateFieldUtil
{
    /// <summary>
    /// <c>FactionDef.factionIcon</c>: the texture lazily loaded from <c>factionIconPath</c> by the
    /// <c>FactionIcon</c> getter and never re-read. Null it after changing the path so the getter
    /// reloads.
    /// </summary>
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "factionIcon")]
    internal static extern ref Texture2D FactionIconCache(FactionDef def);

    /// <summary>
    /// <c>WorldObjectDef.expandingIconTextureInt</c>: the cached texture behind
    /// <c>ExpandingIconTexture</c>, loaded once from <c>expandingIconTexture</c>.
    /// </summary>
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "expandingIconTextureInt")]
    internal static extern ref Texture2D ExpandingIconCache(WorldObjectDef def);

    /// <summary>
    /// <c>WorldObjectDef.material</c>: the cached world-map material built once from <c>texture</c>.
    /// </summary>
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "material")]
    internal static extern ref Material MaterialCache(WorldObjectDef def);
}
