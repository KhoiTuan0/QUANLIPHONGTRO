using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLPT.API.Models;

namespace QLPT.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LoaiPhongController : ControllerBase
    {
        private readonly Klcn052QuanLyPhongTroContext _context;

        public LoaiPhongController(Klcn052QuanLyPhongTroContext context)
        {
            _context = context;
        }

        // Lấy danh sách loại phòng
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var list = await _context.LoaiPhongs.ToListAsync();
                return Ok(list);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Lỗi server: {ex.Message}" });
            }
        }

        // Thêm loại phòng mới (Dành cho Desktop Admin)
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] LoaiPhong model)
        {
            try
            {
                if (model == null) return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ" });

                _context.LoaiPhongs.Add(model);
                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = "Thêm loại phòng thành công", data = model });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Lỗi server: {ex.Message}" });
            }
        }

        // Sửa loại phòng.
        // Dùng DTO toàn trường nullable thay vì bind thẳng entity: entity khai báo
        // TenLoaiPhong non-nullable nên ApiController sẽ bắt buộc phải gửi, chặn mất
        // khả năng cập nhật từng phần.
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] LoaiPhongUpdateDto model)
        {
            try
            {
                var item = await _context.LoaiPhongs.FindAsync(id);
                if (item == null) return NotFound(new { success = false, message = "Không tìm thấy loại phòng" });

                item.TenLoaiPhong = model.TenLoaiPhong ?? item.TenLoaiPhong;
                item.DienTich     = model.DienTich     ?? item.DienTich;
                item.SoNguoiToiDa = model.SoNguoiToiDa ?? item.SoNguoiToiDa;
                item.GiaMacDinh   = model.GiaMacDinh   ?? item.GiaMacDinh;
                item.MoTa         = model.MoTa         ?? item.MoTa;

                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = "Cập nhật loại phòng thành công" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Lỗi server: {ex.InnerException?.Message ?? ex.Message}" });
            }
        }

        // Xóa loại phòng
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var item = await _context.LoaiPhongs.FindAsync(id);
                if (item == null) return NotFound(new { success = false, message = "Không tìm thấy loại phòng" });

                // Còn phòng dùng loại này thì chặn, tránh lỗi khóa ngoại.
                if (await _context.Phongs.AnyAsync(p => p.MaLoaiPhong == id))
                    return BadRequest(new { success = false, message = "Loại phòng đang được dùng bởi ít nhất một phòng nên không thể xóa." });

                _context.LoaiPhongs.Remove(item);
                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = "Xóa loại phòng thành công" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Lỗi server: {ex.Message}" });
            }
        }
    }

    public class LoaiPhongUpdateDto
    {
        public string? TenLoaiPhong { get; set; }
        public decimal? DienTich { get; set; }
        public int? SoNguoiToiDa { get; set; }
        public decimal? GiaMacDinh { get; set; }
        public string? MoTa { get; set; }
    }
}