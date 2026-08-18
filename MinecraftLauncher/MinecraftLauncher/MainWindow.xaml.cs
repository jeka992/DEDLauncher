using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Text.Json;
using DedLauncher.Models;
using DedLauncher.ViewModels;

namespace DedLauncher;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly Dictionary<Button, string> _navBtns = new();
    private static SolidColorBrush ActiveBg = new(Color.FromRgb(0xB3, 0x00, 0x00));
    private static readonly SolidColorBrush ActiveFg = new(Color.FromRgb(0xFF, 0xFF, 0xFF));
    private static readonly SolidColorBrush InactiveFg = new(Color.FromRgb(0xA1, 0xA1, 0xAA));
    private static string _currentActiveTag = "";
    private static MainWindow? _instance;

    public static void UpdateAccentBrush(SolidColorBrush brush)
    {
        ActiveBg = brush;
        _instance?.RefreshActiveButton();
    }

    
    
    
    
    public static void UpdateUiScale(int fontSize)
    {
        if (_instance == null) return;
        double scale = Math.Clamp(fontSize, 11, 15) / 13.0;
        double oldScale = 1.0;
        if (_instance.RootBorder.LayoutTransform is ScaleTransform existing)
            oldScale = existing.ScaleX;

        if (Math.Abs(oldScale - scale) < 0.001) return;

        _instance.RootBorder.LayoutTransform = new ScaleTransform(scale, scale);

        
        double factor = scale / oldScale;
        _instance.Width = Math.Max(_instance.MinWidth, _instance.Width * factor);
        _instance.Height = Math.Max(_instance.MinHeight, _instance.Height * factor);
    }

    private void RefreshActiveButton()
    {
        foreach (var (btn, tag) in _navBtns)
        {
            bool isActive = tag == _currentActiveTag;
            btn.Background = isActive ? ActiveBg : Brushes.Transparent;
            btn.Foreground = isActive ? ActiveFg : InactiveFg;
            btn.BorderBrush = Brushes.Transparent;
        }
        if (_currentActiveTag == "home")
            BtnLogo.Background = ActiveBg;
        else if (TryFindResource("BgCardBrush") is Brush cardBrush)
            BtnLogo.Background = cardBrush;
    }

    public MainWindow()
    {
        InitializeComponent();
        _instance = this;
        _vm = new MainViewModel();
        DataContext = _vm;

        
        try
        {
            var logoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "logo.png");
            if (File.Exists(logoPath))
            {
                BtnLogo.ApplyTemplate();
                if (BtnLogo.Template.FindName("BtnLogoImage", BtnLogo) is Image logoImg)
                    logoImg.Source = new BitmapImage(new Uri(logoPath));
            }
        }
        catch { }

        _navBtns[BtnPlay] = "play";
        _navBtns[BtnMods] = "mods";
        _navBtns[BtnConsole] = "console";
        _navBtns[BtnScreenshots] = "screenshots";
        _navBtns[BtnFriends] = "friends";
        _navBtns[BtnSetup] = "setup";

        RestoreWindowState();

        Loaded += async (s, e) =>
        {
            await _vm.InitAsync();
            SetActive("home");
        };
    }

    private void RestoreWindowState()
    {
        try
        {
            var path = Path.Combine(Helpers.MinecraftPathHelper.BaseDir, "window.json");
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var state = JsonSerializer.Deserialize<WinPos>(json);
                if (state != null && state.W > 0)
                {
                    Width = state.W;
                    Height = state.H;
                }
                if (state != null && state.X > -10000)
                {
                    Left = state.X;
                    Top = state.Y;
                }
                if (state != null && state.SW >= 72 && state.SW <= 220)
                {
                    SidebarColumn.Width = new GridLength(state.SW);
                }
            }
        }
        catch { }
    }

    private void SaveWindowState()
    {
        try
        {
            var state = new { X = Left, Y = Top, W = Width, H = Height, SW = SidebarColumn.Width.Value };
            File.WriteAllText(Path.Combine(Helpers.MinecraftPathHelper.BaseDir, "window.json"),
                JsonSerializer.Serialize(state));
        }
        catch { }
    }

    private void Nav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && _navBtns.TryGetValue(btn, out var tag))
            SetActive(tag);
    }

    private void Logo_Click(object sender, RoutedEventArgs e)
    {
        SetActive("home");
    }

    private void SetActive(string tag)
    {
        _currentActiveTag = tag;
        RefreshActiveButton();

        switch (tag)
        {
            case "home": _vm.NavHomeCmd.Execute(null); break;
            case "play": _vm.NavPlayCmd.Execute(null); break;
            case "mods": _vm.NavModsCmd.Execute(null); HighlightSubTab(); break;
            case "console": _vm.NavConsoleCmd.Execute(null); break;
            case "screenshots": _vm.NavScreenshotsCmd.Execute(null); break;
            case "friends": _vm.NavFriendsCmd.Execute(null); break;
            case "setup": _vm.NavSetupCmd.Execute(null); HighlightSettingsSection(); break;
        }
    }

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void Maximize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void SwitchToModrinth(object sender, RoutedEventArgs e) { }

    private void SourceModrinth_Click(object sender, RoutedEventArgs e)
    {
        _vm.SetModSource("Modrinth");
        HighlightModSourceButtons();
    }

    private void SourceCurseForge_Click(object sender, RoutedEventArgs e)
    {
        _vm.SetModSource("CurseForge");
        HighlightModSourceButtons();
    }

    private void SubTabMods_Click(object sender, RoutedEventArgs e)
    {
        _vm.ModsSubTab = 0;
        HighlightSubTab();
    }

    private void SubTabResourcePacks_Click(object sender, RoutedEventArgs e)
    {
        _vm.ModsSubTab = 1;
        HighlightSubTab();
    }

    private void SubTabShaders_Click(object sender, RoutedEventArgs e)
    {
        _vm.ModsSubTab = 2;
        HighlightSubTab();
    }

    private void SubTabServers_Click(object sender, RoutedEventArgs e)
    {
        _vm.ModsSubTab = 3;
        HighlightSubTab();
    }

    private void HighlightSubTab()
    {
        var activeStyle = (Style)FindResource("BtnRed");
        var inactiveStyle = (Style)FindResource("BtnOutline");
        TabModsSub.Style = _vm.ModsSubTab == 0 ? activeStyle : inactiveStyle;
        TabResourcePacksSub.Style = _vm.ModsSubTab == 1 ? activeStyle : inactiveStyle;
        TabShadersSub.Style = _vm.ModsSubTab == 2 ? activeStyle : inactiveStyle;
        TabServersSub.Style = _vm.ModsSubTab == 3 ? activeStyle : inactiveStyle;
        HighlightModSourceButtons();
        HighlightRpSourceButtons();
        HighlightShaderSourceButtons();
        HighlightViewToggles();
    }

    private void HighlightModSourceButtons()
    {
        var activeStyle = (Style)FindResource("BtnRed");
        var inactiveStyle = (Style)FindResource("BtnOutline");
        BtnSrcModrinth.Style = _vm.ModSource == "Modrinth" ? activeStyle : inactiveStyle;
        BtnSrcCurse.Style = _vm.ModSource == "CurseForge" ? activeStyle : inactiveStyle;
    }

    private async void SearchResourcePacks_Click(object sender, RoutedEventArgs e)
    {
        await _vm.SearchResourcePacksAsync();
    }

    private async void SearchShaders_Click(object sender, RoutedEventArgs e)
    {
        await _vm.SearchShadersAsync();
    }

    

    private void RpSourceModrinth_Click(object sender, RoutedEventArgs e)
    {
        _vm.SetRpSource("Modrinth");
        HighlightRpSourceButtons();
    }

    private void RpSourceCurseForge_Click(object sender, RoutedEventArgs e)
    {
        _vm.SetRpSource("CurseForge");
        HighlightRpSourceButtons();
    }

    private void ShaderSourceModrinth_Click(object sender, RoutedEventArgs e)
    {
        _vm.SetShaderSource("Modrinth");
        HighlightShaderSourceButtons();
    }

    private void ShaderSourceCurseForge_Click(object sender, RoutedEventArgs e)
    {
        _vm.SetShaderSource("CurseForge");
        HighlightShaderSourceButtons();
    }

    private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        await _vm.CheckForUpdatesAsync(manual: true);
    }

    private void CopyModsToProfile_Click(object sender, RoutedEventArgs e)
    {
        _vm.CopyModsToCurrentProfile();
    }

    private void InvitePlay_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ChatLine line)
            _ = _vm.JoinInviteServer(line);
    }

    private void OpenUpdateChannel_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(_vm.UpdateChannelLink) { UseShellExecute = true });
        }
        catch { }
    }

    private void HighlightRpSourceButtons()
    {
        var active = (Style)FindResource("BtnRed");
        var inactive = (Style)FindResource("BtnOutline");
        BtnRpSrcModrinth.Style = _vm.RpSource == "Modrinth" ? active : inactive;
        BtnRpSrcCurse.Style = _vm.RpSource == "CurseForge" ? active : inactive;
    }

    private void HighlightShaderSourceButtons()
    {
        var active = (Style)FindResource("BtnRed");
        var inactive = (Style)FindResource("BtnOutline");
        BtnShaderSrcModrinth.Style = _vm.ShaderSource == "Modrinth" ? active : inactive;
        BtnShaderSrcCurse.Style = _vm.ShaderSource == "CurseForge" ? active : inactive;
    }

    

    private void ModsViewToggle_Click(object sender, RoutedEventArgs e)
    {
        _vm.ShowInstalledMods = (sender as Button)?.Tag as string == "installed";
        if (_vm.ShowInstalledMods) _vm.RefreshInstalledMods();
        HighlightViewToggles();
    }

    private void RpViewToggle_Click(object sender, RoutedEventArgs e)
    {
        _vm.ShowInstalledRps = (sender as Button)?.Tag as string == "installed";
        if (_vm.ShowInstalledRps) _vm.LoadInstalledResourcePacks();
        HighlightViewToggles();
    }

    private void ShaderViewToggle_Click(object sender, RoutedEventArgs e)
    {
        _vm.ShowInstalledShaders = (sender as Button)?.Tag as string == "installed";
        if (_vm.ShowInstalledShaders) _vm.LoadInstalledShaders();
        HighlightViewToggles();
    }

    private void HighlightViewToggles()
    {
        var active = (Style)FindResource("BtnRed");
        var inactive = (Style)FindResource("BtnOutline");
        BtnModsBrowserV.Style = _vm.ShowInstalledMods ? inactive : active;
        BtnModsInstalledV.Style = _vm.ShowInstalledMods ? active : inactive;
        BtnRpBrowserV.Style = _vm.ShowInstalledRps ? inactive : active;
        BtnRpInstalledV.Style = _vm.ShowInstalledRps ? active : inactive;
        BtnShaderBrowserV.Style = _vm.ShowInstalledShaders ? inactive : active;
        BtnShaderInstalledV.Style = _vm.ShowInstalledShaders ? active : inactive;
    }

    

    private async void AddServer_Click(object sender, RoutedEventArgs e)
    {
        await _vm.AddServerAsync();
    }

    private async void ServerInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            await _vm.AddServerAsync();
    }

    private async void RefreshServers_Click(object sender, RoutedEventArgs e)
    {
        await _vm.RefreshServersAsync(force: true);
    }

    private void RemoveServer_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ServerEntry server)
            _vm.RemoveServer(server);
    }

    private async void JoinServer_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ServerEntry server)
            await _vm.JoinServerAsync(server);
    }

    

    private async void ModsScroll_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.VerticalOffset + e.ViewportHeight >= e.ExtentHeight - 250)
        {
            if (_vm.ModSource == "CurseForge")
                await _vm.LoadMoreCurseForgeModsAsync();
            else
                await _vm.LoadMoreModsAsync();
        }
    }

    private async void RpScroll_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.VerticalOffset + e.ViewportHeight >= e.ExtentHeight - 250)
            await _vm.LoadMoreResourcePacksAsync();
    }

    private async void ShadersScroll_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.VerticalOffset + e.ViewportHeight >= e.ExtentHeight - 250)
            await _vm.LoadMoreShadersAsync();
    }

    private async void RpCfScroll_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.VerticalOffset + e.ViewportHeight >= e.ExtentHeight - 250)
            await _vm.LoadMoreCurseForgeResourcePacksAsync();
    }

    private async void ShadersCfScroll_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.VerticalOffset + e.ViewportHeight >= e.ExtentHeight - 250)
            await _vm.LoadMoreCurseForgeShadersAsync();
    }

    

    private async void InstallCurseForgeResourcePack_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is CurseForgeMod mod)
            await _vm.InstallCurseForgeResourcePackAsync(mod);
    }

    private async void InstallCurseForgeShader_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is CurseForgeMod mod)
            await _vm.InstallCurseForgeShaderAsync(mod);
    }

    private void RefreshInstalledMods_Click(object sender, RoutedEventArgs e)
    {
        _vm.RefreshInstalledMods();
    }

    private void RefreshInstalledRps_Click(object sender, RoutedEventArgs e)
    {
        _vm.LoadInstalledResourcePacks();
    }

    private void RefreshInstalledShaders_Click(object sender, RoutedEventArgs e)
    {
        _vm.LoadInstalledShaders();
    }

    private void RemoveInstalledRp_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is InstalledPackItem item)
            _vm.RemoveInstalledPack(item, false);
    }

    private void RemoveInstalledShader_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is InstalledPackItem item)
            _vm.RemoveInstalledPack(item, true);
    }

    private async void InstallResourcePack_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ModrinthMod mod)
            await _vm.InstallResourcePackAsync(mod);
    }

    private async void InstallShader_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ModrinthMod mod)
            await _vm.InstallShaderAsync(mod);
    }

    private void ViewMode_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag && int.TryParse(tag, out int mode))
        {
            _vm.ModViewMode = mode;
        }
    }

    private void OpenModPage_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is Button btn && btn.Tag is ModrinthMod mod && !string.IsNullOrEmpty(mod.Slug))
            {
                Process.Start(new ProcessStartInfo($"https://modrinth.com/mod/{mod.Slug}") { UseShellExecute = true });
            }
            else if (sender is Button btn2 && btn2.Tag is CurseForgeMod cmod && !string.IsNullOrEmpty(cmod.Slug))
            {
                Process.Start(new ProcessStartInfo($"https://www.curseforge.com/minecraft/mc-mods/{cmod.Slug}") { UseShellExecute = true });
            }
        }
        catch { }
    }

    private void OpenRpPage_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is Button btn && btn.Tag is ModrinthMod mod && !string.IsNullOrEmpty(mod.Slug))
                Process.Start(new ProcessStartInfo($"https://modrinth.com/resourcepack/{mod.Slug}") { UseShellExecute = true });
            else if (sender is Button btn2 && btn2.Tag is CurseForgeMod cmod && !string.IsNullOrEmpty(cmod.Slug))
                Process.Start(new ProcessStartInfo($"https://www.curseforge.com/minecraft/texture-packs/{cmod.Slug}") { UseShellExecute = true });
        }
        catch { }
    }

    private void OpenShaderPage_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is Button btn && btn.Tag is ModrinthMod mod && !string.IsNullOrEmpty(mod.Slug))
                Process.Start(new ProcessStartInfo($"https://modrinth.com/shader/{mod.Slug}") { UseShellExecute = true });
            else if (sender is Button btn2 && btn2.Tag is CurseForgeMod cmod && !string.IsNullOrEmpty(cmod.Slug))
                Process.Start(new ProcessStartInfo($"https://www.curseforge.com/minecraft/shaders/{cmod.Slug}") { UseShellExecute = true });
        }
        catch { }
    }

    private void Category_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Content is string category)
            _vm.SearchCategory(category);
    }

    private void ApplyRecommendedRam_Click(object sender, RoutedEventArgs e)
    {
        _vm.ApplyRecommendedRam();
    }

    private void Diagnostics_Click(object sender, RoutedEventArgs e)
    {
        _vm.RunDiagnostics();
    }

    private void OpenScreenshotsFolder_Click(object sender, RoutedEventArgs e)
    {
        _vm.OpenScreenshotsFolder();
    }

    private void Screenshot_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement el && el.Tag is ScreenshotItem item)
            _vm.OpenScreenshot(item);
    }

    private void ScreenshotsScroll_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        ScreenshotsScroll.ScrollToVerticalOffset(ScreenshotsScroll.VerticalOffset - e.Delta / 2.0);
        e.Handled = true;
    }

    private void CopyScreenshot_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ScreenshotItem shot)
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(shot.FilePath);
                bmp.EndInit();
                bmp.Freeze();
                System.Windows.Clipboard.SetImage(bmp);
                _vm.Status = "cyr1";
                _vm.ShowToast("cyr2");
            }
            catch { _vm.Status = "cyr3"; }
        }
    }

    private void HighlightModSource()
    {
        HighlightSubTab();
    }

    private async void InstallModrinth_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ModrinthMod mod)
        {
            var idx = _vm.ModrinthResults.IndexOf(mod);
            if (idx >= 0) await _vm.InstallModrinthByIndexAsync(idx);
        }
    }

    private async void PickModVersion_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ModrinthMod mod)
            await _vm.ShowModVersionsAsync(mod);
    }

    private async void PickCurseForgeVersion_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is CurseForgeMod mod)
            await _vm.ShowCurseForgeVersionsAsync(mod);
    }

    private async void InstallCurseForge_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is CurseForgeMod mod)
            await _vm.InstallCurseForgeModAsync(mod);
    }

    private void RemoveModBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement el && el.Tag is ModInfo mod)
            _vm.RemoveMod(mod);
    }

    private async void ModSearch_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        if (_vm.IsResourcePacksSubTab) await _vm.SearchResourcePacksAsync();
        else if (_vm.IsShadersSubTab) await _vm.SearchShadersAsync();
        else await _vm.SearchModsAsync();
    }

    private void HistoryToggle_Click(object sender, RoutedEventArgs e)
    {
        if (HistoryScroll.Visibility == Visibility.Visible)
        {
            HistoryScroll.Visibility = Visibility.Collapsed;
            BtnHistoryToggle.Content = "▸";
        }
        else
        {
            HistoryScroll.Visibility = Visibility.Visible;
            BtnHistoryToggle.Content = "▾";
        }
    }

    private void RemoveAccountFromPopup_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is AccountInfo account)
            _vm.RemoveAccount(account);
    }

    private void LogoutDiscord_Click(object sender, RoutedEventArgs e)
    {
        _vm.LogoutDiscord();
    }

    private void FriendSelect_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement el && el.Tag is FriendEntry friend)
            _vm.SelectedFriend = friend;
    }

    private void RemoveFriend_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is FriendEntry friend)
            _vm.RemoveFriend(friend);
    }

    private void JoinFriend_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedFriend != null)
            _vm.JoinFriendServer(_vm.SelectedFriend);
    }

    private async void ChatInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            await _vm.SendChatAsync();
    }

    private async void GroupChatInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            await _vm.SendGroupChatAsync();
    }

    private void GroupSelect_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement el && el.Tag is GroupChat group)
            _vm.SelectedGroup = group;
    }

    private async void AcceptRequest_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is FriendEntry request)
            await _vm.AcceptRequestAsync(request);
    }

    private void DeclineRequest_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is FriendEntry request)
            _vm.DeclineRequest(request);
    }

    private void RenameFriend_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is FriendEntry friend)
            _vm.RenameFriend(friend);
    }

    private async void InviteFriend_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is FriendEntry friend)
            await _vm.InviteFriendAsync(friend);
    }

    private void LeaveGroup_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is GroupChat group)
            _vm.LeaveGroup(group);
    }

    private void ModEnabled_Click(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox cb && cb.DataContext is ModInfo mod)
            _vm.SetModEnabled(mod, cb.IsChecked == true);
    }

    private void SelectCape_Click(object sender, RoutedEventArgs e)
    {
        _vm.SelectCape();
    }

    private void RemoveCape_Click(object sender, RoutedEventArgs e)
    {
        _vm.RemoveCape();
    }

    private async void ToggleOptimization_Click(object sender, RoutedEventArgs e)
    {
        await _vm.ToggleOptimizationPackAsync();
    }

    

    private void Window_DragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            DropOverlay.Visibility = Visibility.Visible;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_DragLeave(object sender, DragEventArgs e)
    {
        DropOverlay.Visibility = Visibility.Collapsed;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        DropOverlay.Visibility = Visibility.Collapsed;
        if (e.Data.GetDataPresent(DataFormats.FileDrop) &&
            e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
        {
            _vm.InstallDroppedFiles(files);
        }
    }

    private void SettingsSection_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && int.TryParse(btn.Tag as string, out int section))
        {
            _vm.SettingsSection = section;
            HighlightSettingsSection();
        }
    }

    private void VersionCategory_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && int.TryParse(btn.Tag as string, out int category))
        {
            _vm.VersionCategory = category;
            var active = (Style)FindResource("BtnRed");
            var inactive = (Style)FindResource("BtnOutline");
            BtnVcAll.Style = category == 0 ? active : inactive;
            BtnVcReleases.Style = category == 1 ? active : inactive;
            BtnVcSnapshots.Style = category == 2 ? active : inactive;
            BtnVcOld.Style = category == 3 ? active : inactive;
        }
    }

    private void HighlightSettingsSection()
    {
        var active = (Style)FindResource("BtnRed");
        var inactive = (Style)FindResource("BtnOutline");
        BtnSetGeneral.Style = _vm.SettingsSection == 0 ? active : inactive;
        BtnSetProfile.Style = _vm.SettingsSection == 1 ? active : inactive;
        BtnSetLaunch.Style = _vm.SettingsSection == 2 ? active : inactive;
        BtnSetAppearance.Style = _vm.SettingsSection == 3 ? active : inactive;
    }

    private void ClearDataBtn_Click(object sender, RoutedEventArgs e)
    {
        var r = MessageBox.Show("cyr4", "cyr5",
            MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (r != MessageBoxResult.Yes) return;
        try
        {
            var dir = Helpers.MinecraftPathHelper.BaseDir;
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            Helpers.MinecraftPathHelper.Initialize();
            MessageBox.Show("cyr6", "cyr7", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) { MessageBox.Show($"cyr8", "cyr9", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    protected override void OnClosed(EventArgs e)
    {
        if (!_vm.GameDetached) _vm.StopGame();
        if (_vm.CurrentProfile != null) _vm.SaveProfile(_vm.CurrentProfile);
        _vm.SaveSettings();
        SaveWindowState();
        base.OnClosed(e);
    }
}

internal class WinPos
{
    public double X { get; set; }
    public double Y { get; set; }
    public double W { get; set; }
    public double H { get; set; }
    public double SW { get; set; }
}
