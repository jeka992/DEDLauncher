using System.Diagnostics;
using System.IO;
using System.Runtime;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using DedLauncher.Helpers;

namespace DedLauncher;

public partial class App : Application
{
    private static Mutex? _singleMutex;

    /// <summary>Версия лаунчера из сборки (например 2.0.0).</summary>
    public static string VersionLabel
    {
        get
        {
            try
            {
                var v = typeof(App).Assembly.GetName().Version;
                if (v != null) return $"{v.Major}.{v.Minor}.{v.Build}";
            }
            catch { }
            return "1.0.0";
        }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Режим установщика: копируем новую версию поверх существующей установки
        if (e.Args.Length >= 3 && e.Args[0] == "--updatefolder")
        {
            Updater.CopyFolder(e.Args[1], e.Args[2]);
            Shutdown();
            return;
        }

        // Один экземпляр лаунчера
        _singleMutex = new Mutex(true, "DEDLauncher.SingleInstance", out bool isNew);
        if (!isNew)
        {
            Shutdown();
            return;
        }

        // Меньше GC-пауз при работе UI (важно для слабых ПК)
        try { GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency; } catch { }

        ApplyPerfSettings();

        // В режиме слабого ПК снимаем все визуальные эффекты (тени — самое
        // дорогое в WPF) и отключаем анимацию попапов у ЛЮБОГО элемента,
        // включая те, что появятся позже (шаблоны, тултипы).
        if (PerfSettings.LowEndMode)
        {
            EventManager.RegisterClassHandler(typeof(FrameworkElement), FrameworkElement.LoadedEvent,
                new RoutedEventHandler((s, _) =>
                {
                    if (s is UIElement ue && ue.Effect != null) ue.Effect = null;
                    if (s is Popup pop && pop.PopupAnimation != PopupAnimation.None)
                        pop.PopupAnimation = PopupAnimation.None;
                }));
        }

        CheckInstallUpdate();

        var window = new MainWindow();
        window.Show();
    }

    /// <summary>
    /// Если на ПК уже установлен DED Launcher (другая папка), а запущенная
    /// копия новее — предлагаем обновить установленный. Так раздаются
    /// обновления вручную: скачал новую версию из Telegram/Discord,
    /// запустил — лаунчер сам перенесёт себя на место старой установки.
    /// </summary>
    private void CheckInstallUpdate()
    {
        try
        {
            var currentDir = Path.GetFullPath(AppContext.BaseDirectory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var markerPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                ".dedlauncher", "install.json");

            string? installed = null;
            if (File.Exists(markerPath))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(markerPath));
                installed = doc.RootElement.TryGetProperty("path", out var p) ? p.GetString() : null;
            }

            // Первый запуск — запоминаем текущую папку как установку
            if (string.IsNullOrWhiteSpace(installed))
            {
                WriteInstallMarker(markerPath, currentDir);
                return;
            }

            var installedDir = Path.GetFullPath(installed)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            // Запущено из той же папки — это и есть установка
            if (string.Equals(installedDir, currentDir, StringComparison.OrdinalIgnoreCase))
                return;

            // Старая установка исчезла — принимаем текущую папку за новую
            var targetExe = Path.Combine(installedDir, "DEDLauncher.exe");
            if (!File.Exists(targetExe))
            {
                WriteInstallMarker(markerPath, currentDir);
                return;
            }

            // Сравниваем версии: обновлять только если запущенная копия новее
            Version.TryParse(FileVersionInfo.GetVersionInfo(Environment.ProcessPath!).FileVersion, out var currentVersion);
            Version.TryParse(FileVersionInfo.GetVersionInfo(targetExe).FileVersion, out var installedVersion);
            currentVersion ??= new Version();
            installedVersion ??= new Version();
            if (currentVersion <= installedVersion)
                return;

            var answer = MessageBox.Show(
                $"Найден установленный DED Launcher {installedVersion.Major}.{installedVersion.Minor}.{installedVersion.Build}:\n" +
                $"{installedDir}\n\n" +
                $"Эта копия новее ({currentVersion.Major}.{currentVersion.Minor}.{currentVersion.Build}). " +
                $"Обновить установленный лаунчер?",
                "DED Launcher", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes)
                return;

            // Проверка цифровой подписи новой версии перед установкой
            var sigPath = Path.Combine(currentDir, "DEDLauncher.exe.sig");
            if (File.Exists(sigPath))
            {
                var exeBytes = File.ReadAllBytes(Environment.ProcessPath!);
                var sigContent = File.ReadAllText(sigPath).Trim();
                if (!UpdateSigning.Verify(exeBytes, sigContent))
                {
                    MessageBox.Show(
                        "Цифровая подпись новой версии недействительна. Обновление отменено.\n\n" +
                        "Скачайте лаунчер только из официального Telegram-канала или Discord.",
                        "Ошибка проверки подписи", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            // Копия-обновщик перенесёт файлы и запустит установленную версию
            var updaterPath = Path.Combine(Path.GetTempPath(), "DEDUpdater.exe");
            File.Copy(Environment.ProcessPath!, updaterPath, true);
            Process.Start(new ProcessStartInfo
            {
                FileName = updaterPath,
                Arguments = $"--updatefolder \"{currentDir}\" \"{installedDir}\"",
                UseShellExecute = false
            });
            Shutdown();
        }
        catch { Logger.Error("CheckInstallUpdate", new Exception("Failed to check install update")); }
    }

    private static void WriteInstallMarker(string markerPath, string dir)
    {
        try
        {
            File.WriteAllText(markerPath, JsonSerializer.Serialize(new { path = dir }));
        }
        catch (Exception ex) { Logger.Error("WriteInstallMarker", ex); }
    }

    /// <summary>
    /// Отдельный процесс-установщик: переносит файлы новой копии поверх
    /// существующей установки и запускает обновлённый лаунчер.
    /// </summary>
    private static class Updater
    {
        public static void CopyFolder(string srcDir, string dstDir)
        {
            try
            {
                foreach (var file in Directory.GetFiles(srcDir, "*", SearchOption.AllDirectories))
                {
                    var rel = Path.GetRelativePath(srcDir, file);
                    var dest = Path.Combine(dstDir, rel);
                    try
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                        File.Copy(file, dest, true);
                    }
                    catch { } // файл занят — пропускаем
                }

                // Установкой теперь считается целевая папка
                var markerPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    ".dedlauncher", "install.json");
                File.WriteAllText(markerPath, JsonSerializer.Serialize(new { path = dstDir }));
            }
            catch { }
            finally
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = Path.Combine(dstDir, "DEDLauncher.exe"),
                        UseShellExecute = false
                    });
                }
                catch { }
            }
        }
    }

    /// <summary>
    /// Читает настройки производительности из settings.json ДО создания окна —
    /// программный рендеринг и режим слабого ПК действуют с первого кадра.
    /// </summary>
    private void ApplyPerfSettings()
    {
        try
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                ".dedlauncher", "settings.json");

            if (File.Exists(path))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                var root = doc.RootElement;
                if (root.TryGetProperty("LowEndMode", out var lowEnd) && lowEnd.ValueKind == JsonValueKind.True)
                    PerfSettings.LowEndMode = true;
                if (root.TryGetProperty("SoftwareRendering", out var soft) && soft.ValueKind == JsonValueKind.True)
                    PerfSettings.SoftwareRendering = true;
            }
        }
        catch { }

        if (PerfSettings.SoftwareRendering)
        {
            try
            {
                RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
            }
            catch { }
        }
    }
}
