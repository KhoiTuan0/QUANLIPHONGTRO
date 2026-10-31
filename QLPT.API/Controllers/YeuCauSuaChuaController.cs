using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLPT.API.Models;
using QLPT.API.Services;

namespace QLPT.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class YeuCauSuaChuaController : ControllerBase
    {
        private readonly Klcn052QuanLyPhongTroContext _context;

        public YeuCauSuaChuaController(Klcn052QuanLyPhongTroContext context)
        {
            _context = context;
        }

        // Lấy danh sách yêu cầu sửa chữa (Chủ trọ xem tất cả hoặc khách xem của mình)
        [HttpGet]
        [HttpGet("customer/{maKhach}")]
        public async Task<IActionResult> GetAll(int? maKhach = null)
        {
            try
            {
                var list = await _context.YeuCauSuaChuas
                    .Include(y => y.MaPhongNavigation)
                    .Include(y => y.MaKhachNavigation)
                    .Where(y => !maKhach.HasValue || y.MaKhach == maKhach.Value)
                    .Select(y => new
                    {
                        y.MaYeuCau, y.MaPhong, y.MaKhach, y.NgayBao, y.NoiDung, y.TrangThai, y.ChiPhi,
                        y.NgayHoanThanh, y.DuongDanAnh, y.KhachXacNhan,
                        TenPhong = y.MaPhongNavigation.TenPhong,
                        HoTenKhach = y.MaKhachNavigation.HoTen
                    })
                    .ToListAsync();
                return Ok(list);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Lỗi server: {ex.Message}" });
            }
        }

        // Khách gửi yêu cầu sửa chữa mới từ App Mobile
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] YeuCauSuaChua model)
        {
            try
            {
                if (model == null) return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ" });
                if (model.MaPhong <= 0 || model.MaKhach <= 0)
                    return BadRequest(new { success = false, message = "Chọn phòng và khách thuê trong danh sách." });
                if (string.IsNullOrWhiteSpace(model.NoiDung) || model.NoiDung.Trim().Length > 500)
                    return BadRequest(new { success = false, message = "Nội dung sửa chữa phải từ 1 đến 500 ký tự." });
                if (!await _context.Phongs.AnyAsync(p => p.MaPhong == model.MaPhong))
                    return NotFound(new { success = false, message = "Không tìm thấy phòng." });
                if (!await _context.KhachThues.AnyAsync(k => k.MaKhach == model.MaKhach))
                    return NotFound(new { success = false, message = "Không tìm thấy khách thuê." });
                if (!await NghiepVu.KhachDangThuePhongAsync(_context, model.MaPhong, model.MaKhach))
                    return BadRequest(new { success = false, message = "Khách thuê không đang cư trú ở phòng đã chọn." });
                model = new YeuCauSuaChua
                {
                    MaPhong = model.MaPhong, MaKhach = model.MaKhach, NoiDung = model.NoiDung.Trim(), DuongDanAnh = model.DuongDanAnh
                };
                model.NgayBao = DateTime.Now;
                model.TrangThai = NghiepVu.SuaChua.ChoTiepNhan;
                model.KhachXacNhan = false;

                _context.YeuCauSuaChuas.Add(model);
                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = "Gửi yêu cầu sửa chữa thành công", data = model });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Lỗi server: {ex.Message}" });
            }
        }

        // Khách xác nhận đã hoàn thành sửa chữa
        [HttpPost("{id}/xac-nhan")]
        public async Task<IActionResult> ConfirmRepair(int id)
        {
            try
            {
                var yc = await _context.YeuCauSuaChuas.FindAsync(id);
                if (yc == null) return NotFound(new { success = false, message = "Không tìm thấy yêu cầu" });
                if (yc.TrangThai != NghiepVu.SuaChua.DaHoanThanh)
                    return BadRequest(new { success = false, message = "Chủ trọ phải cập nhật yêu cầu đã hoàn thành trước khi khách xác nhận." });
                if (yc.KhachXacNhan == true)
                    return Ok(new { success = true, message = "Yêu cầu đã được xác nhận." });

                yc.KhachXacNhan = true;

                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = "Xác nhận thành công" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Lỗi server: {ex.Message}" });
            }
        }
    }
}
