using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QLPT.WPF.Services;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length == 6 && args[0] == "--prepare")
        {
            var region = new Int32Rect(int.Parse(args[2]), int.Parse(args[3]), int.Parse(args[4]), int.Parse(args[5]));
            var images = MeterAiImages.Prepare(args[1], region);
            if (images.Crop.Length >= 2_000_000 || images.Overview.Length >= 1_500_000 ||
                images.Crop[0] != 137 || images.Crop[1] != 80 ||
                images.Overview[0] != 0xff || images.Overview[1] != 0xd8 ||
                images.Overview[^2] != 0xff || images.Overview[^1] != 0xd9)
                throw new InvalidOperationException("OCR images are not valid or exceed API limits.");
            Console.WriteLine($"PASS: AI payload crop={images.Crop.Length} bytes, overview={images.Overview.Length} bytes.");
            return;
        }
        if (args.Length == 6 && args[0] == "--region")
        {
            var region = new Int32Rect(int.Parse(args[2]), int.Parse(args[3]), int.Parse(args[4]), int.Parse(args[5]));
            var reading = MeterOcr.Read(args[1], region, 5);
            Console.WriteLine($"REGION: {reading.Suggestion?.ToString(CultureInfo.InvariantCulture) ?? "(không đọc được)"} {reading.Error}");
            return;
        }

        string imagePath = Path.Combine(Path.GetTempPath(), $"qlpt-meter-ocr-{Guid.NewGuid():N}.png");
        try
        {
            var visual = new DrawingVisual();
            using (var drawing = visual.RenderOpen())
            {
                drawing.DrawRectangle(Brushes.White, null, new Rect(0, 0, 500, 130));
                drawing.DrawText(new FormattedText("12345", CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight, new Typeface("Arial"), 86, Brushes.Black, 1),
                    new Point(50, 10));
            }
            var bitmap = new RenderTargetBitmap(500, 130, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var file = File.Create(imagePath)) encoder.Save(file);

            var result = MeterOcr.Read(imagePath);
            if (result.Suggestion != 12345)
                throw new InvalidOperationException($"OCR expected 12345; got {result.Suggestion} ({result.Error}).");
            Console.WriteLine("PASS: OCR reads a rendered meter number with deployed native/model files.");
        }
        finally
        {
            if (File.Exists(imagePath)) File.Delete(imagePath);
        }
    }
}
