using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLPT.API.Models;

namespace QLPT.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class TaiSanController : ControllerBase
{
    private readonly Klcn052QuanLyPhongTroContext _ctx;
    public TaiSanController(Klcn052QuanLyPhongTroContext ctx) => _ctx = ctx;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        try { return Ok(await _ctx.TaiSans.ToListAsync()); }
        catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }

    [HttpGet("phong/{maPhong}")]
    public async Task<IActionResult> GetByPhong(int maPhong)
    {
        try
        {
            var list = await _ctx.TaiSanPhongs
                .Where(x => x.MaPhong == maPhong)
                .Include(x => x.MaTaiSanNavigation)
                .Select(x => new {
                    x.MaTaiSanPhong, x.MaPhong, x.SoLuong, x.TinhTrang, x.GhiChu,
                    TenTaiSan = x.MaTaiSanNavigation != null ? x.MaTaiSanNavigation.TenTaiSan : ""
                })
                .ToListAsync();
            return Ok(list);
        }
        catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }

    // Cập nhật tình trạng / số lượng một tài sản đang gán trong phòng (bàn giao, kiểm kê).
    [HttpPut("phong/{id}")]
    public async Task<IActionResult> UpdateTaiSanPhong(int id, [FromBody] TaiSanPhongUpdateDto dto)
    {
        var item = await _ctx.TaiSanPhongs.FindAsync(id);
        if (item == null) return NotFound(new { success = false, message = "Không tìm thấy tài sản trong phòng." });
        if (dto.SoLuong is < 0) return BadRequest(new { success = false, message = "Số lượng không được âm." });
        try
        {
            item.TinhTrang = dto.TinhTrang ?? item.TinhTrang;
            item.SoLuong   = dto.SoLuong   ?? item.SoLuong;
            item.GhiChu    = dto.GhiChu    ?? item.GhiChu;
            await _ctx.SaveChangesAsync();
            return Ok(new { success = true, message = "Đã cập nhật tình trạng tài sản." });
        }
        catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.InnerException?.Message ?? ex.Message }); }
    }

    // Gỡ tài sản khỏi phòng (không xóa tài sản mẫu trong danh mục).
    [HttpDelete("phong/{id}")]
    public async Task<IActionResult> GoKhoiPhong(int id)
    {
        var item = await _ctx.TaiSanPhongs.FindAsync(id);
        if (item == null) return NotFound(new { success = false, message = "Không tìm thấy tài sản trong phòng." });
        try
        {
            _ctx.TaiSanPhongs.Remove(item);
            await _ctx.SaveChangesAsync();
            return Ok(new { success = true, message = "Đã gỡ tài sản khỏi phòng." });
        }
        catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.InnerException?.Message ?? ex.Message }); }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] TaiSan model)
    {
        try
        {
            _ctx.TaiSans.Add(model);
            await _ctx.SaveChangesAsync();
            return Ok(new { success = true, message = "Thêm tài sản thành công", data = model });
        }
        catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] TaiSan model)
    {
        try
        {
            var item = await _ctx.TaiSans.FindAsync(id);
            if (item == null) return NotFound(new { message = "Không tìm thấy tài sản" });
            item.TenTaiSan = model.TenTaiSan;
            item.DonViTinh = model.DonViTinh;
            item.GiaTriThamKhao = model.GiaTriThamKhao;
            item.MoTa = model.MoTa;
            await _ctx.SaveChangesAsync();
            return Ok(new { success = true, message = "Cập nhật thành công", data = item });
        }
        catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }

    [HttpPost("gan-phong")]
    public async Task<IActionResult> GanVaoPhong([FromBody] TaiSanPhong model)
    {
        try
        {
            _ctx.TaiSanPhongs.Add(model);
            await _ctx.SaveChangesAsync();
            return Ok(new { success = true, message = "Gán tài sản vào phòng thành công", data = model });
        }
        catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var item = await _ctx.TaiSans.FindAsync(id);
            if (item == null) return NotFound(new { message = "Không tìm thấy tài sản" });
            _ctx.TaiSans.Remove(item);
            await _ctx.SaveChangesAsync();
            return Ok(new { success = true, message = "Xóa tài sản thành công" });
        }
        catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }
}

public class TaiSanPhongUpdateDto
{
    public string? TinhTrang { get; set; }
    public int? SoLuong { get; set; }
    public string? GhiChu { get; set; }
}
