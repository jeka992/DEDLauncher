using System.Text;

namespace DedLauncher.Helpers;

/// <summary>Простой файловый логгер в %appdata%/.dedlauncher/launcher.log.</summary>
public static class Logger
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        ".dedlauncher", "launcher.log");

    private static readonly SemaphoreSlim _gate = new(1, 1);

    static Logger()
    {
        try
        {
            var dir = Path.GetDirectoryName(LogPath);
            if (dir != null) Directory.CreateDirectory(dir);
            if (File.Exists(LogPath) && new FileInfo(LogPath).Length > 2 * 1024 * 1024)
                File.WriteAllText(LogPath, ""); // обрезаем если >2 МБ
        }
        catch { }
    }

    public static void Log(string message)
    {
        _ = WriteAsync($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] {message}");
    }

    public static void Error(string context, Exception ex)
    {
        _ = WriteAsync($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] ERROR [{context}] {ex.GetType().Name}: {ex.Message}");
    }

    private static async Task WriteAsync(string line)
    {
        await _gate.WaitAsync();
        try
        {
            await File.AppendAllTextAsync(LogPath, line + Environment.NewLine, Encoding.UTF8);
        }
        catch { }
        finally { _gate.Release(); }
    }
}