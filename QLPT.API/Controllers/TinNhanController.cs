using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLPT.API.Models;
using QLPT.API.Services;

namespace QLPT.API.Controllers;

/// <summary>
/// Tin nhắn giữa chủ trọ (Admin) và khách thuê cho màn hình chat bên Flutter.
///
/// Giống cách ThongBaoController lưu "phản hồi": KHÔNG thêm bảng/cột mới vào CSDL, mỗi tin nhắn
/// là một bản ghi ThongBao loại "Tin nhắn":
///   - TieuDe   = "Chat#{maKhach}|A" (Admin gửi) hoặc "Chat#{maKhach}|K" (Khách gửi)
///                -> khóa xác định cuộc trò chuyện (maKhach) và chiều gửi.
///   - NoiDung  = nội dung tin nhắn.
///   - Người nhận (ThongBaoNguoiNhan): Admin gửi -> tài khoản của khách; Khách gửi -> mọi tài khoản Admin.
///     Cột DaDoc của người nhận dùng làm trạng thái "đã đọc".
/// Tin loại này bị loại khỏi danh sách thông báo ở ThongBaoController.
///
/// Có 2 route (api/TinNhan và api/Messages) để gọi kiểu nào cũng được.
/// </summary>
[Route("api/[controller]")]
[Route("api/Messages")]
[ApiController]
public class TinNhanController : ControllerBase
{
    private readonly Klcn052QuanLyPhongTroContext _ctx;
    public TinNhanController(Klcn052QuanLyPhongTroContext ctx) => _ctx = ctx;
    private const string LoaiTinNhan = "Tin nhắn";
    private static string TienTo(int maKhach) => $"Chat#{maKhach}|";
    private static string TieuDe(int maKhach, bool laAdmin) => TienTo(maKhach) + (laAdmin ? "A" : "K");

    // "Chat#12|A" -> (12, true)
    private static (int maKhach, bool laAdmin)? PhanTich(string? tieuDe)
    {
        if (tieuDe == null || !tieuDe.StartsWith("Chat#")) return null;
        int bar = tieuDe.IndexOf('|');
        if (bar < 0 || bar + 1 >= tieuDe.Length) return null;
        if (!int.TryParse(tieuDe.AsSpan(5, bar - 5), out int ma)) return null;
        return (ma, tieuDe[bar + 1] == 'A');
    }

    // Lấy toàn bộ tin của 1 cuộc trò chuyện (Dart: getMessages).
    [HttpGet("{maKhach:int}")]
    public async Task<IActionResult> GetMessages(int maKhach)
    {
        try
        {
            string tienTo = TienTo(maKhach);
            var list = await _ctx.ThongBaos
                .Where(t => t.LoaiThongBao == LoaiTinNhan && t.TieuDe!.StartsWith(tienTo))
                .OrderBy(t => t.NgayTao).ThenBy(t => t.MaThongBao)
                .Select(t => new
                {
                    id = t.MaThongBao,
                    content = t.NoiDung,
                    isAdmin = t.TieuDe!.EndsWith("|A"),
                    sendDate = t.NgayTao,
                    isRead = t.ThongBaoNguoiNhans.All(n => n.DaDoc)
                })
                .ToListAsync();

            return Ok(list.Select(t => new
            {
                t.id,
                customerIdentity = maKhach,
                t.content,
                t.isAdmin,
                t.sendDate,
                t.isRead
            }));
        }
        catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.InnerException?.Message ?? ex.Message }); }
    }

    // Danh sách cuộc trò chuyện cho Admin: khách, tin cuối, số tin khách gửi chưa đọc.
    [HttpGet("conversations")]
    public async Task<IActionResult> GetConversations()
    {
        try
        {
            var raw = await _ctx.ThongBaos
                .Where(t => t.LoaiThongBao == LoaiTinNhan && t.TieuDe!.StartsWith("Chat#"))
                .Select(t => new
                {
                    t.MaThongBao,
                    t.TieuDe,
                    t.NoiDung,
                    t.NgayTao,
                    chuaDoc = t.ThongBaoNguoiNhans.Any(n => !n.DaDoc)
                })
                .ToListAsync();

            var nhom = raw
                .Select(t => new { t, pt = PhanTich(t.TieuDe) })
                .Where(x => x.pt.HasValue)
                .GroupBy(x => x.pt!.Value.maKhach)
                .Select(g =>
                {
                    var cuoi = g.OrderByDescending(x => x.t.NgayTao).ThenByDescending(x => x.t.MaThongBao).First();
                    return new
                    {
                        customerIdentity = g.Key,
                        lastContent = cuoi.t.NoiDung,
                        lastDate = cuoi.t.NgayTao,
                        unread = g.Count(x => !x.pt!.Value.laAdmin && x.t.chuaDoc) // tin khách gửi, admin chưa đọc
                    };
                })
                .OrderByDescending(x => x.lastDate)
                .ToList();

            var ids = nhom.Select(x => x.customerIdentity).ToList();
            var ten = await _ctx.KhachThues.Where(k => ids.Contains(k.MaKhach))
                .ToDictionaryAsync(k => k.MaKhach, k => k.HoTen);

            return Ok(nhom.Select(x => new
            {
                x.customerIdentity,
                customerName = ten.GetValueOrDefault(x.customerIdentity, $"Khách #{x.customerIdentity}"),
                x.lastContent,
                x.lastDate,
                x.unread
            }));
        }
        catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.InnerException?.Message ?? ex.Message }); }
    }

    // Gửi tin nhắn (Dart: sendMessage).
    [HttpPost]
    public async Task<IActionResult> Send([FromBody] GuiTinNhanDto dto)
    {
        if (dto == null || string.IsNullOrWhiteSpace(dto.Content))
            return BadRequest(new { success = false, message = "Nội dung tin nhắn không được để trống." });
        if (dto.CustomerIdentity <= 0)
            return BadRequest(new { success = false, message = "Thiếu mã khách." });

        try
        {
            int? tkKhach = await NghiepVu.TaiKhoanCuaKhachAsync(_ctx, dto.CustomerIdentity);

            List<int> nguoiNhan;
            if (dto.IsAdmin)
            {
                if (tkKhach == null)
                    return BadRequest(new { success = false, message = "Khách này chưa có tài khoản để nhận tin nhắn." });
                nguoiNhan = new List<int> { tkKhach.Value };
            }
            else
            {
                if (tkKhach == null)
                    return BadRequest(new { success = false, message = "Không tìm thấy tài khoản của khách." });
                nguoiNhan = await _ctx.TaiKhoans
                    .Where(t => t.VaiTro == "Admin" && t.TrangThai == NghiepVu.TaiKhoanActive)
                    .Select(t => t.MaTaiKhoan).ToListAsync();
                if (nguoiNhan.Count == 0)
                    return BadRequest(new { success = false, message = "Không có tài khoản quản trị nào để nhận tin nhắn." });
            }

            var tb = NghiepVu.TaoThongBao(_ctx, TieuDe(dto.CustomerIdentity, dto.IsAdmin),
                                          dto.Content.Trim(), LoaiTinNhan, nguoiNhan);
            await _ctx.SaveChangesAsync();
            return Ok(new { success = true, message = "Gửi thành công", id = tb!.MaThongBao });
        }
        catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.InnerException?.Message ?? ex.Message }); }
    }

    // Đánh dấu đã đọc: isAdmin=true -> Admin đọc các tin khách gửi; isAdmin=false -> khách đọc các tin admin gửi.
    [HttpPut("doc/{maKhach:int}")]
    public async Task<IActionResult> MarkRead(int maKhach, [FromQuery] bool isAdmin)
    {
        try
        {
            string tieuDeCanDoc = TieuDe(maKhach, !isAdmin);
            var chuaDoc = await _ctx.ThongBaoNguoiNhans
                .Where(n => !n.DaDoc
                            && n.MaThongBaoNavigation.LoaiThongBao == LoaiTinNhan
                            && n.MaThongBaoNavigation.TieuDe == tieuDeCanDoc)
                .ToListAsync();

            foreach (var n in chuaDoc) { n.DaDoc = true; n.NgayDoc = DateTime.Now; }
            await _ctx.SaveChangesAsync();
            return Ok(new { success = true, message = "Đã đánh dấu đã đọc", count = chuaDoc.Count });
        }
        catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.InnerException?.Message ?? ex.Message }); }
    }

    // Xóa 1 tin nhắn.
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var item = await _ctx.ThongBaos.Include(t => t.ThongBaoNguoiNhans)
                .FirstOrDefaultAsync(t => t.MaThongBao == id && t.LoaiThongBao == LoaiTinNhan);
            if (item == null) return NotFound(new { success = false, message = "Không tìm thấy tin nhắn." });

            _ctx.ThongBaoNguoiNhans.RemoveRange(item.ThongBaoNguoiNhans);
            _ctx.ThongBaos.Remove(item);
            await _ctx.SaveChangesAsync();
            return Ok(new { success = true, message = "Xóa thành công" });
        }
        catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.InnerException?.Message ?? ex.Message }); }
    }
}

// Khớp JSON Flutter gửi: customerIdentity, content, isAdmin, sendDate (sendDate bỏ qua, server dùng giờ của mình).
public class GuiTinNhanDto
{
    public int CustomerIdentity { get; set; }
    public string Content { get; set; } = "";
    public bool IsAdmin { get; set; }
    public DateTime? SendDate { get; set; }
}