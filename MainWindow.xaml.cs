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
        _instance?.UpdateAmbientBackground(brush.Color);
    }

    /// <summary>Обновляет цвет фонового градиента под текущую тему (акцент «дышит» в цвет темы).</summary>
    public void UpdateAmbientBackground(Color accent)
    {
        if (BgAmbientStop == null) return;

        // Останавливаем старую анимацию цвета, чтобы не конфликтовала
        BgAmbientStop.BeginAnimation(System.Windows.Media.GradientStop.ColorProperty, null);

        // Плавно перекрашиваем фон в цвет темы (полупрозрачный, чтобы не перекрывал контент)
        BgAmbientStop.BeginAnimation(System.Windows.Media.GradientStop.ColorProperty,
            new ColorAnimation(
                BgAmbientStop.Color,
                System.Windows.Media.Color.FromArgb(0x1A, accent.R, accent.G, accent.B),
                TimeSpan.FromMilliseconds(500)));

        // Возобновляем медленную пульсацию в новом цвете
        BgAmbientStop.BeginAnimation(System.Windows.Media.GradientStop.ColorProperty,
            new ColorAnimation(
                System.Windows.Media.Color.FromArgb(0x1A, accent.R, accent.G, accent.B),
                System.Windows.Media.Color.FromArgb(0x40, (byte)(accent.R * 0.84), (byte)(accent.G * 0.84), (byte)(accent.B * 0.84)),
                TimeSpan.FromMilliseconds(3200))
            { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
    }

    /// <summary>
    /// Масштаб всего интерфейса (настройка «Размер шрифта»).
    /// 13 = 100% (дизайнерский размер), 11–15 = 85%–115%.
    /// </summary>
    public static void UpdateUiScale(int fontSize)
    {
        if (_instance == null) return;
        double scale = Math.Clamp(fontSize, 11, 15) / 13.0;
        double oldScale = 1.0;
        if (_instance.RootBorder.LayoutTransform is ScaleTransform existing)
            oldScale = existing.ScaleX;

        if (Math.Abs(oldScale - scale) < 0.001) return;

        _instance.RootBorder.LayoutTransform = new ScaleTransform(scale, scale);

        // Компенсируем размер окна, чтобы контент не обрезался по краям
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

        // Логотип-картинка для кнопки «Главная»
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
        _navBtns[BtnFriends] = "friends";
        _navBtns[BtnSetup] = "setup";

        RestoreWindowState();

        // Плавная реакция на растяжение окна: контент мягко перерисовывается
        // (лёгкий fade), вместо резкого скачка при ресайзе.
        SizeChanged += OnWindowResize;

        Loaded += async (s, e) =>
        {
            await _vm.InitAsync();
            SetActive("home");
            StartPlayPulse();
            StartAmbientBackground();
        };
    }

    private DateTime _lastResize = DateTime.MinValue;

    /// <summary>Мягкая анимация контента при изменении размера окна.</summary>
    private void OnWindowResize(object sender, SizeChangedEventArgs e)
    {
        if (PagesHost == null) return;

        // Троттлинг: не дёргаем анимацию на каждый пиксель ресайза
        if ((DateTime.UtcNow - _lastResize).TotalMilliseconds < 120) return;
        _lastResize = DateTime.UtcNow;

        var fade = new DoubleAnimation(0.55, 1, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseOut }
        };
        PagesHost.BeginAnimation(OpacityProperty, fade);
    }

    /// <summary>Медленная пульсация + дрейф фонового градиента (премиум-атмосфера).</summary>
    private void StartAmbientBackground()
    {
        if (BgAmbientStop == null || BgAmbient == null) return;

        // Пульсация яркости акцента в цвете текущей темы
        var accent = ActiveBg?.Color ?? System.Windows.Media.Color.FromRgb(0xB3, 0x00, 0x00);
        BgAmbientStop.BeginAnimation(System.Windows.Media.GradientStop.ColorProperty,
            new ColorAnimation(
                System.Windows.Media.Color.FromArgb(26, accent.R, accent.G, accent.B),
                System.Windows.Media.Color.FromArgb(64, (byte)(accent.R * 0.84), (byte)(accent.G * 0.84), (byte)(accent.B * 0.84)),
                TimeSpan.FromMilliseconds(3200))
            { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });

        // Медленный дрейф центра градиента (влево-вправо)
        BgAmbient.BeginAnimation(System.Windows.Media.RadialGradientBrush.CenterProperty,
            new PointAnimation(
                new System.Windows.Point(0.35, 0.15),
                new System.Windows.Point(0.65, 0.3),
                TimeSpan.FromMilliseconds(14000))
            { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
    }

    /// <summary>Пульсация свечения большой кнопки «Играть» на главной (премиум).</summary>
    private void StartPlayPulse()
    {
        var btn = BtnPlayBig;
        if (btn == null) return;

        btn.Effect = new System.Windows.Media.Effects.DropShadowEffect
        {
            Color = System.Windows.Media.Color.FromRgb(0xB3, 0x00, 0x00),
            BlurRadius = 16,
            ShadowDepth = 0,
            Opacity = 0.35
        };

        var pulse = new DoubleAnimation(0.3, 0.7, TimeSpan.FromMilliseconds(1600))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
        (btn.Effect as System.Windows.Media.Effects.DropShadowEffect).BeginAnimation(
            System.Windows.Media.Effects.DropShadowEffect.OpacityProperty, pulse);
    }

    private void RestoreWindowState()
    {
        // Размер и позиция окна всегда сбрасываются к дефолту (1000×720, центр).
        // Восстанавливаем только ширину боковой панели — это удобно.
        try
        {
            var path = Path.Combine(Helpers.MinecraftPathHelper.BaseDir, "window.json");
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var state = JsonSerializer.Deserialize<WinPos>(json);
                if (state != null && state.SW >= 72 && state.SW <= 220)
                {
                    SidebarColumn.Width = new GridLength(state.SW);
                }
            }
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
            case "home": _vm.NavHomeCmd.Execute(null); HighlightHomeTabs(); break;
            case "play": _vm.NavPlayCmd.Execute(null); break;
            case "mods": _vm.NavModsCmd.Execute(null); HighlightSubTab(); break;
            case "console": _vm.NavConsoleCmd.Execute(null); break;
            case "screenshots": _vm.NavScreenshotsCmd.Execute(null); break;
            case "friends": _vm.NavFriendsCmd.Execute(null); break;
            case "setup": _vm.NavSetupCmd.Execute(null); HighlightSettingsSection(); break;
        }

        AnimatePageSwitch();
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
        CloseAnimated();
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

    // ─── Источники ресурспаков/шейдеров ───

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

    // ─── Тумблеры «Браузер / Установленные» ───

    private void ModsViewToggle_Click(object sender, RoutedEventArgs e)
    {
        _vm.ShowInstalledMods = (sender as Button)?.Tag as string == "installed";
        if (_vm.ShowInstalledMods) _vm.RefreshInstalledMods();
        HighlightViewToggles();
    }

    /// <summary>Переключение подвкладок «Главной»: 0=Игра, 1=Консоль, 2=Фото.</summary>
    private void HomeSubTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && int.TryParse(btn.Tag as string, out var tab))
        {
            _vm.HomeSubTab = tab;
            HighlightHomeTabs();
        }
    }

    private void HighlightHomeTabs()
    {
        var active = (Style)FindResource("BtnRed");
        var inactive = (Style)FindResource("BtnOutline");
        BtnHomeGameTab.Style = _vm.HomeSubTab == 0 ? active : inactive;
        BtnHomeConsoleTab.Style = _vm.HomeSubTab == 1 ? active : inactive;
        BtnHomeScreensTab.Style = _vm.HomeSubTab == 2 ? active : inactive;
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

    // ─── Серверы ───

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

    // ─── Бесконечная прокрутка ───

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

    // ─── Установленные РП/шейдеры ───

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
                _vm.Status = "Скриншот скопирован в буфер обмена";
                _vm.ShowToast("Скриншот скопирован ✓");
            }
            catch { _vm.Status = "Не удалось скопировать скриншот"; }
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

    // ─── Drag & drop установка файлов ───

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
        var r = MessageBox.Show("Удалить ВСЕ данные лаунчера?", "Очистка",
            MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (r != MessageBoxResult.Yes) return;
        try
        {
            var dir = Helpers.MinecraftPathHelper.BaseDir;
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            Helpers.MinecraftPathHelper.Initialize();
            MessageBox.Show("Данные очищены.", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) { MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    protected override void OnClosed(EventArgs e)
    {
        if (!_vm.GameDetached) _vm.StopGame();
        // Сохраняем статистику ВСЕГДА при закрытии лаунчера — даже если игра
        // осталась работать (PostLaunchAction=close): иначе время сессии теряется.
        _vm.SaveStatsNow();
        if (_vm.CurrentProfile != null) _vm.SaveProfile(_vm.CurrentProfile);
        _vm.SaveSettings();
        base.OnClosed(e);
    }

    // ═══════════════ ПРЕМИУМ-АНИМАЦИИ ОКНА ═══════════════

    /// <summary>
    /// Плавное появление окна: мягкий fade. Без ScaleTransform на окне —
    /// он конфликтует с WindowChrome (ошибка Ownership).
    /// </summary>
    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);

        Opacity = 0;
        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(320))
        {
            EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseOut }
        };
        BeginAnimation(OpacityProperty, fadeIn);
    }

    /// <summary>Плавное закрытие окна: fade + сжатие, затем реальное закрытие.</summary>
    public void CloseAnimated()
    {
        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(280))
        {
            EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseIn }
        };
        fadeOut.Completed += (s, e) => Close();
        BeginAnimation(OpacityProperty, fadeOut);

        var shrink = new DoubleAnimation(1, 0.94, TimeSpan.FromMilliseconds(280))
        {
            EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseIn }
        };
        var st = RenderTransform as ScaleTransform ?? new ScaleTransform(1, 1);
        RenderTransform = st;
        st.BeginAnimation(ScaleTransform.ScaleXProperty, shrink);
        st.BeginAnimation(ScaleTransform.ScaleYProperty, shrink);
    }

    /// <summary>Плавное появление контента вкладок с учётом премиум-длительности.</summary>
    private void AnimatePageSwitch()
    {
        if (PagesHost == null) return;
        PagesHost.BeginAnimation(OpacityProperty, null);

        var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(400))
        {
            EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseOut }
        };
        PagesHost.BeginAnimation(OpacityProperty, fade);

        var tt = new TranslateTransform(18, 0);
        PagesHost.RenderTransform = tt;
        var slide = new DoubleAnimation(18, 0, TimeSpan.FromMilliseconds(500))
        {
            EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseOut }
        };
        tt.BeginAnimation(TranslateTransform.YProperty, slide);
    }
}

internal class WinPos
{
    public int V { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double W { get; set; }
    public double H { get; set; }
    public double SW { get; set; }
}
