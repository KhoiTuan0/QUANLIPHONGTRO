using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLPT.API.Models;
using QLPT.API.Services;

namespace QLPT.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class DangKyDichVuController : ControllerBase
{
    private readonly Klcn052QuanLyPhongTroContext _ctx;
    public DangKyDichVuController(Klcn052QuanLyPhongTroContext ctx) => _ctx = ctx;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int? maHopDong)
    {
        try
        {
            var q = _ctx.DangKyDichVus
                .Include(x => x.MaDichVuNavigation)
                .AsQueryable();
            if (maHopDong.HasValue)
                q = q.Where(x => x.MaHopDong == maHopDong.Value);
            var list = await q.Select(x => new {
                x.MaDangKy, x.MaHopDong, x.MaDichVu, x.NgayBatDau, x.NgayKetThuc,
                x.DonGiaApDung, x.TrangThai,
                TenDichVu = x.MaDichVuNavigation != null ? x.MaDichVuNavigation.TenDichVu : ""
            }).ToListAsync();
            return Ok(list);
        }
        catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }

    // WPF chỉ gửi { maHopDong, maDichVu, ngayBatDau }. Thiếu TrangThai thì bản ghi lưu null
    // và hóa đơn (chỉ lấy "Đang sử dụng") sẽ không bao giờ tính dịch vụ này; thiếu
    // DonGiaApDung thì mất giá chốt tại thời điểm đăng ký.
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] DangKyDichVu model)
    {
        var hd = await _ctx.HopDongs.FindAsync(model.MaHopDong);
        if (hd == null) return NotFound(new { success = false, message = $"Không tìm thấy hợp đồng #{model.MaHopDong}." });
        if (hd.TrangThai != NghiepVu.HopDong.HieuLuc)
            return BadRequest(new { success = false, message = "Chỉ đăng ký dịch vụ cho hợp đồng đang hiệu lực." });
        var dv = await _ctx.DichVus.FindAsync(model.MaDichVu);
        if (dv == null) return NotFound(new { success = false, message = $"Không tìm thấy dịch vụ #{model.MaDichVu}." });
        if (await _ctx.DangKyDichVus.AnyAsync(d => d.MaHopDong == model.MaHopDong && d.MaDichVu == model.MaDichVu
                                                  && d.TrangThai == NghiepVu.DichVu.DangSuDung))
            return BadRequest(new { success = false, message = $"Hợp đồng #{model.MaHopDong} đang dùng dịch vụ '{dv.TenDichVu}' rồi." });

        try
        {
            model.MaDangKy       = 0;
            model.TrangThai      = NghiepVu.DichVu.DangSuDung;
            model.NgayBatDau   ??= DateOnly.FromDateTime(DateTime.Now);
            model.DonGiaApDung ??= dv.DonGia;
            model.NgayKetThuc    = null;
            _ctx.DangKyDichVus.Add(model);
            await _ctx.SaveChangesAsync();
            return Ok(new { success = true, message = "Đăng ký dịch vụ thành công", data = new { model.MaDangKy } });
        }
        catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.InnerException?.Message ?? ex.Message }); }
    }

    [HttpPut("{id}/ngung")]
    public async Task<IActionResult> Ngung(int id)
    {
        try
        {
            var item = await _ctx.DangKyDichVus.FindAsync(id);
            if (item == null) return NotFound(new { message = "Không tìm thấy đăng ký" });
            // CSDL dùng "Đã kết thúc"; ghi "Ngưng" sẽ tạo ra trạng thái lạc loài
            // và hóa đơn sẽ vẫn tính dịch vụ này vì bộ lọc chỉ nhận "Đang sử dụng".
            item.TrangThai = "Đã kết thúc";
            item.NgayKetThuc = DateOnly.FromDateTime(DateTime.Today);
            await _ctx.SaveChangesAsync();
            return Ok(new { success = true, message = "Đã ngưng dịch vụ" });
        }
        catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var item = await _ctx.DangKyDichVus.FindAsync(id);
            if (item == null) return NotFound(new { message = "Không tìm thấy đăng ký" });
            _ctx.DangKyDichVus.Remove(item);
            await _ctx.SaveChangesAsync();
            return Ok(new { success = true, message = "Xóa thành công" });
        }
        catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }
}
