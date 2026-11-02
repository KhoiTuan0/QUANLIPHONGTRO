using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLPT.API.Models;
using System.Security.Claims;

namespace QLPT.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class GiaHanHopDongController : ControllerBase
{
    private readonly Klcn052QuanLyPhongTroContext _ctx;
    private const string Pending = "[CHO_DUYET]";
    private const string Approved = "[DA_DUYET]";
    private const string Rejected = "[TU_CHOI]";

    public GiaHanHopDongController(Klcn052QuanLyPhongTroContext ctx) => _ctx = ctx;

    public sealed class GuiYeuCauDto
    {
        public int MaHopDong { get; set; }
        public DateOnly NgayKetThucMoi { get; set; }
        public string? LyDo { get; set; }
    }

    public sealed class XuLyDto
    {
        public string? GhiChu { get; set; }
    }

    private bool IsAdmin() => User.IsInRole("Admin") || User.IsInRole("Manager");

    private int? CurrentCustomerId()
    {
        var raw = User.FindFirst("MaKhach")?.Value;
        return int.TryParse(raw, out var id) ? id : null;
    }

    private static string Status(string? note)
    {
        if (note?.StartsWith(Pending, StringComparison.Ordinal) == true) return "Chờ duyệt";
        if (note?.StartsWith(Approved, StringComparison.Ordinal) == true) return "Đã duyệt";
        if (note?.StartsWith(Rejected, StringComparison.Ordinal) == true) return "Từ chối";
        return "Lịch sử gia hạn";
    }

    private static object ViewModel(GiaHanHopDong x) => new
    {
        x.MaGiaHan,
        x.MaHopDong,
        x.NgayGiaHan,
        x.NgayKetThucCu,
        x.NgayKetThucMoi,
        x.GiaThueMoi,
        x.GhiChu,
        TrangThai = Status(x.GhiChu),
        MaKhach = x.MaHopDongNavigation.MaKhachDaiDien,
        TenKhach = x.MaHopDongNavigation.MaKhachDaiDienNavigation?.HoTen,
        MaPhong = x.MaHopDongNavigation.MaPhong,
        TenPhong = x.MaHopDongNavigation.MaPhongNavigation?.TenPhong
    };

    // GET api/GiaHanHopDong - chủ trọ xem tất cả, khách chỉ xem của mình.
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        if (!IsAdmin() && CurrentCustomerId() is null) return Forbid();
        var query = _ctx.GiaHanHopDongs.AsNoTracking()
            .Include(x => x.MaHopDongNavigation).ThenInclude(h => h.MaPhongNavigation)
            .Include(x => x.MaHopDongNavigation).ThenInclude(h => h.MaKhachDaiDienNavigation)
            .AsQueryable();
        if (User.IsInRole("Manager"))
        {
            var account = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            query = query.Where(x => _ctx.QuanLyCoSos.Any(q => q.MaTaiKhoan == account &&
                q.MaCoSo == x.MaHopDongNavigation.MaPhongNavigation.MaCoSo));
        }
        else if (!IsAdmin())
        {
            var customerId = CurrentCustomerId()!.Value;
            query = query.Where(x => x.MaHopDongNavigation.MaKhachDaiDien == customerId);
        }
        var items = await query.OrderByDescending(x => x.MaGiaHan).ToListAsync();
        return Ok(items.Select(ViewModel));
    }

    // POST api/GiaHanHopDong/gui - khách gửi yêu cầu.
    [HttpPost("gui")]
    public async Task<IActionResult> Gui([FromBody] GuiYeuCauDto dto)
    {
        var customerId = CurrentCustomerId();
        if (customerId is null) return Forbid();
        if (dto.MaHopDong <= 0) return BadRequest(new { success = false, message = "Mã hợp đồng không hợp lệ." });
        var hopDong = await _ctx.HopDongs.FirstOrDefaultAsync(h => h.MaHopDong == dto.MaHopDong);
        if (hopDong is null || hopDong.MaKhachDaiDien != customerId.Value)
            return NotFound(new { success = false, message = "Không tìm thấy hợp đồng của bạn." });
        if (!string.Equals(hopDong.TrangThai?.Trim(), "Đang hiệu lực", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { success = false, message = "Chỉ hợp đồng đang hiệu lực mới được yêu cầu gia hạn." });
        if (dto.NgayKetThucMoi <= hopDong.NgayKetThuc)
            return BadRequest(new { success = false, message = "Ngày kết thúc mới phải sau ngày kết thúc hiện tại." });
        if (dto.LyDo?.Length > 280)
            return BadRequest(new { success = false, message = "Lý do không được quá 280 ký tự." });
        await using var transaction = await _ctx.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var hasPending = await _ctx.GiaHanHopDongs.AnyAsync(x => x.MaHopDong == dto.MaHopDong &&
            x.GhiChu != null && x.GhiChu.StartsWith(Pending));
        if (hasPending)
            return Conflict(new { success = false, message = "Hợp đồng đã có yêu cầu gia hạn đang chờ duyệt." });

        var item = new GiaHanHopDong
        {
            MaHopDong = hopDong.MaHopDong,
            NgayGiaHan = DateOnly.FromDateTime(DateTime.Now),
            NgayKetThucCu = hopDong.NgayKetThuc,
            NgayKetThucMoi = dto.NgayKetThucMoi,
            GiaThueMoi = hopDong.GiaThueThang,
            GhiChu = $"{Pending} {dto.LyDo?.Trim() ?? string.Empty}".Trim()
        };
        _ctx.GiaHanHopDongs.Add(item);
        await _ctx.SaveChangesAsync();
        await transaction.CommitAsync();
        return Ok(new { success = true, message = "Đã gửi yêu cầu gia hạn, chờ chủ trọ duyệt.", maGiaHan = item.MaGiaHan });
    }

    // PUT api/GiaHanHopDong/{id}/duyet
    [HttpPut("{id:int}/duyet")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Duyet(int id, [FromBody] XuLyDto? dto)
    {
        await using var transaction = await _ctx.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var item = await _ctx.GiaHanHopDongs.Include(x => x.MaHopDongNavigation)
            .FirstOrDefaultAsync(x => x.MaGiaHan == id);
        if (item is null) return NotFound(new { success = false, message = "Không tìm thấy yêu cầu." });
        if (Status(item.GhiChu) != "Chờ duyệt")
            return Conflict(new { success = false, message = "Yêu cầu không còn ở trạng thái chờ duyệt." });
        var contract = item.MaHopDongNavigation;
        if (!string.Equals(contract.TrangThai?.Trim(), "Đang hiệu lực", StringComparison.OrdinalIgnoreCase))
            return Conflict(new { success = false, message = "Hợp đồng không còn hiệu lực." });
        if (item.NgayKetThucMoi is null || item.NgayKetThucCu != contract.NgayKetThuc || item.NgayKetThucMoi <= contract.NgayKetThuc)
            return Conflict(new { success = false, message = "Thời hạn hợp đồng đã thay đổi; cần gửi yêu cầu mới." });
        if (dto?.GhiChu?.Length > 280)
            return BadRequest(new { success = false, message = "Ghi chú không được quá 280 ký tự." });
        contract.NgayKetThuc = item.NgayKetThucMoi.Value;
        item.GiaThueMoi = contract.GiaThueThang;
        item.GhiChu = $"{Approved} {dto?.GhiChu?.Trim() ?? string.Empty}".Trim();
        await _ctx.SaveChangesAsync();
        await transaction.CommitAsync();
        return Ok(new { success = true, message = "Đã duyệt gia hạn và cập nhật ngày kết thúc hợp đồng." });
    }

    // PUT api/GiaHanHopDong/{id}/tu-choi
    [HttpPut("{id:int}/tu-choi")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> TuChoi(int id, [FromBody] XuLyDto? dto)
    {
        await using var transaction = await _ctx.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var item = await _ctx.GiaHanHopDongs.FirstOrDefaultAsync(x => x.MaGiaHan == id);
        if (item is null) return NotFound(new { success = false, message = "Không tìm thấy yêu cầu." });
        if (Status(item.GhiChu) != "Chờ duyệt")
            return Conflict(new { success = false, message = "Yêu cầu không còn ở trạng thái chờ duyệt." });
        if (dto?.GhiChu?.Length > 280)
            return BadRequest(new { success = false, message = "Ghi chú không được quá 280 ký tự." });
        item.GhiChu = $"{Rejected} {dto?.GhiChu?.Trim() ?? string.Empty}".Trim();
        await _ctx.SaveChangesAsync();
        await transaction.CommitAsync();
        return Ok(new { success = true, message = "Đã từ chối yêu cầu. Hợp đồng không thay đổi." });
    }
}
