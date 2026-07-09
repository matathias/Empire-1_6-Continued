using FactionColonies.util;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    class PatchNoteDef : Def
    {
        private int major = 0;
        private int minor = 0;
        private int patch = 0;

        [NoTranslate]
        private readonly string releaseDate = "";

        private DateTime releaseDateParsed;

        private readonly PatchNoteType patchNoteType = PatchNoteType.Undefined;
        private readonly List<string> patchNoteLines = new List<string>();
        private readonly List<string> additionalNotes = new List<string>();
        private readonly List<string> linkButtonToolTips = new List<string>();

        [NoTranslate]
        public readonly string modId = "";

        [NoTranslate]
        private readonly List<string> links = new List<string>();

        [NoTranslate]
        private readonly List<string> authors = new List<string>();

        [NoTranslate]
        private readonly List<string> linkButtonImagePaths = new List<string>();

        [NoTranslate]
        private readonly string bannerImagePath = "";

        private ModContentPack modContentPackCached = null;
        private List<Texture2D> linkButtonImagesCached = new List<Texture2D>();
        private Texture2D bannerImageCached;

        /// <summary>
        /// The title of the update example: [Empire] Update 0.38.00
        /// </summary>
        public string Title => $"[{ModName}] {label} {VersionNumber}";

        /// <summary>
        /// Short title without mod name prefix: "Prosperity Change 0.114.00"
        /// </summary>
        public string ShortTitle => $"{label} {VersionNumber}";

        /// <summary>
        /// Returns the ModContentPack assosiated with the given ModId
        /// </summary>
        public ModContentPack ModContentPack
        {
            get
            {
                modContentPackCached = modContentPackCached ?? (modContentPackCached = LoadedModManager.RunningModsListForReading.FirstOrFallback(PackHasModId));

                if (modContentPackCached == null)
                {
                    LogUtil.ErrorOnce($"Couldn't find mod with ModId: {modId} Please check the spelling in the PatchNoteDef!", VersionSortKey);
                }

                return modContentPackCached;
            }
        }

        /// <summary>
        /// Checks if a given <paramref name="pack"/> has the saved modId
        /// </summary>
        /// <param name="pack"></param>
        /// <returns>true if it does, false otherwise</returns>
        private bool PackHasModId(ModContentPack pack)
        {
            if (pack.ModMetaData.appendPackageIdSteamPostfix)
            {
                return pack.PackageId == modId + ModMetaData.SteamModPostfix;
            }

            return pack.PackageId == modId;
        }

        /// <summary>
        /// Returns the mod name or an error if the mod wasn't found
        /// </summary>
        public string ModName => ModContentPack?.ModMetaData.Name ?? "MissingModContentPack";

        /// <summary>
        /// The def description
        /// </summary>
        public string Description => description;

        /// <summary>
        /// The complete Version number formatted like: "1.02.03"
        /// </summary>
        public string VersionNumber => $"{major}.{minor}.{patch}";

        /// <summary>
        /// The Major version number
        /// </summary>
        public int Major => major;

        /// <summary>
        /// The Minor version number
        /// </summary>
        public int Minor => minor;

        /// <summary>
        /// The Patch version number
        /// </summary>
        public int Patch => patch;

        /// <summary>
        /// Integer sort key encoding the full version for correct ordering.
        /// </summary>
        public int VersionSortKey => major * 1000000 + minor * 1000 + patch;

        /// <summary>
        /// Returns the PatchNoteType
        /// </summary>
        public PatchNoteType GetPatchNoteType => patchNoteType;

        /// <summary>
        /// Returns a link as provided by the def
        /// </summary>
        public List<string> Links => links;

        /// <summary>
        /// Returns the patch notes seperated by new lines
        /// </summary>
        public string PatchNotesFormatted => string.Join("\n", patchNoteLines.Select(line => "\u2022 " + line));

        /// <summary>
        /// Returns additional notes as provided by the def
        /// </summary>
        public string AdditionalNotesFormatted => string.Join("\n", additionalNotes);

        /// <summary>
        /// Returns the list of authors in this format: "name0, name1, name2, ..., nameN-1 and nameN" where N is the amount of authors
        /// only returns the name of one author if there is only one
        /// </summary>
        public string AuthorsFormatted
        {
            get
            {
                if (authors.NullOrEmpty()) return "";

                List<string> workList = authors.ListFullCopy();
                string lastAuthor = workList.Pop();

                if (workList.NullOrEmpty()) return lastAuthor;

                return $"{string.Join(", ", workList)} and {lastAuthor}";
            }
        }

        /// <summary>
        /// Returns the cached link button images, caches them if not yet cached
        /// </summary>
        public List<Texture2D> LinkButtonImages
        {
            get
            {
                if (linkButtonImagesCached.NullOrEmpty())
                {
                    foreach (string path in linkButtonImagePaths)
                    {
                        linkButtonImagesCached.Add(ContentFinder<Texture2D>.Get(path));
                    }
                }

                return linkButtonImagesCached;
            }
        }

        /// <summary>
        /// Returns the tool tips for each LinkButton. May include empty strings
        /// </summary>
        public List<string> LinkButtonToolTips => linkButtonToolTips;

        /// <summary>
        /// Returns the cached banner image, or null if no bannerImagePath is set.
        /// </summary>
        public Texture2D BannerImage
        {
            get
            {
                if (bannerImageCached is null && !bannerImagePath.NullOrEmpty())
                    bannerImageCached = ContentFinder<Texture2D>.Get(bannerImagePath, false);
                return bannerImageCached;
            }
        }

        public DateTime ReleaseDate => releaseDateParsed;

        public string CompactBodyString
        {
            get
            {
                string result = description + "\n\nChanges:\n" + PatchNotesFormatted + "\n\nContributors: " + AuthorsFormatted;
                if (!additionalNotes.NullOrEmpty())
                    result += "\n" + AdditionalNotesFormatted;
                return result;
            }
        }

        public override void ResolveReferences()
        {
            base.ResolveReferences();

            // Parse version from defName (format: major_minor_patch)
            if (defName != null)
            {
                string[] parts = defName.Split('_');
                int len = parts.Length;
                if (len >= 3
                    && int.TryParse(parts[len - 3], out int maj)
                    && int.TryParse(parts[len - 2], out int min)
                    && int.TryParse(parts[len - 1], out int pat))
                {
                    major = maj;
                    minor = min;
                    patch = pat;
                }
            }

            // Parse release date
            if (!string.IsNullOrEmpty(releaseDate))
            {
                DateTime.TryParseExact(releaseDate, "yyyy-MM-dd",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out releaseDateParsed);
            }
        }

        /// <summary>
        /// Clears the cached data of this def
        /// </summary>
        public override void ClearCachedData()
        {
            base.ClearCachedData();

            linkButtonImagesCached = new List<Texture2D>();
            bannerImageCached = null;
            modContentPackCached = null;
        }

        /// <summary>
        /// Returns true if this patch note's version is newer than the given version components.
        /// </summary>
        public bool IsNewerThan(int maj, int min, int pat)
        {
            if (major != maj) return major > maj;
            if (minor != min) return minor > min;
            return patch > pat;
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }

            if (patchNoteLines.NullOrEmpty())
                yield return "patchNoteLines is empty";
            if (major == 0 && minor == 0 && patch == 0)
                yield return $"defName '{defName}' did not parse to a valid version (expected format: major_minor_patch)";
            if (releaseDateParsed == default(DateTime))
                yield return $"releaseDate '{releaseDate}' did not parse (expected format: yyyy-MM-dd)";
            if (string.IsNullOrEmpty(modId))
                yield return "modId is empty";
            if (authors.NullOrEmpty())
                yield return "authors list is empty";
        }

        /// <summary>
        /// Sorts all patchNoteDefs to find the latest one for a mod using it's <paramref name="modId"/>.
        /// Logs a warning if the latest def's version doesn't match About.xml modVersion.
        /// </summary>
        public static PatchNoteDef GetLatestForMod(string modId)
        {
            List<PatchNoteDef> patchNoteDefs = DefDatabase<PatchNoteDef>.AllDefsListForReading.Where(def => def.modId == modId).ToList();

            if (patchNoteDefs.NullOrEmpty())
            {
                LogUtil.Error($"Could not find any PatchNoteDefs for {modId}!");
                return null;
            }

            //This way of sorting produces a list: oldest => newest
            patchNoteDefs.SortBy(def => def.VersionSortKey);
            PatchNoteDef latest = patchNoteDefs.Last();

            return latest;
        }
    }
}
