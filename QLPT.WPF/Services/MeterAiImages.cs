using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace QLPT.WPF.Services;

public static class MeterAiImages
{
    public static (byte[] Crop, byte[] Overview) Prepare(string imagePath, Int32Rect region)
    {
        using var stream = File.OpenRead(imagePath);
        var source = BitmapFrame.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        if (region.X < 0 || region.Y < 0 || region.Width < 30 || region.Height < 12 ||
            region.X + region.Width > source.PixelWidth || region.Y + region.Height > source.PixelHeight)
            throw new ArgumentOutOfRangeException(nameof(region), "Vùng số vượt ngoài ảnh.");
        var crop = new CroppedBitmap(source, region);
        double cropScale = Math.Min(8, 1100.0 / crop.PixelWidth);
        BitmapSource enlarged = cropScale > 1 ? new TransformedBitmap(crop, new ScaleTransform(cropScale, cropScale)) : crop;
        var png = new PngBitmapEncoder();
        png.Frames.Add(BitmapFrame.Create(enlarged));
        using var cropStream = new MemoryStream();
        png.Save(cropStream);
        double overviewScale = Math.Min(1, 1024.0 / source.PixelWidth);
        BitmapSource overview = overviewScale < 1 ? new TransformedBitmap(source, new ScaleTransform(overviewScale, overviewScale)) : source;
        var jpeg = new JpegBitmapEncoder { QualityLevel = 78 };
        jpeg.Frames.Add(BitmapFrame.Create(overview));
        using var overviewStream = new MemoryStream();
        jpeg.Save(overviewStream);
        return (cropStream.ToArray(), overviewStream.ToArray());
    }
}
