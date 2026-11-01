using System;
using Verse;

namespace FactionColonies.util
{
    /// <summary>
    /// Minimal comparable semantic version (major.minor.patch) value type.
    /// Used by the save-format version stamp (see <see cref="FactionFC"/>) to gate save migrations
    /// on an ordered comparison of the version that wrote a save against the active mod version.
    /// </summary>
    public struct FCVersion : IComparable<FCVersion>, IEquatable<FCVersion>
    {
        public readonly int major;
        public readonly int minor;
        public readonly int patch;

        public FCVersion(int major, int minor, int patch)
        {
            this.major = major;
            this.minor = minor;
            this.patch = patch;
        }

        /// <summary>
        /// Parses a "major.minor.patch" string (e.g. "1.5.43"). Returns false and 0.0.0 on
        /// null/malformed input. Mirrors the parsing used by FCSettings.GetLastSeenVersion.
        /// </summary>
        public static bool TryParse(string s, out FCVersion version)
        {
            if (!s.NullOrEmpty())
            {
                string[] parts = s.Split('.');
                if (parts.Length == 3
                    && int.TryParse(parts[0], out int maj)
                    && int.TryParse(parts[1], out int min)
                    && int.TryParse(parts[2], out int pat))
                {
                    version = new FCVersion(maj, min, pat);
                    return true;
                }
            }
            version = new FCVersion(0, 0, 0);
            return false;
        }

        public int CompareTo(FCVersion other)
        {
            if (major != other.major) return major.CompareTo(other.major);
            if (minor != other.minor) return minor.CompareTo(other.minor);
            return patch.CompareTo(other.patch);
        }

        public bool IsOlderThan(FCVersion other) => CompareTo(other) < 0;
        public bool IsNewerThan(FCVersion other) => CompareTo(other) > 0;
        public bool Equals(FCVersion other) => CompareTo(other) == 0;

        public override bool Equals(object obj) => obj is FCVersion other && Equals(other);
        public override int GetHashCode() => major * 1000000 + minor * 1000 + patch;
        public override string ToString() => $"{major}.{minor}.{patch}";
    }
}
