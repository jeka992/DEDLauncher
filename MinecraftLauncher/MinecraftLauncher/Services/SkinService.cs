using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DedLauncher.Helpers;

namespace DedLauncher.Services;

public class SkinService
{
    public static string SkinsDir => Path.Combine(MinecraftPathHelper.BaseDir, "skins");

    public SkinService()
    {
        Directory.CreateDirectory(SkinsDir);
    }

    
    
    
    
    public (bool Valid, int Width, int Height) ValidateSkin(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            var frame = decoder.Frames[0];
            int w = frame.PixelWidth;
            int h = frame.PixelHeight;

            bool valid = (w == 64 && h == 64) || (w == 64 && h == 32) || (w == 128 && h == 128)
                || (w == h && w > 64 && (w & (w - 1)) == 0); 
            return (valid, w, h);
        }
        catch (Exception)
        {
            return (false, 0, 0);
        }
    }

    
    
    
    
    public string PrepareSkin(string path)
    {
        using var stream = File.OpenRead(path);
        var decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];
        int w = frame.PixelWidth;
        int h = frame.PixelHeight;

        if ((w == 64 && h == 64) || (w == 64 && h == 32) || (w == 128 && h == 128))
            return path; 

        
        var resized = new TransformedBitmap(frame, new ScaleTransform(64.0 / w, 64.0 / h));
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(resized));

        var tmp = Path.Combine(Path.GetTempPath(), $"skin_{Guid.NewGuid():N}.png");
        using (var fs = File.Create(tmp))
            encoder.Save(fs);
        return tmp;
    }

    
    
    
    public string SaveSkin(string sourcePath, string username)
    {
        var safeName = Sanitize(username);
        var dest = Path.Combine(SkinsDir, $"{safeName}.png");
        File.Copy(sourcePath, dest, true);
        return dest;
    }

    public string? GetSkinPath(string username)
    {
        var safeName = Sanitize(username);
        var exact = Path.Combine(SkinsDir, $"{safeName}.png");
        if (File.Exists(exact)) return exact;

        
        try
        {
            if (Directory.Exists(SkinsDir))
            {
                var match = Directory.GetFiles(SkinsDir, "*.png")
                    .FirstOrDefault(f => string.Equals(Path.GetFileNameWithoutExtension(f), safeName, StringComparison.OrdinalIgnoreCase));
                if (match != null) return match;
            }
        }
        catch { }

        return null;
    }

    

    private string CapesDir => Path.Combine(MinecraftPathHelper.BaseDir, "capes");

    
    public string SaveCape(string sourcePath, string username)
    {
        Directory.CreateDirectory(CapesDir);
        var safeName = Sanitize(username);
        var dest = Path.Combine(CapesDir, $"{safeName}.png");
        File.Copy(sourcePath, dest, true);
        return dest;
    }

    public string? GetCapePath(string username)
    {
        var safeName = Sanitize(username);
        var path = Path.Combine(CapesDir, $"{safeName}.png");
        return File.Exists(path) ? path : null;
    }

    public void RemoveCape(string username)
    {
        var path = GetCapePath(username);
        if (path != null) { try { File.Delete(path); } catch { } }
    }

    
    
    
    
    
    public BitmapSource? GetHeadPreview(string skinPath, int size = 256)
    {
        try
        {
            using var stream = File.OpenRead(skinPath);
            var decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            var frame = decoder.Frames[0];
            int scale = frame.PixelWidth / 64; 

            
            int px = 8 * scale;
            int py = 8 * scale;
            int sz = 8 * scale;

            var crop = new CroppedBitmap(frame, new System.Windows.Int32Rect(px, py, sz, sz));
            var bgra = new FormatConvertedBitmap(crop, PixelFormats.Bgra32, null, 0);
            var preview = NearestNeighborScale(bgra, size, size);
            preview.Freeze();
            return preview;
        }
        catch
        {
            return null;
        }
    }

    
    private static BitmapSource NearestNeighborScale(BitmapSource src, int targetW, int targetH)
    {
        int srcW = src.PixelWidth;
        int srcH = src.PixelHeight;
        var srcPixels = new int[srcW * srcH];
        src.CopyPixels(srcPixels, srcW * 4, 0);

        var dstPixels = new int[targetW * targetH];
        int xRatio = (srcW << 16) / targetW;
        int yRatio = (srcH << 16) / targetH;

        for (int y = 0; y < targetH; y++)
        {
            int sy = (y * yRatio) >> 16;
            int dstRow = y * targetW;
            int srcRow = sy * srcW;
            for (int x = 0; x < targetW; x++)
            {
                int sx = (x * xRatio) >> 16;
                dstPixels[dstRow + x] = srcPixels[srcRow + sx];
            }
        }

        var wb = new WriteableBitmap(targetW, targetH, 96, 96, PixelFormats.Bgra32, null);
        wb.WritePixels(new System.Windows.Int32Rect(0, 0, targetW, targetH), dstPixels, targetW * 4, 0);
        return wb;
    }

    
    
    
    public BitmapSource? GetFullPreview(string skinPath, int maxSize = 192)
    {
        try
        {
            using var stream = File.OpenRead(skinPath);
            var decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            var frame = decoder.Frames[0];
            double ratio = (double)maxSize / Math.Max(frame.PixelWidth, frame.PixelHeight);
            var preview = new TransformedBitmap(frame, new ScaleTransform(ratio, ratio));
            preview.Freeze();
            return preview;
        }
        catch
        {
            return null;
        }
    }

    private static string Sanitize(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return string.IsNullOrWhiteSpace(name) ? "player" : name;
    }
}
