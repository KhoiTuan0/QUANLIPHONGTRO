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
        try
        {
            _ctx.DichVus.Add(model);
            await _ctx.SaveChangesAsync();
            return Ok(new { success = true, message = "Thêm dịch vụ thành công", data = model });
        }
        catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] DichVu model)
    {
        try
        {
            var item = await _ctx.DichVus.FindAsync(id);
            if (item == null) return NotFound(new { message = "Không tìm thấy dịch vụ" });
            item.TenDichVu = model.TenDichVu;
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
            _ctx.DichVus.Remove(item);
            await _ctx.SaveChangesAsync();
            return Ok(new { success = true, message = "Xóa dịch vụ thành công" });
        }
        catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }
}
