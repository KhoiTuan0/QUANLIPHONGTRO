using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLPT.API.Models;

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
                model.NgayBao = DateOnly.FromDateTime(DateTime.Now);
                model.TrangThai = "Chờ xử lý";

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