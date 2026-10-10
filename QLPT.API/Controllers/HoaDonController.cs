using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLPT.API.Models;
using QLPT.API.Services;

namespace QLPT.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class HoaDonController : ControllerBase
    {
        private readonly Klcn052QuanLyPhongTroContext _context;

        public HoaDonController(Klcn052QuanLyPhongTroContext context)
        {
            _context = context;
        }

        // 1. Lấy danh sách toàn bộ hóa đơn
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var list = await _context.HoaDons
                    .Include(h => h.MaHopDongNavigation)
                        .ThenInclude(hd => hd.MaPhongNavigation)
                    .Include(h => h.MaHopDongNavigation)
                        .ThenInclude(hd => hd.MaKhachDaiDienNavigation)
                    .Include(h => h.ChiTietHoaDons)
                    .OrderByDescending(h => h.Nam).ThenByDescending(h => h.Thang)
                    .ToListAsync();
                return Ok(list);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Lỗi server: {ex.Message}" });
            }
        }

        // 2. Lấy hóa đơn theo Mã Hợp Đồng (Cho App Mobile của khách thuê)
        [HttpGet("hop-dong/{maHopDong}")]
        public async Task<IActionResult> GetByHopDong(int maHopDong)
        {
            try
            {
                var list = await _context.HoaDons
                    .Where(h => h.MaHopDong == maHopDong)
                    .Include(h => h.ChiTietHoaDons)
                    .OrderByDescending(h => h.Nam).ThenByDescending(h => h.Thang)
                    .ToListAsync();
                return Ok(list);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Lỗi server: {ex.Message}" });
            }
        }

        // Xem trước hóa đơn kỳ: chỉ tính, không lưu. Dùng để soát số trước khi phát hành.
        [HttpGet("xem-truoc")]
        public async Task<IActionResult> XemTruoc([FromQuery] int maHopDong, [FromQuery] int thang, [FromQuery] int nam)
        {
            var hd = await _context.HopDongs.FindAsync(maHopDong);
            if (hd == null) return NotFound(new { success = false, message = $"Không tìm thấy hợp đồng #{maHopDong}." });
            if (thang is < 1 or > 12) return BadRequest(new { success = false, message = "Tháng phải từ 1 đến 12." });

            var kq = await HoaDonBuilder.TinhAsync(_context, hd, thang, nam);
            if (kq.Loi != null) return BadRequest(new { success = false, message = kq.Loi });
            return Ok(new
            {
                success  = true,
                tongTien = kq.TongTien,
                daCoHoaDon = await _context.HoaDons.AnyAsync(h => h.MaHopDong == maHopDong && h.Thang == thang && h.Nam == nam),
                chiTiet  = kq.ChiTiet.Select(c => new { c.NoiDung, c.SoLuong, c.DonGia, c.ThanhTien })
            });
        }

        // 3. Lập hóa đơn tự động từ chỉ số đã ghi + dịch vụ đã đăng ký.
        [HttpPost("generate")]
        public async Task<IActionResult> Generate([FromBody] LapHoaDonDto dto)
        {
            if (!dto.TryGetKy(out int thang, out int nam, out string? loiKy))
                return BadRequest(new { success = false, message = loiKy });

            var hd = await _context.HopDongs.FindAsync(dto.MaHopDong);
            if (hd == null) return NotFound(new { success = false, message = $"Không tìm thấy hợp đồng #{dto.MaHopDong}." });

            try
            {
                var (hoaDon, loi) = await HoaDonBuilder.LapAsync(_context, hd, thang, nam);
                if (loi != null) return BadRequest(new { success = false, message = loi });

                await _context.SaveChangesAsync();
                return Ok(new
                {
                    success = true,
                    message = $"Đã lập hóa đơn tháng {thang}/{nam}: {NghiepVu.Tien(hoaDon!.TongTien)}.",
                    data = HoaDonBuilder.ToDto(hoaDon)
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Lỗi server: {ex.InnerException?.Message ?? ex.Message}" });
            }
        }

        // 4. Lập hóa đơn nhập tay tổng tiền (khi không có chỉ số hoặc cần điều chỉnh).
        [HttpPost]
        public async Task<IActionResult> CreateManual([FromBody] LapHoaDonDto dto)
        {
            if (!dto.TryGetKy(out int thang, out int nam, out string? loiKy))
                return BadRequest(new { success = false, message = loiKy });
            if (dto.TongTien is not > 0)
                return BadRequest(new { success = false, message = "Tổng tiền phải lớn hơn 0." });

            var hd = await _context.HopDongs.FindAsync(dto.MaHopDong);
            if (hd == null) return NotFound(new { success = false, message = $"Không tìm thấy hợp đồng #{dto.MaHopDong}." });
            if (hd.TrangThai != NghiepVu.HopDong.HieuLuc)
                return BadRequest(new { success = false, message = $"Hợp đồng #{hd.MaHopDong} không còn hiệu lực." });
            if (await _context.HoaDons.AnyAsync(h => h.MaHopDong == hd.MaHopDong && h.Thang == thang && h.Nam == nam))
                return BadRequest(new { success = false, message = $"Hóa đơn tháng {thang}/{nam} của hợp đồng #{hd.MaHopDong} đã tồn tại." });

            try
            {
                string noiDung = string.IsNullOrWhiteSpace(dto.GhiChu)
                    ? $"Tiền phòng và dịch vụ tháng {thang}/{nam}"
                    : dto.GhiChu.Trim();
                var hoaDon = HoaDonBuilder.TaoHoaDon(_context, hd.MaHopDong, thang, nam, new[]
                {
                    new ChiTietHoaDon { NoiDung = noiDung, SoLuong = 1, DonGia = dto.TongTien, ThanhTien = dto.TongTien }
                });
                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = "Đã lập hóa đơn.", data = HoaDonBuilder.ToDto(hoaDon) });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Lỗi server: {ex.InnerException?.Message ?? ex.Message}" });
            }
        }

        // 5. Thu tiền: ghi ThanhToan, cập nhật còn nợ và ghi phiếu thu vào sổ thu chi
        //    (đúng quy ước dữ liệu đang có: "Thu tiền hóa đơn tháng 9/2026 - Phòng A101").
        [HttpPut("{id}/thu-tien")]
        public async Task<IActionResult> ThuTien(int id, [FromBody] ThuTienDto dto)
        {
            var hoaDon = await _context.HoaDons
                .Include(h => h.MaHopDongNavigation).ThenInclude(hd => hd.MaPhongNavigation)
                .Include(h => h.MaHopDongNavigation).ThenInclude(hd => hd.MaKhachDaiDienNavigation)
                .FirstOrDefaultAsync(h => h.MaHoaDon == id);
            if (hoaDon == null) return NotFound(new { success = false, message = "Không tìm thấy hóa đơn." });

            decimal tong = hoaDon.TongTien ?? 0;
            decimal daThu = hoaDon.DaThanhToan ?? 0;
            decimal conLai = hoaDon.ConLai ?? (tong - daThu);

            if (hoaDon.TrangThai == NghiepVu.HoaDon.DaThanhToan || conLai <= 0)
                return BadRequest(new { success = false, message = "Hóa đơn này đã thanh toán đủ." });
            if (dto.SoTienThu <= 0)
                return BadRequest(new { success = false, message = "Số tiền thu phải lớn hơn 0." });
            if (dto.SoTienThu > conLai)
                return BadRequest(new { success = false, message = $"Số tiền thu vượt quá số còn lại ({NghiepVu.Tien(conLai)})." });

            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                _context.ThanhToans.Add(new ThanhToan
                {
                    MaHoaDon      = hoaDon.MaHoaDon,
                    NgayThanhToan = DateTime.Now,
                    SoTien        = dto.SoTienThu,
                    PhuongThuc    = string.IsNullOrWhiteSpace(dto.PhuongThuc) ? "Tiền mặt" : dto.PhuongThuc.Trim(),
                    GhiChu        = dto.GhiChu
                });

                hoaDon.DaThanhToan = daThu + dto.SoTienThu;
                hoaDon.ConLai      = conLai - dto.SoTienThu;
                hoaDon.TrangThai   = hoaDon.ConLai <= 0 ? NghiepVu.HoaDon.DaThanhToan : NghiepVu.HoaDon.MotPhan;

                var hopDong = hoaDon.MaHopDongNavigation;
                NghiepVu.GhiPhieu(_context, NghiepVu.PhieuThu,
                    $"Thu tiền hóa đơn tháng {hoaDon.Thang}/{hoaDon.Nam} - {hopDong?.MaPhongNavigation?.TenPhong ?? $"HĐ #{hoaDon.MaHopDong}"}",
                    dto.SoTienThu, hopDong?.MaKhachDaiDienNavigation?.HoTen);

                await _context.SaveChangesAsync();
                await tx.CommitAsync();
                return Ok(new
                {
                    success = true,
                    message = hoaDon.ConLai <= 0
                        ? "Đã thu đủ, hóa đơn chuyển sang 'Đã thanh toán'."
                        : $"Đã thu {NghiepVu.Tien(dto.SoTienThu)}, còn lại {NghiepVu.Tien(hoaDon.ConLai)}.",
                    data = new { hoaDon.MaHoaDon, hoaDon.DaThanhToan, hoaDon.ConLai, hoaDon.TrangThai }
                });
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return StatusCode(500, new { success = false, message = $"Lỗi server: {ex.InnerException?.Message ?? ex.Message}" });
            }
        }

        // 6. Luồng OCR từ WinForms: ghi (hoặc cập nhật) chỉ số điện nước của kỳ rồi lập hóa đơn.
        //    Nay dùng chung HoaDonBuilder nên không còn tính điện/nước hai lần, và cập nhật
        //    chỉ số đã ghi trước đó thay vì vấp index duy nhất Phòng-Loại-Kỳ.
        [HttpPost("tinh-tien-ocr")]
        public async Task<IActionResult> TinhTienOcr([FromBody] OcrRequestDto model)
        {
            if (model == null) return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ." });
            if (model.Thang is < 1 or > 12) return BadRequest(new { success = false, message = "Tháng phải từ 1 đến 12." });
            if (model.ChiSoDienMoi < model.ChiSoDienCu)
                return BadRequest(new { success = false, message = "Chỉ số điện mới không được nhỏ hơn chỉ số cũ." });
            if (model.ChiSoNuocMoi < model.ChiSoNuocCu)
                return BadRequest(new { success = false, message = "Chỉ số nước mới không được nhỏ hơn chỉ số cũ." });

            var hopDong = await _context.HopDongs
                .FirstOrDefaultAsync(h => h.MaPhong == model.MaPhong && h.TrangThai == NghiepVu.HopDong.HieuLuc);
            if (hopDong == null)
                return BadRequest(new { success = false, message = $"Phòng {model.MaPhong} hiện không có hợp đồng đang hiệu lực." });
            if (await _context.HoaDons.AnyAsync(h => h.MaHopDong == hopDong.MaHopDong && h.Thang == model.Thang && h.Nam == model.Nam))
                return BadRequest(new { success = false, message = $"Hóa đơn tháng {model.Thang}/{model.Nam} của phòng này đã tồn tại rồi!" });

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var today = DateOnly.FromDateTime(DateTime.Now);
                await GhiChiSoAsync(model.MaPhong, "Điện", model.Thang, model.Nam, model.ChiSoDienCu, model.ChiSoDienMoi,
                                    model.ChiSoDienOCR, model.DuongDanAnhDien, today);
                await GhiChiSoAsync(model.MaPhong, "Nước", model.Thang, model.Nam, model.ChiSoNuocCu, model.ChiSoNuocMoi,
                                    null, null, today);
                await _context.SaveChangesAsync(); // để HoaDonBuilder đọc được chỉ số vừa ghi

                var (hoaDon, loi) = await HoaDonBuilder.LapAsync(_context, hopDong, model.Thang, model.Nam,
                    model.DonGiaDien > 0 ? model.DonGiaDien : null,
                    model.DonGiaNuoc > 0 ? model.DonGiaNuoc : null);
                if (loi != null)
                {
                    await transaction.RollbackAsync();
                    return BadRequest(new { success = false, message = loi });
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new
                {
                    success = true,
                    message = "Lập hóa đơn, lưu chỉ số điện nước thành công!",
                    maHoaDon = hoaDon!.MaHoaDon,
                    tongTien = hoaDon.TongTien,
                    chiTiet = hoaDon.ChiTietHoaDons.Select(c => new { c.NoiDung, c.SoLuong, c.DonGia, c.ThanhTien })
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { success = false, message = $"Lỗi server: {ex.InnerException?.Message ?? ex.Message}" });
            }
        }

        private async Task GhiChiSoAsync(int maPhong, string loai, int thang, int nam, decimal cu, decimal moi,
                                         decimal? ocr, string? anh, DateOnly ngay)
        {
            var cs = await _context.ChiSoDienNuocs
                .FirstOrDefaultAsync(c => c.MaPhong == maPhong && c.LoaiChiSo == loai && c.Thang == thang && c.Nam == nam);
            if (cs == null)
            {
                cs = new ChiSoDienNuoc { MaPhong = maPhong, LoaiChiSo = loai, Thang = thang, Nam = nam };
                _context.ChiSoDienNuocs.Add(cs);
            }
            cs.ChiSoCu      = cu;
            cs.ChiSoMoi     = moi;
            cs.ChiSoXacNhan = moi;
            cs.NgayGhi      = ngay;
            if (ocr.HasValue) cs.ChiSoOcr = ocr;
            if (anh != null)  cs.DuongDanAnh = anh;
        }
    }

    // DTO nhận dữ liệu từ WinForm (Điện có OCR riêng, Nước nhập thủ công)
    public class OcrRequestDto
    {
        public int MaPhong { get; set; }
        public int Thang { get; set; }
        public int Nam { get; set; }

        // Điện (Hỗ trợ OCR)
        public decimal ChiSoDienCu { get; set; }
        public decimal ChiSoDienMoi { get; set; }
        public decimal ChiSoDienOCR { get; set; }
        public string? DuongDanAnhDien { get; set; }
        public decimal DonGiaDien { get; set; }

        // Nước (Nhập thủ công)
        public decimal ChiSoNuocCu { get; set; }
        public decimal ChiSoNuocMoi { get; set; }
        public decimal DonGiaNuoc { get; set; }
    }

    /// <summary>Kỳ hóa đơn nhận được ở dạng "yyyy-MM" (WPF) hoặc tách Thang/Nam.</summary>
    public class LapHoaDonDto
    {
        public int MaHopDong { get; set; }
        public string? ThangNam { get; set; }
        public int? Thang { get; set; }
        public int? Nam { get; set; }
        public decimal? TongTien { get; set; }
        public string? GhiChu { get; set; }

        public bool TryGetKy(out int thang, out int nam, out string? loi)
        {
            thang = Thang ?? 0; nam = Nam ?? 0; loi = null;
            if (!string.IsNullOrWhiteSpace(ThangNam))
            {
                var p = ThangNam.Trim().Split('-', '/');
                if (p.Length != 2 || !int.TryParse(p[0], out int a) || !int.TryParse(p[1], out int b))
                { loi = "Kỳ hóa đơn phải có dạng yyyy-MM."; return false; }
                // chấp nhận cả "2026-09" và "09/2026"
                (nam, thang) = a > 12 ? (a, b) : (b, a);
            }
            if (MaHopDong <= 0) { loi = "Thiếu mã hợp đồng."; return false; }
            if (thang is < 1 or > 12 || nam < 2000) { loi = "Kỳ hóa đơn không hợp lệ."; return false; }
            return true;
        }
    }

    public class ThuTienDto
    {
        public decimal SoTienThu { get; set; }
        public string? PhuongThuc { get; set; }
        public string? GhiChu { get; set; }
    }
}
