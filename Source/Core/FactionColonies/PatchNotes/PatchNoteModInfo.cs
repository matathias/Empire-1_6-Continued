using UnityEngine;

namespace FactionColonies;

/// <summary>
/// Settings shared by every patch note of one mod, read from that mod's PatchNotes/_mod.md:
/// banner, link buttons and the notes shown under each entry unless the entry has its own.
/// </summary>
public class PatchNoteModInfo
{
    /// <summary>A link button in the window's title bar.</summary>
    public class Link
    {
        public string url;
        public string tooltip;
        public string iconPath;
    }

    /// <summary>Lowercase package id without the Steam postfix, e.g. "matathias.empire".</summary>
    public readonly string modId;

    /// <summary>The mod that owns the PatchNotes folder. Null for mod info built in tests.</summary>
    public readonly ModContentPack pack;

    /// <summary>The "# Title" line of _mod.md. Used as the mod name when there is no pack.</summary>
    public string displayName = "";

    public string bannerImagePath = "";
    public readonly List<Link> links = [];
    public readonly List<string> notes = [];

    private List<Texture2D> linkButtonImagesCached;
    private Texture2D bannerImageCached;

    public PatchNoteModInfo(string modId, ModContentPack pack = null)
    {
        this.modId = modId;
        this.pack = pack;
    }

    public string ModName => pack?.Name ?? (displayName.NullOrEmpty() ? modId : displayName);

    public List<string> Urls => field ??= links.Select(link => link.url).ToList();

    public List<string> Tooltips => field ??= links.Select(link => link.tooltip).ToList();

    /// <summary>Icons of the link buttons, loaded on first use (textures can only load on the main thread).</summary>
    public List<Texture2D> LinkButtonImages
        => linkButtonImagesCached ??= links.Select(link => ContentFinder<Texture2D>.Get(link.iconPath)).ToList();

    /// <summary>The banner texture, or null if no banner is set.</summary>
    public Texture2D BannerImage
    {
        get
        {
            if (bannerImageCached == null && !bannerImagePath.NullOrEmpty())
                bannerImageCached = ContentFinder<Texture2D>.Get(bannerImagePath, false);
            return bannerImageCached;
        }
    }
}
