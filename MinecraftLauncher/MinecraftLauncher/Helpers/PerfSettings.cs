namespace DedLauncher.Helpers;






public static class PerfSettings
{
    
    public static bool LowEndMode { get; set; }

    
    public static bool SoftwareRendering { get; set; }

    
    public static int IconParallelism => LowEndMode ? 4 : 12;

    
    public static int ProgressSmoothIntervalMs => LowEndMode ? 60 : 30;

    
    public static bool AutoDetectLowEnd() =>
        SystemInfo.TotalRamMb <= 6144 || SystemInfo.CoreCount <= 2;
}
