using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLPT.API.Models;

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
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var list = await _context.YeuCauSuaChuas
                    .Include(y => y.MaPhongNavigation)
                    .Include(y => y.MaKhachNavigation)
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
                model.NgayBao = DateTime.Now;
                model.TrangThai = "Chờ tiếp nhận";
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

                yc.KhachXacNhan = true;
                yc.TrangThai = "Đã hoàn thành";
                yc.NgayHoanThanh = DateTime.Now;

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