using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLPT.API.Models;

namespace QLPT.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class DichVuController : ControllerBase
{
    private readonly Klcn052QuanLyPhongTroContext _ctx;
    public DichVuController(Klcn052QuanLyPhongTroContext ctx) => _ctx = ctx;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        try { return Ok(await _ctx.DichVus.ToListAsync()); }
        catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] DichVu model)
    {
        if (string.IsNullOrWhiteSpace(model.TenDichVu) || model.TenDichVu.Trim().Length > 100 || model.DonGia is < 0)
            return BadRequest(new { message = "Tên dịch vụ phải từ 1 đến 100 ký tự; đơn giá không được âm." });
        try
        {
            model.MaDichVu = 0; model.TenDichVu = model.TenDichVu.Trim(); model.DangKyDichVus = new List<DangKyDichVu>();
            _ctx.DichVus.Add(model);
            await _ctx.SaveChangesAsync();
            return Ok(new { success = true, message = "Thêm dịch vụ thành công", data = model });
        }
        catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] DichVu model)
    {
        if (string.IsNullOrWhiteSpace(model.TenDichVu) || model.TenDichVu.Trim().Length > 100 || model.DonGia is < 0)
            return BadRequest(new { message = "Tên dịch vụ phải từ 1 đến 100 ký tự; đơn giá không được âm." });
        try
        {
            var item = await _ctx.DichVus.FindAsync(id);
            if (item == null) return NotFound(new { message = "Không tìm thấy dịch vụ" });
            item.TenDichVu = model.TenDichVu.Trim();
            item.DonViTinh = model.DonViTinh;
            item.DonGia = model.DonGia;
            item.TrangThai = model.TrangThai;
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
            var item = await _ctx.DichVus.FindAsync(id);
            if (item == null) return NotFound(new { message = "Không tìm thấy dịch vụ" });
            if (await _ctx.DangKyDichVus.AnyAsync(d => d.MaDichVu == id))
                return BadRequest(new { message = "Dịch vụ đã có lịch sử đăng ký nên không thể xóa." });
            _ctx.DichVus.Remove(item);
            await _ctx.SaveChangesAsync();
            return Ok(new { success = true, message = "Xóa dịch vụ thành công" });
        }
        catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }
}
