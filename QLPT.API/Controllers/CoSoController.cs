using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using QLPT.API.Models;
using QLPT.API.Services;

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
        try
        {
            var query = _ctx.CoSos.AsNoTracking().AsQueryable();
            if (User.IsInRole("Manager") && int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int managerId))
                query = query.Where(x => _ctx.QuanLyCoSos.Any(a => a.MaTaiKhoan == managerId && a.MaCoSo == x.MaCoSo));
            return Ok(await query.Select(x => new {
                x.MaCoSo, x.TenCoSo, x.DiaChi, x.SoDienThoai, x.MoTa,
                CoTheSuaDiaChi = !_ctx.HopDongs.Any(h => h.MaPhongNavigation.MaCoSo == x.MaCoSo &&
                    h.TrangThai == NghiepVu.HopDong.HieuLuc)
            }).ToListAsync());
        }
        catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CoSo model)
    {
        if (string.IsNullOrWhiteSpace(model.TenCoSo) || model.TenCoSo.Trim().Length > 100 || string.IsNullOrWhiteSpace(model.DiaChi)
            || model.DiaChi.Length > 300 || model.MoTa?.Length > 300 || !HopLeSdt(model.SoDienThoai))
            return BadRequest(new { message = "Cần tên cơ sở (tối đa 100 ký tự), địa chỉ (tối đa 300 ký tự) và số điện thoại 10 chữ số bắt đầu bằng 0." });
        try
        {
            model = new CoSo { TenCoSo = model.TenCoSo.Trim(), DiaChi = model.DiaChi.Trim(), SoDienThoai = model.SoDienThoai!.Trim(), MoTa = model.MoTa };
            _ctx.CoSos.Add(model);
            await _ctx.SaveChangesAsync();
            return Ok(new { success = true, message = "Thêm cơ sở thành công", data = model });
        }
        catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] CoSo model)
    {
        if (string.IsNullOrWhiteSpace(model.TenCoSo) || model.TenCoSo.Trim().Length > 100 || string.IsNullOrWhiteSpace(model.DiaChi)
            || model.DiaChi.Length > 300 || model.MoTa?.Length > 300 || !HopLeSdt(model.SoDienThoai))
            return BadRequest(new { message = "Cần tên cơ sở (tối đa 100 ký tự), địa chỉ (tối đa 300 ký tự) và số điện thoại 10 chữ số bắt đầu bằng 0." });
        try
        {
            var item = await _ctx.CoSos.FindAsync(id);
            if (item == null) return NotFound(new { message = "Không tìm thấy cơ sở" });
            if (item.DiaChi != model.DiaChi.Trim() &&
                await _ctx.HopDongs.AnyAsync(h => h.MaPhongNavigation.MaCoSo == id &&
                    h.TrangThai == NghiepVu.HopDong.HieuLuc))
                return BadRequest(new { message = "Cơ sở có phòng đang thuê; không thể đổi địa chỉ khi hợp đồng còn hiệu lực." });
            item.TenCoSo = model.TenCoSo.Trim();
            item.DiaChi = model.DiaChi.Trim();
            item.SoDienThoai = model.SoDienThoai!.Trim();
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
            if (await _ctx.Phongs.AnyAsync(p => p.MaCoSo == id))
                return BadRequest(new { message = "Cơ sở còn phòng. Chuyển hoặc xóa các phòng trước khi xóa cơ sở." });
            _ctx.CoSos.Remove(item);
            await _ctx.SaveChangesAsync();
            return Ok(new { success = true, message = "Xóa cơ sở thành công" });
        }
        catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }

    private static bool HopLeSdt(string? sdt) => !string.IsNullOrWhiteSpace(sdt)
        && System.Text.RegularExpressions.Regex.IsMatch(sdt.Trim(), @"^0\d{9}$");
}
