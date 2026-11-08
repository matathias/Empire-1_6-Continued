namespace FactionColonies;

/// <summary>Reads a mod's version from its About.xml, for Empire and the submods.</summary>
public static class ModVersionUtil
{
    /// <summary>The version in <paramref name="content"/>'s About.xml, or "Unknown".</summary>
    public static string Get(ModContentPack content)
    {
        string version = content?.ModMetaData?.ModVersion;
        return version.NullOrEmpty() ? "Unknown" : version;
    }

    /// <summary>The running <typeparamref name="TMod"/>'s version, or "Unknown".</summary>
    public static string Get<TMod>() where TMod : Mod => Get(LoadedModManager.GetMod<TMod>()?.Content);

    /// <summary>Logs the loaded version at startup, from a Mod constructor.</summary>
    public static void LogLoaded(ModContentPack content, Action<string> logForce)
    {
        string version = content?.ModMetaData?.ModVersion;
        logForce(version.NullOrEmpty() ? "Did not load a mod version" : $"v{version}");
    }
}
