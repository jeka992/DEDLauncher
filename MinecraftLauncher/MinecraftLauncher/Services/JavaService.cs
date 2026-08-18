using System.Diagnostics;
using DedLauncher.Models;

namespace DedLauncher.Services;

public class JavaService
{
    public async Task<List<JavaInfo>> FindJavaInstallationsAsync()
    {
        var results = new List<JavaInfo>();

        await Task.Run(() =>
        {
            var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
            if (!string.IsNullOrEmpty(javaHome))
            {
                var javaExe = Path.Combine(javaHome, "bin", "java.exe");
                if (File.Exists(javaExe))
                {
                    var info = GetJavaInfo(javaExe);
                    if (info != null) results.Add(info);
                }
            }

            var paths = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (var dir in paths.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var javaExe = Path.Combine(dir.Trim(), "java.exe");
                if (File.Exists(javaExe) && !results.Any(j => j.Path == javaExe))
                {
                    var info = GetJavaInfo(javaExe);
                    if (info != null) results.Add(info);
                }
            }

            SearchCommonJavaPaths(results);
        });

        return results.OrderByDescending(j => j.MajorVersion).ToList();
    }

    private void SearchCommonJavaPaths(List<JavaInfo> results)
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

            foreach (var javaExe in Directory.GetFiles(basePath, "java.exe", SearchOption.AllDirectories))
            {
                if (results.Any(j => j.Path == javaExe)) continue;
                var info = GetJavaInfo(javaExe);
                if (info != null) results.Add(info);
            }
        }
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
