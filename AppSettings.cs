using System.Text.Json;
using UnloyalFront.Utility;

namespace UnloyalFront.Models;

public class AppSettings
{
    /// <summary>Master switch for mod packs.</summary>
    public bool ApplyMods { get; set; } = true;

    /// <summary>Names of mod pack folders the user turned off.</summary>
    public List<string> DisabledPacks { get; set; } = new();

    public bool UseCustomFont { get; set; }

    /// <summary>Path of the chosen font, relative to Paths.FontStore.</summary>
    public string CustomFontFile { get; set; } = "";

    /// <summary>Raw JSON object written to ClientSettings/ClientAppSettings.json.</summary>
    public string FastFlagsJson { get; set; } = "{}";

    static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(Paths.SettingsFile))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(Paths.SettingsFile)) ?? new AppSettings();
        }
        catch (Exception ex) { Log.Write("Loading settings", ex); }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Paths.Base);
            File.WriteAllText(Paths.SettingsFile, JsonSerializer.Serialize(this, Options));
        }
        catch (Exception ex) { Log.Write("Saving settings", ex); }
    }
}
