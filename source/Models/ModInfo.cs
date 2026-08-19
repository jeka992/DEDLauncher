using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Windows.Media;

namespace DedLauncher.Models;

public class ModInfo
{
    public string FileName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Version { get; set; } = "";
    public string ModId { get; set; } = "";
    public string Description { get; set; } = "";
    public string FilePath { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public string IconUrl { get; set; } = "";
    public string Source { get; set; } = "";
    public DateTime InstalledAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Установленный ресурспак или шейдер (файл в папке resourcepacks/shaderpacks).
/// </summary>
public class InstalledPackItem
{
    public string FileName { get; set; } = "";
    public string FilePath { get; set; } = "";
    public string Description { get; set; } = "";
    public string SizeText { get; set; } = "";
    public DateTime InstalledAt { get; set; }
}

public class ForgeVersionEntry
{
    public string Version { get; set; } = "";
    public string McVersion { get; set; } = "";
}

public class FabricVersionEntry
{
    public string Version { get; set; } = "";
    public bool Stable { get; set; }
}

public class FabricLoaderEntry
{
    public string Version { get; set; } = "";
    public bool Stable { get; set; }
}

// Ответ /loader/{mcVersion} — вложенная структура с полем loader
public class FabricLoaderMcEntry
{
    [JsonPropertyName("loader")]
    public FabricLoaderMcInfo Loader { get; set; } = new();
}

public class FabricLoaderMcInfo
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "";

    [JsonPropertyName("stable")]
    public bool Stable { get; set; }

    [JsonPropertyName("maven")]
    public string Maven { get; set; } = "";
}

public class ModrinthSearchResult
{
    [JsonPropertyName("hits")]
    public List<ModrinthMod> Hits { get; set; } = new();

    [JsonPropertyName("total_hits")]
    public int TotalHits { get; set; }
}

public class ModrinthMod : INotifyPropertyChanged
{
    [JsonPropertyName("project_id")]
    public string ProjectId { get; set; } = "";

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("icon_url")]
    public string IconUrl { get; set; } = "";

    [JsonPropertyName("slug")]
    public string Slug { get; set; } = "";

    [JsonPropertyName("author")]
    public string Author { get; set; } = "";

    [JsonPropertyName("versions")]
    public List<string> Versions { get; set; } = new();

    [JsonPropertyName("downloads")]
    public int Downloads { get; set; }

    private ImageSource? _icon;
    [JsonIgnore]
    public ImageSource? Icon
    {
        get => _icon;
        set { _icon = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class ModrinthVersion
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("version_number")]
    public string VersionNumber { get; set; } = "";

    [JsonPropertyName("game_versions")]
    public List<string> GameVersions { get; set; } = new();

    [JsonPropertyName("loaders")]
    public List<string> Loaders { get; set; } = new();

    [JsonPropertyName("files")]
    public List<ModrinthFile> Files { get; set; } = new();

    [JsonPropertyName("dependencies")]
    public List<ModrinthDependency> Dependencies { get; set; } = new();
}

public class ModrinthDependency
{
    [JsonPropertyName("version_id")]
    public string? VersionId { get; set; }

    [JsonPropertyName("project_id")]
    public string? ProjectId { get; set; }

    [JsonPropertyName("file_name")]
    public string? FileName { get; set; }

    [JsonPropertyName("dependency_type")]
    public string DependencyType { get; set; } = "";
}

public class ModrinthFile
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = "";

    [JsonPropertyName("filename")]
    public string Filename { get; set; } = "";

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("primary")]
    public bool Primary { get; set; }
}

public class CurseForgeSearchResult
{
    [JsonPropertyName("data")]
    public List<CurseForgeMod> Data { get; set; } = new();
}

public class CurseForgeMod : INotifyPropertyChanged
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = "";

    [JsonPropertyName("slug")]
    public string Slug { get; set; } = "";

    [JsonPropertyName("gameId")]
    public int GameId { get; set; }

    [JsonPropertyName("logo")]
    public CurseForgeLogo? Logo { get; set; }

    [JsonPropertyName("downloadCount")]
    public int DownloadCount { get; set; }

    [JsonPropertyName("thumbnailUrl")]
    public string ThumbnailUrl { get; set; } = "";

    private ImageSource? _icon;
    [JsonIgnore]
    public ImageSource? Icon
    {
        get => _icon;
        set { _icon = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class CurseForgeLogo
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = "";

    [JsonPropertyName("thumbnailUrl")]
    public string ThumbnailUrl { get; set; } = "";
}

public class CurseForgeFilesResult
{
    [JsonPropertyName("data")]
    public List<CurseForgeFile> Data { get; set; } = new();
}

public class CurseForgeFile
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = "";

    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = "";

    [JsonPropertyName("fileLength")]
    public long FileLength { get; set; }

    [JsonPropertyName("gameVersions")]
    public List<string> GameVersions { get; set; } = new();

    [JsonPropertyName("downloadUrl")]
    public string DownloadUrl { get; set; } = "";
}

// CFWidget API models (no API key required)
public class CfWidgetProject
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = "";

    [JsonPropertyName("slug")]
    public string Slug { get; set; } = "";

    [JsonPropertyName("downloads")]
    public CfWidgetDownloads? Downloads { get; set; }

    [JsonPropertyName("thumbnail")]
    public string Thumbnail { get; set; } = "";

    [JsonPropertyName("files")]
    public List<CfWidgetFile> Files { get; set; } = new();
}

public class CfWidgetDownloads
{
    [JsonPropertyName("total")]
    public long Total { get; set; }

    [JsonPropertyName("monthly")]
    public long Monthly { get; set; }
}

public class CfWidgetFile
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("display")]
    public string Display { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("version")]
    public string Version { get; set; } = "";

    [JsonPropertyName("filesize")]
    public long Filesize { get; set; }

    [JsonPropertyName("versions")]
    public List<string> Versions { get; set; } = new();
}
