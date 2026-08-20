using System.Windows;
using System.Windows.Media;

namespace DedLauncher.Services;

public static class ThemeManager
{
    public static readonly List<ThemePalette> Themes = new()
    {
        new("Красный", "Red",
            Color.FromRgb(0xB3, 0x00, 0x00), Color.FromRgb(0xD6, 0x00, 0x00), Color.FromRgb(0xFF, 0x4D, 0x4D), Color.FromRgb(0x4D, 0x1A, 0x1A)),
        new("Синий", "Blue",
            Color.FromRgb(0x00, 0x66, 0xCC), Color.FromRgb(0x00, 0x88, 0xFF), Color.FromRgb(0x4D, 0xB8, 0xFF), Color.FromRgb(0x0A, 0x28, 0x47)),
        new("Зелёный", "Green",
            Color.FromRgb(0x00, 0x8A, 0x3C), Color.FromRgb(0x00, 0xB8, 0x50), Color.FromRgb(0x4D, 0xE8, 0x80), Color.FromRgb(0x0A, 0x3A, 0x20)),
        new("Фиолетовый", "Purple",
            Color.FromRgb(0x7B, 0x1F, 0xA2), Color.FromRgb(0x9C, 0x27, 0xB0), Color.FromRgb(0xCE, 0x93, 0xD8), Color.FromRgb(0x33, 0x15, 0x4A)),
        new("Оранжевый", "Orange",
            Color.FromRgb(0xE6, 0x51, 0x00), Color.FromRgb(0xFF, 0x8F, 0x00), Color.FromRgb(0xFF, 0xB7, 0x4D), Color.FromRgb(0x4D, 0x2A, 0x0A)),
        new("Монохром", "Mono",
            Color.FromRgb(0x6B, 0x6B, 0x6B), Color.FromRgb(0x8A, 0x8A, 0x8A), Color.FromRgb(0xBB, 0xBB, 0xBB), Color.FromRgb(0x2E, 0x2E, 0x2E)),
    };

    private static string _currentTheme = "Red";

    public static string CurrentThemeName
    {
        get => _currentTheme;
        set => ApplyTheme(value);
    }

    public static void ApplyTheme(string themeName)
    {
        _currentTheme = themeName;
        var theme = Themes.Find(t => t.Key == themeName) ?? Themes[0];

        SetBrush("AccentBrush", theme.Accent);
        SetBrush("AccentHoverBrush", theme.AccentHover);
        SetBrush("AccentLightBrush", theme.AccentLight);
        SetBrush("AccentDimBrush", theme.AccentDim);

        MainWindow.UpdateAccentBrush(new SolidColorBrush(theme.Accent));
    }

    private static void SetBrush(string key, Color color)
    {
        if (Application.Current.Resources[key] is SolidColorBrush existing && !existing.IsFrozen)
        {
            existing.Color = color;
        }
        else
        {
            Application.Current.Resources[key] = new SolidColorBrush(color);
        }
    }
}

public class ThemePalette
{
    public string Name { get; set; }
    public string Key { get; set; }
    public Color Accent { get; set; }
    public Color AccentHover { get; set; }
    public Color AccentLight { get; set; }
    public Color AccentDim { get; set; }

    public ThemePalette(string name, string key, Color accent, Color accentHover, Color accentLight, Color accentDim)
    {
        Name = name; Key = key; Accent = accent; AccentHover = accentHover; AccentLight = accentLight; AccentDim = accentDim;
    }

    public override string ToString() => Name;
}
