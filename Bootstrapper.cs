using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using UnloyalFront.Integrations;
using UnloyalFront.Models;
using UnloyalFront.Utility;

namespace UnloyalFront;

/// <summary>
/// Downloads the latest Roblox player into our own Versions folder, applies the font /
/// mods / FastFlags, then starts RobloxPlayerBeta.exe with the launch arguments.
/// </summary>
public class Bootstrapper
{
    const string VersionEndpoint = "https://clientsettingscdn.roblox.com/v2/client-version/WindowsPlayer";
    const string Cdn = "https://setup.rbxcdn.com/";

    // Where each package zip gets extracted inside the version folder.
    static readonly Dictionary<string, string> PackageDirs = new(StringComparer.OrdinalIgnoreCase)
    {
        ["RobloxApp.zip"]                    = "",
        ["WebView2.zip"]                     = "",
        ["WebView2RuntimeInstaller.zip"]     = "WebView2RuntimeInstaller/",
        ["shaders.zip"]                      = "shaders/",
        ["ssl.zip"]                          = "ssl/",
        ["content-avatar.zip"]               = "content/avatar/",
        ["content-configs.zip"]              = "content/configs/",
        ["content-fonts.zip"]                = "content/fonts/",
        ["content-sky.zip"]                  = "content/sky/",
        ["content-sounds.zip"]               = "content/sounds/",
        ["content-textures2.zip"]            = "content/textures/",
        ["content-models.zip"]               = "content/models/",
        ["content-textures3.zip"]            = "PlatformContent/pc/textures/",
        ["content-terrain.zip"]              = "PlatformContent/pc/terrain/",
        ["content-platform-fonts.zip"]       = "PlatformContent/pc/fonts/",
        ["extracontent-luapackages.zip"]     = "ExtraContent/LuaPackages/",
        ["extracontent-translations.zip"]    = "ExtraContent/translations/",
        ["extracontent-models.zip"]          = "ExtraContent/models/",
        ["extracontent-textures.zip"]        = "ExtraContent/textures/",
        ["extracontent-places.zip"]          = "ExtraContent/places/",
    };

    record Package(string Name, string Md5, long Packed, long Size);

    static readonly HttpClient Http = CreateClient();
    static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("UnloyalFront/1.0");
        return c;
    }

    readonly Action<string, int> _report;
    public Bootstrapper(Action<string, int> report) => _report = report;

    // ------------------------------------------------------------------ main flow

    public async Task RunAsync(string? launchUri)
    {
        Paths.EnsureAll();
        var settings = AppSettings.Load();

        _report("Checking for Roblox updates...", 0);
        var version = await GetLatestVersionAsync() ?? FindInstalledVersion();
        if (version == null)
            throw new InvalidOperationException(
                "Could not reach Roblox to download the game, and no copy is installed yet. Check your internet connection.");

        var dir    = Path.Combine(Paths.Versions, version);
        var player = Path.Combine(dir, "RobloxPlayerBeta.exe");
        var marker = Path.Combine(dir, ".unloyal-complete");

        if (!File.Exists(marker) || !File.Exists(player))
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            await InstallAsync(version, dir);
            ModManager.ResetState();
            File.WriteAllText(marker, version);
        }

        _report("Applying your font, mods and flags...", 95);
        FontManager.Generate(dir, version, settings);
        ModManager.Apply(dir, version, settings);
        FastFlagManager.Apply(dir, settings);

        CleanOldVersions(version);

        _report("Launching Roblox...", 100);
        Launch(player, dir, launchUri);
    }

    // ------------------------------------------------------------------ version / install

    static async Task<string?> GetLatestVersionAsync()
    {
        try
        {
            var json = await Http.GetStringAsync(VersionEndpoint);
            var v = JsonNode.Parse(json)?["clientVersionUpload"]?.GetValue<string>();
            return v != null && Regex.IsMatch(v, "^version-[0-9a-fA-F]+$") ? v : null;
        }
        catch (Exception ex)
        {
            Log.Write("Version check failed", ex);
            return null;
        }
    }

    static string? FindInstalledVersion()
    {
        if (!Directory.Exists(Paths.Versions)) return null;
        return Directory.GetDirectories(Paths.Versions)
            .Where(d => File.Exists(Path.Combine(d, ".unloyal-complete")))
            .OrderByDescending(Directory.GetCreationTimeUtc)
            .Select(d => Path.GetFileName(d))
            .FirstOrDefault();
    }

    async Task InstallAsync(string version, string dir)
    {
        _report("Getting package list...", 1);
        var manifest = await Http.GetStringAsync($"{Cdn}{version}-rbxPkgManifest.txt");
        var lines = manifest.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
        if (lines.Count == 0 || lines[0] != "v0")
            throw new InvalidDataException("Unexpected package manifest format from Roblox.");

        var packages = new List<Package>();
        for (int i = 1; i + 3 < lines.Count; i += 4)
        {
            var name = lines[i];
            if (!name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) continue;   // skip launcher installers
            packages.Add(new Package(name, lines[i + 1].ToLowerInvariant(), long.Parse(lines[i + 2]), long.Parse(lines[i + 3])));
        }

        var dlDir = Path.Combine(Paths.Downloads, version);
        Directory.CreateDirectory(dlDir);

        long total = Math.Max(1, packages.Sum(p => p.Packed));
        long done = 0;
        using var gate = new SemaphoreSlim(4);

        await Task.WhenAll(packages.Select(async p =>
        {
            await gate.WaitAsync();
            try
            {
                await DownloadPackageAsync(version, p, dlDir);
                var d = Interlocked.Add(ref done, p.Packed);
                _report($"Downloading Roblox... {d * 100 / total}%", 2 + (int)(d * 83 / total));
            }
            finally { gate.Release(); }
        }));

        Directory.CreateDirectory(dir);
        int n = 0;
        foreach (var p in packages)
        {
            _report($"Installing... ({++n}/{packages.Count})", 85 + 9 * n / packages.Count);
            var target = Path.Combine(dir, PackageDirs.GetValueOrDefault(p.Name, ""));
            Directory.CreateDirectory(target);
            ZipFile.ExtractToDirectory(Path.Combine(dlDir, p.Name), target, true);
        }

        File.WriteAllText(Path.Combine(dir, "AppSettings.xml"),
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\r\n<Settings>\r\n\t<ContentFolder>content</ContentFolder>\r\n\t<BaseUrl>http://www.roblox.com</BaseUrl>\r\n</Settings>\r\n");

        try { Directory.Delete(dlDir, true); } catch { }
    }

    static async Task DownloadPackageAsync(string version, Package p, string dlDir)
    {
        var path = Path.Combine(dlDir, p.Name);
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                if (File.Exists(path) && Md5Of(path) == p.Md5) return;

                using var resp = await Http.GetAsync($"{Cdn}{version}-{p.Name}", HttpCompletionOption.ResponseHeadersRead);
                resp.EnsureSuccessStatusCode();
                await using (var net = await resp.Content.ReadAsStreamAsync())
                await using (var file = File.Create(path))
                    await net.CopyToAsync(file);

                if (Md5Of(path) == p.Md5) return;
                Log.Write($"Checksum mismatch for {p.Name} (attempt {attempt})");
            }
            catch (Exception ex) when (attempt < 3) { Log.Write($"Download {p.Name} attempt {attempt}", ex); }
        }
        throw new IOException($"Could not download {p.Name} correctly. Try again in a moment.");
    }

    static string Md5Of(string path)
    {
        using var fs = File.OpenRead(path);
        return Convert.ToHexString(MD5.HashData(fs)).ToLowerInvariant();
    }

    static void CleanOldVersions(string keep)
    {
        try
        {
            foreach (var d in Directory.GetDirectories(Paths.Versions))
                if (Path.GetFileName(d) != keep)
                    try { Directory.Delete(d, true); } catch { /* still running? leave it */ }
        }
        catch { }
    }

    // ------------------------------------------------------------------ launching

    // Launch links come from a web browser, so treat them as untrusted input:
    // only the characters Roblox links actually use are allowed (no quotes/spaces/flags).
    static readonly Regex LaunchUriPattern = new(@"^(roblox-player|roblox):[A-Za-z0-9:+/_.%?=&,\-#@]*$", RegexOptions.Compiled);

    public static bool IsValidLaunchUri(string? uri) => uri != null && uri.Length < 8192 && LaunchUriPattern.IsMatch(uri);

    static void Launch(string playerExe, string workingDir, string? launchUri)
    {
        var psi = new ProcessStartInfo(playerExe) { WorkingDirectory = workingDir, UseShellExecute = false };

        if (IsValidLaunchUri(launchUri))
        {
            // refresh the launch time so the ticket isn't treated as stale
            var uri = Regex.Replace(launchUri!, @"launchtime:\d+", $"launchtime:{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}");
            psi.ArgumentList.Add(uri);
        }
        else
        {
            psi.ArgumentList.Add("--app");
        }

        Process.Start(psi);
    }
}
