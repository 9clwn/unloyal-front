using Microsoft.Win32;
using UnloyalFront.Utility;

namespace UnloyalFront;

/// <summary>
/// "Installing" = copying this exe to %LocalAppData%\UnloyalFront and telling Windows that
/// roblox-player:// and roblox:// links should open it (per-user, no admin needed).
/// </summary>
public static class Installer
{
    static readonly string[] Schemes = { "roblox-player", "roblox" };

    static string ShortcutPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Unloyal Front.lnk");

    public static bool IsInstalled()
    {
        if (!File.Exists(Paths.InstalledExe)) return false;
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(@"Software\Classes\roblox-player\shell\open\command");
            var cmd = k?.GetValue("") as string;
            return cmd != null && cmd.Contains(Paths.InstalledExe, StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    public static void Install()
    {
        Directory.CreateDirectory(Paths.Base);

        var self = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(self) &&
            !string.Equals(Path.GetFullPath(self), Path.GetFullPath(Paths.InstalledExe), StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(self, Paths.InstalledExe, true);
        }

        foreach (var scheme in Schemes) RegisterScheme(scheme);
        CreateShortcut();
        Log.Write("Installed and registered protocol handlers.");
    }

    public static void Uninstall()
    {
        foreach (var scheme in Schemes)
        {
            try { Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\{scheme}", false); }
            catch (Exception ex) { Log.Write($"Removing {scheme} handler", ex); }
        }
        try { if (File.Exists(ShortcutPath)) File.Delete(ShortcutPath); } catch { }
        Log.Write("Removed protocol handlers.");
    }

    static void RegisterScheme(string scheme)
    {
        using var key = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{scheme}");
        key.SetValue("", $"URL: {scheme} Protocol");
        key.SetValue("URL Protocol", "");
        using var cmd = key.CreateSubKey(@"shell\open\command");
        cmd.SetValue("", $"\"{Paths.InstalledExe}\" \"%1\"");
    }

    static void CreateShortcut()
    {
        try
        {
            var type = Type.GetTypeFromProgID("WScript.Shell");
            if (type == null) return;
            dynamic shell = Activator.CreateInstance(type)!;
            dynamic link = shell.CreateShortcut(ShortcutPath);
            link.TargetPath = Paths.InstalledExe;
            link.WorkingDirectory = Paths.Base;
            link.Description = "Unloyal Front";
            link.Save();
        }
        catch (Exception ex) { Log.Write("Creating shortcut", ex); }
    }
}
