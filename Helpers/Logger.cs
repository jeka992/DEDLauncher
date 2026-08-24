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
        // Фильтр «ожидаемых» ошибок (как ErrorDiagnose в XMCL):
        // не засоряем лог тем, что не является реальной проблемой.
        if (IsExpected(ex)) return;
        _ = WriteAsync($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] ERROR [{context}] {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
    }

    /// <summary>Ошибки пользовательской среды/операций, которые не стоит логировать как баг.</summary>
    private static bool IsExpected(Exception ex)
    {
        if (ex is OperationCanceledException) return true;                    // пользователь отменил
        if (ex is TaskCanceledException) return true;

        // Диск полон
        if (ex is IOException io && (io.Message.Contains("no space", StringComparison.OrdinalIgnoreCase)
                                     || io.Message.Contains("достаточно места", StringComparison.OrdinalIgnoreCase)))
            return true;

        // Файл занят другим процессом (антивирус/Explorer) — пробуем позже, не баг
        if (ex is IOException)
            return true;

        // Известные отмены/обрывы MQTT
        if (ex.GetType().Name.Contains("MqttCommunicationException", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
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