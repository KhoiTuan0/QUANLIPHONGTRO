using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLPT.API.Models;

[Route("api/[controller]")]
[ApiController]
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

        var user = await _context.TaiKhoans
            .FirstOrDefaultAsync(u => u.TenDangNhap == model.Username);

        // Trả cùng một thông báo cho "sai tên" lẫn "sai mật khẩu" để không lộ
        // tài khoản nào tồn tại.
        if (user == null || !VerifyPassword(model.Password, user.MatKhauHash))
            return Unauthorized(new { message = "Tên đăng nhập hoặc mật khẩu không đúng." });

        if (!string.Equals(user.TrangThai, "Active", StringComparison.OrdinalIgnoreCase))
            return Unauthorized(new { message = "Tài khoản đã bị khóa." });

        return Ok(new
        {
            message = "Đăng nhập thành công",
            maTaiKhoan = user.MaTaiKhoan,
            vaiTro = user.VaiTro ?? "Customer",
            maKhach = user.MaKhach
        });
    }

    // Mật khẩu seed lưu dạng bcrypt ($2a/$2b/$2y). Nếu gặp bản ghi cũ lưu thô
    // (do Register bản trước ghi plaintext) thì so sánh trực tiếp để không khóa
    // oan tài khoản đó — vẫn an toàn vì không còn đường tạo bản ghi thô mới.
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

    // API không giữ phiên phía server (client tự xóa session), nên đăng xuất chỉ cần
    // xác nhận để client không nhận về 404.
    [HttpPost("logout")]
    public IActionResult Logout() => Ok(new { success = true, message = "Đã đăng xuất" });

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterModel model)
    {
        try
        {
            // 1. Kiểm tra xem tên đăng nhập đã tồn tại chưa
            var existingUser = await _context.TaiKhoans
                .FirstOrDefaultAsync(u => u.TenDangNhap == model.TenDangNhap);

            if (existingUser != null)
            {
                return BadRequest(new { message = "Tên đăng nhập đã tồn tại!" });
            }
            if (string.IsNullOrEmpty(model.MatKhau) || model.MatKhau.Length < 6)
                return BadRequest(new { message = "Mật khẩu tối thiểu 6 ký tự." });

            // Ô không bắt buộc để trống thì lưu NULL, không lưu chuỗi rỗng.
            static string? RongLaNull(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
            var cccd = RongLaNull(model.Cccd);
            // Báo lỗi dễ hiểu thay vì để vấp index UX_KhachThue_CCCD thành lỗi 500.
            if (cccd != null && await _context.KhachThues.AnyAsync(k => k.Cccd == cccd))
                return BadRequest(new { message = $"CCCD {cccd} đã được đăng ký cho khách thuê khác." });

            // 2. Tạo thông tin khách thuê mới trước
            var khachThue = new KhachThue
            {
                HoTen = model.HoTen,
                Email = RongLaNull(model.Email),
                SoDienThoai = RongLaNull(model.SoDienThoai),
                Cccd = cccd,
                DiaChi = RongLaNull(model.DiaChi)
            };

            _context.KhachThues.Add(khachThue);
            await _context.SaveChangesAsync();

            // 3. Tạo tài khoản đăng nhập liên kết
            var taiKhoan = new TaiKhoan
            {
                TenDangNhap = model.TenDangNhap,
                MatKhauHash = BCrypt.Net.BCrypt.HashPassword(model.MatKhau), // hash, không lưu thô
                VaiTro = "Customer",
                TrangThai = "Active",
                NgayTao = DateTime.Now,
                MaKhach = khachThue.MaKhach // Hoặc MaKhachThue tùy theo bảng của bạn
            };

            _context.TaiKhoans.Add(taiKhoan);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Đăng ký thành công" });
        }
        catch (Exception ex)
        {
            // Trả thẳng lỗi chi tiết về cho App Flutter hiển thị lên màn hình
            return StatusCode(500, new { message = "Lỗi Server: " + (ex.InnerException?.Message ?? ex.Message) });
        }
    }

    // --- Các Model dùng để nhận dữ liệu từ Client ---
    public class RegisterModel
    {
        public string TenDangNhap { get; set; }
        public string MatKhau { get; set; }
        public string HoTen { get; set; }
        public string Email { get; set; }
        public string SoDienThoai { get; set; }
        public string Cccd { get; set; }
        public string DiaChi { get; set; }
    }

    public class LoginModel
    {
        public string Username { get; set; }
        public string Password { get; set; }
    }
}