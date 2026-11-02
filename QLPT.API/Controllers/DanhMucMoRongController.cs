using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLPT.API.Models;
using QLPT.API.Services;

namespace QLPT.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class DanhMucMoRongController : ControllerBase
{
    private readonly Klcn052QuanLyPhongTroContext _ctx;
    public DanhMucMoRongController(Klcn052QuanLyPhongTroContext ctx) => _ctx = ctx;

    // Giá trị đúng như đang lưu trong CSDL. Trước đây code so với "Hiệu lực"
    // nên mọi thống kê hợp đồng đều ra 0.
    private const string HopDongHieuLuc = "Đang hiệu lực";
    private const string HoaDonDaThanhToan = "Đã thanh toán";

    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard()
    {
        try
        {
            var tongPhong   = await _ctx.Phongs.CountAsync();
            var phongTrong  = await _ctx.Phongs.CountAsync(x => x.TrangThai == "Trống");
            var dangThue    = await _ctx.Phongs.CountAsync(x => x.TrangThai == "Đang thuê");
            var tongKhach   = await _ctx.KhachThues.CountAsync();
            // Người đang thực sự ở: cư trú "Đang ở"/"Tạm vắng" của hợp đồng còn hiệu lực,
            // cộng người đại diện hợp đồng (phòng khi chưa khai báo cư trú). Không tính
            // khách cũ đã thanh lý hay khách mới chỉ đặt cọc như tongKhach.
            var trangThaiO  = new[] { NghiepVu.CuTru.DangO, NghiepVu.CuTru.TamVang };
            var khachCuTru  = await _ctx.CuTrus
                .Where(c => trangThaiO.Contains(c.TrangThaiCuTru!) && c.MaHopDongNavigation.TrangThai == HopDongHieuLuc)
                .Select(c => c.MaKhach).ToListAsync();
            var khachDaiDien = await _ctx.HopDongs
                .Where(h => h.TrangThai == HopDongHieuLuc)
                .Select(h => h.MaKhachDaiDien).ToListAsync();
            var khachDangO  = khachCuTru.Concat(khachDaiDien).Distinct().Count();
            var hdHieuLuc   = await _ctx.HopDongs.CountAsync(x => x.TrangThai == HopDongHieuLuc);
            var cutoff      = DateOnly.FromDateTime(DateTime.Today.AddDays(30));
            var sapHetHan   = await _ctx.HopDongs.CountAsync(x =>
                x.TrangThai == HopDongHieuLuc && x.NgayKetThuc <= cutoff);
            // Công nợ = phần CÒN LẠI của mọi hóa đơn chưa tất toán.
            // Bản cũ cộng TongTien và bỏ sót hóa đơn "Thanh toán một phần".
            var noChuaThu   = await _ctx.HoaDons
                .Where(x => x.TrangThai != HoaDonDaThanhToan)
                .SumAsync(x => (decimal?)(x.ConLai ?? 0)) ?? 0;
            var thangNay    = DateTime.Now.Month;
            var namNay      = DateTime.Now.Year;
            var doanhThu    = await _ctx.ThanhToans
                .Where(x => x.NgayThanhToan.HasValue
                    && x.NgayThanhToan.Value.Month == thangNay
                    && x.NgayThanhToan.Value.Year == namNay)
                .SumAsync(x => (decimal?)(x.SoTien ?? 0)) ?? 0;

            return Ok(new {
                tongPhong, phongTrong, dangThue, tongKhach, khachDangO,
                hdHieuLuc, sapHetHan, noChuaThu, doanhThuThang = doanhThu
            });
        }
        catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }

    [HttpGet("cu-tru")]
    public async Task<IActionResult> GetCuTru([FromQuery] int? maHopDong)
    {
        try
        {
            var q = _ctx.CuTrus
                .Include(x => x.MaKhachNavigation)
                .AsQueryable();
            if (maHopDong.HasValue)
                q = q.Where(x => x.MaHopDong == maHopDong.Value);
            var list = await q.Select(x => new {
                x.MaCuTru, x.MaHopDong, x.MaKhach, x.NgayVao, x.NgayRoi,
                x.TrangThaiCuTru, x.TrangThaiKhaiBao, x.NgayKhaiBao, x.GhiChu,
                HoTen = x.MaKhachNavigation != null ? x.MaKhachNavigation.HoTen : ""
            }).ToListAsync();
            return Ok(list);
        }
        catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }

    // Thêm người ở cùng vào hợp đồng. Chặn vượt số người tối đa của loại phòng và
    // chặn một người đang ở hai nơi cùng lúc.
    [HttpPost("cu-tru")]
    public async Task<IActionResult> AddCuTru([FromBody] CuTru model)
    {
        if (model.MaHopDong <= 0 || model.MaKhach <= 0)
            return BadRequest(new { success = false, message = "Thiếu mã hợp đồng hoặc mã khách." });

        var hd = await _ctx.HopDongs
            .Include(h => h.MaPhongNavigation).ThenInclude(p => p.MaLoaiPhongNavigation)
            .FirstOrDefaultAsync(h => h.MaHopDong == model.MaHopDong);
        if (hd == null) return NotFound(new { success = false, message = $"Không tìm thấy hợp đồng #{model.MaHopDong}." });
        if (hd.TrangThai != HopDongHieuLuc)
            return BadRequest(new { success = false, message = "Chỉ thêm người ở cùng vào hợp đồng đang hiệu lực." });
        if (await _ctx.KhachThues.FindAsync(model.MaKhach) == null)
            return NotFound(new { success = false, message = $"Không tìm thấy khách thuê #{model.MaKhach}." });

        var dangO = new[] { NghiepVu.CuTru.DangO, NghiepVu.CuTru.TamVang };
        int? hdDangO = await _ctx.CuTrus
            .Where(c => c.MaKhach == model.MaKhach && dangO.Contains(c.TrangThaiCuTru!)
                        && c.MaHopDongNavigation.TrangThai == HopDongHieuLuc)
            .Select(c => (int?)c.MaHopDong).FirstOrDefaultAsync();
        if (hdDangO.HasValue)
            return BadRequest(new
            {
                success = false,
                message = hdDangO == model.MaHopDong
                    ? $"Khách #{model.MaKhach} đã có trong hợp đồng #{model.MaHopDong} rồi."
                    : $"Khách #{model.MaKhach} đang cư trú ở hợp đồng #{hdDangO}."
            });

        int? toiDa = hd.MaPhongNavigation?.MaLoaiPhongNavigation?.SoNguoiToiDa;
        if (toiDa.HasValue)
        {
            int hienTai = await _ctx.CuTrus.CountAsync(c => c.MaHopDong == hd.MaHopDong && dangO.Contains(c.TrangThaiCuTru!));
            if (hienTai >= toiDa.Value)
                return BadRequest(new { success = false, message = $"Phòng đã đủ {toiDa} người theo loại phòng." });
        }

        try
        {
            model.MaCuTru = 0;
            model.NgayVao          ??= DateOnly.FromDateTime(DateTime.Now);
            model.TrangThaiCuTru   ??= NghiepVu.CuTru.DangO;
            model.TrangThaiKhaiBao ??= NghiepVu.CuTru.ChuaKhaiBao;
            _ctx.CuTrus.Add(model);
            await _ctx.SaveChangesAsync();
            return Ok(new { success = true, message = "Đã thêm người ở cùng.", data = new { model.MaCuTru } });
        }
        catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.InnerException?.Message ?? ex.Message }); }
    }

    [HttpPut("cu-tru/{id}")]
    public async Task<IActionResult> UpdateCuTru(int id, [FromBody] CuTru model)
    {
        try
        {
            var item = await _ctx.CuTrus.FindAsync(id);
            if (item == null) return NotFound(new { message = "Không tìm thấy cư trú" });
            item.TrangThaiCuTru    = model.TrangThaiCuTru    ?? item.TrangThaiCuTru;
            item.TrangThaiKhaiBao  = model.TrangThaiKhaiBao  ?? item.TrangThaiKhaiBao;
            item.NgayKhaiBao       = model.NgayKhaiBao        ?? item.NgayKhaiBao;
            item.NgayRoi           = model.NgayRoi            ?? item.NgayRoi;
            item.GhiChu            = model.GhiChu             ?? item.GhiChu;
            await _ctx.SaveChangesAsync();
            return Ok(new { success = true, message = "Cập nhật cư trú thành công" });
        }
        catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }
}
