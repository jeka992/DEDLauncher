using System.Runtime.InteropServices;

namespace DedLauncher.Helpers;

public static class SystemInfo
{
    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    public static long TotalRamMb
    {
        get
        {
            try
            {
                var mem = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX)) };
                if (GlobalMemoryStatusEx(ref mem))
                    return (long)(mem.ullTotalPhys / (1024 * 1024));
            }
            catch { }
            return 8192; // fallback 8 GB
        }
    }

    public static long AvailableRamMb
    {
        get
        {
            try
            {
                var mem = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX)) };
                if (GlobalMemoryStatusEx(ref mem))
                    return (long)(mem.ullAvailPhys / (1024 * 1024));
            }
            catch { }
            return 4096;
        }
    }

    public static int CoreCount
    {
        get
        {
            try { return Environment.ProcessorCount; }
            catch { return 4; }
        }
    }

    /// <summary>
    /// Максимум RAM, который можно выделить Minecraft (оставляем 2 ГБ системе).
    /// </summary>
    public static int MaxAllocatableMb
    {
        get
        {
            var max = TotalRamMb - 2048;
            return (int)Math.Clamp(max, 2048, 32768);
        }
    }

    /// <summary>
    /// Рекомендуемый объём RAM для Minecraft.
    /// Ванилле хватает 2 ГБ, модам — 4 ГБ. Больше 6 ГБ почти никогда не нужно.
    /// </summary>
    public static int RecommendedRamMb
    {
        get
        {
            var quarter = TotalRamMb / 4;
            return (int)Math.Clamp(quarter, 2048, 6144);
        }
    }
}
