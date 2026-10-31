using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLPT.API.Models;
using QLPT.API.Services;

namespace QLPT.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ThuChiController : ControllerBase
{
    private readonly Klcn052QuanLyPhongTroContext _ctx;
    public ThuChiController(Klcn052QuanLyPhongTroContext ctx) => _ctx = ctx;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            var list = await _ctx.PhieuThuChis.AsNoTracking()
                .OrderByDescending(x => x.NgayLap)
                .ToListAsync();
            return Ok(list.Select(x => new { x.MaPhieu, x.LoaiPhieu, x.NgayLap, x.NoiDung,
                x.SoTien, x.GhiChu, x.NguoiNopNhan, x.TrangThai,
                CoTheSuaXoa = !NghiepVu.LaPhieuTuDong(x) }).ToList());
        }
        catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] PhieuThuChi model)
    {
        if (model.LoaiPhieu is not ("Thu" or "Chi"))
            return BadRequest(new { success = false, message = "Loại phiếu phải là 'Thu' hoặc 'Chi'." });
        if (model.SoTien is not > 0)
            return BadRequest(new { success = false, message = "Số tiền phải lớn hơn 0." });
        if (model.GhiChu?.StartsWith("[QLPT:auto]", StringComparison.Ordinal) == true ||
            NghiepVu.LaPhieuTuDong(model))
            return BadRequest(new { message = "Phiếu nhập thủ công không được dùng dấu nguồn của nghiệp vụ tự động." });
        try
        {
            model.MaPhieu     = 0;
            model.NgayLap   ??= DateTime.Now;   // giữ ngày người dùng chọn
            model.TrangThai ??= "Hoàn tất";
            _ctx.PhieuThuChis.Add(model);
            await _ctx.SaveChangesAsync();
            return Ok(new { success = true, message = "Thêm phiếu thành công", data = new { model.MaPhieu } });
        }
        catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.InnerException?.Message ?? ex.Message }); }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] PhieuThuChi model)
    {
        try
        {
            var item = await _ctx.PhieuThuChis.FindAsync(id);
            if (item == null) return NotFound(new { message = "Không tìm thấy phiếu" });
            if (NghiepVu.LaPhieuTuDong(item))
                return BadRequest(new { message = "Phiếu do nghiệp vụ tạo không được sửa trực tiếp; hãy xử lý tại nghiệp vụ gốc." });
            if (model.LoaiPhieu is not (null or "Thu" or "Chi") || model.SoTien is <= 0 ||
                model.GhiChu?.StartsWith("[QLPT:auto]", StringComparison.Ordinal) == true ||
                NghiepVu.LaPhieuTuDong(model))
                return BadRequest(new { message = "Loại, số tiền hoặc nội dung phiếu không hợp lệ." });
            item.LoaiPhieu    = model.LoaiPhieu    ?? item.LoaiPhieu;
            item.NoiDung      = model.NoiDung      ?? item.NoiDung;
            item.SoTien       = model.SoTien       ?? item.SoTien;
            item.NgayLap      = model.NgayLap      ?? item.NgayLap;
            item.NguoiNopNhan = model.NguoiNopNhan ?? item.NguoiNopNhan;
            item.GhiChu       = model.GhiChu       ?? item.GhiChu;
            item.TrangThai    = model.TrangThai    ?? item.TrangThai;
            await _ctx.SaveChangesAsync();
            return Ok(new { success = true, message = "Cập nhật thành công", data = item });
        }
        catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var item = await _ctx.PhieuThuChis.FindAsync(id);
            if (item == null) return NotFound(new { message = "Không tìm thấy phiếu" });
            if (NghiepVu.LaPhieuTuDong(item))
                return BadRequest(new { message = "Phiếu do nghiệp vụ tạo không được xóa trực tiếp; hãy xử lý tại nghiệp vụ gốc." });
            _ctx.PhieuThuChis.Remove(item);
            await _ctx.SaveChangesAsync();
            return Ok(new { success = true, message = "Xóa phiếu thành công" });
        }
        catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }
}
