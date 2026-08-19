namespace DedLauncher.Helpers;

/// <summary>
/// Глобальные настройки производительности лаунчера.
/// Устанавливаются в App.OnStartup ДО создания окна (из settings.json),
/// поэтому действуют сразу, без перезапуска во время сессии.
/// </summary>
public static class PerfSettings
{
    /// <summary>Режим слабого ПК: без теней/эффектов, меньше сетевой нагрузки.</summary>
    public static bool LowEndMode { get; set; }

    /// <summary>Программный рендеринг WPF (для старых/слабых GPU).</summary>
    public static bool SoftwareRendering { get; set; }

    /// <summary>Сколько иконок качать параллельно.</summary>
    public static int IconParallelism => LowEndMode ? 4 : 12;

    /// <summary>Интервал плавного догона прогресс-бара (мс).</summary>
    public static int ProgressSmoothIntervalMs => LowEndMode ? 60 : 30;

    /// <summary>Автоопределение слабого ПК при первом запуске.</summary>
    public static bool AutoDetectLowEnd() =>
        SystemInfo.TotalRamMb <= 6144 || SystemInfo.CoreCount <= 2;
}
