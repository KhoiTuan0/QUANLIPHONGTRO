using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using QLPT.API.Services;

namespace QLPT.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class OcrChiSoDienController(
    GeminiMeterReader geminiReader, OpenAiMeterReader openAiReader, IConfiguration configuration) : ControllerBase
{
    [HttpPost]
    [Consumes("multipart/form-data")]
    [EnableRateLimiting("meter-ocr")]
    [RequestSizeLimit(4 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 4 * 1024 * 1024)]
    public async Task<IActionResult> Read(IFormFile? crop, IFormFile? overview, [FromForm] int digitCount)
    {
        if (digitCount is < 3 or > 7 || crop is null || overview is null ||
            crop.Length is < 100 or > 2_000_000 || overview.Length is < 100 or > 1_500_000)
            return BadRequest(new { message = "Ảnh hoặc số lượng chữ số không hợp lệ. Hãy khoanh lại vùng số." });
        byte[] cropped = await ReadBytes(crop, HttpContext.RequestAborted);
        byte[] full = await ReadBytes(overview, HttpContext.RequestAborted);
        if (!IsPng(cropped) || !IsJpeg(full))
            return BadRequest(new { message = "Ảnh OCR phải gồm vùng số PNG và ảnh tổng thể JPG." });
        bool useGemini = !string.IsNullOrWhiteSpace(configuration["GEMINI_API_KEY"]);
        var result = useGemini
            ? await geminiReader.ReadAsync(cropped, full, digitCount, HttpContext.RequestAborted)
            : await openAiReader.ReadAsync(cropped, full, digitCount, HttpContext.RequestAborted);
        string provider = useGemini ? "Gemini" : "OpenAI";
        return result.Status switch
        {
            MeterAiReadStatus.Success => Ok(new { suggestion = result.Digits, source = provider, message = "Đây chỉ là gợi ý. Hãy đối chiếu ảnh trước khi lưu." }),
            MeterAiReadStatus.Unreadable => Ok(new { suggestion = (string?)null, message = $"{provider} không đọc chắc chắn đủ các chữ số. Hãy khoanh lại hoặc nhập tay." }),
            MeterAiReadStatus.NotConfigured => StatusCode(503, new { message = "Máy API chưa cấu hình GEMINI_API_KEY hoặc OPENAI_API_KEY." }),
            MeterAiReadStatus.QuotaUnavailable when useGemini => StatusCode(429, new { message = "Gemini đã chạm hạn mức của dự án. Hãy xem Rate Limit trong AI Studio hoặc nhập chỉ số bằng tay." }),
            MeterAiReadStatus.QuotaUnavailable => StatusCode(402, new { message = "Tài khoản OpenAI API đã hết credits hoặc chạm giới hạn chi tiêu. Hãy kiểm tra Billing; vẫn có thể nhập chỉ số bằng tay." }),
            MeterAiReadStatus.AccessDenied => StatusCode(424, new { message = $"{provider} từ chối khóa hoặc quyền truy cập mô hình. Hãy kiểm tra API key; vẫn có thể nhập chỉ số bằng tay." }),
            _ => StatusCode(502, new { message = "Dịch vụ AI chưa phản hồi. Hãy thử lại hoặc nhập tay." })
        };
    }

    private static async Task<byte[]> ReadBytes(IFormFile file, CancellationToken token)
    {
        using var stream = new MemoryStream((int)file.Length);
        await file.CopyToAsync(stream, token);
        return stream.ToArray();
    }

    private static bool IsPng(byte[] bytes) => bytes.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
    private static bool IsJpeg(byte[] bytes) => bytes.Length > 3 && bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[^2] == 0xff && bytes[^1] == 0xd9;
}
