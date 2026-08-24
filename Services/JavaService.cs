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

        // Стратегия как в XMCL: сначала официальный/основной источник,
        // при любой ошибке — fallback на другой вендор.
        try
        {
            return await InstallTemurinAsync(majorVersion, destDir, javaExe, progress);
        }
        catch (Exception ex)
        {
            Logger.Error($"Java {majorVersion} Temurin install failed, trying Zulu", ex);
        }

        try
        {
            return await InstallZuluAsync(majorVersion, destDir, javaExe, progress);
        }
        catch (Exception ex)
        {
            Logger.Error($"Java {majorVersion} Zulu install failed", ex);
        }

        return null;
    }

    private async Task<string?> InstallTemurinAsync(int majorVersion, string destDir, string javaExe,
        IProgress<DownloadProgress>? progress)
    {
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
        var size = package.TryGetProperty("size", out var sz) ? sz.GetInt64() : 0L;

        if (string.IsNullOrEmpty(link)) return null;

        // 2. Скачиваем ZIP во временную папку
        var zipPath = Path.Combine(Path.GetTempPath(), $"temurin-{releaseName}.zip");
        progress?.Report(new DownloadProgress { FileName = $"Java {majorVersion} (Temurin)", TotalBytes = size });

        await DownloadToFileAsync(link, zipPath, progress);

        // 3. Распаковываем
        System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, JavaDir, true);
        File.Delete(zipPath);

        // 4. Находим папку (имя jdk-{majorVersion}.{minor}+{build}…)
        var extracted = Directory.GetDirectories(JavaDir, $"{releaseName}*").FirstOrDefault();
        if (extracted != null && extracted != destDir)
        {
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

    /// <summary>Fallback: установка Azul Zulu JRE/JDK через API api.azul.com.</summary>
    private async Task<string?> InstallZuluAsync(int majorVersion, string destDir, string javaExe,
        IProgress<DownloadProgress>? progress)
    {
        var zuluApi = $"https://api.azul.com/metadata/v1/zulu/packages/?java_version={majorVersion}&os=windows&arch=x64&archive_type=zip&java_package_type=jdk&javafx_bundled=false&crac_supported=false&release_status=ga&latest=true&page_size=1&page=1";
        var json = await _http.GetStringAsync(zuluApi);
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0) return null;

        var item = doc.RootElement[0];
        var downloadUrl = item.GetProperty("download_url").GetString() ?? "";
        var name = item.TryGetProperty("name", out var n) ? n.GetString() ?? $"zulu-{majorVersion}" : $"zulu-{majorVersion}";
        var size = item.TryGetProperty("size", out var sz) ? sz.GetInt64() : 0L;
        if (string.IsNullOrEmpty(downloadUrl)) return null;

        var zipPath = Path.Combine(Path.GetTempPath(), $"{name}.zip");
        progress?.Report(new DownloadProgress { FileName = $"Java {majorVersion} (Zulu)", TotalBytes = size });
        await DownloadToFileAsync(downloadUrl, zipPath, progress);

        System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, JavaDir, true);
        File.Delete(zipPath);

        // Zulu распаковывается в папку {name}/ — переносим в предсказуемую
        var extracted = Directory.GetDirectories(JavaDir, $"{name}*").FirstOrDefault();
        if (extracted != null && extracted != destDir)
        {
            if (Directory.Exists(destDir)) Directory.Delete(destDir, true);
            Directory.Move(extracted, destDir);
        }

        progress?.Report(new DownloadProgress
        {
            FileName = $"Java {majorVersion} (Zulu)",
            DownloadedBytes = size,
            TotalBytes = size
        });

        return File.Exists(javaExe) ? javaExe : null;
    }

    private async Task DownloadToFileAsync(string url, string destPath, IProgress<DownloadProgress>? progress)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(15));
        var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cts.Token);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
        await using var fs = File.Create(destPath);
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await stream.ReadAsync(buffer, cts.Token)) > 0)
        {
            await fs.WriteAsync(buffer.AsMemory(0, read), cts.Token);
            total += read;
            progress?.Report(new DownloadProgress
            {
                FileName = Path.GetFileName(destPath),
                DownloadedBytes = total,
                TotalBytes = 0
            });
        }
        await fs.DisposeAsync();
    }

    /// <summary>Быстро проверяет конкретный путь к java.exe и возвращает информацию о нём.</summary>
    public JavaInfo? ProbeJava(string javaExe)
    {
        if (string.IsNullOrEmpty(javaExe) || !File.Exists(javaExe)) return null;
        return GetJavaInfo(javaExe);
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

        // Новая схема версий Minecraft (2025+): 26.3, 27.1 и т.д. — требуют Java 21+
        if (major >= 2) return 21;

        // Старая схема: 1.x
        if (major == 1 && minor >= 21) return 21;  // 1.21+ нужна Java 21
        if (major == 1 && minor >= 18) return 17;  // 1.18–1.20: Java 17
        if (major == 1 && minor >= 17) return 16;  // 1.17: Java 16
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
            // Читаем оба потока ПАРАЛЛЕЛЬНО (последовательное чтение может
            // заблокироваться: stderr и stdout заполняют буферы одновременно)
            var errTask = process.StandardError.ReadToEndAsync();
            var outTask = process.StandardOutput.ReadToEndAsync();
            Task.WaitAll(new Task[] { errTask, outTask }, 5000);
            var output = errTask.Result + outTask.Result;
            if (!process.WaitForExit(2000))
            {
                try { process.Kill(); } catch { }
            }

            var info = new JavaInfo { Path = javaExe };

            foreach (var line in output.Split('\n'))
            {
                // Тримим — в выводе -XshowSettings строки идут с отступом ("    java.version = ...")
                var trimmed = line.TrimStart();
                // ВАЖНО: матчим ТОЧНОЕ "java.version =", иначе строка "java.version.date = ..."
                // (тоже содержит "java.version") затирает версию датой, и парсинг даёт Major=0.
                if (trimmed.StartsWith("java.version", StringComparison.Ordinal)
                    && trimmed.Length > "java.version".Length
                    && (trimmed["java.version".Length] == ' ' || trimmed["java.version".Length] == '='))
                    info.Version = ExtractValue(line);
                else if (trimmed.StartsWith("java.vendor", StringComparison.Ordinal)
                         && trimmed.Length > "java.vendor".Length
                         && (trimmed["java.vendor".Length] == ' ' || trimmed["java.vendor".Length] == '='))
                    info.Vendor = ExtractValue(line);
                else if (trimmed.StartsWith("sun.arch.data.model", StringComparison.Ordinal))
                    info.Is64Bit = ExtractValue(line) == "64";
            }

            if (string.IsNullOrEmpty(info.Version))
            {
                info.Version = ReadVersionFallback(javaExe);
            }

            info.MajorVersion = ParseMajorVersion(info.Version);
            return info;
        }
        catch
        {
            return null;
        }
    }

    private static string ReadVersionFallback(string javaExe)
    {
        try
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
            var errTask = versionProcess.StandardError.ReadToEndAsync();
            var outTask = versionProcess.StandardOutput.ReadToEndAsync();
            Task.WaitAll(new Task[] { errTask, outTask }, 3000);
            if (!versionProcess.WaitForExit(2000))
            {
                try { versionProcess.Kill(); } catch { }
            }
            return (errTask.Result + outTask.Result).Split('\n').FirstOrDefault() ?? "";
        }
        catch { return ""; }
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