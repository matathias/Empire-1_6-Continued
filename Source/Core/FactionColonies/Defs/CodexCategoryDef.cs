using UnityEngine;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Defines a category grouping for <see cref="CodexEntryDef"/> entries in the Codex.
    /// Categories hold the accent color, display order, and mod association.
    /// <para>Submods can define their own categories by adding CodexCategoryDef XML.</para>
    /// </summary>
    public class CodexCategoryDef : Def
    {
        /// <summary>
        /// Accent color used for category headers, entry accent bars, and title accent lines.
        /// </summary>
        public Color color = Color.gray;

        /// <summary>
        /// Sort order in the left pane. Lower values appear first.
        /// </summary>
        public int displayOrder = 100;

        /// <summary>
        /// The packageId of the mod that owns this category (e.g. "matathias.empire").
        /// Used for top-level grouping in the Codex window.
        /// </summary>
        [NoTranslate]
        public string modId = "";

        /// <summary>
        /// Optional banner texture path displayed in the right info pane.
        /// If empty, falls back to the source mod's About/Preview.png.
        /// </summary>
        [NoTranslate]
        public string bannerPath;

        /* Cached runtime data */
        private Texture2D bannerCached;
        private bool bannerLookedUp;
        private ModContentPack modContentPackCached;

        /// <summary>
        /// Returns the banner texture. Checks <see cref="bannerPath"/> first,
        /// then falls back to the source mod's About/Preview.png.
        /// </summary>
        public Texture2D BannerImage
        {
            get
            {
                if (!bannerLookedUp)
                {
                    bannerLookedUp = true;
                    if (!bannerPath.NullOrEmpty())
                        bannerCached = ContentFinder<Texture2D>.Get(bannerPath, false);
                    if (bannerCached is null)
                        bannerCached = ModContentPack?.ModMetaData?.PreviewImage;
                }
                return bannerCached;
            }
        }

        /// <summary>
        /// Returns the <see cref="ModContentPack"/> matching <see cref="modId"/>.
        /// </summary>
        public ModContentPack ModContentPack
        {
            get
            {
                if (modContentPackCached is null && !modId.NullOrEmpty())
                {
                    modContentPackCached = LoadedModManager.RunningModsListForReading
                        .FirstOrFallback(p => PackMatchesModId(p));
                }
                return modContentPackCached;
            }
        }

        /// <summary>
        /// The display name of the owning mod, or the raw modId if not found.
        /// </summary>
        public string ModName => ModContentPack?.ModMetaData.Name ?? modId;

        private bool PackMatchesModId(ModContentPack pack)
        {
            if (pack.ModMetaData.appendPackageIdSteamPostfix)
                return pack.PackageId == modId + ModMetaData.SteamModPostfix;
            return pack.PackageId == modId;
        }

        public override void ClearCachedData()
        {
            base.ClearCachedData();
            bannerCached = null;
            bannerLookedUp = false;
            modContentPackCached = null;
        }
    }
}
