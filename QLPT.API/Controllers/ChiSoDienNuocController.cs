using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLPT.API.Models;

namespace QLPT.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ChiSoDienNuocController : ControllerBase
{
    private readonly Klcn052QuanLyPhongTroContext _ctx;
    public ChiSoDienNuocController(Klcn052QuanLyPhongTroContext ctx) => _ctx = ctx;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            var list = await _ctx.ChiSoDienNuocs
                .Include(x => x.MaPhongNavigation)
                .Select(x => new {
                    x.MaChiSo, x.MaPhong, x.LoaiChiSo, x.Thang, x.Nam,
                    x.ChiSoCu, x.ChiSoMoi, x.NgayGhi, x.ChiSoXacNhan,
                    TenPhong = x.MaPhongNavigation != null ? x.MaPhongNavigation.TenPhong : ""
                })
                .OrderByDescending(x => x.Nam).ThenByDescending(x => x.Thang)
                .ToListAsync();
            return Ok(list);
        }
        catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ChiSoDienNuoc model)
    {
        try
        {
            _ctx.ChiSoDienNuocs.Add(model);
            await _ctx.SaveChangesAsync();
            return Ok(new { success = true, message = "Thêm chỉ số thành công", data = model });
        }
        catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] ChiSoDienNuoc model)
    {
        try
        {
            var item = await _ctx.ChiSoDienNuocs.FindAsync(id);
            if (item == null) return NotFound(new { message = "Không tìm thấy chỉ số" });
            item.ChiSoMoi = model.ChiSoMoi;
            item.ChiSoXacNhan = model.ChiSoXacNhan;
            item.NgayGhi = model.NgayGhi;
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
            var item = await _ctx.ChiSoDienNuocs.FindAsync(id);
            if (item == null) return NotFound(new { message = "Không tìm thấy chỉ số" });
            _ctx.ChiSoDienNuocs.Remove(item);
            await _ctx.SaveChangesAsync();
            return Ok(new { success = true, message = "Xóa thành công" });
        }
        catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }
}
