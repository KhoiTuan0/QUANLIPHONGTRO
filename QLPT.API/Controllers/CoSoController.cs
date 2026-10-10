using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLPT.API.Models;

namespace QLPT.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class CoSoController : ControllerBase
{
    private readonly Klcn052QuanLyPhongTroContext _ctx;
    public CoSoController(Klcn052QuanLyPhongTroContext ctx) => _ctx = ctx;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        try { return Ok(await _ctx.CoSos.ToListAsync()); }
        catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CoSo model)
    {
        try
        {
            _ctx.CoSos.Add(model);
            await _ctx.SaveChangesAsync();
            return Ok(new { success = true, message = "Thêm cơ sở thành công", data = model });
        }
        catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] CoSo model)
    {
        try
        {
            var item = await _ctx.CoSos.FindAsync(id);
            if (item == null) return NotFound(new { message = "Không tìm thấy cơ sở" });
            item.TenCoSo = model.TenCoSo;
            item.DiaChi = model.DiaChi;
            item.SoDienThoai = model.SoDienThoai;
            item.MoTa = model.MoTa;
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
            var item = await _ctx.CoSos.FindAsync(id);
            if (item == null) return NotFound(new { message = "Không tìm thấy cơ sở" });
            _ctx.CoSos.Remove(item);
            await _ctx.SaveChangesAsync();
            return Ok(new { success = true, message = "Xóa cơ sở thành công" });
        }
        catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }
}
