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
                    x.MaTaiKhoan, x.TenDangNhap, x.VaiTro, x.TrangThai, x.NgayTao, x.MaKhach,
                    HoTen = x.MaKhachNavigation == null ? "" : x.MaKhachNavigation.HoTen,
                    MaCoSos = _ctx.QuanLyCoSos.Where(a => a.MaTaiKhoan == x.MaTaiKhoan).Select(a => a.MaCoSo).ToList()
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
        if (dto.TenDangNhap.Trim().Length > 50 || System.Text.Encoding.UTF8.GetByteCount(dto.MatKhau) > 72)
            return BadRequest(new { message = "Tên đăng nhập tối đa 50 ký tự; mật khẩu tối đa 72 byte UTF-8." });
        if (dto.VaiTro is not (null or "Admin" or "Manager" or "BackupOperator" or "Customer")) return BadRequest(new { message = "Vai trò không hợp lệ." });

        string vaiTro = dto.VaiTro ?? "Customer";
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
        if (vaiTro != "Customer" && dto.MaKhach.HasValue)
            return BadRequest(new { message = "Tài khoản chủ trọ/quản lý không gắn hồ sơ khách thuê." });
        if (vaiTro == "Manager" && !await ValidSites(dto.MaCoSos))
            return BadRequest(new { message = "Quản lý phải được giao ít nhất một cơ sở hợp lệ." });

        try
        {
            await using var tx = _ctx.Database.CurrentTransaction == null ? await _ctx.Database.BeginTransactionAsync() : null;
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
            if (vaiTro == "Manager")
            {
                _ctx.QuanLyCoSos.AddRange(dto.MaCoSos!.Distinct().Select(site => new QuanLyCoSo { MaTaiKhoan = tk.MaTaiKhoan, MaCoSo = site }));
                await _ctx.SaveChangesAsync();
            }
            if (tx != null) await tx.CommitAsync();
            return Ok(new { success = true, message = "Tạo tài khoản thành công", data = new { tk.MaTaiKhoan, tk.TenDangNhap, tk.VaiTro } });
        }
        catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.InnerException?.Message ?? ex.Message }); }
    }

    // WPF chỉ gửi { vaiTro }. Bản cũ gán thẳng item.TrangThai = model.TrangThai (= null),
    // khiến sửa vai trò xong thì tài khoản bị coi là khóa và không đăng nhập được nữa.
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] SuaTaiKhoanDto dto)
    {
        if (dto.VaiTro is not (null or "Admin" or "Manager" or "BackupOperator" or "Customer") || dto.TrangThai is not (null or "Active" or "Locked"))
            return BadRequest(new { message = "Vai trò hoặc trạng thái tài khoản không hợp lệ." });
        if (dto.MatKhau != null && (dto.MatKhau.Length < 6 || System.Text.Encoding.UTF8.GetByteCount(dto.MatKhau) > 72))
            return BadRequest(new { message = "Mật khẩu tối thiểu 6 ký tự và tối đa 72 byte UTF-8." });
        var item = await _ctx.TaiKhoans.FindAsync(id);
        if (item == null) return NotFound(new { success = false, message = "Không tìm thấy tài khoản" });
        if (!string.IsNullOrEmpty(dto.MatKhau) && dto.MatKhau.Length < 6)
            return BadRequest(new { success = false, message = "Mật khẩu tối thiểu 6 ký tự." });
        try
        {
            if (dto.TrangThai == "Locked" && item.VaiTro == "Admin" &&
                !await _ctx.TaiKhoans.AnyAsync(t => t.MaTaiKhoan != id && t.VaiTro == "Admin" && t.TrangThai == "Active"))
                return BadRequest(new { message = "Không thể khóa tài khoản quản trị cuối cùng đang hoạt động." });
            if (dto.VaiTro == "Customer" && item.MaKhach is not > 0)
                return BadRequest(new { message = "Tài khoản khách thuê phải liên kết với hồ sơ khách thuê." });
            string targetRole = dto.VaiTro ?? item.VaiTro ?? "Customer";
            if (targetRole == "Manager" && !await ValidSites(dto.MaCoSos ?? await _ctx.QuanLyCoSos
                    .Where(x => x.MaTaiKhoan == id).Select(x => x.MaCoSo).ToListAsync()))
                return BadRequest(new { message = "Quản lý phải được giao ít nhất một cơ sở hợp lệ." });
            if (targetRole != "Customer" && item.MaKhach.HasValue)
                return BadRequest(new { message = "Tài khoản gắn khách thuê không thể chuyển thành chủ trọ/quản lý." });
            if (!string.IsNullOrWhiteSpace(dto.VaiTro))
            {
                string vaiTro = dto.VaiTro;
                // Không cho hạ quyền admin cuối cùng, tránh mất hẳn quyền quản trị.
                if (item.VaiTro == "Admin" && vaiTro != "Admin" &&
                    !await _ctx.TaiKhoans.AnyAsync(t => t.MaTaiKhoan != id && t.VaiTro == "Admin" && t.TrangThai == "Active"))
                    return BadRequest(new { success = false, message = "Đây là tài khoản quản trị cuối cùng, không thể hạ quyền." });
                item.VaiTro = vaiTro;
            }
            item.TrangThai = dto.TrangThai ?? item.TrangThai;
            if (dto.MaCoSos != null || targetRole != "Manager")
            {
                var oldAssignments = await _ctx.QuanLyCoSos.Where(x => x.MaTaiKhoan == id).ToListAsync();
                var desired = targetRole == "Manager" ? dto.MaCoSos!.Distinct().ToHashSet() : new HashSet<int>();
                _ctx.QuanLyCoSos.RemoveRange(oldAssignments.Where(x => !desired.Contains(x.MaCoSo)));
                var oldIds = oldAssignments.Select(x => x.MaCoSo).ToHashSet();
                _ctx.QuanLyCoSos.AddRange(desired.Where(site => !oldIds.Contains(site))
                    .Select(site => new QuanLyCoSo { MaTaiKhoan = id, MaCoSo = site }));
            }
            if (!string.IsNullOrWhiteSpace(dto.MatKhau))
                item.MatKhauHash = BCrypt.Net.BCrypt.HashPassword(dto.MatKhau);
            await _ctx.SaveChangesAsync();
            return Ok(new { success = true, message = "Cập nhật thành công" });
        }
        catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.InnerException?.Message ?? ex.Message }); }
    }

    [HttpPut("{id}/trang-thai")]
    public Task<IActionResult> ChangeStatus(int id, [FromBody] SuaTaiKhoanDto dto) =>
        dto.TrangThai is not ("Active" or "Locked") ? Task.FromResult<IActionResult>(BadRequest(new { message = "Trạng thái phải là Active hoặc Locked." }))
        : Update(id, new SuaTaiKhoanDto { TrangThai = dto.TrangThai });

    [HttpPost("{id}/mat-khau")]
    public Task<IActionResult> ResetPassword(int id, [FromBody] DoiMatKhauDto dto) =>
        string.IsNullOrWhiteSpace(dto.MatKhauMoi) ? Task.FromResult<IActionResult>(BadRequest(new { message = "Vui lòng nhập mật khẩu mới." }))
        : Update(id, new SuaTaiKhoanDto { MatKhau = dto.MatKhauMoi });

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var item = await _ctx.TaiKhoans.FindAsync(id);
            if (item == null) return NotFound(new { message = "Không tìm thấy tài khoản" });
            if (item.VaiTro == "Admin" && item.TrangThai == "Active" &&
                !await _ctx.TaiKhoans.AnyAsync(t => t.MaTaiKhoan != id && t.VaiTro == "Admin" && t.TrangThai == "Active"))
                return BadRequest(new { message = "Không thể xóa tài khoản quản trị cuối cùng đang hoạt động." });
            if (await _ctx.ThongBaoNguoiNhans.AnyAsync(n => n.MaTaiKhoan == id))
                return BadRequest(new { message = "Tài khoản có lịch sử thông báo. Hãy khóa tài khoản để giữ lịch sử." });
            _ctx.QuanLyCoSos.RemoveRange(await _ctx.QuanLyCoSos.Where(x => x.MaTaiKhoan == id).ToListAsync());
            _ctx.TaiKhoans.Remove(item);
            await _ctx.SaveChangesAsync();
            return Ok(new { success = true, message = "Xóa tài khoản thành công" });
        }
        catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }

    private async Task<bool> ValidSites(List<int>? sites) => sites is { Count: > 0 } &&
        sites.All(x => x > 0) && await _ctx.CoSos.CountAsync(x => sites.Contains(x.MaCoSo)) == sites.Distinct().Count();
}

public class TaoTaiKhoanDto
{
    public string? TenDangNhap { get; set; }
    public string? MatKhau { get; set; }
    public string? VaiTro { get; set; }
    public int? MaKhach { get; set; }
    public List<int>? MaCoSos { get; set; }
}

public class SuaTaiKhoanDto
{
    public string? VaiTro { get; set; }
    public string? TrangThai { get; set; }
    public string? MatKhau { get; set; }
    public List<int>? MaCoSos { get; set; }
}

public sealed class DoiMatKhauDto
{
    public string? MatKhauMoi { get; set; }
}
