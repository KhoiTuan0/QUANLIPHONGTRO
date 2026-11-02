using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QLPT.API.Models;

[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly Klcn052QuanLyPhongTroContext _context;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        Klcn052QuanLyPhongTroContext context,
        ILogger<AuthController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginModel? model)
    {
        if (model == null ||
            string.IsNullOrWhiteSpace(model.Username) ||
            string.IsNullOrWhiteSpace(model.Password))
        {
            return BadRequest(new
            {
                success = false,
                message = "Vui lòng nhập tên đăng nhập và mật khẩu."
            });
        }

        var username = model.Username.Trim();

        var user = await _context.TaiKhoans
            .FirstOrDefaultAsync(u => u.TenDangNhap == username);

        // Dùng cùng thông báo cho tên đăng nhập và mật khẩu sai để tránh
        // tiết lộ tài khoản có tồn tại hay không.
        if (user == null || !VerifyPassword(model.Password, user.MatKhauHash))
        {
            return Unauthorized(new
            {
                success = false,
                message = "Tên đăng nhập hoặc mật khẩu không đúng."
            });
        }

        if (!string.Equals(
                user.TrangThai,
                "Active",
                StringComparison.OrdinalIgnoreCase))
        {
            return Unauthorized(new
            {
                success = false,
                message = "Tài khoản đã bị khóa."
            });
        }

        var role = NormalizeRole(user.VaiTro);
        if (role == null)
        {
            return Unauthorized(new
            {
                success = false,
                message = "Tài khoản chưa được gán vai trò hợp lệ."
            });
        }

        // API đăng nhập hiện không phát hành JWT.
        return Ok(new
        {
            success = true,
            message = "Đăng nhập thành công",
            maTaiKhoan = user.MaTaiKhoan,
            vaiTro = role,
            maKhach = user.MaKhach
        });
    }

    // Hỗ trợ BCrypt và bản ghi cũ đang lưu mật khẩu dạng thô.
    // Tài khoản mới luôn lưu mật khẩu bằng BCrypt.
    private static bool VerifyPassword(string? password, string? stored)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(stored))
        {
            return false;
        }

        if (stored.StartsWith("$2", StringComparison.Ordinal))
        {
            try
            {
                return BCrypt.Net.BCrypt.Verify(password, stored);
            }
            catch
            {
                return false;
            }
        }

        return password == stored;
    }

    private static string? NormalizeRole(string? role)
    {
        var normalized = role?.Trim().ToLowerInvariant();

        if (normalized is "admin" or "landlord" or "chủ trọ" or "chu tro")
        {
            return "Admin";
        }

        if (normalized is "customer" or "tenant" or
            "khách thuê" or "khach thue" or
            "khách hàng" or "khach hang")
        {
            return "Customer";
        }

        return null;
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        // API không lưu phiên phía server; client tự xử lý đăng xuất.
        return Ok(new
        {
            success = true,
            message = "Đã đăng xuất"
        });
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterModel? model)
    {
        if (model == null)
        {
            return BadRequest(new
            {
                success = false,
                message = "Dữ liệu đăng ký không hợp lệ."
            });
        }

        var tenDangNhap = model.TenDangNhap?.Trim();
        var hoTen = model.HoTen?.Trim();

        if (string.IsNullOrWhiteSpace(tenDangNhap))
        {
            return BadRequest(new
            {
                success = false,
                message = "Vui lòng nhập tên đăng nhập."
            });
        }

        if (string.IsNullOrWhiteSpace(hoTen))
        {
            return BadRequest(new
            {
                success = false,
                message = "Vui lòng nhập họ tên khách thuê."
            });
        }

        if (string.IsNullOrEmpty(model.MatKhau) || model.MatKhau.Length < 6)
        {
            return BadRequest(new
            {
                success = false,
                message = "Mật khẩu tối thiểu 6 ký tự."
            });
        }

        var existingUser = await _context.TaiKhoans
            .AnyAsync(u => u.TenDangNhap == tenDangNhap);

        if (existingUser)
        {
            return BadRequest(new
            {
                success = false,
                message = "Tên đăng nhập đã tồn tại!"
            });
        }

        static string? RongLaNull(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        var email = RongLaNull(model.Email);
        var soDienThoai = RongLaNull(model.SoDienThoai);
        var cccd = RongLaNull(model.Cccd);
        var diaChi = RongLaNull(model.DiaChi);

        if (cccd != null &&
            await _context.KhachThues.AnyAsync(k => k.Cccd == cccd))
        {
            return BadRequest(new
            {
                success = false,
                message = $"CCCD {cccd} đã được đăng ký cho khách thuê khác."
            });
        }

        await using var transaction =
            await _context.Database.BeginTransactionAsync();

        try
        {
            var khachThue = new KhachThue
            {
                HoTen = hoTen,
                Email = email,
                SoDienThoai = soDienThoai,
                Cccd = cccd,
                DiaChi = diaChi
            };

            _context.KhachThues.Add(khachThue);
            await _context.SaveChangesAsync();

            var taiKhoan = new TaiKhoan
            {
                TenDangNhap = tenDangNhap,
                MatKhauHash = BCrypt.Net.BCrypt.HashPassword(model.MatKhau),
                VaiTro = "Customer",
                TrangThai = "Active",
                NgayTao = DateTime.Now,
                MaKhach = khachThue.MaKhach
            };

            _context.TaiKhoans.Add(taiKhoan);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            return Ok(new
            {
                success = true,
                message = "Đăng ký thành công",
                maKhach = khachThue.MaKhach,
                maTaiKhoan = taiKhoan.MaTaiKhoan
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không thể tạo khách thuê và tài khoản mới.");

            await transaction.RollbackAsync();

            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                success = false,
                message = "Không thể tạo tài khoản lúc này. Vui lòng thử lại."
            });
        }
    }

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordModel? model)
    {
        if (model == null ||
            model.MaKhach <= 0 ||
            string.IsNullOrWhiteSpace(model.MatKhauHienTai) ||
            string.IsNullOrWhiteSpace(model.MatKhauMoi))
        {
            return BadRequest(new
            {
                success = false,
                message = "Vui lòng nhập đầy đủ thông tin."
            });
        }

        if (model.MatKhauMoi.Length < 6)
        {
            return BadRequest(new
            {
                success = false,
                message = "Mật khẩu mới tối thiểu 6 ký tự."
            });
        }

        if (model.MatKhauHienTai == model.MatKhauMoi)
        {
            return BadRequest(new
            {
                success = false,
                message = "Mật khẩu mới phải khác mật khẩu hiện tại."
            });
        }

        var taiKhoan = await _context.TaiKhoans.FirstOrDefaultAsync(x =>
            x.MaKhach == model.MaKhach &&
            x.VaiTro == "Customer");

        if (taiKhoan == null ||
            !string.Equals(
                taiKhoan.TrangThai,
                "Active",
                StringComparison.OrdinalIgnoreCase))
        {
            return NotFound(new
            {
                success = false,
                message = "Không tìm thấy tài khoản khách đang hoạt động."
            });
        }

        if (!VerifyPassword(model.MatKhauHienTai, taiKhoan.MatKhauHash))
        {
            return Unauthorized(new
            {
                success = false,
                message = "Mật khẩu hiện tại không đúng."
            });
        }

        try
        {
            taiKhoan.MatKhauHash =
                BCrypt.Net.BCrypt.HashPassword(model.MatKhauMoi);

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Đổi mật khẩu thành công."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Không thể đổi mật khẩu cho khách thuê {MaKhach}.",
                model.MaKhach);

            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                success = false,
                message = "Không thể đổi mật khẩu lúc này. Vui lòng thử lại."
            });
        }
    }

    public class RegisterModel
    {
        public string? TenDangNhap { get; set; }
        public string? MatKhau { get; set; }
        public string? HoTen { get; set; }
        public string? Email { get; set; }
        public string? SoDienThoai { get; set; }
        public string? Cccd { get; set; }
        public string? DiaChi { get; set; }
    }

    public class LoginModel
    {
        public string? Username { get; set; }
        public string? Password { get; set; }
    }

    public class ChangePasswordModel
    {
        public int MaKhach { get; set; }
        public string? MatKhauHienTai { get; set; }
        public string? MatKhauMoi { get; set; }
    }
}