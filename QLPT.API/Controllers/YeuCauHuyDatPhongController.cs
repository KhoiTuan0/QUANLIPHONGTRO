using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLPT.API.Models;
using QLPT.API.Services;

namespace QLPT.API.Controllers;

[ApiController]
[Route("api/DatPhong/yeu-cau-huy")]
public class YeuCauHuyDatPhongController(Klcn052QuanLyPhongTroContext db) : ControllerBase
{
    private int Account => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int id) ? id : 0;
    private IQueryable<DatPhong> ScopedBookings()
    {
        var query = db.DatPhongs.AsQueryable();
        if (User.IsInRole("Admin")) return query;
        if (User.IsInRole("Manager")) return query.Where(b => b.MaPhongNavigation.MaCoSo.HasValue &&
            db.QuanLyCoSos.Any(q => q.MaTaiKhoan == Account && q.MaCoSo == b.MaPhongNavigation.MaCoSo.Value));
        if (User.IsInRole("Customer") && int.TryParse(User.FindFirstValue("MaKhach"), out int customer))
            return query.Where(b => b.MaKhach == customer);
        return query.Where(b => false);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var allowed = ScopedBookings().Select(b => b.MaDatPhong);
        return Ok(await db.YeuCauHuyDatPhongs.AsNoTracking().Where(r => allowed.Contains(r.MaDatPhong))
            .OrderByDescending(r => r.MaYeuCau).Select(r => new {
                r.MaYeuCau, r.MaDatPhong, r.NgayYeuCau, r.LyDo, r.TrangThai, r.NgayXuLy,
                r.SoNgayBaoTruoc, r.TyLeHoan, r.SoTienHoan, r.GhiChuXuLy,
                r.MaTaiKhoanYeuCau, r.MaTaiKhoanXuLy,
                TenPhong = r.MaDatPhongNavigation.MaPhongNavigation.TenPhong,
                TenKhach = r.MaDatPhongNavigation.MaKhachNavigation.HoTen,
                TienCoc = r.MaDatPhongNavigation.TienCoc,
                r.MaDatPhongNavigation.NgayDuKienNhan
            }).ToListAsync());
    }

    [HttpPost]
    public async Task<IActionResult> Create(TaoYeuCauHuyDto dto)
    {
        if (dto.MaDatPhong <= 0 || string.IsNullOrWhiteSpace(dto.LyDo) || dto.LyDo.Trim().Length > 500)
            return BadRequest(new { message = "Chọn phiếu đặt và nhập lý do hủy (1–500 ký tự)." });
        if (!await ScopedBookings().AnyAsync(b => b.MaDatPhong == dto.MaDatPhong)) return Forbid();
        await using var tx = db.Database.CurrentTransaction == null ? await db.Database.BeginTransactionAsync() : null;
        var booking = await HuyDatPhongService.LockAsync(db, dto.MaDatPhong);
        if (booking == null || booking.TrangThai != NghiepVu.DatPhong.DaDatCoc)
            return BadRequest(new { message = "Chỉ gửi yêu cầu cho phiếu đang giữ chỗ." });
        if (await db.YeuCauHuyDatPhongs.AnyAsync(r => r.MaDatPhong == dto.MaDatPhong && r.TrangThai == "Chờ duyệt"))
            return Conflict(new { message = "Phiếu đặt đã có yêu cầu hủy chờ duyệt." });
        var now = DateTime.Now;
        var request = new YeuCauHuyDatPhong {
            MaDatPhong = dto.MaDatPhong, LyDo = dto.LyDo.Trim(), NgayYeuCau = now,
            MaTaiKhoanYeuCau = Account > 0 ? Account : null,
            SoNgayBaoTruoc = booking.NgayDuKienNhan?.DayNumber - DateOnly.FromDateTime(now).DayNumber
        };
        db.YeuCauHuyDatPhongs.Add(request);
        await db.SaveChangesAsync();
        if (tx != null) await tx.CommitAsync();
        return Ok(new { success = true, request.MaYeuCau, message = "Đã gửi yêu cầu. Phòng vẫn được giữ đến khi yêu cầu được duyệt." });
    }

    [HttpPut("{id:int}/xu-ly")]
    public async Task<IActionResult> XuLy(int id, XuLyYeuCauHuyDto dto)
    {
        if (!User.IsInRole("Admin") && !User.IsInRole("Manager")) return Forbid();
        if (string.IsNullOrWhiteSpace(dto.GhiChuXuLy) || dto.GhiChuXuLy.Trim().Length > 500)
            return BadRequest(new { message = "Nhập lý do quyết định / thỏa thuận hoàn cọc (1–500 ký tự)." });
        var bookingId = await db.YeuCauHuyDatPhongs.Where(r => r.MaYeuCau == id).Select(r => (int?)r.MaDatPhong).FirstOrDefaultAsync();
        if (bookingId == null) return NotFound(new { message = "Không tìm thấy yêu cầu." });
        if (!await ScopedBookings().AnyAsync(b => b.MaDatPhong == bookingId)) return Forbid();
        await using var tx = db.Database.CurrentTransaction == null ? await db.Database.BeginTransactionAsync() : null;
        var booking = await HuyDatPhongService.LockAsync(db, bookingId.Value);
        var request = await db.YeuCauHuyDatPhongs.SingleAsync(r => r.MaYeuCau == id);
        if (request.TrangThai != "Chờ duyệt") return Conflict(new { message = "Yêu cầu đã được xử lý; tải lại danh sách." });
        decimal refund = dto.SoTienHoan ?? 0;
        if (dto.Duyet && dto.SoTienHoan == null)
            return BadRequest(new { message = "Cần nhập rõ số tiền hoàn (0 nếu không hoàn cọc)." });
        if (!dto.Duyet && refund != 0) return BadRequest(new { message = "Từ chối yêu cầu không phát sinh hoàn cọc." });
        if (dto.Duyet)
        {
            var error = await HuyDatPhongService.CancelAsync(db, booking!, refund, id);
            if (error != null) return BadRequest(new { message = error });
        }
        request.TrangThai = dto.Duyet ? "Đã duyệt" : "Từ chối";
        request.SoTienHoan = dto.Duyet ? refund : 0;
        request.TyLeHoan = booking!.TienCoc is > 0 ? decimal.Round((request.SoTienHoan ?? 0) * 100 / booking.TienCoc.Value, 2) : 0;
        request.NgayXuLy = DateTime.Now;
        request.MaTaiKhoanXuLy = Account > 0 ? Account : null;
        request.GhiChuXuLy = dto.GhiChuXuLy.Trim();
        if (await NghiepVu.TaiKhoanCuaKhachAsync(db, booking.MaKhach) is int account)
            NghiepVu.TaoThongBao(db, "Kết quả yêu cầu hủy đặt phòng",
                $"Yêu cầu #{id}: {request.TrangThai}. Hoàn cọc: {NghiepVu.Tien(request.SoTienHoan)}. {request.GhiChuXuLy}",
                NghiepVu.ThongBao.LoaiCaNhan, new[] { account });
        await db.SaveChangesAsync();
        if (tx != null) await tx.CommitAsync();
        return Ok(new { success = true, request.TrangThai, request.SoTienHoan, message = "Đã xử lý yêu cầu." });
    }
}

public record TaoYeuCauHuyDto(int MaDatPhong, string? LyDo);
public record XuLyYeuCauHuyDto(bool Duyet, decimal? SoTienHoan, string? GhiChuXuLy);
