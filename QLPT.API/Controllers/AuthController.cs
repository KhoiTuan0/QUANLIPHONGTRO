using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLPT.API.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using Microsoft.AspNetCore.RateLimiting;

[Route("api/[controller]")]
[ApiController]
[EnableRateLimiting("auth")]
public class AuthController : ControllerBase
{
    private readonly Klcn052QuanLyPhongTroContext _context;

    public AuthController(Klcn052QuanLyPhongTroContext context)
    {
        _context = context;
    }

    
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Username) || string.IsNullOrWhiteSpace(model.Password))
            return BadRequest(new { message = "Vui lòng nhập tên đăng nhập và mật khẩu." });
        if (model.Username.Trim().Length > 50 || System.Text.Encoding.UTF8.GetByteCount(model.Password) > 72)
            return BadRequest(new { message = "Tên đăng nhập tối đa 50 ký tự; mật khẩu tối đa 72 byte UTF-8." });

        var user = await _context.TaiKhoans
            .FirstOrDefaultAsync(u => u.TenDangNhap == model.Username.Trim());

        // Trả cùng một thông báo cho "sai tên" lẫn "sai mật khẩu" để không lộ
        // tài khoản nào tồn tại.
        if (user == null || !VerifyPassword(model.Password, user.MatKhauHash))
            return Unauthorized(new { message = "Tên đăng nhập hoặc mật khẩu không đúng." });

        if (!string.Equals(user.TrangThai, "Active", StringComparison.OrdinalIgnoreCase))
            return Unauthorized(new { message = "Tài khoản đã bị khóa." });
        if (user.VaiTro is not ("Admin" or "Manager" or "BackupOperator" or "Customer"))
            return Unauthorized(new { message = "Vai trò tài khoản không hợp lệ. Liên hệ chủ trọ." });
        if (user.VaiTro == "Customer" && user.MaKhach is not > 0)
            return Unauthorized(new { message = "Tài khoản chưa được liên kết với hồ sơ khách thuê." });
        if (user.VaiTro == "Manager" && !await _context.QuanLyCoSos.AnyAsync(x => x.MaTaiKhoan == user.MaTaiKhoan))
            return Unauthorized(new { message = "Tài khoản quản lý chưa được phân công cơ sở." });
        // Upgrade legacy plaintext only after a successful password check.
        if (user.MatKhauHash is { } legacy && !legacy.StartsWith("$2"))
        {
            user.MatKhauHash = BCrypt.Net.BCrypt.HashPassword(model.Password);
            await _context.SaveChangesAsync();
        }

        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, user.MaTaiKhoan.ToString()),
            new(ClaimTypes.Name, user.TenDangNhap), new(ClaimTypes.Role, user.VaiTro),
            new("PasswordStamp", QLPT.API.Services.SessionClaims.PasswordStamp(user.MatKhauHash)) };
        if (user.MaKhach.HasValue) claims.Add(new Claim("MaKhach", user.MaKhach.Value.ToString()));
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));
        return Ok(new
        {
            message = "Đăng nhập thành công",
            maTaiKhoan = user.MaTaiKhoan,
            vaiTro = user.VaiTro ?? "Customer",
            maKhach = user.MaKhach
        });
    }

    // Mật khẩu seed lưu dạng bcrypt ($2a/$2b/$2y). Tài khoản cũ lưu thô vẫn
    // đăng nhập được một lần để chuyển sang bcrypt sau khi xác thực thành công.
    private static bool VerifyPassword(string? password, string? stored)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(stored)) return false;
        if (stored.StartsWith("$2"))
        {
            try { return BCrypt.Net.BCrypt.Verify(password, stored); }
            catch { return false; }
        }
        return password == stored;
    }

    [HttpPost("register")]
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Admin")]
    public async Task<IActionResult> Register(CustomerAccountModel dto)
    {
        if (string.IsNullOrWhiteSpace(dto.TenDangNhap) || dto.TenDangNhap.Trim().Length > 50 ||
            string.IsNullOrWhiteSpace(dto.MatKhau) || dto.MatKhau.Length < 6 ||
            System.Text.Encoding.UTF8.GetByteCount(dto.MatKhau) > 72)
            return BadRequest(new { message = "Tên đăng nhập tối đa 50 ký tự, mật khẩu từ 6 ký tự và tối đa 72 byte." });
        if (await _context.TaiKhoans.AnyAsync(a => a.TenDangNhap == dto.TenDangNhap.Trim()))
            return Conflict(new { message = "Tên đăng nhập đã tồn tại." });
        await using var tx = await _context.Database.BeginTransactionAsync();
        var tenants = new QLPT.API.Controllers.KhachThueController(_context) { ControllerContext = ControllerContext };
        var result = await tenants.Create(new KhachThue { HoTen = dto.HoTen, Cccd = dto.Cccd,
            SoDienThoai = dto.SoDienThoai, Email = dto.Email, DiaChi = dto.DiaChi });
        if (result is not OkObjectResult ok) return result;
        var tenant = (KhachThue)ok.Value!.GetType().GetProperty("data")!.GetValue(ok.Value)!;
        _context.TaiKhoans.Add(new TaiKhoan { TenDangNhap = dto.TenDangNhap.Trim(),
            MatKhauHash = BCrypt.Net.BCrypt.HashPassword(dto.MatKhau), MaKhach = tenant.MaKhach,
            VaiTro = "Customer", TrangThai = "Active", NgayTao = DateTime.Now });
        await _context.SaveChangesAsync();
        await tx.CommitAsync();
        return Ok(new { success = true, maKhach = tenant.MaKhach, message = "Đã tạo hồ sơ và cấp tài khoản khách." });
    }
    public sealed record CustomerAccountModel(string TenDangNhap, string MatKhau, string HoTen,
        string? Cccd, string? SoDienThoai, string? Email, string? DiaChi);

    [HttpPost("change-password")]
    // Password changes use the signed-in account, never a client-selected account.
    [Microsoft.AspNetCore.Authorization.Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordModel model)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var accountId))
            return Unauthorized();
        if (string.IsNullOrWhiteSpace(model.MatKhauMoi) || model.MatKhauMoi.Length < 8 ||
            System.Text.Encoding.UTF8.GetByteCount(model.MatKhauMoi) > 72)
            return BadRequest(new { message = "Mật khẩu mới từ 8 ký tự, tối đa 72 byte UTF-8." });
        var account = await _context.TaiKhoans.FindAsync(accountId);
        if (account == null || (model.MaKhach > 0 && model.MaKhach != account.MaKhach)) return Forbid();
        if (!VerifyPassword(model.MatKhauHienTai, account.MatKhauHash))
            return BadRequest(new { message = "Mật khẩu hiện tại không đúng." });
        if (model.MatKhauMoi == model.MatKhauHienTai)
            return BadRequest(new { message = "Mật khẩu mới phải khác mật khẩu hiện tại." });
        account.MatKhauHash = BCrypt.Net.BCrypt.HashPassword(model.MatKhauMoi);
        await _context.SaveChangesAsync();
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Ok(new { success = true, message = "Đã đổi mật khẩu. Vui lòng đăng nhập lại." });
    }

    public sealed record ChangePasswordModel(int MaKhach, string MatKhauHienTai, string MatKhauMoi);

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Ok(new { success = true, message = "Đã đăng xuất" });
    }

    public class LoginModel
    {
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
    }
}
