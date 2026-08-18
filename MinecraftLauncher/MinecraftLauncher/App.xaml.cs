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

        
        if (e.Args.Length >= 3 && e.Args[0] == "--updatefolder")
        {
            Updater.CopyFolder(e.Args[1], e.Args[2]);
            Shutdown();
            return;
        }

        
        _singleMutex = new Mutex(true, "DEDLauncher.SingleInstance", out bool isNew);
        if (!isNew)
        {
            Shutdown();
            return;
        }

        
        try { GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency; } catch { }

        ApplyPerfSettings();

        
        
        
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

            
            if (string.IsNullOrWhiteSpace(installed))
            {
                WriteInstallMarker(markerPath, currentDir);
                return;
            }

            var installedDir = Path.GetFullPath(installed)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            
            if (string.Equals(installedDir, currentDir, StringComparison.OrdinalIgnoreCase))
                return;

            
            var targetExe = Path.Combine(installedDir, "DEDLauncher.exe");
            if (!File.Exists(targetExe))
            {
                WriteInstallMarker(markerPath, currentDir);
                return;
            }

            
            Version.TryParse(FileVersionInfo.GetVersionInfo(Environment.ProcessPath!).FileVersion, out var currentVersion);
            Version.TryParse(FileVersionInfo.GetVersionInfo(targetExe).FileVersion, out var installedVersion);
            currentVersion ??= new Version();
            installedVersion ??= new Version();
            if (currentVersion <= installedVersion)
                return;

            var answer = MessageBox.Show(
                $"cyr1" +
                $"{installedDir}\n\n" +
                $"cyr2" +
                $"cyr3",
                "DED Launcher", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes)
                return;

            
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
        catch { }
    }

    private static void WriteInstallMarker(string markerPath, string dir)
    {
        try
        {
            File.WriteAllText(markerPath, JsonSerializer.Serialize(new { path = dir }));
        }
        catch { }
    }

    
    
    
    
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
                    catch { } 
                }

                
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
