using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DedLauncher.Models;

public class LaunchProfile : INotifyPropertyChanged
{
    private string _id = Guid.NewGuid().ToString("N")[..8];
    private string _name = "cyr1";
    private string _versionId = "1.21.1";
    private string _versionType = "release";
    private string _modLoader = "Vanilla";
    private string _modLoaderVersion = "";
    private string _javaPath = "";
    private int _minRamMb = 2048;
    private int _maxRamMb = 4096;
    private string _jvmArgs = "";
    private string _gameArgs = "";
    private int _windowWidth = 854;
    private int _windowHeight = 480;
    private bool _fullscreen;
    private string _gameDir = "";

    public string Id { get => _id; set => Set(ref _id, value); }
    public string Name { get => _name; set => Set(ref _name, value); }
    public string VersionId { get => _versionId; set { if (Set(ref _versionId, value)) { OnPropertyChanged(nameof(DisplayVersion)); OnPropertyChanged(nameof(ShortInfo)); } } }
    public string VersionType { get => _versionType; set => Set(ref _versionType, value); }
    public string ModLoader { get => _modLoader; set { if (Set(ref _modLoader, value)) { OnPropertyChanged(nameof(DisplayVersion)); OnPropertyChanged(nameof(ShortInfo)); } } }
    public string ModLoaderVersion { get => _modLoaderVersion; set { if (Set(ref _modLoaderVersion, value)) OnPropertyChanged(nameof(DisplayVersion)); } }
    public string JavaPath { get => _javaPath; set => Set(ref _javaPath, value); }
    public int MinRamMb { get => _minRamMb; set => Set(ref _minRamMb, value); }
    public int MaxRamMb { get => _maxRamMb; set => Set(ref _maxRamMb, value); }
    public string JvmArgs { get => _jvmArgs; set => Set(ref _jvmArgs, value); }
    public string GameArgs { get => _gameArgs; set => Set(ref _gameArgs, value); }
    public int WindowWidth { get => _windowWidth; set => Set(ref _windowWidth, value); }
    public int WindowHeight { get => _windowHeight; set => Set(ref _windowHeight, value); }
    public bool Fullscreen { get => _fullscreen; set => Set(ref _fullscreen, value); }
    public string GameDir { get => _gameDir; set => Set(ref _gameDir, value); }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastPlayed { get; set; }

    public string DisplayVersion => ModLoader == "Vanilla"
        ? $"Minecraft {VersionId}"
        : $"Minecraft {VersionId} · {ModLoader} {ModLoaderVersion}";

    public string ShortInfo => $"{VersionId} · {ModLoader}";

    public override string ToString() => Name;

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
