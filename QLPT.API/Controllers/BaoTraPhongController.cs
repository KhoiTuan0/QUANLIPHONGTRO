using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLPT.API.Models;
using QLPT.API.Services;

namespace QLPT.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BaoTraPhongController : ControllerBase
    {
        private readonly Klcn052QuanLyPhongTroContext _context;

        public BaoTraPhongController(Klcn052QuanLyPhongTroContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var list = await _context.BaoTraPhongs
                    .Include(b => b.MaHopDongNavigation)
                    .Include(b => b.MaKhachNavigation)
                    .Select(b => new
                    {
                        b.MaBaoTra, b.MaHopDong, b.MaKhach, b.NgayBao, b.NgayDuKienTra, b.LyDo, b.TrangThai, b.GhiChu,
                        MaPhong = b.MaHopDongNavigation.MaPhong,
                        TenPhong = b.MaHopDongNavigation.MaPhongNavigation.TenPhong,
                        HoTenKhach = b.MaKhachNavigation.HoTen
                    })
                    .ToListAsync();
                return Ok(list);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Lỗi server: {ex.Message}" });
            }
        }

        // Khách gửi báo trả phòng
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] BaoTraPhong model)
        {
            try
            {
                if (model == null) return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ" });
                if (model.MaHopDong <= 0 || model.MaKhach <= 0)
                    return BadRequest(new { success = false, message = "Chọn hợp đồng và khách thuê trong danh sách." });
                var homNay = DateOnly.FromDateTime(DateTime.Now);
                if (model.NgayDuKienTra.HasValue && model.NgayDuKienTra < homNay)
                    return BadRequest(new { success = false, message = "Ngày dự kiến trả phòng không được trước hôm nay." });
                var hd = await _context.HopDongs.FindAsync(model.MaHopDong);
                if (hd == null) return NotFound(new { success = false, message = "Không tìm thấy hợp đồng." });
                if (hd.TrangThai != NghiepVu.HopDong.HieuLuc)
                    return BadRequest(new { success = false, message = "Chỉ báo trả phòng cho hợp đồng đang hiệu lực." });
                if (model.NgayDuKienTra < hd.NgayBatDau)
                    return BadRequest(new { success = false, message = "Ngày dự kiến trả phòng phải từ ngày bắt đầu hợp đồng trở đi." });
                bool thuocHopDong = hd.MaKhachDaiDien == model.MaKhach || await _context.CuTrus.AnyAsync(c =>
                    c.MaHopDong == hd.MaHopDong && c.MaKhach == model.MaKhach
                    && (c.TrangThaiCuTru == NghiepVu.CuTru.DangO || c.TrangThaiCuTru == NghiepVu.CuTru.TamVang));
                if (!thuocHopDong)
                    return BadRequest(new { success = false, message = "Khách thuê không thuộc hợp đồng đã chọn." });
                if (await _context.BaoTraPhongs.AnyAsync(b => b.MaHopDong == model.MaHopDong && b.TrangThai == NghiepVu.BaoTra.ChoXuLy))
                    return BadRequest(new { success = false, message = "Hợp đồng đã có báo trả phòng đang chờ xử lý." });
                model = new BaoTraPhong
                {
                    MaHopDong = model.MaHopDong, MaKhach = model.MaKhach,
                    NgayDuKienTra = model.NgayDuKienTra, LyDo = model.LyDo?.Trim(), GhiChu = model.GhiChu
                };
                model.NgayBao = DateOnly.FromDateTime(DateTime.Now);
                model.TrangThai = NghiepVu.BaoTra.ChoXuLy;

                _context.BaoTraPhongs.Add(model);
                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = "Đã gửi thông báo trả phòng đến chủ trọ", data = model });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Lỗi server: {ex.Message}" });
            }
        }
    }
}
