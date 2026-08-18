using System.Collections.Concurrent;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DedLauncher.Helpers;
using DedLauncher.Models;

namespace DedLauncher.Services;

public class ModService
{
    private const string ForgeApiUrl = "https://bmclapi2.bangbang93.com/forge/minecraft";
    private const string FabricMetaUrl = "https://meta.fabricmc.net/v2/versions";
    private const string OptiFineApi = "https://bmclapi2.bangbang93.com";
    private const string ModrinthApi = "https://api.modrinth.com/v2";

    private readonly HttpClient _http;
    private readonly string _cacheDir;

    
    private static readonly ConcurrentDictionary<string, ImageSource?> IconMemoryCache = new();
    private static readonly ConcurrentDictionary<string, Task<ImageSource?>> IconInflight = new();

    private class ModrinthPageCache
    {
        public List<ModrinthMod> Results { get; set; } = new();
        public int TotalHits { get; set; }
    }

    public ModService()
    {
        _http = new HttpClient();
        _http.DefaultRequestHeaders.Add("User-Agent", "DedLauncher/2.0");
        _cacheDir = Path.Combine(MinecraftPathHelper.BaseDir, "cache");
        Directory.CreateDirectory(_cacheDir);
        MigrateModsToStandardFolder();
    }

    
    
    
    private void MigrateModsToStandardFolder()
    {
        try
        {
            var standardDir = Path.Combine(MinecraftPathHelper.GameDir, "mods");
            Directory.CreateDirectory(standardDir);

            var profilesDir = Path.Combine(MinecraftPathHelper.GameDir, "profiles");
            if (!Directory.Exists(profilesDir)) return;

            foreach (var profileDir in Directory.GetDirectories(profilesDir))
            {
                var oldModsDir = Path.Combine(profileDir, "mods");
                if (!Directory.Exists(oldModsDir)) continue;

                foreach (var jar in Directory.GetFiles(oldModsDir, "*.jar"))
                {
                    var dest = Path.Combine(standardDir, Path.GetFileName(jar));
                    if (!File.Exists(dest))
                        File.Move(jar, dest);
                }
            }
        }
        catch { }
    }

    public string GetModsDir(string profileId = "")
    {
        
        var dir = Path.Combine(GameDir, "mods");
        Directory.CreateDirectory(dir);
        return dir;
    }

    
    
    
    
    public string GameDir { get; set; } = MinecraftPathHelper.GameDir;

    public async Task<System.Windows.Media.ImageSource?> LoadIconAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        if (IconMemoryCache.TryGetValue(url, out var done)) return done;
        return await IconInflight.GetOrAdd(url, _ => LoadIconCoreAsync(url));
    }

    private async Task<ImageSource?> LoadIconCoreAsync(string url)
    {
        try
        {
            var file = Path.Combine(_cacheDir, "icons", IconCacheFileName(url));
            if (File.Exists(file) && new FileInfo(file).Length > 0)
            {
                var bmp = await Task.Run(() =>
                {
                    var b = new BitmapImage();
                    b.BeginInit();
                    b.CacheOption = BitmapCacheOption.OnLoad;
                    b.StreamSource = new MemoryStream(File.ReadAllBytes(file));
                    b.EndInit();
                    b.Freeze();
                    return b;
                });
                IconMemoryCache[url] = bmp;
                return bmp;
            }

            var bytes = await _http.GetByteArrayAsync(url);
            using var ms = new MemoryStream(bytes);
            var icon = new BitmapImage();
            icon.BeginInit();
            icon.CacheOption = BitmapCacheOption.OnLoad;
            icon.StreamSource = ms;
            icon.EndInit();
            icon.Freeze();

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(file)!);
                await File.WriteAllBytesAsync(file, bytes);
            }
            catch { }

            IconMemoryCache[url] = icon;
            return icon;
        }
        catch { return null; }
        finally { IconInflight.TryRemove(url, out _); }
    }

    private static string IconCacheFileName(string url)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(url));
        return Convert.ToHexString(hash).ToLowerInvariant() + ".png";
    }

    
    
    
    
    private async Task<T?> GetCachedOrFetchAsync<T>(string key, Func<Task<T?>> fetch, TimeSpan ttl)
    {
        var file = Path.Combine(_cacheDir, key + ".json");
        try
        {
            if (File.Exists(file) && DateTime.UtcNow - File.GetLastWriteTimeUtc(file) < ttl)
            {
                var value = JsonSerializer.Deserialize<T>(await File.ReadAllTextAsync(file));
                if (value != null) return value;
            }
        }
        catch { }

        var result = await fetch();
        if (result != null)
        {
            try
            {
                Directory.CreateDirectory(_cacheDir);
                await File.WriteAllTextAsync(file, JsonSerializer.Serialize(result));
            }
            catch { }
        }
        return result;
    }

    
    
    
    
    private static readonly string[] ServiceMods = { "ded-mod" };

    
    private static readonly string[] DedModVersions =
    {
        "1.16.5", "1.17.1", "1.18.2",
        "1.19.2", "1.19.4", "1.20.1", "1.20.4", "1.20.6",
        "1.21.1", "1.21.4", "1.21.11"
    };

    public static bool IsDedModCompatible(string mcVersion) =>
        DedModVersions.Contains(mcVersion);

    public List<ModInfo> GetInstalledMods(string profileId)
    {
        var mods = new List<ModInfo>();
        var modsDir = GetModsDir(profileId);
        if (!Directory.Exists(modsDir)) return mods;

        foreach (var file in Directory.GetFiles(modsDir))
        {
            var fileName = Path.GetFileName(file);
            var isDisabled = fileName.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase);
            var isJar = fileName.EndsWith(".jar", StringComparison.OrdinalIgnoreCase);
            if (!isJar && !isDisabled) continue;

            
            var baseName = Path.GetFileNameWithoutExtension(fileName).ToLower();
            if (ServiceMods.Any(s => baseName.StartsWith(s, StringComparison.OrdinalIgnoreCase)))
                continue;

            
            try
            {
                if (new FileInfo(file).Length == 0)
                {
                    File.Delete(file);
                    continue;
                }
            }
            catch { }

            var displayName = isDisabled
                ? Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(fileName))
                : Path.GetFileNameWithoutExtension(fileName);

            var modInfo = new ModInfo
            {
                FileName = fileName,
                DisplayName = displayName,
                FilePath = file,
                Enabled = !isDisabled,
                InstalledAt = File.GetCreationTime(file)
            };
            try { ReadModMetadata(file, modInfo); } catch { }
            mods.Add(modInfo);
        }
        return mods;
    }

    
    
    
    
    
    public string? SetModEnabled(ModInfo mod, bool enabled)
    {
        if (mod == null || string.IsNullOrEmpty(mod.FilePath) || !File.Exists(mod.FilePath)) return null;

        if (enabled && mod.FilePath.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase))
        {
            var newPath = mod.FilePath[..^".disabled".Length];
            File.Move(mod.FilePath, newPath);
            return newPath;
        }
        if (!enabled && !mod.FilePath.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase))
        {
            var newPath = mod.FilePath + ".disabled";
            File.Move(mod.FilePath, newPath);
            return newPath;
        }
        return null;
    }

    private void ReadModMetadata(string jarPath, ModInfo modInfo)
    {
        using var archive = ZipFile.OpenRead(jarPath);
        var fabricEntry = archive.GetEntry("fabric.mod.json");
        if (fabricEntry != null)
        {
            using var stream = fabricEntry.Open();
            var json = JsonSerializer.Deserialize<JsonDocument>(stream);
            var root = json!.RootElement;
            if (root.TryGetProperty("id", out var id)) modInfo.ModId = id.GetString() ?? "";
            if (root.TryGetProperty("name", out var name)) modInfo.DisplayName = name.GetString() ?? modInfo.DisplayName;
            if (root.TryGetProperty("version", out var version)) modInfo.Version = version.GetString() ?? "";
            if (root.TryGetProperty("description", out var desc)) modInfo.Description = desc.GetString() ?? "";
            if (root.TryGetProperty("icon", out var icon)) modInfo.IconUrl = icon.GetString() ?? "";
            return;
        }
    }

    public void InstallMod(string jarPath, string profileId)
    {
        var modsDir = GetModsDir(profileId);
        var destPath = Path.Combine(modsDir, Path.GetFileName(jarPath));
        if (!File.Exists(destPath))
            File.Copy(jarPath, destPath, true);
    }

    public void RemoveMod(string filePath)
    {
        if (File.Exists(filePath)) File.Delete(filePath);
    }

    

    public async Task<List<ModrinthMod>> SearchModrinthAsync(string query, string mcVersion = "", string loader = "", int limit = 30)
        => (await SearchModrinthPageAsync(query, "mod", mcVersion, loader, limit, 0)).Results;

    
    
    
    public async Task<(List<ModrinthMod> Results, int TotalHits)> SearchModrinthPageAsync(
        string query, string projectType, string mcVersion, string loader, int limit, int offset)
    {
        
        if (string.IsNullOrWhiteSpace(query) && string.IsNullOrEmpty(mcVersion) && string.IsNullOrEmpty(loader))
        {
            var cached = await GetCachedOrFetchAsync(
                $"modrinth_pop_{projectType}_{limit}_{offset}",
                async () =>
                {
                    var (results, total) = await SearchModrinthCoreAsync(query, projectType, "", "", limit, offset);
                    return new ModrinthPageCache { Results = results, TotalHits = total };
                },
                TimeSpan.FromMinutes(30));
            if (cached != null) return (cached.Results, cached.TotalHits);
        }

        
        var (results2, total2) = await SearchModrinthCoreAsync(query, projectType, "", "", limit, offset);
        if (results2.Count == 0 && !string.IsNullOrEmpty(mcVersion))
        {
            
            (results2, total2) = await SearchModrinthCoreAsync(query, projectType, mcVersion, loader, limit, offset);
        }
        return (results2, total2);
    }

    private async Task<(List<ModrinthMod>, int)> SearchModrinthCoreAsync(
        string query, string projectType, string mcVersion, string loader, int limit, int offset)
    {
        var facets = new List<List<string>> { new() { $"project_type:{projectType}" } };
        if (!string.IsNullOrEmpty(mcVersion))
            facets.Add(new() { $"versions:{mcVersion}" });
        if (!string.IsNullOrEmpty(loader) && loader != "Vanilla")
            facets.Add(new() { $"categories:{loader.ToLower()}" });

        var facetsJson = JsonSerializer.Serialize(facets);
        var url = $"{ModrinthApi}/search?query={Uri.EscapeDataString(query)}&limit={limit}&offset={offset}&index=relevance";
        url += $"&facets={Uri.EscapeDataString(facetsJson)}";

        var response = await _http.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ModrinthSearchResult>(json);
        return (result?.Hits ?? new(), result?.TotalHits ?? 0);
    }

    public async Task<ModrinthVersion?> GetModrinthLatestVersion(string projectId, string mcVersion, string loader)
    {
        try
        {
            var query = new List<string>();
            if (!string.IsNullOrEmpty(mcVersion))
                query.Add($"game_versions=[\"{mcVersion}\"]");
            if (!string.IsNullOrEmpty(loader))
                query.Add($"loaders=[\"{loader.ToLower()}\"]");
            var url = $"{ModrinthApi}/project/{projectId}/version";
            if (query.Count > 0) url += "?" + string.Join("&", query);
            var json = await _http.GetStringAsync(url);
            var versions = JsonSerializer.Deserialize<List<ModrinthVersion>>(json);
            return versions?.FirstOrDefault();
        }
        catch { return null; }
    }

    
    
    
    
    
    
    public async Task<ModrinthVersion?> GetModrinthBestVersionAsync(string projectId, string mcVersion, string loader)
    {
        var version = await GetModrinthLatestVersion(projectId, mcVersion, loader);
        if (version != null) return version;

        if (!string.IsNullOrEmpty(mcVersion))
        {
            version = await GetModrinthLatestVersion(projectId, mcVersion, "");
            if (version != null) return version;
        }

        version = await GetModrinthLatestVersion(projectId, "", "");
        return version;
    }

    
    
    
    public async Task<List<ModrinthVersion>> GetModrinthVersionsAsync(string projectId, string mcVersion, string loader)
    {
        try
        {
            
            var cached = await GetCachedOrFetchAsync(
                $"mrv_{projectId}_{mcVersion}_{loader}",
                async () =>
                {
                    var query = "";
                    if (!string.IsNullOrEmpty(mcVersion)) query += $"game_versions=[\"{mcVersion}\"]";
                    if (!string.IsNullOrEmpty(loader)) query += (query.Length > 0 ? "&" : "") + $"loaders=[\"{loader.ToLower()}\"]";
                    var url = $"{ModrinthApi}/project/{projectId}/version{(query.Length > 0 ? "?" + query : "")}";
                    var json = await _http.GetStringAsync(url);
                    return JsonSerializer.Deserialize<List<ModrinthVersion>>(json) ?? new();
                },
                TimeSpan.FromMinutes(10));
            return cached ?? new();
        }
        catch { return new(); }
    }

    
    
    
    public async Task DownloadModrinthVersionAsync(ModrinthVersion version, string targetFolder,
        IProgress<DownloadProgress>? progress = null)
    {
        var file = version.Files.FirstOrDefault(f => f.Primary) ?? version.Files.FirstOrDefault();
        if (file == null) throw new Exception("cyr1");

        Directory.CreateDirectory(targetFolder);
        var destPath = Path.Combine(targetFolder, file.Filename);

        progress?.Report(new DownloadProgress { FileName = file.Filename, TotalBytes = file.Size });
        await DownloadFileAsync(file.Url, destPath, file.Size, progress);
    }

    public async Task DownloadModrinthModAsync(ModrinthMod mod, string mcVersion, string loader, string profileId,
        IProgress<DownloadProgress>? progress = null)
    {
        var version = await GetModrinthLatestVersion(mod.ProjectId, mcVersion, loader);
        if (version == null) throw new Exception("No compatible version found");

        var file = version.Files.FirstOrDefault(f => f.Primary) ?? version.Files.FirstOrDefault();
        if (file == null) throw new Exception("No file found");

        var modsDir = GetModsDir(profileId);
        var destPath = Path.Combine(modsDir, file.Filename);

        progress?.Report(new DownloadProgress { FileName = file.Filename, TotalBytes = file.Size });
        await DownloadFileAsync(file.Url, destPath, file.Size, progress);

        
        var installed = new HashSet<string>();
        foreach (var dep in version.Dependencies.Where(d => d.DependencyType == "required"))
        {
            if (string.IsNullOrEmpty(dep.ProjectId)) continue;
            if (installed.Contains(dep.ProjectId)) continue;
            installed.Add(dep.ProjectId);
            try
            {
                await DownloadModrinthDependencyAsync(dep.ProjectId, mcVersion, loader, profileId, progress);
            }
            catch { }
        }
    }

    
    
    
    public async Task DownloadModrinthToFolderAsync(ModrinthMod item, string mcVersion, string loader,
        string targetFolder, IProgress<DownloadProgress>? progress = null)
    {
        var version = await GetModrinthBestVersionAsync(item.ProjectId, mcVersion, loader);
        if (version == null) throw new Exception("cyr2");

        var file = version.Files.FirstOrDefault(f => f.Primary) ?? version.Files.FirstOrDefault();
        if (file == null) throw new Exception("cyr3");

        Directory.CreateDirectory(targetFolder);
        var destPath = Path.Combine(targetFolder, file.Filename);

        progress?.Report(new DownloadProgress { FileName = file.Filename, TotalBytes = file.Size });
        await DownloadFileAsync(file.Url, destPath, file.Size, progress);
    }

    
    
    
    
    
    public async Task<bool> DownloadModrinthProjectAsync(string projectId, string mcVersion, string loader,
        string targetFolder, IProgress<DownloadProgress>? progress = null)
    {
        var version = await GetModrinthBestVersionAsync(projectId, mcVersion, loader);
        if (version == null) return false;

        var file = version.Files.FirstOrDefault(f => f.Primary) ?? version.Files.FirstOrDefault();
        if (file == null) return false;

        Directory.CreateDirectory(targetFolder);
        var destPath = Path.Combine(targetFolder, file.Filename);
        if (!File.Exists(destPath) || new FileInfo(destPath).Length == 0)
        {
            progress?.Report(new DownloadProgress { FileName = file.Filename, TotalBytes = file.Size });
            await DownloadFileAsync(file.Url, destPath, file.Size, progress);
        }

        
        var installed = new HashSet<string> { projectId };
        foreach (var dep in version.Dependencies.Where(d => d.DependencyType == "required"))
        {
            if (string.IsNullOrEmpty(dep.ProjectId) || installed.Contains(dep.ProjectId)) continue;
            installed.Add(dep.ProjectId);
            try
            {
                await DownloadModrinthProjectAsync(dep.ProjectId, mcVersion, loader, targetFolder, progress);
            }
            catch { }
        }
        return true;
    }

    public string GetResourcePacksDir()
    {
        var dir = Path.Combine(GameDir, "resourcepacks");
        Directory.CreateDirectory(dir);
        return dir;
    }

    public string GetShadersDir()
    {
        var dir = Path.Combine(GameDir, "shaderpacks");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private async Task DownloadModrinthDependencyAsync(string projectId, string mcVersion, string loader,
        string profileId, IProgress<DownloadProgress>? progress)
    {
        var version = await GetModrinthLatestVersion(projectId, mcVersion, loader);
        if (version == null) return;

        var file = version.Files.FirstOrDefault(f => f.Primary) ?? version.Files.FirstOrDefault();
        if (file == null) return;

        var modsDir = GetModsDir(profileId);
        var destPath = Path.Combine(modsDir, file.Filename);
        if (File.Exists(destPath)) return;

        progress?.Report(new DownloadProgress { FileName = $"cyr4", TotalBytes = file.Size });
        await DownloadFileAsync(file.Url, destPath, file.Size, progress);
    }

    private async Task DownloadFileAsync(string url, string destPath, long size, IProgress<DownloadProgress>? progress)
    {
        
        try
        {
            if (File.Exists(destPath) && new FileInfo(destPath).Length > 0)
            {
                progress?.Report(new DownloadProgress { FileName = Path.GetFileName(destPath), DownloadedBytes = size, TotalBytes = size });
                return;
            }
        }
        catch { }

        var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync();
        await using var fs = File.Create(destPath);
        var buffer = new byte[8192];
        var read = 0;
        long total = 0;
        while ((read = await stream.ReadAsync(buffer)) > 0)
        {
            await fs.WriteAsync(buffer.AsMemory(0, read));
            total += read;
            progress?.Report(new DownloadProgress
            {
                FileName = Path.GetFileName(destPath),
                DownloadedBytes = total,
                TotalBytes = size
            });
        }

        
        if (total == 0)
        {
            await fs.DisposeAsync();
            try { File.Delete(destPath); } catch { }
            throw new Exception($"cyr5");
        }
    }

    

    private const string CfWidgetApi = "https://api.cfwidget.com";

    
    private static readonly string[] PopularCurseForgeMods =
    {
        "sodium", "iris", "lithium", "fabric-api", "jei", "journeymap",
        "xaeros-minimap", "create", "botania", "twilight-forest",
        "applied-energistics-2", "refined-storage", "quark", "waystones",
        "mouse-tweaks", "mod-menu", "continuity", "lambdynamiclights",
        "emi", "wthit", "roughly-enough-items", "curios",
        "iron-chests", "storage-drawers", "controlling", "trashslot"
    };

    private static readonly string[] PopularCurseForgeResourcePacks =
    {
        "faithful-32x", "unity", "f8thful", "clarity", "bettervanillabuilding",
        "stay-true", "dramatic-skys", "dokucraft-light", "pixel-perfection",
        "battered-old-stuff", "smooth-operator", "soft-bits", "dandelion",
        "summerfields", "faithful-64x", "default-hd", "enhanced-default",
        "vividity", "jicklus", "excalibur", "dandelion-x", "alacrity", "nature-x"
    };

    private static readonly string[] PopularCurseForgeShaders =
    {
        "complementary-reimagined", "complementary-unbound", "bsl-shaders",
        "sildurs-vibrant-shaders", "sildurs-basic-shaders", "sildurs-enhanced-default",
        "nostalgia-shader", "solstice", "super-duper-vanilla-shaders", "bliss-shader",
        "spectrum-shaders", "mellow", "pastel-shaders", "hysteria-shaders",
        "oceano-shaders", "lux-shaders"
    };

    public const string CfCategoryMods = "mc-mods";
    public const string CfCategoryResourcePacks = "texture-packs";
    public const string CfCategoryShaders = "shaders";

    public async Task<List<CurseForgeMod>> SearchCurseForgeAsync(string query, string mcVersion)
        => await SearchCurseForgeByCategoryAsync(query, CfCategoryMods);

    public async Task<List<CurseForgeMod>> SearchCurseForgeByCategoryAsync(string query, string category)
    {
        var results = new List<CurseForgeMod>();
        var q = (query ?? "").Trim().ToLower();

        var slugs = category switch
        {
            CfCategoryResourcePacks => PopularCurseForgeResourcePacks,
            CfCategoryShaders => PopularCurseForgeShaders,
            _ => PopularCurseForgeMods
        };

        
        
        var projects = await GetCachedOrFetchAsync(
            $"cf_pop_{category}",
            async () =>
            {
                var tasks = slugs.Select(s => GetCfWidgetProjectAsync(category, s)).ToArray();
                return (await Task.WhenAll(tasks)).Where(p => p != null).Select(p => p!).ToList();
            },
            TimeSpan.FromHours(1));

        foreach (var project in projects ?? new())
        {
            if (!string.IsNullOrEmpty(q) &&
                !project.Title.ToLower().Contains(q) &&
                !project.Summary.ToLower().Contains(q))
                continue;

            results.Add(new CurseForgeMod
            {
                Id = project.Id,
                Name = project.Title,
                Summary = project.Summary,
                Slug = project.Slug,
                DownloadCount = (int)(project.Downloads?.Total ?? 0),
                ThumbnailUrl = project.Thumbnail
            });
        }

        return results;
    }

    private async Task<CfWidgetProject?> GetCfWidgetProjectAsync(string category, string slug)
    {
        try
        {
            var url = $"{CfWidgetApi}/minecraft/{category}/{Uri.EscapeDataString(slug)}";
            var json = await _http.GetStringAsync(url);
            return JsonSerializer.Deserialize<CfWidgetProject>(json);
        }
        catch { return null; }
    }

    private async Task<CfWidgetProject?> GetCfWidgetProjectByIdAsync(int projectId)
    {
        
        return await GetCachedOrFetchAsync(
            $"cf_project_{projectId}",
            async () =>
            {
                try
                {
                    var url = $"{CfWidgetApi}/{projectId}";
                    var json = await _http.GetStringAsync(url);
                    return JsonSerializer.Deserialize<CfWidgetProject>(json);
                }
                catch { return null; }
            },
            TimeSpan.FromHours(6));
    }

    public async Task<List<CurseForgeFile>> GetCurseForgeFilesAsync(int modId)
    {
        var files = new List<CurseForgeFile>();
        var project = await GetCfWidgetProjectByIdAsync(modId);
        if (project == null) return files;

        foreach (var f in project.Files)
        {
            files.Add(new CurseForgeFile
            {
                Id = (int)f.Id,
                DisplayName = f.Display,
                FileName = f.Name,
                FileLength = f.Filesize,
                GameVersions = f.Versions,
                DownloadUrl = $"https://www.curseforge.com/api/v1/mods/{modId}/files/{f.Id}/download"
            });
        }

        return files;
    }

    public async Task DownloadCurseForgeFileAsync(CurseForgeFile cfFile, string profileId,
        IProgress<DownloadProgress>? progress = null)
    {
        await DownloadCurseForgeFileToFolderAsync(cfFile, GetModsDir(profileId), progress);
    }

    public async Task DownloadCurseForgeFileToFolderAsync(CurseForgeFile cfFile, string targetFolder,
        IProgress<DownloadProgress>? progress = null)
    {
        Directory.CreateDirectory(targetFolder);
        var destPath = Path.Combine(targetFolder, cfFile.FileName);

        progress?.Report(new DownloadProgress { FileName = cfFile.FileName, TotalBytes = cfFile.FileLength });
        var response = await _http.GetAsync(cfFile.DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync();
        await using var fs = File.Create(destPath);
        var buffer = new byte[8192];
        var read = 0;
        long total = 0;
        while ((read = await stream.ReadAsync(buffer)) > 0)
        {
            await fs.WriteAsync(buffer.AsMemory(0, read));
            total += read;
            progress?.Report(new DownloadProgress
            {
                FileName = cfFile.FileName,
                DownloadedBytes = total,
                TotalBytes = cfFile.FileLength
            });
        }
    }

    

    public class OptiFineEntry
    {
        public string Type { get; set; } = "";
        public string Patch { get; set; } = "";
        public string McVersion { get; set; } = "";
        public string Filename { get; set; } = "";
    }

    
    
    
    public async Task<List<OptiFineEntry>> GetOptiFineVersionsAsync(string mcVersion)
    {
        try
        {
            var json = await _http.GetStringAsync($"{OptiFineApi}/optifine/{mcVersion}");
            var list = JsonSerializer.Deserialize<List<OptiFineEntry>>(json) ?? new();
            return list
                .Where(v => v.Filename.StartsWith("OptiFine_", StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
        catch { return new(); }
    }

    
    
    
    
    
    
    public async Task InstallOptiFineAsync(string mcVersion, string typePatch,
        IProgress<DownloadProgress>? progress = null)
    {
        var parts = typePatch.Split('_');
        if (parts.Length < 2) throw new Exception("cyr6");

        var type = parts[0];
        var patch = string.Join('_', parts.Skip(1));
        var optiVersion = $"{mcVersion}_{type}_{patch}";
        var versionId = $"{mcVersion}-OptiFine_{type}_{patch}";

        
        var optiJar = Path.Combine(MinecraftPathHelper.LibrariesDir,
            "optifine", "OptiFine", optiVersion, $"OptiFine-{optiVersion}.jar");
        if (!File.Exists(optiJar) || new FileInfo(optiJar).Length == 0)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(optiJar)!);
            progress?.Report(new DownloadProgress { FileName = $"OptiFine {optiVersion}", TotalBytes = 1 });
            await DownloadFileAsync($"{OptiFineApi}/optifine/{mcVersion}/{type}/{patch}", optiJar, 1, progress);
        }

        
        
        string? launchWrapperVersion = null;
        string? launchWrapperPath = null;
        using (var probe = ZipFile.OpenRead(optiJar))
        {
            var entry = probe.Entries.FirstOrDefault(e =>
                Path.GetFileName(e.FullName).StartsWith("launchwrapper-of-", StringComparison.OrdinalIgnoreCase)
                && e.FullName.EndsWith(".jar", StringComparison.OrdinalIgnoreCase));
            if (entry != null)
            {
                launchWrapperVersion = Path.GetFileNameWithoutExtension(entry.Name)["launchwrapper-of-".Length..];
                launchWrapperPath = entry.FullName;
            }
        }
        if (launchWrapperVersion == null || launchWrapperPath == null)
            throw new Exception("cyr7");

        var lwJar = Path.Combine(MinecraftPathHelper.LibrariesDir,
            "optifine", "launchwrapper-of", launchWrapperVersion, $"launchwrapper-of-{launchWrapperVersion}.jar");
        if (!File.Exists(lwJar) || new FileInfo(lwJar).Length == 0)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(lwJar)!);
            using var zip = ZipFile.OpenRead(optiJar);
            var entry = zip.GetEntry(launchWrapperPath)!;
            entry.ExtractToFile(lwJar, true);
        }

        
        var vanillaJsonPath = Path.Combine(MinecraftPathHelper.VersionsDir, mcVersion, $"{mcVersion}.json");
        if (!File.Exists(vanillaJsonPath))
            throw new Exception($"cyr8");

        var root = JsonNode.Parse(await File.ReadAllTextAsync(vanillaJsonPath)) as JsonObject
                   ?? throw new Exception("cyr9");
        root["id"] = versionId;
        root["mainClass"] = "net.minecraft.launchwrapper.Launch";
        root["type"] = "release";

        
        if (root["arguments"] is JsonObject args && args["game"] is JsonArray gameArgs)
        {
            foreach (var item in gameArgs.ToList())
            {
                var str = item is JsonObject o && o["value"] != null
                    ? o["value"]!.GetValue<string>()
                    : item?.GetValue<string>();
                if (str != null && str.StartsWith("--tweakClass", StringComparison.OrdinalIgnoreCase))
                    gameArgs.Remove(item);
            }
            gameArgs.Add(new JsonObject { ["value"] = "--tweakClass optifine.OptiFineTweaker" });
        }
        else
        {
            root["minecraftArguments"] =
                "--username ${auth_player_name} --version ${version_name} --gameDir ${game_directory} " +
                "--assetsDir ${assets_root} --assetIndex ${assets_index_name} --uuid ${auth_uuid} " +
                "--accessToken ${auth_access_token} --userType ${user_type} --versionType ${version_type} " +
                "--tweakClass optifine.OptiFineTweaker";
        }

        
        var libraries = root["libraries"] as JsonArray;
        if (libraries == null)
        {
            libraries = new JsonArray();
            root["libraries"] = libraries;
        }
        libraries.Add(new JsonObject { ["name"] = $"optifine:launchwrapper-of:{launchWrapperVersion}" });
        libraries.Add(new JsonObject { ["name"] = $"optifine:OptiFine:{optiVersion}" });

        var versionDir = Path.Combine(MinecraftPathHelper.VersionsDir, versionId);
        Directory.CreateDirectory(versionDir);
        await File.WriteAllTextAsync(
            Path.Combine(versionDir, $"{versionId}.json"),
            root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    

    public async Task<List<ForgeVersionEntry>> GetForgeVersionsAsync(string mcVersion)
    {
        try
        {
            var json = await _http.GetStringAsync($"{ForgeApiUrl}/{mcVersion}");
            return JsonSerializer.Deserialize<List<ForgeVersionEntry>>(json) ?? new();
        }
        catch { return new(); }
    }

    public async Task<List<FabricLoaderEntry>> GetFabricLoadersAsync()
    {
        try
        {
            var json = await _http.GetStringAsync($"{FabricMetaUrl}/loader");
            return JsonSerializer.Deserialize<List<FabricLoaderEntry>>(json) ?? new();
        }
        catch { return new(); }
    }

    
    
    
    public async Task<string?> GetLatestFabricLoaderAsync(string mcVersion)
    {
        try
        {
            var json = await _http.GetStringAsync($"{FabricMetaUrl}/loader/{mcVersion}");
            var loaders = JsonSerializer.Deserialize<List<FabricLoaderMcEntry>>(json);
            return loaders?.FirstOrDefault(l => l.Loader.Stable)?.Loader.Version ?? loaders?.FirstOrDefault()?.Loader.Version;
        }
        catch { return null; }
    }

    
    
    
    public async Task<List<string>> GetFabricLoadersForMcAsync(string mcVersion)
    {
        try
        {
            var json = await _http.GetStringAsync($"{FabricMetaUrl}/loader/{mcVersion}");
            var loaders = JsonSerializer.Deserialize<List<FabricLoaderMcEntry>>(json);
            return loaders?.Select(l => l.Loader.Version).ToList() ?? new();
        }
        catch { return new(); }
    }

    public async Task InstallForgeAsync(string mcVersion, string forgeVersion,
        IProgress<DownloadProgress>? progress = null)
    {
        var tempVersionId = $"forge-{mcVersion}-{forgeVersion}";
        var versionDir = Path.Combine(MinecraftPathHelper.VersionsDir, tempVersionId);
        Directory.CreateDirectory(versionDir);

        var installerUrl = $"https://bmclapi2.bangbang93.com/forge/download?mcversion={mcVersion}&version={forgeVersion}&category=installer&format=jar";
        var installerPath = Path.Combine(versionDir, "forge-installer.jar");
        progress?.Report(new DownloadProgress { FileName = "Forge Installer", TotalBytes = 1 });

        var response = await _http.GetAsync(installerUrl);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync();
        await using var fs = File.Create(installerPath);
        await stream.CopyToAsync(fs);

        var tempDir = Path.Combine(versionDir, "temp");
        Directory.CreateDirectory(tempDir);
        ZipFile.ExtractToDirectory(installerPath, tempDir, true);

        var versionJsonPath = Path.Combine(tempDir, "version.json");
        string versionId = tempVersionId;
        if (File.Exists(versionJsonPath))
        {
            var json = await File.ReadAllTextAsync(versionJsonPath);
            
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("id", out var id))
                    versionId = id.GetString() ?? versionId;
            }
            catch { }

            var finalDir = Path.Combine(MinecraftPathHelper.VersionsDir, versionId);
            Directory.CreateDirectory(finalDir);
            File.Copy(versionJsonPath, Path.Combine(finalDir, $"{versionId}.json"), true);
        }

        var mavenDir = Path.Combine(tempDir, "maven");
        if (Directory.Exists(mavenDir))
        {
            foreach (var file in Directory.GetFiles(mavenDir, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(mavenDir, file);
                var destPath = Path.Combine(MinecraftPathHelper.LibrariesDir, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                File.Copy(file, destPath, true);
            }
        }

        Directory.Delete(tempDir, true);
        File.Delete(installerPath);
    }

    public async Task InstallFabricAsync(string mcVersion, string loaderVersion,
        IProgress<DownloadProgress>? progress = null)
    {
        var fabricUrl = $"https://meta.fabricmc.net/v2/versions/loader/{mcVersion}/{loaderVersion}/profile/json";
        var json = await _http.GetStringAsync(fabricUrl);

        
        var versionId = "fabric-" + mcVersion;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("id", out var id))
                versionId = id.GetString() ?? versionId;
        }
        catch { }

        var versionDir = Path.Combine(MinecraftPathHelper.VersionsDir, versionId);
        Directory.CreateDirectory(versionDir);
        await File.WriteAllTextAsync(Path.Combine(versionDir, $"{versionId}.json"), json);

        var profile = JsonSerializer.Deserialize<JsonDocument>(json)!;
        if (profile.RootElement.TryGetProperty("libraries", out var libraries))
        {
            foreach (var lib in libraries.EnumerateArray())
            {
                var name = lib.GetProperty("name").GetString()!;
                var url = lib.GetProperty("url").GetString()!;
                var libPath = ConvertMavenToPath(name);
                var fullUrl = $"{url}{libPath}";
                var destPath = Path.Combine(MinecraftPathHelper.LibrariesDir, libPath);
                Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);

                if (!File.Exists(destPath))
                {
                    progress?.Report(new DownloadProgress { FileName = $"Fabric: {Path.GetFileName(libPath)}", TotalBytes = 1 });
                    var response = await _http.GetAsync(fullUrl);
                    response.EnsureSuccessStatusCode();
                    await using var s = await response.Content.ReadAsStreamAsync();
                    await using var f = File.Create(destPath);
                    await s.CopyToAsync(f);
                }
            }
        }
    }

    private string ConvertMavenToPath(string mavenCoord)
    {
        var parts = mavenCoord.Split(':');
        var group = parts[0].Replace('.', '/');
        var artifact = parts[1];
        var version = parts[2];
        var classifier = parts.Length > 3 ? $"-{parts[3]}" : "";
        return $"{group}/{artifact}/{version}/{artifact}-{version}{classifier}.jar";
    }

    
    
    
    
    
    public async Task PrefetchPopularAsync(bool includeIcons = true)
    {
        try
        {
            await Task.WhenAll(
                SearchModrinthPageAsync("", "mod", "", "", 24, 0),
                SearchModrinthPageAsync("", "resourcepack", "", "", 24, 0),
                SearchModrinthPageAsync("", "shader", "", "", 24, 0),
                SearchCurseForgeByCategoryAsync("", CfCategoryMods),
                SearchCurseForgeByCategoryAsync("", CfCategoryResourcePacks),
                SearchCurseForgeByCategoryAsync("", CfCategoryShaders));
        }
        catch { }

        
        if (!includeIcons) return;
        try
        {
            var (mods, _) = await SearchModrinthPageAsync("", "mod", "", "", 24, 0);
            var cfMods = await SearchCurseForgeByCategoryAsync("", CfCategoryMods);
            var urls = mods.Where(m => !string.IsNullOrEmpty(m.IconUrl)).Select(m => m.IconUrl)
                .Concat(cfMods.Where(m => !string.IsNullOrEmpty(m.ThumbnailUrl)).Select(m => m.ThumbnailUrl))
                .Distinct()
                .ToList();

            using var gate = new SemaphoreSlim(8);
            await Parallel.ForEachAsync(urls, async (u, _) =>
            {
                await gate.WaitAsync();
                try { await LoadIconAsync(u); } catch { }
                finally { gate.Release(); }
            });
        }
        catch { }
    }
}
