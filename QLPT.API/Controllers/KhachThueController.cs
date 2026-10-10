using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
                var list = await _context.KhachThues.ToListAsync();
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

        // Thêm mới khách thuê
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] KhachThue model)
        {
            try
            {
                if (model == null) return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ" });

                _context.KhachThues.Add(model);
                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = "Thêm khách thuê thành công", data = model });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Lỗi server: {ex.Message}" });
            }
        }

        // Sửa thông tin khách thuê.
        // DTO toàn trường nullable để cập nhật từng phần; bind thẳng entity sẽ bị
        // ApiController bắt buộc gửi HoTen.
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] KhachThueUpdateDto model)
        {
            try
            {
                var item = await _context.KhachThues.FindAsync(id);
                if (item == null) return NotFound(new { success = false, message = "Không tìm thấy khách thuê" });

                // CCCD là duy nhất nên phải kiểm tra trước khi lưu,
                // nếu không SQL sẽ ném lỗi unique index khó đọc.
                if (!string.IsNullOrWhiteSpace(model.Cccd) && model.Cccd != item.Cccd)
                {
                    bool trung = await _context.KhachThues
                        .AnyAsync(k => k.MaKhach != id && k.Cccd == model.Cccd);
                    if (trung)
                        return BadRequest(new { success = false, message = $"CCCD {model.Cccd} đã thuộc về khách thuê khác." });
                }

                item.HoTen       = model.HoTen       ?? item.HoTen;
                item.Cccd        = model.Cccd        ?? item.Cccd;
                item.NgaySinh    = model.NgaySinh    ?? item.NgaySinh;
                item.GioiTinh    = model.GioiTinh    ?? item.GioiTinh;
                item.SoDienThoai = model.SoDienThoai ?? item.SoDienThoai;
                item.Email       = model.Email       ?? item.Email;
                item.DiaChi      = model.DiaChi      ?? item.DiaChi;

                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = "Cập nhật khách thuê thành công" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Lỗi server: {ex.InnerException?.Message ?? ex.Message}" });
            }
        }
    }

    public class KhachThueUpdateDto
    {
        public string? HoTen { get; set; }
        public string? Cccd { get; set; }
        public DateOnly? NgaySinh { get; set; }
        public string? GioiTinh { get; set; }
        public string? SoDienThoai { get; set; }
        public string? Email { get; set; }
        public string? DiaChi { get; set; }
    }
}