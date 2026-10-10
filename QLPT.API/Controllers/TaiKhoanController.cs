using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLPT.API.Models;

namespace QLPT.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class TaiKhoanController : ControllerBase
{
    private readonly Klcn052QuanLyPhongTroContext _ctx;
    public TaiKhoanController(Klcn052QuanLyPhongTroContext ctx) => _ctx = ctx;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            var list = await _ctx.TaiKhoans
                .Select(x => new {
                    x.MaTaiKhoan, x.TenDangNhap, x.VaiTro, x.TrangThai, x.NgayTao, x.MaKhach
                })
                .ToListAsync();
            return Ok(list);
        }
        catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }

    // WPF gửi { tenDangNhap, matKhau, vaiTro, maKhach }. Bản cũ bind thẳng entity nên
    // "matKhau" rơi mất → MatKhauHash null → lỗi DB. Giờ nhận đúng trường và hash bcrypt.
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] TaoTaiKhoanDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.TenDangNhap) || string.IsNullOrWhiteSpace(dto.MatKhau))
            return BadRequest(new { success = false, message = "Cần tên đăng nhập và mật khẩu." });
        if (dto.MatKhau.Length < 6)
            return BadRequest(new { success = false, message = "Mật khẩu tối thiểu 6 ký tự." });

        string vaiTro = string.Equals(dto.VaiTro, "Admin", StringComparison.OrdinalIgnoreCase) ? "Admin" : "Customer";
        string ten = dto.TenDangNhap.Trim();
        if (await _ctx.TaiKhoans.AnyAsync(x => x.TenDangNhap == ten))
            return BadRequest(new { success = false, message = "Tên đăng nhập đã tồn tại." });
        if (dto.MaKhach.HasValue)
        {
            if (await _ctx.KhachThues.FindAsync(dto.MaKhach.Value) == null)
                return BadRequest(new { success = false, message = $"Không tìm thấy khách thuê #{dto.MaKhach}." });
            // UQ_TaiKhoan_MaKhach: mỗi khách chỉ một tài khoản.
            if (await _ctx.TaiKhoans.AnyAsync(x => x.MaKhach == dto.MaKhach))
                return BadRequest(new { success = false, message = $"Khách thuê #{dto.MaKhach} đã có tài khoản." });
        }
        else if (vaiTro == "Customer")
            return BadRequest(new { success = false, message = "Tài khoản khách thuê phải gắn với một mã khách." });

        try
        {
            var tk = new TaiKhoan
            {
                TenDangNhap = ten,
                MatKhauHash = BCrypt.Net.BCrypt.HashPassword(dto.MatKhau),
                VaiTro      = vaiTro,
                MaKhach     = dto.MaKhach,
                TrangThai   = "Active",
                NgayTao     = DateTime.Now
            };
            _ctx.TaiKhoans.Add(tk);
            await _ctx.SaveChangesAsync();
            return Ok(new { success = true, message = "Tạo tài khoản thành công", data = new { tk.MaTaiKhoan, tk.TenDangNhap, tk.VaiTro } });
        }
        catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.InnerException?.Message ?? ex.Message }); }
    }

    // WPF chỉ gửi { vaiTro }. Bản cũ gán thẳng item.TrangThai = model.TrangThai (= null),
    // khiến sửa vai trò xong thì tài khoản bị coi là khóa và không đăng nhập được nữa.
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] SuaTaiKhoanDto dto)
    {
        var item = await _ctx.TaiKhoans.FindAsync(id);
        if (item == null) return NotFound(new { success = false, message = "Không tìm thấy tài khoản" });
        if (!string.IsNullOrEmpty(dto.MatKhau) && dto.MatKhau.Length < 6)
            return BadRequest(new { success = false, message = "Mật khẩu tối thiểu 6 ký tự." });
        try
        {
            if (!string.IsNullOrWhiteSpace(dto.VaiTro))
            {
                string vaiTro = string.Equals(dto.VaiTro, "Admin", StringComparison.OrdinalIgnoreCase) ? "Admin" : "Customer";
                // Không cho hạ quyền admin cuối cùng, tránh mất hẳn quyền quản trị.
                if (item.VaiTro == "Admin" && vaiTro != "Admin" &&
                    !await _ctx.TaiKhoans.AnyAsync(t => t.MaTaiKhoan != id && t.VaiTro == "Admin" && t.TrangThai == "Active"))
                    return BadRequest(new { success = false, message = "Đây là tài khoản quản trị cuối cùng, không thể hạ quyền." });
                item.VaiTro = vaiTro;
            }
            item.TrangThai = dto.TrangThai ?? item.TrangThai;
            if (!string.IsNullOrWhiteSpace(dto.MatKhau))
                item.MatKhauHash = BCrypt.Net.BCrypt.HashPassword(dto.MatKhau);
            await _ctx.SaveChangesAsync();
            return Ok(new { success = true, message = "Cập nhật thành công" });
        }
        catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.InnerException?.Message ?? ex.Message }); }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var item = await _ctx.TaiKhoans.FindAsync(id);
            if (item == null) return NotFound(new { message = "Không tìm thấy tài khoản" });
            _ctx.TaiKhoans.Remove(item);
            await _ctx.SaveChangesAsync();
            return Ok(new { success = true, message = "Xóa tài khoản thành công" });
        }
        catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }
}

public class TaoTaiKhoanDto
{
    public string? TenDangNhap { get; set; }
    public string? MatKhau { get; set; }
    public string? VaiTro { get; set; }
    public int? MaKhach { get; set; }
}

public class SuaTaiKhoanDto
{
    public string? VaiTro { get; set; }
    public string? TrangThai { get; set; }
    public string? MatKhau { get; set; }
}
