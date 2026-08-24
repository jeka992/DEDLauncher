using System.Text;
using System.Text.Json;

namespace DedLauncher.Helpers;

/// <summary>
/// Надёжные атомарные операции с файлами (по образцу XMCL):
/// запись через временный файл + rename, бэкап перед перезаписью,
/// восстановление повреждённого JSON из бэкапа.
/// </summary>
public static class AtomicFile
{
    /// <summary>Атомарная запись: temp-файл → переименование. Не оставляет половинчатых файлов при краше.</summary>
    public static void WriteAllText(string path, string content)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var tmp = path + ".tmp";
        File.WriteAllText(tmp, content, Encoding.UTF8);
        File.Move(tmp, path, true);
    }

    /// <summary>Запись с бэкапом предыдущей версии: создаёт {path}.backup перед перезаписью.</summary>
    public static void WriteAllTextWithBackup(string path, string content)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        if (File.Exists(path))
        {
            try { File.Copy(path, path + ".backup", true); } catch { }
        }

        var tmp = path + ".tmp";
        File.WriteAllText(tmp, content, Encoding.UTF8);
        File.Move(tmp, path, true);
    }

    /// <summary>Читает JSON. При повреждении основного файла — пробует бэкап, затем возвращает дефолт.</summary>
    public static T? ReadJsonOrDefault<T>(string path, T? fallback = default)
    {
        var errors = new List<Exception>();

        foreach (var candidate in new[] { path, path + ".backup" })
        {
            if (!File.Exists(candidate)) continue;
            try
            {
                var result = JsonSerializer.Deserialize<T>(File.ReadAllText(candidate));
                if (result != null) return result;
            }
            catch (Exception ex) { errors.Add(ex); }
        }

        if (errors.Count > 0)
            Logger.Error($"ReadJsonOrDefault({path})", errors[0]);
        return fallback;
    }
}