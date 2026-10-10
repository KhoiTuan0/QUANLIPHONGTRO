using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLPT.API.Models;
using QLPT.API.Services;

namespace QLPT.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DatPhongController : ControllerBase
    {
        private readonly Klcn052QuanLyPhongTroContext _context;

        public DatPhongController(Klcn052QuanLyPhongTroContext context)
        {
            _context = context;
        }

        // Lấy danh sách phiếu đặt phòng (kèm thông tin phòng và khách)
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var list = await _context.DatPhongs
                    .Include(d => d.MaPhongNavigation)
                    .Include(d => d.MaKhachNavigation)
                    .Select(d => new {
                        maDatPhong = d.MaDatPhong,
                        maPhong = d.MaPhong,
                        tenPhong = d.MaPhongNavigation != null ? d.MaPhongNavigation.TenPhong : "",
                        maKhach = d.MaKhach,
                        tenKhach = d.MaKhachNavigation != null ? d.MaKhachNavigation.HoTen : "",
                        ngayDat = d.NgayDat,
                        ngayDuKienNhan = d.NgayDuKienNhan,
                        tienCoc = d.TienCoc,
                        trangThai = d.TrangThai,
                        ghiChu = d.GhiChu
                    })
                    .ToListAsync();

                return Ok(list);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Lỗi server: {ex.Message}" });
            }
        }

        // Tạo mới phiếu đặt phòng (giữ chỗ) và tự động đổi trạng thái phòng sang 'Đã đặt cọc'.
        // Chỉ nhận đặt khi phòng đang trống — bản cũ ghi đè cả phòng đang có người thuê.
        // Tiền cọc ghi phiếu thu đúng như sổ đang có: "Thu tiền đặt cọc phòng B102".
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] DatPhong model)
        {
            if (model == null) return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ" });
            if (model.TienCoc is < 0) return BadRequest(new { success = false, message = "Tiền cọc không được âm." });

            var phong = await _context.Phongs.FindAsync(model.MaPhong);
            if (phong == null) return NotFound(new { success = false, message = $"Không tìm thấy phòng #{model.MaPhong}." });
            if (phong.TrangThai != NghiepVu.Phong.Trong)
                return BadRequest(new { success = false, message = $"{phong.TenPhong} đang ở trạng thái '{phong.TrangThai}', chỉ đặt được phòng trống." });

            var khach = await _context.KhachThues.FindAsync(model.MaKhach);
            if (khach == null) return NotFound(new { success = false, message = $"Không tìm thấy khách thuê #{model.MaKhach}." });

            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                if (model.NgayDat == default) model.NgayDat = DateOnly.FromDateTime(DateTime.Now);
                model.TrangThai = NghiepVu.DatPhong.DaDatCoc;
                _context.DatPhongs.Add(model);
                phong.TrangThai = NghiepVu.Phong.DaDatCoc;

                NghiepVu.GhiPhieu(_context, NghiepVu.PhieuThu,
                    $"Thu tiền đặt cọc {NghiepVu.ChuThuongDau(phong.TenPhong)}", model.TienCoc ?? 0, khach.HoTen);

                await _context.SaveChangesAsync();
                await tx.CommitAsync();
                return Ok(new { success = true, message = "Đặt phòng/Giữ chỗ thành công", data = new { model.MaDatPhong } });
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return StatusCode(500, new { success = false, message = $"Lỗi server: {ex.InnerException?.Message ?? ex.Message}" });
            }
        }

        // Hủy giữ chỗ: trả phòng về "Trống" và hoàn cọc. Dữ liệu đang có cho thấy hủy thì
        // hoàn đủ (A202 cọc 1.000.000 → "Hoàn tiền đặt cọc phòng A202 (khách hủy)" 1.000.000),
        // nên mặc định hoàn đủ; gửi { "hoanCoc": false } nếu khách mất cọc,
        // hoặc { "soTienHoan": x } để hoàn một phần.
        [HttpPut("{id}/huy")]
        public async Task<IActionResult> Huy(int id, [FromBody] HuyDatPhongDto? dto)
        {
            dto ??= new HuyDatPhongDto();
            var dp = await _context.DatPhongs
                .Include(d => d.MaPhongNavigation)
                .Include(d => d.MaKhachNavigation)
                .FirstOrDefaultAsync(d => d.MaDatPhong == id);
            if (dp == null) return NotFound(new { success = false, message = "Không tìm thấy phiếu đặt phòng." });
            if (dp.TrangThai != NghiepVu.DatPhong.DaDatCoc)
                return BadRequest(new { success = false, message = $"Chỉ hủy được phiếu đang giữ chỗ (hiện: {dp.TrangThai})." });

            decimal coc = dp.TienCoc ?? 0;
            decimal hoan = !dto.HoanCoc ? 0 : dto.SoTienHoan ?? coc;
            if (hoan < 0 || hoan > coc)
                return BadRequest(new { success = false, message = $"Số tiền hoàn phải từ 0 đến tiền cọc ({NghiepVu.Tien(coc)})." });

            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                dp.TrangThai = NghiepVu.DatPhong.DaHuy;

                // Chỉ nhả phòng nếu không còn phiếu giữ chỗ nào khác cho phòng này.
                var phong = dp.MaPhongNavigation;
                bool conGiuCho = await _context.DatPhongs.AnyAsync(d =>
                    d.MaPhong == dp.MaPhong && d.MaDatPhong != id && d.TrangThai == NghiepVu.DatPhong.DaDatCoc);
                if (phong != null && phong.TrangThai == NghiepVu.Phong.DaDatCoc && !conGiuCho)
                    phong.TrangThai = NghiepVu.Phong.Trong;

                NghiepVu.GhiPhieu(_context, NghiepVu.PhieuChi,
                    $"Hoàn tiền đặt cọc {NghiepVu.ChuThuongDau(phong?.TenPhong ?? $"phòng #{dp.MaPhong}")} (khách hủy)",
                    hoan, dp.MaKhachNavigation?.HoTen);

                await _context.SaveChangesAsync();
                await tx.CommitAsync();
                return Ok(new
                {
                    success = true,
                    message = hoan > 0 ? $"Đã hủy giữ chỗ, hoàn cọc {NghiepVu.Tien(hoan)}." : "Đã hủy giữ chỗ, không hoàn cọc."
                });
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return StatusCode(500, new { success = false, message = $"Lỗi server: {ex.InnerException?.Message ?? ex.Message}" });
            }
        }
    }

    public class HuyDatPhongDto
    {
        public bool HoanCoc { get; set; } = true;
        public decimal? SoTienHoan { get; set; }
    }
}