using System.Buffers.Binary;
using Microsoft.AspNetCore.Mvc;

namespace QLPT.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AnhController : ControllerBase
{
    public const long MaxUploadBytes = 10 * 1024 * 1024;
    private static readonly string[] Extensions = [".jpg", ".png", ".bmp", ".gif", ".webp"];
    private static readonly string[] Purposes = ["dong-ho", "phong", "sua-chua", "tai-san", "khach-thue"];
    private readonly string _directory;
    private readonly ILogger<AnhController>? _logger;

    public AnhController(IWebHostEnvironment environment, ILogger<AnhController>? logger = null)
    {
        _directory = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "App_Data", "Anh"));
        _logger = logger;
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxUploadBytes + 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadBytes)]
    public async Task<IActionResult> Upload(IFormFile? image, [FromForm] string loai = "dong-ho")
    {
        if (image == null || image.Length == 0) return BadRequest(new { success = false, message = "Chọn tệp ảnh để tải lên." });
        if (image.Length > MaxUploadBytes) return BadRequest(new { success = false, message = "Ảnh tối đa 10 MB." });
        if (!Purposes.Contains(loai)) return BadRequest(new { success = false, message = "Loại ảnh không hợp lệ." });
        string extension;
        try { extension = Path.GetExtension(image.FileName).ToLowerInvariant(); }
        catch (ArgumentException) { return BadRequest(new { success = false, message = "Tên tệp ảnh không hợp lệ." }); }
        if (extension == ".jpeg") extension = ".jpg";
        if (!Extensions.Contains(extension))
            return BadRequest(new { success = false, message = "Chỉ nhận ảnh JPG, PNG, BMP, GIF hoặc WebP." });

        string? createdPath = null;
        bool outputCreated = false;
        try
        {
            var cancellation = HttpContext?.RequestAborted ?? CancellationToken.None;
            using var bytes = new MemoryStream((int)image.Length);
            await using (var input = image.OpenReadStream())
            {
                var buffer = new byte[64 * 1024];
                int read;
                while ((read = await input.ReadAsync(buffer, cancellation)) > 0)
                {
                    if (bytes.Length + read > MaxUploadBytes)
                        return BadRequest(new { success = false, message = "Ảnh tối đa 10 MB." });
                    await bytes.WriteAsync(buffer.AsMemory(0, read), cancellation);
                }
            }
            var data = bytes.ToArray();
            var detected = DetectImage(data);
            string? suppliedMime = image.ContentType?.Split(';')[0].Trim().ToLowerInvariant();
            if (detected == null || detected.Value.Extension != extension
                || (!string.IsNullOrEmpty(suppliedMime) && suppliedMime != "application/octet-stream"
                    && suppliedMime != detected.Value.Mime && !(extension == ".bmp" && suppliedMime == "image/x-ms-bmp")))
                return BadRequest(new { success = false, message = "Nội dung, định dạng và loại MIME của tệp ảnh không khớp hoặc ảnh bị hỏng." });

            Directory.CreateDirectory(_directory);
            var id = Guid.NewGuid();
            createdPath = Path.Combine(_directory, id.ToString("N") + extension);
            await using (var output = new FileStream(createdPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, useAsync: true))
            {
                outputCreated = true;
                await output.WriteAsync(data, cancellation);
            }
            return Ok(new { success = true, maAnh = id, duongDanAnh = $"/api/Anh/{id:N}", loai, kichThuoc = data.Length });
        }
        catch (Exception ex)
        {
            if (outputCreated && createdPath != null && System.IO.File.Exists(createdPath))
            {
                try { System.IO.File.Delete(createdPath); }
                catch (Exception cleanupError) when (cleanupError is IOException or UnauthorizedAccessException)
                { _logger?.LogWarning(cleanupError, "Không dọn được tệp ảnh tải lên bị gián đoạn"); }
            }
            _logger?.LogError(ex, "Không lưu được ảnh tải lên");
            return StatusCode(500, new { success = false, message = "Không lưu được ảnh. Vui lòng thử lại." });
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        if (id == Guid.Empty) return BadRequest(new { success = false, message = "Mã ảnh không hợp lệ." });
        try
        {
            string? path = Extensions.Select(extension => Path.Combine(_directory, id.ToString("N") + extension)).FirstOrDefault(System.IO.File.Exists);
            if (path == null) return NotFound(new { success = false, message = "Không tìm thấy ảnh." });
            if (new FileInfo(path).Length > MaxUploadBytes) return NotFound(new { success = false, message = "Tệp ảnh không hợp lệ." });
            var data = await System.IO.File.ReadAllBytesAsync(path, HttpContext?.RequestAborted ?? CancellationToken.None);
            var detected = DetectImage(data);
            if (detected == null || detected.Value.Extension != Path.GetExtension(path))
                return NotFound(new { success = false, message = "Tệp ảnh không hợp lệ." });
            if (HttpContext != null)
            {
                Response.Headers.XContentTypeOptions = "nosniff";
                Response.Headers.CacheControl = "private, no-store";
            }
            return File(data, detected.Value.Mime);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Không đọc được ảnh {MaAnh}", id);
            return StatusCode(500, new { success = false, message = "Không tải được ảnh. Vui lòng thử lại." });
        }
    }

    public static (string Extension, string Mime)? DetectImage(ReadOnlySpan<byte> data)
    {
        if (data.Length >= 4 && data[0] == 0xff && data[1] == 0xd8 && data[2] == 0xff && data[^2] == 0xff && data[^1] == 0xd9)
            return (".jpg", "image/jpeg");
        if (data.Length >= 45 && data[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })
            && data.Slice(12, 4).SequenceEqual("IHDR"u8) && data.Slice(data.Length - 8, 4).SequenceEqual("IEND"u8))
            return (".png", "image/png");
        if (data.Length >= 54 && data[..2].SequenceEqual("BM"u8) && BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(2, 4)) == data.Length)
            return (".bmp", "image/bmp");
        if (data.Length >= 14 && (data[..6].SequenceEqual("GIF87a"u8) || data[..6].SequenceEqual("GIF89a"u8)) && data[^1] == 0x3b)
            return (".gif", "image/gif");
        if (data.Length >= 20 && data[..4].SequenceEqual("RIFF"u8) && data.Slice(8, 4).SequenceEqual("WEBP"u8)
            && BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(4, 4)) == data.Length - 8)
            return (".webp", "image/webp");
        return null;
    }
}
