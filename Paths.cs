namespace UnloyalFront;

/// <summary>All folders/files the app uses. Everything lives in %LocalAppData%\UnloyalFront.</summary>
public static class Paths
{
    public static readonly string Base = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UnloyalFront");

    public static string Versions  => Path.Combine(Base, "Versions");
    public static string Downloads => Path.Combine(Base, "Downloads");
    public static string Mods      => Path.Combine(Base, "Mods");        // user mod packs (one folder each)
    public static string Generated => Path.Combine(Base, "Generated");   // files the app generates (custom font)
    public static string Backups   => Path.Combine(Base, "Backups");     // originals of files that mods replaced
    public static string FontStore => Path.Combine(Base, "FontStore");   // the chosen custom font file
    public static string Logs      => Path.Combine(Base, "Logs");

    public static string SettingsFile  => Path.Combine(Base, "settings.json");
    public static string ModStateFile  => Path.Combine(Base, "modstate.json");
    public static string InstalledExe  => Path.Combine(Base, "UnloyalFront.exe");

    public static void EnsureAll()
    {
        foreach (var d in new[] { Base, Versions, Downloads, Mods, Generated, Backups, FontStore, Logs })
            Directory.CreateDirectory(d);
    }
}
