namespace DedLauncher.Helpers;

public static class MinecraftPathHelper
{
    public static string BaseDir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        ".dedlauncher"
    );

    public static string GameDir => Path.Combine(BaseDir, "minecraft");
    public static string VersionsDir => Path.Combine(GameDir, "versions");
    public static string LibrariesDir => Path.Combine(GameDir, "libraries");
    public static string AssetsDir => Path.Combine(GameDir, "assets");
    public static string ProfilesDir => Path.Combine(BaseDir, "profiles");
    public static string ConfigPath => Path.Combine(BaseDir, "config.json");

    public static void Initialize()
    {
        Directory.CreateDirectory(BaseDir);
        Directory.CreateDirectory(GameDir);
        Directory.CreateDirectory(VersionsDir);
        Directory.CreateDirectory(LibrariesDir);
        Directory.CreateDirectory(AssetsDir);
        Directory.CreateDirectory(ProfilesDir);
    }

    public static string GetVersionJar(string versionId)
    {
        return Path.Combine(VersionsDir, versionId, $"{versionId}.jar");
    }
}
