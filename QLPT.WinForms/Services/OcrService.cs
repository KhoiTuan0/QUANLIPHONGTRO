using System.Text.RegularExpressions;

namespace QLPT.WinForms.Services;

/// <summary>
/// Đọc chỉ số điện từ ảnh đồng hồ (OCR).
/// Dùng Tesseract nếu có tessdata/vie+eng, nếu không fallback về nhập tay (vẫn lưu ảnh).
/// </summary>
public static class OcrService
{
    public record OcrResult(bool Ok, string Number, string RawText, string Engine);

    public static string TessDataDir => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");

    public static bool IsTesseractAvailable()
    {
        try
        {
            return Directory.Exists(TessDataDir) &&
                (File.Exists(Path.Combine(TessDataDir, "eng.traineddata")) ||
                 File.Exists(Path.Combine(TessDataDir, "vie.traineddata")));
        }
        catch { return false; }
    }

    public static OcrResult ReadMeterNumber(string imagePath)
    {
        if (!File.Exists(imagePath)) return new OcrResult(false, "", "", "missing-file");
        // Tiền xử lý: copy ảnh gốc, thử OCR
        try
        {
            if (IsTesseractAvailable())
            {
                string lang = File.Exists(Path.Combine(TessDataDir, "vie.traineddata")) ? "vie" : "eng";
                using var engine = new Tesseract.TesseractEngine(TessDataDir, lang, Tesseract.EngineMode.Default);
                // Chỉ nhận chữ số + dấu chấm (đồng hồ điện)
                engine.SetVariable("tessedit_char_whitelist", "0123456789.,");
                engine.SetVariable("classify_bln_numeric_mode", "1");
                using var img = Tesseract.Pix.LoadFromFile(imagePath);
                using var page = engine.Process(img);
                string raw = page.GetText() ?? "";
                string num = ExtractNumber(raw);
                return new OcrResult(!string.IsNullOrEmpty(num), num, raw.Trim(), "tesseract-" + lang);
            }
            return new OcrResult(false, "", "Chưa có tessdata (tải vie.traineddata vào thư mục tessdata để bật OCR tự động). Vui lòng nhập tay.", "no-tessdata");
        }
        catch (Exception ex)
        {
            return new OcrResult(false, "", "Lỗi OCR: " + ex.Message, "error");
        }
    }

    /// <summary>Lấy cụm số có ý nghĩa nhất (thường là dãy dài nhất) từ text OCR.</summary>
    public static string ExtractNumber(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        // Bỏ dấu phẩy/chấm phân cách, giữ số
        var matches = Regex.Matches(raw.Replace(" ", ""), @"\d[\d\.,]*\d|\d+");
        string best = "";
        foreach (Match m in matches)
        {
            string digits = Regex.Replace(m.Value, @"[^\d]", "");
            // Chỉ số đồng hồ thường 3-8 chữ số
            if (digits.Length >= 3 && digits.Length <= 8 && digits.Length > best.Length)
                best = digits.TrimStart('0') == "" ? "0" : digits.TrimStart('0');
        }
        // Nếu không có dãy dài, lấy dãy số dài nhất bất kỳ
        if (best == "" && matches.Count > 0)
        {
            foreach (Match m in matches)
            {
                string d = Regex.Replace(m.Value, @"[^\d]", "");
                if (d.Length > best.Length) best = d;
            }
        }
        return best;
    }

    public static string InstallGuide =>
        "Để bật OCR tự động:\n1. Tạo thư mục 'tessdata' cạnh file .exe\n2. Tải vie.traineddata (https://github.com/tesseract-ocr/tessdata) bỏ vào tessdata/\n3. Chụp ảnh đồng hồ rõ nét, chỉ crop vùng số rồi bấm 'OCR'.";
}
