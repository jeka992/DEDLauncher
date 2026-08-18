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
            return 8192; 
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

    
    
    
    public static int MaxAllocatableMb
    {
        get
        {
            var max = TotalRamMb - 2048;
            return (int)Math.Clamp(max, 2048, 32768);
        }
    }

    
    
    
    
    public static int RecommendedRamMb
    {
        get
        {
            var quarter = TotalRamMb / 4;
            return (int)Math.Clamp(quarter, 2048, 6144);
        }
    }
}
