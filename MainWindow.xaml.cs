using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using UnloyalFront.Integrations;
using UnloyalFront.Models;
using UnloyalFront.Utility;

namespace UnloyalFront.UI;

public partial class MainWindow : Window
{
    readonly AppSettings _settings = AppSettings.Load();
    readonly ObservableCollection<ModPackItem> _packs = new();
    bool _loading;

    public MainWindow()
    {
        InitializeComponent();
        PackList.ItemsSource = _packs;

        _loading = true;
        ApplyModsCheck.IsChecked = _settings.ApplyMods;
        FlagsBox.Text = PrettyJson(_settings.FastFlagsJson);
        _loading = false;

        RefreshPacks();
        RefreshFont();
        RefreshInstallStatus();

        Nav.SelectedIndex = 0;
        Loaded += OnLoaded;
        Closed += (_, _) => { if (!App.Launching) Application.Current.Shutdown(); };
    }

    void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (Installer.IsInstalled()) return;

        var answer = MessageBox.Show(
            "Set up Unloyal Front now?\n\nThis copies the app to your user folder and makes Roblox website links (the Play button) open through it. " +
            "You can undo it any time in Settings.",
            "Unloyal Front", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer == MessageBoxResult.Yes) DoInstall();
    }

    // ------------------------------------------------------------ navigation

    void Nav_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ModsPage == null || FontsPage == null || FlagsPage == null || SettingsPage == null) return;
        int i = Nav.SelectedIndex;
        ModsPage.Visibility     = i == 0 ? Visibility.Visible : Visibility.Collapsed;
        FontsPage.Visibility    = i == 1 ? Visibility.Visible : Visibility.Collapsed;
        FlagsPage.Visibility    = i == 2 ? Visibility.Visible : Visibility.Collapsed;
        SettingsPage.Visibility = i == 3 ? Visibility.Visible : Visibility.Collapsed;
    }

    void Launch_Click(object sender, RoutedEventArgs e)
    {
        _settings.Save();
        ((App)Application.Current).StartLaunch(null);
        Close();
    }

    // ------------------------------------------------------------ mods

    void RefreshPacks()
    {
        _packs.Clear();
        foreach (var p in ModManager.ListPacks(_settings)) _packs.Add(p);
        NoPacksText.Visibility = _packs.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    void ImportZip_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Title = "Choose a mod pack", Filter = "Zip files (*.zip)|*.zip" };
        if (dlg.ShowDialog() != true) return;

        try
        {
            Cursor = System.Windows.Input.Cursors.Wait;
            var name = ModManager.ImportZip(dlg.FileName);
            RefreshPacks();
            MessageBox.Show($"Imported \"{name}\". It will be applied next time you launch Roblox.",
                "Unloyal Front", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            Log.Write("Import zip", ex);
            MessageBox.Show(ex.Message, "Could not import", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally { Cursor = System.Windows.Input.Cursors.Arrow; }
    }

    void PackToggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox box || box.DataContext is not ModPackItem item) return;
        item.Enabled = box.IsChecked == true;
        _settings.DisabledPacks.RemoveAll(n => n == item.Name);
        if (!item.Enabled) _settings.DisabledPacks.Add(item.Name);
        _settings.Save();
    }

    void RemovePack_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not ModPackItem item) return;
        if (MessageBox.Show($"Remove the mod pack \"{item.Name}\"?", "Unloyal Front",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        try
        {
            ModManager.RemovePack(item.Name);
            _settings.DisabledPacks.RemoveAll(n => n == item.Name);
            _settings.Save();
            RefreshPacks();
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Could not remove", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    void ApplyMods_Click(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _settings.ApplyMods = ApplyModsCheck.IsChecked == true;
        _settings.Save();
    }

    void OpenMods_Click(object sender, RoutedEventArgs e) => OpenFolder(Paths.Mods);
    void RefreshPacks_Click(object sender, RoutedEventArgs e) => RefreshPacks();

    // ------------------------------------------------------------ fonts

    void RefreshFont()
    {
        _loading = true;
        UseFontCheck.IsChecked = _settings.UseCustomFont;
        _loading = false;

        var file = FontManager.ResolveFont(_settings);
        if (file == null)
        {
            FontNameText.Text = "No custom font chosen.";
            FontPreview.FontFamily = new FontFamily("Segoe UI");
            UseFontCheck.IsEnabled = false;
            return;
        }

        UseFontCheck.IsEnabled = true;
        try
        {
            var folder = new Uri(Path.GetDirectoryName(file)! + Path.DirectorySeparatorChar);
            var family = Fonts.GetFontFamilies(folder).FirstOrDefault();
            if (family != null)
            {
                FontPreview.FontFamily = family;
                var name = family.FamilyNames.Values.FirstOrDefault() ?? "custom font";
                FontNameText.Text = $"Selected: {name}";
                return;
            }
        }
        catch (Exception ex) { Log.Write("Font preview", ex); }

        FontNameText.Text = "Selected font file (preview unavailable).";
    }

    void ChooseFont_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Title = "Choose a font", Filter = "Fonts (*.ttf;*.otf)|*.ttf;*.otf" };
        if (dlg.ShowDialog() != true) return;
        try
        {
            FontManager.ImportFont(dlg.FileName, _settings);
            RefreshFont();
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Could not use that font", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    void ResetFont_Click(object sender, RoutedEventArgs e)
    {
        FontManager.ClearFont(_settings);
        RefreshFont();
    }

    void UseFont_Click(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _settings.UseCustomFont = UseFontCheck.IsChecked == true;
        _settings.Save();
    }

    // ------------------------------------------------------------ fastflags

    static string PrettyJson(string json)
    {
        try
        {
            var node = System.Text.Json.Nodes.JsonNode.Parse(json);
            return node?.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }) ?? "{}";
        }
        catch { return json; }
    }

    void SaveFlags_Click(object sender, RoutedEventArgs e)
    {
        if (!FastFlagManager.TryValidate(FlagsBox.Text, out var error))
        {
            FlagsStatus.Foreground = Brushes.IndianRed;
            FlagsStatus.Text = "  Invalid JSON: " + error;
            return;
        }
        _settings.FastFlagsJson = FlagsBox.Text;
        _settings.Save();
        FlagsStatus.Foreground = (Brush)FindResource("Muted");
        FlagsStatus.Text = "  Saved. Applied next launch.";
    }

    // ------------------------------------------------------------ settings / install

    void RefreshInstallStatus()
    {
        var ok = Installer.IsInstalled();
        InstallStatus.Text = ok
            ? "Installed. Roblox links open in Unloyal Front."
            : "Not installed yet. Roblox links still open the normal launcher.";
        InstallStatus.Foreground = ok ? Brushes.MediumSeaGreen : Brushes.Orange;
    }

    void DoInstall()
    {
        try
        {
            Installer.Install();
            RefreshInstallStatus();
        }
        catch (Exception ex)
        {
            Log.Write("Install", ex);
            MessageBox.Show("Could not finish setup:\n" + ex.Message, "Unloyal Front",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    void Install_Click(object sender, RoutedEventArgs e) => DoInstall();

    void Uninstall_Click(object sender, RoutedEventArgs e)
    {
        Installer.Uninstall();
        RefreshInstallStatus();
        MessageBox.Show("Removed. To get the normal Roblox launcher back for website links, reinstall Roblox from roblox.com.",
            "Unloyal Front", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    void OpenData_Click(object sender, RoutedEventArgs e) => OpenFolder(Paths.Base);

    void OpenLog_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!File.Exists(Log.FilePath)) { MessageBox.Show("No log yet."); return; }
            Process.Start(new ProcessStartInfo(Log.FilePath) { UseShellExecute = true });
        }
        catch (Exception ex) { Log.Write("Open log", ex); }
    }

    static void OpenFolder(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
        }
        catch (Exception ex) { Log.Write("Open folder", ex); }
    }
}
