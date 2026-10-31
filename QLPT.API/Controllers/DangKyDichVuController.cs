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
        if (model.MaHopDong <= 0 || model.MaDichVu <= 0)
            return BadRequest(new { success = false, message = "Chọn hợp đồng và dịch vụ trong danh sách." });
        if (model.DonGiaApDung.HasValue && (model.DonGiaApDung < 0 || model.DonGiaApDung > 9999999999999999.99m
            || decimal.Round(model.DonGiaApDung.Value, 2) != model.DonGiaApDung))
            return BadRequest(new { success = false, message = "Đơn giá dịch vụ phải không âm, tối đa 2 chữ số thập phân và trong giới hạn lưu trữ." });
        if (model.NgayBatDau == default(DateOnly))
            return BadRequest(new { success = false, message = "Ngày bắt đầu dịch vụ không hợp lệ." });
        var hd = await _ctx.HopDongs.FindAsync(model.MaHopDong);
        if (hd == null) return NotFound(new { success = false, message = $"Không tìm thấy hợp đồng #{model.MaHopDong}." });
        if (hd.TrangThai != NghiepVu.HopDong.HieuLuc)
            return BadRequest(new { success = false, message = "Chỉ đăng ký dịch vụ cho hợp đồng đang hiệu lực." });
        var ngayBatDau = model.NgayBatDau ?? DateOnly.FromDateTime(DateTime.Today);
        if (ngayBatDau < DateOnly.FromDateTime(DateTime.Today))
            return BadRequest(new { success = false, message = "Dịch vụ mới không được bắt đầu trước hôm nay." });
        if (ngayBatDau < hd.NgayBatDau || ngayBatDau > hd.NgayKetThuc)
            return BadRequest(new { success = false, message = "Ngày bắt đầu dịch vụ phải nằm trong thời gian hợp đồng." });
        var dv = await _ctx.DichVus.FindAsync(model.MaDichVu);
        if (dv == null) return NotFound(new { success = false, message = $"Không tìm thấy dịch vụ #{model.MaDichVu}." });
        if (await _ctx.DangKyDichVus.AnyAsync(d => d.MaHopDong == model.MaHopDong && d.MaDichVu == model.MaDichVu
                                                  && d.TrangThai == NghiepVu.DichVu.DangSuDung))
            return BadRequest(new { success = false, message = $"Hợp đồng #{model.MaHopDong} đang dùng dịch vụ '{dv.TenDichVu}' rồi." });

        try
        {
            decimal? gia = model.DonGiaApDung ?? dv.DonGia;
            if (gia is < 0 || gia is > 9999999999999999.99m)
                return BadRequest(new { success = false, message = "Đơn giá trong danh mục dịch vụ chưa hợp lệ." });
            model = new DangKyDichVu
            {
                MaHopDong = model.MaHopDong, MaDichVu = model.MaDichVu,
                TrangThai = NghiepVu.DichVu.DangSuDung, NgayBatDau = ngayBatDau, DonGiaApDung = gia
            };
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
            if (item.TrangThai == NghiepVu.DichVu.DaKetThuc)
                return Ok(new { success = true, message = "Dịch vụ đã kết thúc, giữ nguyên ngày ngừng trước đó." });
            if (item.NgayBatDau > DateOnly.FromDateTime(DateTime.Today))
                return BadRequest(new { success = false, message = "Dịch vụ chưa bắt đầu. Dùng xóa đăng ký để hủy dịch vụ đặt trước." });
            // CSDL dùng "Đã kết thúc"; ghi "Ngưng" sẽ tạo ra trạng thái lạc loài
            // và hóa đơn sẽ vẫn tính dịch vụ này vì bộ lọc chỉ nhận "Đang sử dụng".
            item.TrangThai = NghiepVu.DichVu.DaKetThuc;
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
            if (!item.NgayBatDau.HasValue || item.NgayBatDau <= DateOnly.FromDateTime(DateTime.Today))
                return BadRequest(new { success = false, message = "Không xóa lịch sử dịch vụ đã sử dụng. Dùng ngừng dịch vụ để giữ dữ liệu tính hóa đơn." });
            _ctx.DangKyDichVus.Remove(item);
            await _ctx.SaveChangesAsync();
            return Ok(new { success = true, message = "Xóa thành công" });
        }
        catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }
}
