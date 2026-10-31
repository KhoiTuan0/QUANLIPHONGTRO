using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
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
                var query = _context.DatPhongs.AsQueryable();
                if (User.IsInRole("Customer"))
                {
                    if (!int.TryParse(User.FindFirstValue("MaKhach"), out int customerId)) return Forbid();
                    query = query.Where(d => d.MaKhach == customerId);
                }
                else if (User.IsInRole("Manager"))
                {
                    if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int managerId)) return Forbid();
                    query = query.Where(d => d.MaPhongNavigation.MaCoSo.HasValue && _context.QuanLyCoSos.Any(a =>
                        a.MaTaiKhoan == managerId && a.MaCoSo == d.MaPhongNavigation.MaCoSo.Value));
                }
                var list = await query
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
                        ngayHenKyHopDong = d.NgayHenKyHopDong,
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
            if (model.MaPhong <= 0 || model.MaKhach <= 0)
                return BadRequest(new { success = false, message = "Chọn phòng và khách thuê đã có trong danh sách." });
            if (model.TienCoc is < 0) return BadRequest(new { success = false, message = "Tiền cọc không được âm." });
            if (model.NgayDat == default) model.NgayDat = DateOnly.FromDateTime(DateTime.Now);
            if (model.NgayDuKienNhan < model.NgayDat || model.NgayHenKyHopDong < model.NgayDat)
                return BadRequest(new { success = false, message = "Ngày dự kiến nhận phòng và ngày hẹn ký phải từ ngày đặt phòng trở đi." });

            await using var tx = _context.Database.CurrentTransaction == null
                ? await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable) : null;
            var phong = await _context.Phongs.FromSqlInterpolated($"SELECT * FROM dbo.Phong WITH (UPDLOCK,HOLDLOCK) WHERE MaPhong={model.MaPhong}").FirstOrDefaultAsync();
            if (phong == null) return NotFound(new { success = false, message = $"Không tìm thấy phòng #{model.MaPhong}." });
            if (phong.MaCoSo == null)
                return BadRequest(new { success = false, message = "Phòng chưa thuộc cơ sở; chưa thể đặt phòng." });
            if (User.IsInRole("Customer"))
            {
                if (!int.TryParse(User.FindFirstValue("MaKhach"), out int customerId) ||
                    model.MaKhach != customerId || model.TienCoc is not (null or 0))
                    return StatusCode(403, new { message = "Khách chỉ được giữ chỗ cho chính mình; tiền cọc do chủ trọ ghi nhận." });
            }
            if (phong.TrangThai != NghiepVu.Phong.Trong)
                return BadRequest(new { success = false, message = $"{phong.TenPhong} đang ở trạng thái '{phong.TrangThai}', chỉ đặt được phòng trống." });
            if (await _context.HopDongs.AnyAsync(h => h.MaPhong == model.MaPhong && h.TrangThai == NghiepVu.HopDong.HieuLuc)
                || await _context.DatPhongs.AnyAsync(d => d.MaPhong == model.MaPhong && d.TrangThai == NghiepVu.DatPhong.DaDatCoc))
                return BadRequest(new { success = false, message = "Phòng đã có hợp đồng hiệu lực hoặc phiếu giữ chỗ. Kiểm tra trạng thái phòng trước khi đặt." });

            var khach = await _context.KhachThues.FindAsync(model.MaKhach);
            if (khach == null) return NotFound(new { success = false, message = $"Không tìm thấy khách thuê #{model.MaKhach}." });
            // Người đứng tên có thể đặt thêm phòng trống cho người thân. Người ở thực tế
            // được xác định khi lập hợp đồng; trạng thái phòng được kiểm tra ở trên.

            model = new DatPhong
            {
                MaPhong = model.MaPhong, MaKhach = model.MaKhach, NgayDat = model.NgayDat,
                NgayDuKienNhan = model.NgayDuKienNhan, NgayHenKyHopDong = model.NgayHenKyHopDong,
                TienCoc = model.TienCoc, GhiChu = model.GhiChu
            };

            try
            {
                model.TrangThai = NghiepVu.DatPhong.DaDatCoc;
                _context.DatPhongs.Add(model);
                phong.TrangThai = NghiepVu.Phong.DaDatCoc;
                khach.MaCoSoTiepNhan = phong.MaCoSo;

                NghiepVu.GhiPhieu(_context, NghiepVu.PhieuThu,
                    $"Thu tiền đặt cọc {NghiepVu.ChuThuongDau(phong.TenPhong)}", model.TienCoc ?? 0, khach.HoTen);

                await _context.SaveChangesAsync();
                if (tx != null) await tx.CommitAsync();
                return Ok(new { success = true, message = "Đặt phòng/Giữ chỗ thành công", data = new { model.MaDatPhong } });
            }
            catch (Exception ex)
            {
                if (tx != null) await tx.RollbackAsync();
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
            await using var tx = _context.Database.CurrentTransaction == null
                ? await _context.Database.BeginTransactionAsync() : null;
            var dp = await HuyDatPhongService.LockAsync(_context, id);
            if (dp == null) return NotFound(new { message = "Không tìm thấy phiếu đặt phòng." });
            decimal refund = !dto.HoanCoc ? 0 : dto.SoTienHoan ?? dp.TienCoc ?? 0;
            string? error = await HuyDatPhongService.CancelAsync(_context, dp, refund);
            if (error != null) return BadRequest(new { message = error });
            await _context.SaveChangesAsync();
            if (tx != null) await tx.CommitAsync();
            return Ok(new { success = true, message = $"Đã hủy giữ chỗ, hoàn cọc {NghiepVu.Tien(refund)}." });
        }
    }

    public class HuyDatPhongDto
    {
        public bool HoanCoc { get; set; } = true;
        public decimal? SoTienHoan { get; set; }
    }
}
