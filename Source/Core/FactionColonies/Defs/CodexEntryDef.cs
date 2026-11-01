using FactionColonies.util;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Defines a single entry in the Empire Codex — an in-game reference system
    /// that explains mechanics, formulas, and UI navigation to the player.
    /// <para>Entries are defined in XML and picked up automatically by <c>DefDatabase</c>.
    /// Submods can add their own entries by including CodexEntryDef XML in their Defs folder.</para>
    /// </summary>
    public class CodexEntryDef : Def
    {
        /// <summary>
        /// The category this entry belongs to. Determines grouping in the left pane,
        /// accent color, mod association, and banner image.
        /// </summary>
        public CodexCategoryDef category;

        /// <summary>
        /// Sort order within the category. Lower values appear first.
        /// </summary>
        public int displayOrder;

        /// <summary>
        /// Optional icon path displayed next to the entry title.
        /// </summary>
        [NoTranslate]
        public string iconPath;

        /// <summary>
        /// Optional list of related CodexEntryDefs, rendered as "See Also"
        /// links at the bottom of the detail pane.
        /// </summary>
        public List<CodexEntryDef> seeAlso;

        /// <summary>
        /// Optional list of texture paths for an image carousel displayed
        /// in the detail pane. Useful for UI screenshots or step-by-step guides.
        /// Images are loaded via <c>ContentFinder&lt;Texture2D&gt;.Get()</c>.
        /// </summary>
        [NoTranslate]
        public List<string> imagePaths;

        /// <summary>
        /// Optional Type that implements <see cref="ICodexDynamicProvider"/>.
        /// If set, the Codex window instantiates it and calls
        /// <see cref="ICodexDynamicProvider.GetDynamicContent"/> at render time
        /// to append live game-state information below the static description.
        /// </summary>
        public Type dynamicProvider;

        /* Cached runtime data */
        private Texture2D iconCached;
        private List<Texture2D> imagesCached;
        private ICodexDynamicProvider providerInstance;

        /// <summary>
        /// Returns the cached icon texture, or null if no iconPath is set.
        /// </summary>
        public Texture2D Icon
        {
            get
            {
                if (iconCached is null && !iconPath.NullOrEmpty())
                    iconCached = ContentFinder<Texture2D>.Get(iconPath, false);
                return iconCached;
            }
        }

        /// <summary>
        /// Returns the cached image textures for the carousel, or an empty list.
        /// </summary>
        public List<Texture2D> Images
        {
            get
            {
                if (imagesCached is null)
                {
                    imagesCached = new List<Texture2D>();
                    if (!imagePaths.NullOrEmpty())
                    {
                        foreach (string path in imagePaths)
                        {
                            Texture2D tex = ContentFinder<Texture2D>.Get(path, false);
                            if (tex is object)
                                imagesCached.Add(tex);
                        }
                    }
                }
                return imagesCached;
            }
        }

        /// <summary>
        /// The entry description with player-facing markup resolved: {FACTION}/{FACTION_TITLE} tokens
        /// and [b]/[i] emphasis. Not cached — the name/title are mutable, and the resolve early-outs
        /// when no token is present.
        /// </summary>
        public string FormattedDesc => description.Format();

        /// <summary>
        /// Returns the singleton <see cref="ICodexDynamicProvider"/> instance, or null.
        /// </summary>
        public ICodexDynamicProvider DynamicProvider
        {
            get
            {
                if (providerInstance is null && dynamicProvider is object)
                {
                    providerInstance = (ICodexDynamicProvider)Activator.CreateInstance(dynamicProvider);
                }
                return providerInstance;
            }
        }

        public override void ClearCachedData()
        {
            base.ClearCachedData();
            iconCached = null;
            imagesCached = null;
            providerInstance = null;
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
                yield return error;

            if (category is null)
                yield return "category is null. Must reference a CodexCategoryDef";
            if (dynamicProvider is object && !typeof(ICodexDynamicProvider).IsAssignableFrom(dynamicProvider))
                yield return $"dynamicProvider type '{dynamicProvider.FullName}' does not implement ICodexDynamicProvider";

            // Unknown seeAlso entries are reported by the cross-reference loader (the
            // field is List<CodexEntryDef>, resolved from defNames in XML).
        }
    }
}
