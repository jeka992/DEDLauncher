namespace DedLauncher.Models;

public class JavaInfo
{
    public string Path { get; set; } = "";
    public string Version { get; set; } = "";
    public string Vendor { get; set; } = "";
    public int MajorVersion { get; set; }
    public bool Is64Bit { get; set; }
}

public class DownloadProgress
{
    public string FileName { get; set; } = "";
    public long DownloadedBytes { get; set; }
    public long TotalBytes { get; set; }
    public double Percentage => TotalBytes > 0 ? (double)DownloadedBytes / TotalBytes * 100 : 0;
}
