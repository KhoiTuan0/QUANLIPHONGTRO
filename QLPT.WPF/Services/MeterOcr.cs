using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Tesseract;

namespace QLPT.WPF.Services;

/// <summary>
/// Reads the black, whole-kWh digits of a meter. OCR is only a suggestion;
/// the operator must compare the original photo before saving a reading.
/// </summary>
public static class MeterOcr
{
    public readonly record struct Result(decimal? Suggestion, string? Error);

    public static Result Read(string imagePath, Int32Rect? numberRegion = null, int? digitCount = null)
    {
        if (!File.Exists(imagePath))
            return new(null, "Không tìm thấy ảnh. Hãy chọn lại ảnh hoặc nhập chỉ số bằng tay.");

        string tessdata = Path.Combine(AppContext.BaseDirectory, "tessdata");
        if (!File.Exists(Path.Combine(tessdata, "eng.traineddata")))
            return new(null, "Thiếu mô hình OCR trong thư mục tessdata. Bạn vẫn có thể nhập chỉ số bằng tay.");

        try
        {
            using var stream = File.OpenRead(imagePath);
            var source = BitmapFrame.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            BitmapSource selected = source;
            if (numberRegion is { } region)
            {
                if (region.X < 0 || region.Y < 0 || region.Width < 30 || region.Height < 12 ||
                    region.X + region.Width > source.PixelWidth || region.Y + region.Height > source.PixelHeight)
                    return new(null, "Vùng số đã chọn không hợp lệ. Hãy chọn lại vùng chữ số màu đen.");
                selected = new CroppedBitmap(source, region);
            }

            using var engine = new TesseractEngine(tessdata, "eng", EngineMode.Default);
            engine.SetVariable("tessedit_char_whitelist", "0123456789");

            if (numberRegion.HasValue && digitCount is >= 3 and <= 7)
            {
                if (TryReadSeparateDigits(selected, digitCount.Value, engine, out decimal separated))
                    return new(separated, null);
                return new(null, "OCR chưa đọc chắc chắn đủ từng ô số. Hãy khoanh sát các ô chữ số màu đen, thử lại hoặc nhập tay; bỏ ô số đỏ 1/10 kWh.");
            }

            var readings = new List<(string Digits, float Confidence)>();
            ReadVariant(Encode(selected), engine, readings);

            // Mechanical meters have light digits against a dark window.
            if (numberRegion.HasValue)
            {
                foreach (int threshold in new[] { 70, 90, 110, 130 })
                    ReadVariant(Enhance(selected, threshold), engine, readings);
            }

            var best = readings
                .Where(item => item.Confidence >= 0.55f)
                .GroupBy(item => item.Digits)
                .Select(group => new { Digits = group.Key, Count = group.Count(), Confidence = group.Max(item => item.Confidence) })
                .OrderByDescending(item => item.Count)
                .ThenByDescending(item => item.Confidence)
                .FirstOrDefault();

            if (best == null || (best.Count < 2 && best.Confidence < 0.85f) ||
                !decimal.TryParse(best.Digits, out decimal value))
                return new(null, "OCR chưa đọc chắc chắn dãy số. Hãy khoanh sát các chữ số màu đen, thử lại hoặc nhập tay; bỏ ô số đỏ 1/10 kWh.");

            return new(value, null);
        }
        catch (Exception)
        {
            return new(null, "Không đọc được ảnh bằng OCR. Hãy chọn ảnh khác hoặc nhập chỉ số bằng tay.");
        }
    }

    private static bool TryReadSeparateDigits(BitmapSource strip, int count, TesseractEngine engine, out decimal value)
    {
        value = 0;
        if (strip.PixelWidth / count < 12 || strip.PixelHeight < 12) return false;
        var digits = new char[count];
        for (int index = 0; index < count; index++)
        {
            int left = (int)Math.Round((double)index * strip.PixelWidth / count);
            int right = (int)Math.Round((double)(index + 1) * strip.PixelWidth / count);
            var cell = new CroppedBitmap(strip, new Int32Rect(left, 0, right - left - 1, strip.PixelHeight));
            var options = new[]
            {
                Encode(new TransformedBitmap(cell, new ScaleTransform(4, 4))),
                EnhanceCell(cell, 70, true),
                EnhanceCell(cell, 100, true),
                EnhanceCell(cell, 100, false),
                EnhanceCell(cell, 140, false)
            };
            var candidates = new List<(char Digit, float Confidence)>();
            foreach (byte[] bytes in options)
            {
                using var pix = Pix.LoadFromMemory(bytes);
                using var page = engine.Process(pix, PageSegMode.SingleChar);
                string text = (page.GetText() ?? "").Trim();
                if (text.Length == 1 && text[0] is >= '0' and <= '9')
                    candidates.Add((text[0], page.GetMeanConfidence()));
            }

            var best = candidates.GroupBy(item => item.Digit)
                .Select(group => new { Digit = group.Key, Total = group.Sum(item => item.Confidence),
                    Peak = group.Max(item => item.Confidence) })
                .OrderByDescending(item => item.Total)
                .FirstOrDefault();
            if (best == null || best.Peak < 0.72f) return false;
            digits[index] = best.Digit;
        }

        return decimal.TryParse(new string(digits), out value);
    }

    private static byte[] EnhanceCell(BitmapSource source, int threshold, bool bright)
    {
        var gray = new FormatConvertedBitmap(source, PixelFormats.Gray8, null, 0);
        int width = gray.PixelWidth, height = gray.PixelHeight;
        var pixels = new byte[width * height];
        gray.CopyPixels(pixels, width, 0);
        var output = new byte[pixels.Length];
        for (int index = 0; index < pixels.Length; index++)
            output[index] = (bright ? pixels[index] > threshold : pixels[index] < threshold)
                ? (byte)0 : (byte)255;
        var image = BitmapSource.Create(width, height, 96, 96, PixelFormats.Gray8, null, output, width);
        return Encode(new TransformedBitmap(image, new ScaleTransform(4, 4)));
    }

    private static void ReadVariant(byte[] bytes, TesseractEngine engine, List<(string Digits, float Confidence)> readings)
    {
        using var image = Pix.LoadFromMemory(bytes);
        foreach (PageSegMode mode in new[] { PageSegMode.SingleLine, PageSegMode.SingleWord })
        {
            using var page = engine.Process(image, mode);
            string text = (page.GetText() ?? "").Trim();
            if (Regex.IsMatch(text, @"^\d{3,7}$"))
                readings.Add((text, page.GetMeanConfidence()));
        }
    }

    private static byte[] Encode(BitmapSource image)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var memory = new MemoryStream();
        encoder.Save(memory);
        return memory.ToArray();
    }

    private static byte[] Enhance(BitmapSource source, int threshold)
    {
        var gray = new FormatConvertedBitmap(source, PixelFormats.Gray8, null, 0);
        int width = gray.PixelWidth, height = gray.PixelHeight;
        var pixels = new byte[width * height];
        gray.CopyPixels(pixels, width, 0);

        const int scale = 4;
        const int border = 16;
        int outputWidth = width * scale + border * 2;
        int outputHeight = height * scale + border * 2;
        var output = new byte[outputWidth * outputHeight];
        Array.Fill(output, (byte)255);
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            if (pixels[y * width + x] <= threshold) continue;
            for (int dy = 0; dy < scale; dy++)
            for (int dx = 0; dx < scale; dx++)
                output[(border + y * scale + dy) * outputWidth + border + x * scale + dx] = 0;
        }

        return Encode(BitmapSource.Create(outputWidth, outputHeight, 96, 96,
            PixelFormats.Gray8, null, output, outputWidth));
    }
}
