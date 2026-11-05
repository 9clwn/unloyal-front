using System.Text.Json;
using System.Text.Json.Nodes;
using UnloyalFront.Models;
using UnloyalFront.Utility;

namespace UnloyalFront.Integrations;

/// <summary>
/// Custom font support. Roblox maps its fonts through content\fonts\families\*.json.
/// We copy the user's font into the client's fonts folder and point every face in
/// every family file at it. The result is written to Paths.Generated, which
/// ModManager applies (and cleanly undoes when the font is turned off).
/// </summary>
public static class FontManager
{
    public static string? ResolveFont(AppSettings s)
    {
        if (string.IsNullOrEmpty(s.CustomFontFile)) return null;
        var path = Path.Combine(Paths.FontStore, s.CustomFontFile);
        return File.Exists(path) ? path : null;
    }

    public static void ImportFont(string sourcePath, AppSettings s)
    {
        var ext = Path.GetExtension(sourcePath).ToLowerInvariant();
        if (ext != ".ttf" && ext != ".otf")
            throw new InvalidDataException("Please choose a .ttf or .otf font file.");

        Directory.CreateDirectory(Paths.FontStore);
        var sub = DateTime.Now.Ticks.ToString();   // unique folder so the preview never shows a cached old font
        Directory.CreateDirectory(Path.Combine(Paths.FontStore, sub));
        var rel = Path.Combine(sub, "font" + ext);
        File.Copy(sourcePath, Path.Combine(Paths.FontStore, rel), true);

        DeleteOldFonts(keep: sub);
        s.CustomFontFile = rel;
        s.UseCustomFont = true;
        s.Save();
    }

    public static void ClearFont(AppSettings s)
    {
        s.UseCustomFont = false;
        s.CustomFontFile = "";
        s.Save();
        DeleteOldFonts(keep: null);
    }

    static void DeleteOldFonts(string? keep)
    {
        try
        {
            foreach (var d in Directory.GetDirectories(Paths.FontStore))
                if (Path.GetFileName(d) != keep)
                    try { Directory.Delete(d, true); } catch { /* may be locked by the preview; harmless */ }
        }
        catch { }
    }

    /// <summary>Regenerates the font files in Paths.Generated. Call before ModManager.Apply.</summary>
    public static void Generate(string versionDir, string version, AppSettings s)
    {
        var outDir = Path.Combine(Paths.Generated, "content", "fonts");
        if (Directory.Exists(outDir)) Directory.Delete(outDir, true);

        var font = ResolveFont(s);
        if (!s.UseCustomFont || font == null) return;

        var familiesOut = Path.Combine(outDir, "families");
        Directory.CreateDirectory(familiesOut);

        var fileName = "CustomFont" + Path.GetExtension(font).ToLowerInvariant();
        File.Copy(font, Path.Combine(outDir, fileName), true);
        var assetId = "rbxasset://fonts/" + fileName;

        // Always read the ORIGINAL family files (from backup if we already replaced them).
        var liveDir   = Path.Combine(versionDir, "content", "fonts", "families");
        var backupDir = Path.Combine(Paths.Backups, version, "content", "fonts", "families");

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in new[] { liveDir, backupDir })
            if (Directory.Exists(d))
                foreach (var f in Directory.GetFiles(d, "*.json")) names.Add(Path.GetFileName(f));

        var write = new JsonSerializerOptions { WriteIndented = true };
        int count = 0;
        foreach (var name in names)
        {
            var bak = Path.Combine(backupDir, name);
            var src = File.Exists(bak) ? bak : Path.Combine(liveDir, name);
            try
            {
                var root = JsonNode.Parse(File.ReadAllText(src));
                if (root?["faces"] is not JsonArray faces) continue;
                foreach (var face in faces)
                    if (face is JsonObject o) o["assetId"] = assetId;
                File.WriteAllText(Path.Combine(familiesOut, name), root.ToJsonString(write));
                count++;
            }
            catch (Exception ex) { Log.Write($"Font family {name}", ex); }
        }
        Log.Write($"Custom font: patched {count} family file(s).");
    }
}
