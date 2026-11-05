using System.IO.Compression;
using System.Text.Json;
using UnloyalFront.Models;
using UnloyalFront.Utility;

namespace UnloyalFront.Integrations;

/// <summary>
/// Mod packs live in Mods\&lt;PackName&gt;\ and mirror the Roblox version folder
/// (content\..., PlatformContent\..., etc). On every launch the enabled packs are
/// copied over the version folder. Originals are backed up so disabling or removing
/// a pack restores them.
/// </summary>
public static class ModManager
{
    // Only these top-level folders may be modded. This keeps packs cosmetic: a pack can
    // never replace RobloxPlayerBeta.exe, DLLs or anything else in the install root.
    static readonly string[] AllowedRoots = { "content", "PlatformContent", "ExtraContent", "shaders" };

    static readonly HashSet<string> BlockedExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".exe", ".dll", ".sys", ".bat", ".cmd", ".ps1", ".vbs", ".msi", ".scr", ".com", ".lnk", ".js" };

    class State
    {
        public string Version { get; set; } = "";
        public List<string> Files { get; set; } = new();
    }

    // ---------- listing / importing ----------

    public static List<ModPackItem> ListPacks(AppSettings s)
    {
        Directory.CreateDirectory(Paths.Mods);
        return Directory.GetDirectories(Paths.Mods)
            .Select(d => Path.GetFileName(d)).Where(n => !string.IsNullOrEmpty(n))
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .Select(n => new ModPackItem { Name = n, Enabled = !s.DisabledPacks.Contains(n) })
            .ToList();
    }

    /// <summary>Imports a .zip mod pack. Handles zips with or without a wrapper folder.</summary>
    public static string ImportZip(string zipPath)
    {
        Directory.CreateDirectory(Paths.Mods);
        var temp = Path.Combine(Paths.Base, "tmp-import");
        if (Directory.Exists(temp)) Directory.Delete(temp, true);

        try
        {
            ZipFile.ExtractToDirectory(zipPath, temp);
            var root = FindPackRoot(temp);

            var baseName = SanitizeName(Path.GetFileNameWithoutExtension(zipPath));
            var name = baseName;
            for (int i = 2; Directory.Exists(Path.Combine(Paths.Mods, name)); i++) name = $"{baseName} ({i})";

            CopyDirectory(root, Path.Combine(Paths.Mods, name));
            return name;
        }
        finally
        {
            try { if (Directory.Exists(temp)) Directory.Delete(temp, true); } catch { }
        }
    }

    public static void RemovePack(string name)
    {
        var dir = Path.Combine(Paths.Mods, name);
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
    }

    static bool HasAllowedRoot(string dir) =>
        AllowedRoots.Any(r => Directory.Exists(Path.Combine(dir, r)));

    static string FindPackRoot(string dir)
    {
        if (HasAllowedRoot(dir)) return dir;
        foreach (var sub in Directory.GetDirectories(dir))
            if (HasAllowedRoot(sub)) return sub;
        throw new InvalidDataException(
            "That zip does not look like a mod pack. It needs a \"content\" (or PlatformContent / ExtraContent) folder inside.");
    }

    static string SanitizeName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        name = name.Trim().Trim('.');
        return string.IsNullOrEmpty(name) ? "Imported pack" : name;
    }

    static void CopyDirectory(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var d in Directory.GetDirectories(from, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(to, Path.GetRelativePath(from, d)));
        foreach (var f in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
            File.Copy(f, Path.Combine(to, Path.GetRelativePath(from, f)), true);
    }

    // ---------- applying ----------

    /// <summary>relative path (inside version folder) -> source file. Later packs win.</summary>
    public static Dictionary<string, string> CollectDesired(AppSettings s)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var roots = new List<string>();

        if (s.ApplyMods && Directory.Exists(Paths.Mods))
            foreach (var d in Directory.GetDirectories(Paths.Mods).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                if (!s.DisabledPacks.Contains(Path.GetFileName(d))) roots.Add(d);

        if (Directory.Exists(Paths.Generated)) roots.Add(Paths.Generated);   // e.g. custom font, always last

        foreach (var root in roots)
            foreach (var top in AllowedRoots)
            {
                var dir = Path.Combine(root, top);
                if (!Directory.Exists(dir)) continue;
                foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                {
                    var name = Path.GetFileName(file);
                    if (name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;
                    if (BlockedExtensions.Contains(Path.GetExtension(file))) continue;
                    map[Path.GetRelativePath(root, file)] = file;
                }
            }
        return map;
    }

    public static void Apply(string versionDir, string version, AppSettings s)
    {
        var state = LoadState();
        if (state.Version != version)
        {
            // Different version folder = pristine files. Old backups are useless now.
            if (!string.IsNullOrEmpty(state.Version))
                TryDeleteDir(Path.Combine(Paths.Backups, state.Version));
            state = new State { Version = version };
        }

        var backupRoot = Path.Combine(Paths.Backups, version);
        var desired = CollectDesired(s);
        var previous = new HashSet<string>(state.Files, StringComparer.OrdinalIgnoreCase);

        // 1) undo files whose mod was removed/disabled
        foreach (var rel in previous.Where(r => !desired.ContainsKey(r)))
        {
            var dest = Path.Combine(versionDir, rel);
            var bak = Path.Combine(backupRoot, rel);
            try
            {
                if (File.Exists(bak)) { File.Copy(bak, dest, true); File.Delete(bak); }
                else if (File.Exists(dest)) File.Delete(dest);   // the file did not exist originally
            }
            catch (Exception ex) { Log.Write($"Restoring {rel}", ex); }
        }

        // 2) apply everything wanted
        var applied = new List<string>();
        foreach (var (rel, src) in desired)
        {
            var dest = Path.Combine(versionDir, rel);
            var bak = Path.Combine(backupRoot, rel);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);

                if (File.Exists(dest) && !File.Exists(bak) && !previous.Contains(rel))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(bak)!);
                    File.Copy(dest, bak);
                }
                if (!SameFile(src, dest)) File.Copy(src, dest, true);
                applied.Add(rel);
            }
            catch (Exception ex) { Log.Write($"Applying {rel}", ex); }
        }

        state.Files = applied;
        SaveState(state);
        Log.Write($"Mods: {applied.Count} file(s) applied.");
    }

    /// <summary>Call after a fresh install so stale state does not confuse the next Apply.</summary>
    public static void ResetState()
    {
        try { if (File.Exists(Paths.ModStateFile)) File.Delete(Paths.ModStateFile); } catch { }
    }

    static bool SameFile(string a, string b)
    {
        if (!File.Exists(b)) return false;
        var fa = new FileInfo(a); var fb = new FileInfo(b);
        return fa.Length == fb.Length && fa.LastWriteTimeUtc == fb.LastWriteTimeUtc;
    }

    static State LoadState()
    {
        try
        {
            if (File.Exists(Paths.ModStateFile))
                return JsonSerializer.Deserialize<State>(File.ReadAllText(Paths.ModStateFile)) ?? new State();
        }
        catch (Exception ex) { Log.Write("Loading mod state", ex); }
        return new State();
    }

    static void SaveState(State s)
    {
        try { File.WriteAllText(Paths.ModStateFile, JsonSerializer.Serialize(s)); }
        catch (Exception ex) { Log.Write("Saving mod state", ex); }
    }

    static void TryDeleteDir(string dir)
    {
        try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
    }
}
