using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using System.Security.Claims;
using QLPT.API.Models;

namespace QLPT.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class KhachThueController : ControllerBase
    {
        private readonly Klcn052QuanLyPhongTroContext _context;

        public KhachThueController(Klcn052QuanLyPhongTroContext context)
        {
            _context = context;
        }

        // Lấy danh sách toàn bộ khách thuê
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var query = _context.KhachThues.AsNoTracking().AsQueryable();
                if (User.IsInRole("Manager") && int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int managerId))
                    query = query.Where(k => k.MaCoSoTiepNhan.HasValue && _context.QuanLyCoSos.Any(a =>
                            a.MaTaiKhoan == managerId && a.MaCoSo == k.MaCoSoTiepNhan.Value) ||
                        _context.DatPhongs.Any(d => d.MaKhach == k.MaKhach && d.MaPhongNavigation.MaCoSo.HasValue &&
                            _context.QuanLyCoSos.Any(a => a.MaTaiKhoan == managerId && a.MaCoSo == d.MaPhongNavigation.MaCoSo.Value)) ||
                        _context.HopDongs.Any(h => h.MaKhachDaiDien == k.MaKhach && h.MaPhongNavigation.MaCoSo.HasValue &&
                        _context.QuanLyCoSos.Any(a => a.MaTaiKhoan == managerId && a.MaCoSo == h.MaPhongNavigation.MaCoSo.Value)) ||
                        _context.CuTrus.Any(c => c.MaKhach == k.MaKhach && c.MaHopDongNavigation.MaPhongNavigation.MaCoSo.HasValue &&
                            _context.QuanLyCoSos.Any(a => a.MaTaiKhoan == managerId && a.MaCoSo == c.MaHopDongNavigation.MaPhongNavigation.MaCoSo.Value)));
                var list = await query.OrderBy(k => k.HoTen).ThenBy(k => k.MaKhach).ToListAsync();
                return Ok(list);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Lỗi server: {ex.Message}" });
            }
        }

        // Lấy thông tin chi tiết 1 khách thuê theo mã
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                var khach = await _context.KhachThues.FindAsync(id);
                if (khach == null) return NotFound(new { success = false, message = "Không tìm thấy khách thuê" });
                return Ok(khach);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Lỗi server: {ex.Message}" });
            }
        }

        [HttpGet("{id:int}/lich-su/{kind}")]
        public async Task<IActionResult> GetHistory(int id, string kind)
        {
            if (id <= 0) return BadRequest(new { message = "Mã khách thuê không hợp lệ." });
            if (kind is not ("thanh-toan" or "dien-nuoc" or "dich-vu"))
                return BadRequest(new { message = "Loại lịch sử phải là thanh-toan, dien-nuoc hoặc dich-vu." });
            if (!await _context.KhachThues.AnyAsync(k => k.MaKhach == id)) return NotFound(new { message = "Không tìm thấy khách thuê." });
            if (kind == "thanh-toan")
                return Ok(await _context.ThanhToans.AsNoTracking()
                    .Where(t => t.MaHoaDonNavigation.MaHopDongNavigation.MaKhachDaiDien == id)
                    .OrderByDescending(t => t.NgayThanhToan)
                    .Select(t => new { t.MaThanhToan, t.MaHoaDon, t.NgayThanhToan, t.SoTien, t.PhuongThuc, t.GhiChu,
                        TenPhong = t.MaHoaDonNavigation.MaHopDongNavigation.MaPhongNavigation.TenPhong }).ToListAsync());
            if (kind == "dich-vu")
                return Ok(await _context.DangKyDichVus.AsNoTracking().Where(d => d.MaHopDongNavigation.MaKhachDaiDien == id)
                    .OrderByDescending(d => d.NgayBatDau)
                    .Select(d => new { d.MaDangKy, d.MaHopDong, d.NgayBatDau, d.NgayKetThuc, d.TrangThai,
                        DonGia = d.DonGiaApDung, TenDichVu = d.MaDichVuNavigation.TenDichVu, d.MaDichVuNavigation.DonViTinh,
                        TenPhong = d.MaHopDongNavigation.MaPhongNavigation.TenPhong }).ToListAsync());
            // Limit room readings to the customer's rental periods, so changing rooms
            // does not expose every historic meter record for that room.
            return Ok(await _context.ChiSoDienNuocs.AsNoTracking().Where(c => _context.HopDongs.Any(h =>
                    h.MaKhachDaiDien == id && h.MaPhong == c.MaPhong &&
                    c.Nam * 12 + c.Thang >= h.NgayBatDau.Year * 12 + h.NgayBatDau.Month &&
                    c.Nam * 12 + c.Thang <= h.NgayKetThuc.Year * 12 + h.NgayKetThuc.Month &&
                    (h.ThanhLy == null || h.ThanhLy.NgayThanhLy == null || c.Nam * 12 + c.Thang <= h.ThanhLy.NgayThanhLy.Value.Year * 12 + h.ThanhLy.NgayThanhLy.Value.Month)))
                .OrderByDescending(c => c.Nam).ThenByDescending(c => c.Thang)
                .Select(c => new { c.MaChiSo, c.LoaiChiSo, c.Thang, c.Nam, c.ChiSoCu, c.ChiSoMoi, c.NgayGhi,
                    TieuThu = (c.ChiSoMoi ?? c.ChiSoXacNhan ?? 0) - (c.ChiSoCu ?? 0), TenPhong = c.MaPhongNavigation.TenPhong }).ToListAsync());
        }

        // Thêm mới khách thuê
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] KhachThue model)
        {
            try
            {
                if (model == null) return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ" });
                var loi = KiemTraThongTin(model.HoTen, model.Cccd, model.NgaySinh, model.GioiTinh,
                    model.SoDienThoai, model.Email, model.DiaChi, batBuocTen: true);
                if (loi != null) return BadRequest(new { success = false, message = loi });
                if (string.IsNullOrWhiteSpace(model.Cccd) || string.IsNullOrWhiteSpace(model.SoDienThoai)
                    || string.IsNullOrWhiteSpace(model.DiaChi))
                    return BadRequest(new { success = false, message = "Cần nhập CCCD, số điện thoại và địa chỉ khách thuê." });
                string? cccd = string.IsNullOrWhiteSpace(model.Cccd) ? null : model.Cccd.Trim();
                string? soDienThoai = model.SoDienThoai?.Trim();
                string? email = ChuanHoaTuyChon(model.Email);
                var trung = await KiemTraTrungAsync(null, cccd, soDienThoai, email);
                if (trung != null) return BadRequest(new { success = false, message = trung });
                if (User.IsInRole("Manager") && (!model.MaCoSoTiepNhan.HasValue ||
                    !int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int managerId) ||
                    !await _context.QuanLyCoSos.AnyAsync(a => a.MaTaiKhoan == managerId && a.MaCoSo == model.MaCoSoTiepNhan.Value)))
                    return StatusCode(403, new { message = "Chỉ được tiếp nhận khách tại cơ sở được phân công." });
                model = new KhachThue
                {
                    HoTen = model.HoTen.Trim(), Cccd = cccd, NgaySinh = model.NgaySinh,
                    GioiTinh = model.GioiTinh?.Trim(), SoDienThoai = soDienThoai,
                    Email = email, DiaChi = model.DiaChi?.Trim(), MaCoSoTiepNhan = model.MaCoSoTiepNhan
                };
                _context.KhachThues.Add(model);
                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = "Thêm khách thuê thành công", data = model });
            }
            catch (DbUpdateException ex) when (LaLoiTrungDuLieu(ex))
            {
                return Conflict(new { success = false, message = "CCCD, số điện thoại hoặc email đã được dùng cho khách thuê khác. Vui lòng kiểm tra lại." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Lỗi server: {ex.Message}" });
            }
        }

        // Khách đã có hồ sơ ở cơ sở khác: dùng đúng mã khách cũ, không tạo trùng CCCD.
        [HttpPost("tiep-nhan")]
        public async Task<IActionResult> TiepNhan([FromBody] TiepNhanKhachDto dto)
        {
            if (dto.MaCoSo <= 0 || !System.Text.RegularExpressions.Regex.IsMatch(dto.Cccd?.Trim() ?? "", @"^\d{12}$"))
                return BadRequest(new { message = "Chọn cơ sở và nhập đúng CCCD 12 chữ số." });
            if (User.IsInRole("Manager") && (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int managerId) ||
                !await _context.QuanLyCoSos.AnyAsync(a => a.MaTaiKhoan == managerId && a.MaCoSo == dto.MaCoSo)))
                return StatusCode(403, new { message = "Cơ sở tiếp nhận không thuộc phạm vi quản lý." });
            string cccd = dto.Cccd!.Trim();
            var customer = await _context.KhachThues.SingleOrDefaultAsync(k => k.Cccd == cccd);
            if (customer == null) return NotFound(new { message = "Không thấy hồ sơ có CCCD này. Hãy thêm khách mới." });
            customer.MaCoSoTiepNhan = dto.MaCoSo;
            await _context.SaveChangesAsync();
            return Ok(new { success = true, customer.MaKhach, customer.HoTen,
                message = "Đã tiếp nhận hồ sơ khách cũ tại cơ sở được chọn; lịch sử thuê ở cơ sở trước vẫn giữ nguyên." });
        }

        // Sửa thông tin khách thuê.
        // DTO toàn trường nullable để cập nhật từng phần; bind thẳng entity sẽ bị
        // ApiController bắt buộc gửi HoTen.
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] KhachThueUpdateDto model)
        {
            try
            {
                if (model.Provided.Contains(nameof(model.HoTen)) && string.IsNullOrWhiteSpace(model.HoTen))
                    return BadRequest(new { message = "Tên khách thuê không được để trống." });
                if (model.Provided.Contains(nameof(model.Cccd)) && string.IsNullOrWhiteSpace(model.Cccd)
                    || model.Provided.Contains(nameof(model.SoDienThoai)) && string.IsNullOrWhiteSpace(model.SoDienThoai)
                    || model.Provided.Contains(nameof(model.DiaChi)) && string.IsNullOrWhiteSpace(model.DiaChi))
                    return BadRequest(new { message = "CCCD, số điện thoại và địa chỉ khách thuê không được để trống." });
                var loi = KiemTraThongTin(model.HoTen, model.Cccd, model.NgaySinh, model.GioiTinh,
                    model.SoDienThoai, model.Email, model.DiaChi, batBuocTen: false);
                if (loi != null) return BadRequest(new { success = false, message = loi });
                var item = await _context.KhachThues.FindAsync(id);
                if (item == null) return NotFound(new { success = false, message = "Không tìm thấy khách thuê" });
                var oldValues = new Dictionary<string, string?>
                {
                    ["Họ tên"] = item.HoTen, ["CCCD"] = item.Cccd,
                    ["Ngày sinh"] = item.NgaySinh?.ToString("yyyy-MM-dd"), ["Giới tính"] = item.GioiTinh,
                    ["Số điện thoại"] = item.SoDienThoai, ["Email"] = item.Email, ["Địa chỉ"] = item.DiaChi
                };

                string? cccd = string.IsNullOrWhiteSpace(model.Cccd) ? null : model.Cccd.Trim();
                string? soDienThoai = ChuanHoaTuyChon(model.SoDienThoai);
                string? email = ChuanHoaTuyChon(model.Email);
                var trung = await KiemTraTrungAsync(id,
                    model.Provided.Contains(nameof(model.Cccd)) ? cccd : null,
                    model.Provided.Contains(nameof(model.SoDienThoai)) ? soDienThoai : null,
                    model.Provided.Contains(nameof(model.Email)) ? email : null);
                if (trung != null) return BadRequest(new { success = false, message = trung });

                item.HoTen       = model.HoTen?.Trim() ?? item.HoTen;
                if (model.Provided.Contains(nameof(model.Cccd))) item.Cccd = cccd;
                if (model.Provided.Contains(nameof(model.NgaySinh))) item.NgaySinh = model.NgaySinh;
                if (model.Provided.Contains(nameof(model.GioiTinh))) item.GioiTinh = ChuanHoaTuyChon(model.GioiTinh);
                if (model.Provided.Contains(nameof(model.SoDienThoai))) item.SoDienThoai = soDienThoai;
                if (model.Provided.Contains(nameof(model.Email))) item.Email = email;
                if (model.Provided.Contains(nameof(model.DiaChi))) item.DiaChi = ChuanHoaTuyChon(model.DiaChi);

                var newValues = new Dictionary<string, string?>
                {
                    ["Họ tên"] = item.HoTen, ["CCCD"] = item.Cccd,
                    ["Ngày sinh"] = item.NgaySinh?.ToString("yyyy-MM-dd"), ["Giới tính"] = item.GioiTinh,
                    ["Số điện thoại"] = item.SoDienThoai, ["Email"] = item.Email, ["Địa chỉ"] = item.DiaChi
                };
                int? actor = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int accountId) ? accountId : null;
                foreach (var (field, oldValue) in oldValues)
                    if (oldValue != newValues[field])
                        _context.LichSuKhachThues.Add(new LichSuKhachThue
                        {
                            MaKhach = id, Truong = field, GiaTriCu = oldValue,
                            GiaTriMoi = newValues[field], NgaySua = DateTime.UtcNow, MaTaiKhoanSua = actor
                        });
                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = "Cập nhật khách thuê thành công" });
            }
            catch (DbUpdateException ex) when (LaLoiTrungDuLieu(ex))
            {
                return Conflict(new { success = false, message = "CCCD, số điện thoại hoặc email đã được dùng cho khách thuê khác. Vui lòng kiểm tra lại." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Lỗi server: {ex.InnerException?.Message ?? ex.Message}" });
            }
        }

        [HttpGet("{id:int}/lich-su-chinh-sua")]
        public async Task<IActionResult> GetEditHistory(int id)
        {
            if (!await _context.KhachThues.AnyAsync(k => k.MaKhach == id)) return NotFound();
            return Ok(await _context.LichSuKhachThues.AsNoTracking()
                .Where(x => x.MaKhach == id).OrderByDescending(x => x.NgaySua).ThenByDescending(x => x.MaLichSu)
                .Select(x => new { x.MaLichSu, x.Truong, x.GiaTriCu, x.GiaTriMoi, x.NgaySua, x.MaTaiKhoanSua })
                .ToListAsync());
        }

        private static string? ChuanHoaTuyChon(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private async Task<string?> KiemTraTrungAsync(int? maKhach, string? cccd, string? soDienThoai, string? email)
        {
            if (cccd != null && await _context.KhachThues.AnyAsync(k =>
                    (!maKhach.HasValue || k.MaKhach != maKhach.Value) && k.Cccd == cccd))
                return "CCCD đã thuộc về khách thuê khác.";
            if (soDienThoai != null && await _context.KhachThues.AnyAsync(k =>
                    (!maKhach.HasValue || k.MaKhach != maKhach.Value) && k.SoDienThoai == soDienThoai))
                return "Số điện thoại đã thuộc về khách thuê khác.";
            if (email != null && await _context.KhachThues.AnyAsync(k =>
                    (!maKhach.HasValue || k.MaKhach != maKhach.Value) && k.Email == email))
                return "Email đã thuộc về khách thuê khác.";
            return null;
        }

        private static bool LaLoiTrungDuLieu(DbUpdateException ex) =>
            ex.InnerException is SqlException { Number: 2601 or 2627 };

        private static string? KiemTraThongTin(string? hoTen, string? cccd, DateOnly? ngaySinh,
            string? gioiTinh, string? soDienThoai, string? email, string? diaChi, bool batBuocTen)
        {
            if ((batBuocTen || hoTen != null) && string.IsNullOrWhiteSpace(hoTen))
                return "Tên khách thuê không được để trống.";
            if (hoTen?.Trim().Length > 100) return "Tên khách thuê tối đa 100 ký tự.";
            if (cccd?.Trim().Length > 20) return "CCCD tối đa 20 ký tự.";
            if (!string.IsNullOrWhiteSpace(cccd) && !System.Text.RegularExpressions.Regex.IsMatch(cccd.Trim(), @"^\d{12}$"))
                return "CCCD phải gồm đúng 12 chữ số.";
            if (gioiTinh?.Trim().Length > 20) return "Giới tính tối đa 20 ký tự.";
            if (soDienThoai?.Trim().Length > 20) return "Số điện thoại tối đa 20 ký tự.";
            if (!string.IsNullOrWhiteSpace(soDienThoai) && !System.Text.RegularExpressions.Regex.IsMatch(soDienThoai.Trim(), @"^0\d{9}$"))
                return "Số điện thoại phải gồm 10 chữ số và bắt đầu bằng 0.";
            if (email?.Trim().Length > 100) return "Email tối đa 100 ký tự.";
            if (diaChi?.Trim().Length > 300) return "Địa chỉ tối đa 300 ký tự.";
            if (ngaySinh == default(DateOnly) || ngaySinh > DateOnly.FromDateTime(DateTime.Today))
                return "Ngày sinh không hợp lệ hoặc sau hôm nay.";
            if (!string.IsNullOrWhiteSpace(email) && (!System.Net.Mail.MailAddress.TryCreate(email.Trim(), out var diaChiMail)
                || diaChiMail.Address != email.Trim()))
                return "Email không hợp lệ.";
            return null;
        }
    }

    public class KhachThueUpdateDto
    {
        [System.Text.Json.Serialization.JsonIgnore]
        public HashSet<string> Provided { get; } = new();
        string? name, cccd, gender, phone, email, address;
        DateOnly? birthday;
        public string? HoTen { get => name; set { name = value; Provided.Add(nameof(HoTen)); } }
        public string? Cccd { get => cccd; set { cccd = value; Provided.Add(nameof(Cccd)); } }
        public DateOnly? NgaySinh { get => birthday; set { birthday = value; Provided.Add(nameof(NgaySinh)); } }
        public string? GioiTinh { get => gender; set { gender = value; Provided.Add(nameof(GioiTinh)); } }
        public string? SoDienThoai { get => phone; set { phone = value; Provided.Add(nameof(SoDienThoai)); } }
        public string? Email { get => email; set { email = value; Provided.Add(nameof(Email)); } }
        public string? DiaChi { get => address; set { address = value; Provided.Add(nameof(DiaChi)); } }
    }

    public sealed class TiepNhanKhachDto
    {
        public string Cccd { get; set; } = "";
        public int MaCoSo { get; set; }
    }
}
