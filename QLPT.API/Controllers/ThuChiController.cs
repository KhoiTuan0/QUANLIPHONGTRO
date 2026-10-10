using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLPT.API.Models;

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
            var list = await _ctx.PhieuThuChis
                .OrderByDescending(x => x.NgayLap)
                .ToListAsync();
            return Ok(list);
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
            _ctx.PhieuThuChis.Remove(item);
            await _ctx.SaveChangesAsync();
            return Ok(new { success = true, message = "Xóa phiếu thành công" });
        }
        catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }
}
