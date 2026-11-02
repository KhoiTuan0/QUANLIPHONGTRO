using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLPT.API.Models; // Đảm bảo namespace này khớp với tên dự án API của bạn

[Route("api/[controller]")]
[ApiController]
public class PhongController : ControllerBase
{
    private readonly Klcn052QuanLyPhongTroContext _context;

    public PhongController(Klcn052QuanLyPhongTroContext context)
    {
        _context = context;
    }

    // GET: api/Phong  (lọc tùy chọn theo trạng thái)
    [HttpGet]
    public async Task<IActionResult> GetPhongs([FromQuery] string? trangThai)
    {
        try
        {
            var q = _context.Phongs.AsQueryable();
            if (!string.IsNullOrWhiteSpace(trangThai))
                q = q.Where(p => p.TrangThai == trangThai);
            return Ok(await q.ToListAsync());
        }
        catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }

    // GET: api/Phong/5
    [HttpGet("{id}")]
    public async Task<IActionResult> GetPhong(int id)
    {
        try
        {
            var phong = await _context.Phongs.FindAsync(id);
            if (phong == null) return NotFound(new { message = "Không tìm thấy phòng" });
            return Ok(phong);
        }
        catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }

    // Báo lỗi dễ hiểu thay vì để SQL ném lỗi khóa ngoại khi mã loại phòng / cơ sở không tồn tại.
    private async Task<string?> KiemTraThamChieu(int? maLoaiPhong, int? maCoSo)
    {
        if (maLoaiPhong is int ml && !await _context.LoaiPhongs.AnyAsync(l => l.MaLoaiPhong == ml))
            return $"Không có loại phòng mã {ml}.";
        if (maCoSo is int cs && !await _context.CoSos.AnyAsync(c => c.MaCoSo == cs))
            return $"Không có cơ sở mã {cs}.";
        return null;
    }

    // POST: api/Phong
    [HttpPost]
    public async Task<IActionResult> PostPhong([FromBody] Phong phong)
    {
        if (string.IsNullOrWhiteSpace(phong.TenPhong))
            return BadRequest(new { message = "Tên phòng không được để trống." });
        if (phong.GiaThue is < 0) return BadRequest(new { message = "Giá thuê không được âm." });
        var loi = await KiemTraThamChieu(phong.MaLoaiPhong, phong.MaCoSo);
        if (loi != null) return BadRequest(new { message = loi });
        try
        {
            phong.TrangThai ??= "Trống";
            _context.Phongs.Add(phong);
            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "Thêm phòng thành công", data = new { phong.MaPhong } });
        }
        catch (Exception ex) { return StatusCode(500, new { message = ex.InnerException?.Message ?? ex.Message }); }
    }

    // PUT: api/Phong/5
    // DTO toàn trường nullable để cập nhật từng phần mà không phải gửi lại cả bản ghi.
    [HttpPut("{id}")]
    public async Task<IActionResult> PutPhong(int id, [FromBody] PhongUpdateDto model)
    {
        try
        {
            var item = await _context.Phongs.FindAsync(id);
            if (item == null) return NotFound(new { message = "Không tìm thấy phòng" });
            if (model.TenPhong != null && string.IsNullOrWhiteSpace(model.TenPhong))
                return BadRequest(new { message = "Tên phòng không được để trống." });
            if (model.GiaThue is < 0) return BadRequest(new { message = "Giá thuê không được âm." });
            var loi = await KiemTraThamChieu(model.MaLoaiPhong, model.MaCoSo);
            if (loi != null) return BadRequest(new { message = loi });

            // Chỉ ghi đè trường client thực sự gửi lên, giữ nguyên phần còn lại.
            if (model.MaLoaiPhong is int ml && ml > 0) item.MaLoaiPhong = ml;
            item.TenPhong  = model.TenPhong  ?? item.TenPhong;
            item.Tang      = model.Tang      ?? item.Tang;
            item.GiaThue   = model.GiaThue   ?? item.GiaThue;
            item.TrangThai = model.TrangThai ?? item.TrangThai;
            item.MoTa      = model.MoTa      ?? item.MoTa;
            item.MaCoSo    = model.MaCoSo    ?? item.MaCoSo;
            item.HinhAnh   = model.HinhAnh   ?? item.HinhAnh;

            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "Cập nhật phòng thành công" });
        }
        catch (Exception ex) { return StatusCode(500, new { message = ex.InnerException?.Message ?? ex.Message }); }
    }

    // DELETE: api/Phong/5
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeletePhong(int id)
    {
        try
        {
            var item = await _context.Phongs.FindAsync(id);
            if (item == null) return NotFound(new { message = "Không tìm thấy phòng" });

            // Chặn xóa khi còn dữ liệu tham chiếu, nếu không SQL sẽ ném lỗi khóa ngoại khó hiểu.
            if (await _context.HopDongs.AnyAsync(h => h.MaPhong == id))
                return BadRequest(new { message = "Phòng đã có hợp đồng nên không thể xóa. Chuyển trạng thái sang 'Bảo trì' nếu muốn ngừng cho thuê." });
            if (await _context.DatPhongs.AnyAsync(d => d.MaPhong == id))
                return BadRequest(new { message = "Phòng đang có lượt đặt nên không thể xóa." });
            if (await _context.ChiSoDienNuocs.AnyAsync(c => c.MaPhong == id))
                return BadRequest(new { message = "Phòng đã có dữ liệu chỉ số điện nước nên không thể xóa." });
            if (await _context.TaiSanPhongs.AnyAsync(t => t.MaPhong == id))
                return BadRequest(new { message = "Phòng còn tài sản được gán, hãy gỡ tài sản trước." });
            if (await _context.ViPhams.AnyAsync(v => v.MaPhong == id))
                return BadRequest(new { message = "Phòng đã có dữ liệu vi phạm nên không thể xóa." });
            if (await _context.YeuCauSuaChuas.AnyAsync(y => y.MaPhong == id))
                return BadRequest(new { message = "Phòng đã có yêu cầu sửa chữa nên không thể xóa." });

            _context.Phongs.Remove(item);
            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "Xóa phòng thành công" });
        }
        catch (Exception ex) { return StatusCode(500, new { message = ex.InnerException?.Message ?? ex.Message }); }
    }
}

public class PhongUpdateDto
{
    public int? MaLoaiPhong { get; set; }
    public string? TenPhong { get; set; }
    public int? Tang { get; set; }
    public decimal? GiaThue { get; set; }
    public string? TrangThai { get; set; }
    public string? MoTa { get; set; }
    public int? MaCoSo { get; set; }
    public string? HinhAnh { get; set; }
}
