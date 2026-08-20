using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using DedLauncher.Helpers;
using DedLauncher.Models;

namespace DedLauncher.Services;

public class JavaService
{
    private readonly HttpClient _http;
    private static readonly string JavaDir = Path.Combine(MinecraftPathHelper.BaseDir, "java");

    public JavaService() : this(new HttpClient()) { }

    public JavaService(HttpClient http)
    {
        _http = http;
        Directory.CreateDirectory(JavaDir);
    }

    public async Task<List<JavaInfo>> FindJavaInstallationsAsync()
    {
        var results = new List<JavaInfo>();

        await Task.Run(() =>
        {
            ScanJavaHome(results);
            ScanPath(results);
            ScanCommonPaths(results);

            // Сканируем установленную лаунчером Java
            ScanLauncherJava(results);
        });

        return results.OrderByDescending(j => j.MajorVersion).ToList();
    }

    private void ScanLauncherJava(List<JavaInfo> results)
    {
        if (!Directory.Exists(JavaDir)) return;
        foreach (var dir in Directory.GetDirectories(JavaDir))
        {
            var javaExe = Path.Combine(dir, "bin", "java.exe");
            if (File.Exists(javaExe) && results.All(j => j.Path != javaExe))
            {
                var info = GetJavaInfo(javaExe);
                if (info != null) results.Add(info);
            }
        }
    }

    /// <summary>Скачивает и устанавливает Temurin JDK нужной версии.</summary>
    public async Task<string?> InstallJavaAsync(int majorVersion, IProgress<DownloadProgress>? progress = null)
    {
        var destDir = Path.Combine(JavaDir, $"jdk-{majorVersion}");
        var javaExe = Path.Combine(destDir, "bin", "java.exe");
        if (File.Exists(javaExe)) return javaExe;

        // 1. Получаем последний билд Temurin JDK через API adoptium.net
        var apiUrl = $"https://api.adoptium.net/v3/assets/feature_releases/{majorVersion}/ga?architecture=x64&image_type=jdk&jvm_impl=hotspot&os=windows&page=0&page_size=1&project=jdk&sort_method=DEFAULT&sort_order=DESC&vendor=eclipse";
        var json = await _http.GetStringAsync(apiUrl);
        using var doc = JsonDocument.Parse(json);
        var asset = doc.RootElement[0];

        var releaseName = asset.GetProperty("release_name").GetString() ?? $"jdk-{majorVersion}";
        var binaries = asset.GetProperty("binaries");
        var binary = binaries[0];

        var package = binary.GetProperty("package");
        var link = package.GetProperty("link").GetString() ?? "";
        var checksumLink = package.GetProperty("checksum_link").GetString() ?? "";
        var size = package.TryGetProperty("size", out var sz) ? sz.GetInt64() : 0L;

        if (string.IsNullOrEmpty(link)) return null;

        // 2. Скачиваем ZIP во временную папку
        var zipPath = Path.Combine(Path.GetTempPath(), $"temurin-{releaseName}.zip");
        progress?.Report(new DownloadProgress { FileName = $"Java {majorVersion} (Temurin)", TotalBytes = size });

        var response = await _http.GetAsync(link, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync();
        await using var fs = File.Create(zipPath);
        await stream.CopyToAsync(fs);
        await fs.DisposeAsync();

        // 3. Распаковываем
        System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, JavaDir, true);
        File.Delete(zipPath);

        // 4. Находим папку (имя jdk-{majorVersion}.{minor}+{build}…)
        var extracted = Directory.GetDirectories(JavaDir, $"{releaseName}*").FirstOrDefault();
        if (extracted != null && extracted != destDir)
        {
            // Переименовываем в предсказуемое имя
            if (Directory.Exists(destDir)) Directory.Delete(destDir, true);
            Directory.Move(extracted, destDir);
        }

        progress?.Report(new DownloadProgress
        {
            FileName = $"Java {majorVersion} (Temurin)",
            DownloadedBytes = size,
            TotalBytes = size
        });

        return File.Exists(javaExe) ? javaExe : null;
    }

    private void ScanJavaHome(List<JavaInfo> results)
    {
        var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
        if (string.IsNullOrEmpty(javaHome)) return;
        var javaExe = Path.Combine(javaHome, "bin", "java.exe");
        if (File.Exists(javaExe))
        {
            var info = GetJavaInfo(javaExe);
            if (info != null) results.Add(info);
        }
    }

    private void ScanPath(List<JavaInfo> results)
    {
        var paths = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in paths.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var javaExe = Path.Combine(dir.Trim(), "java.exe");
            if (File.Exists(javaExe) && results.All(j => j.Path != javaExe))
            {
                var info = GetJavaInfo(javaExe);
                if (info != null) results.Add(info);
            }
        }
    }

    private void ScanCommonPaths(List<JavaInfo> results)
    {
        var searchPaths = new List<string>
        {
            @"C:\Program Files\Java",
            @"C:\Program Files (x86)\Java",
            @"C:\Program Files\Eclipse Adoptium",
            @"C:\Program Files\Microsoft",
            @"C:\Program Files\Zulu",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages", "Microsoft.4297127D64EC6_8wekyb3d8bbwe", "LocalCache", "Local", "runtime")
        };

        foreach (var basePath in searchPaths)
        {
            if (!Directory.Exists(basePath)) continue;
            try
            {
                foreach (var javaExe in Directory.GetFiles(basePath, "java.exe", SearchOption.AllDirectories))
                {
                    if (results.Any(j => j.Path == javaExe)) continue;
                    var info = GetJavaInfo(javaExe);
                    if (info != null) results.Add(info);
                }
            }
            catch { }
        }
    }

    /// <summary>Определяет нужную мажорную версию Java для указанной версии Minecraft.</summary>
    public static int RequiredJavaVersion(string mcVersion)
    {
        var parts = mcVersion.Split('.');
        if (parts.Length < 2) return 21;
        var major = int.TryParse(parts[0], out var m) ? m : 0;
        var minor = int.TryParse(parts[1], out var n) ? n : 0;

        if (major >= 1 && minor >= 21) return 21;  // 1.21+ нужна Java 21
        if (major >= 1 && minor >= 18) return 17;  // 1.18–1.20: Java 17
        if (major >= 1 && minor >= 17) return 16;  // 1.17: Java 16
        return 8;                                   // 1.16 и ниже: Java 8
    }

    private JavaInfo? GetJavaInfo(string javaExe)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = javaExe,
                    Arguments = "-XshowSettings:properties -version",
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = process.StandardError.ReadToEnd() + process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);

            var info = new JavaInfo { Path = javaExe };

            foreach (var line in output.Split('\n'))
            {
                if (line.Contains("java.version"))
                    info.Version = ExtractValue(line);
                else if (line.Contains("java.vendor"))
                    info.Vendor = ExtractValue(line);
                else if (line.Contains("sun.arch.data.model"))
                    info.Is64Bit = ExtractValue(line) == "64";
            }

            if (string.IsNullOrEmpty(info.Version))
            {
                var versionProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = javaExe,
                        Arguments = "--version",
                        RedirectStandardError = true,
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                versionProcess.Start();
                var verOutput = versionProcess.StandardError.ReadToEnd();
                versionProcess.WaitForExit(3000);
                info.Version = verOutput.Split('\n').FirstOrDefault() ?? "";
            }

            info.MajorVersion = ParseMajorVersion(info.Version);
            return info;
        }
        catch
        {
            return null;
        }
    }

    private string ExtractValue(string line)
    {
        var parts = line.Split('=');
        return parts.Length >= 2 ? parts[1].Trim() : "";
    }

    private int ParseMajorVersion(string version)
    {
        if (string.IsNullOrEmpty(version)) return 0;

        var versionStr = version
            .Replace("\"", "")
            .Replace("openjdk version", "")
            .Replace("java version", "")
            .Trim();

        var dot = versionStr.IndexOf('.');
        if (dot < 0) return 0;

        var major = versionStr[..dot];
        if (major == "1")
        {
            var secondDot = versionStr.IndexOf('.', dot + 1);
            if (secondDot > 0)
                major = versionStr.Substring(dot + 1, secondDot - dot - 1);
        }

        return int.TryParse(major, out var result) ? result : 0;
    }
}