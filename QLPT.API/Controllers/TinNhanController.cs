using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLPT.API.Models;
using QLPT.API.Services;

namespace QLPT.API.Controllers;

[ApiController]
[Route("api/TinNhan")]
public sealed class TinNhanController(Klcn052QuanLyPhongTroContext db) : ControllerBase
{
    private int Account => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    private bool CanChat(int customer) => User.IsInRole("Admin") ||
        User.IsInRole("Customer") && User.FindFirstValue("MaKhach") == customer.ToString();
    private static string Title(int customer, bool admin) => $"Chat#{customer}|{(admin ? "A" : "K")}";

    [HttpGet("{maKhach:int}")]
    public async Task<IActionResult> GetMessages(int maKhach)
    {
        if (!CanChat(maKhach)) return Forbid();
        var prefix = $"Chat#{maKhach}|";
        return Ok(await db.ThongBaos.AsNoTracking()
            .Where(t => t.LoaiThongBao == "Tin nhắn" && t.TieuDe != null && t.TieuDe.StartsWith(prefix))
            .OrderBy(t => t.NgayTao).ThenBy(t => t.MaThongBao)
            .Select(t => new { id = t.MaThongBao, customerIdentity = maKhach,
                content = t.NoiDung, isAdmin = t.TieuDe!.EndsWith("|A"), sendDate = t.NgayTao,
                isRead = t.ThongBaoNguoiNhans.Any() && t.ThongBaoNguoiNhans.All(n => n.DaDoc) }).ToListAsync());
    }

    [HttpPost]
    public async Task<IActionResult> Send(GuiTinNhanDto dto)
    {
        if (!CanChat(dto.CustomerIdentity)) return Forbid();
        if (string.IsNullOrWhiteSpace(dto.Content) || dto.Content.Trim().Length > 1000)
            return BadRequest(new { message = "Nội dung tin nhắn từ 1 đến 1000 ký tự." });
        bool admin = User.IsInRole("Admin");
        var customerAccount = await NghiepVu.TaiKhoanCuaKhachAsync(db, dto.CustomerIdentity);
        if (customerAccount == null) return BadRequest(new { message = "Khách chưa có tài khoản." });
        var recipients = admin ? new List<int> { customerAccount.Value } :
            await db.TaiKhoans.Where(a => a.VaiTro == "Admin" && a.TrangThai == "Active")
                .Select(a => a.MaTaiKhoan).ToListAsync();
        if (recipients.Count == 0) return BadRequest(new { message = "Chưa có người nhận." });
        var item = NghiepVu.TaoThongBao(db, Title(dto.CustomerIdentity, admin),
            dto.Content.Trim(), "Tin nhắn", recipients);
        await db.SaveChangesAsync();
        return Ok(new { success = true, id = item!.MaThongBao });
    }

    [HttpPut("doc/{maKhach:int}")]
    public async Task<IActionResult> MarkRead(int maKhach)
    {
        if (!CanChat(maKhach)) return Forbid();
        string title = Title(maKhach, !User.IsInRole("Admin"));
        var rows = await db.ThongBaoNguoiNhans.Where(n => n.MaTaiKhoan == Account &&
            !n.DaDoc && n.MaThongBaoNavigation.LoaiThongBao == "Tin nhắn" &&
            n.MaThongBaoNavigation.TieuDe == title).ToListAsync();
        foreach (var row in rows) { row.DaDoc = true; row.NgayDoc = DateTime.Now; }
        await db.SaveChangesAsync();
        return Ok(new { success = true, count = rows.Count });
    }
}

public sealed record GuiTinNhanDto(int CustomerIdentity, string Content, bool IsAdmin);
