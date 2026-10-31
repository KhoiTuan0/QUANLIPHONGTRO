using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLPT.API.Models; // Đảm bảo namespace này khớp với tên dự án API của bạn
using QLPT.API.Services;
using System.Security.Claims;

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
            if (User.IsInRole("Manager") && int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int managerId))
                q = q.Where(p => p.MaCoSo.HasValue && _context.QuanLyCoSos.Any(a => a.MaTaiKhoan == managerId && a.MaCoSo == p.MaCoSo.Value));
            if (!string.IsNullOrWhiteSpace(trangThai))
                q = q.Where(p => p.TrangThai == trangThai);
            return Ok(await q.Select(p => new
            {
                p.MaPhong, p.MaLoaiPhong, p.TenPhong, p.Tang, p.GiaThue, p.TrangThai, p.MoTa, p.MaCoSo, p.HinhAnh,
                TenLoaiPhong = p.MaLoaiPhongNavigation != null ? p.MaLoaiPhongNavigation.TenLoaiPhong : "",
                TenCoSo = p.MaCoSoNavigation != null ? p.MaCoSoNavigation.TenCoSo : "",
                CoLichSu = _context.HopDongs.Any(h => h.MaPhong == p.MaPhong)
                    || _context.DatPhongs.Any(d => d.MaPhong == p.MaPhong)
            }).ToListAsync());
        }
        catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }

    // GET: api/Phong/5
    [HttpGet("{id}")]
    public async Task<IActionResult> GetPhong(int id)
    {
        try
        {
            var phong = await _context.Phongs.Where(p => p.MaPhong == id).Select(p => new
            {
                p.MaPhong, p.MaLoaiPhong, p.TenPhong, p.Tang, p.GiaThue, p.TrangThai, p.MoTa, p.MaCoSo, p.HinhAnh,
                TenLoaiPhong = p.MaLoaiPhongNavigation != null ? p.MaLoaiPhongNavigation.TenLoaiPhong : "",
                TenCoSo = p.MaCoSoNavigation != null ? p.MaCoSoNavigation.TenCoSo : "",
                CoLichSu = _context.HopDongs.Any(h => h.MaPhong == p.MaPhong)
                    || _context.DatPhongs.Any(d => d.MaPhong == p.MaPhong)
            }).FirstOrDefaultAsync();
            if (phong == null) return NotFound(new { message = "Không tìm thấy phòng" });
            return Ok(phong);
        }
        catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }

    // Báo lỗi dễ hiểu thay vì để SQL ném lỗi khóa ngoại khi mã loại phòng / cơ sở không tồn tại.
    private async Task<string?> KiemTraThamChieu(int? maLoaiPhong, int? maCoSo)
    {
        if (maLoaiPhong is <= 0) return "Chọn loại phòng trong danh sách.";
        if (maCoSo is <= 0) return "Chọn cơ sở trong danh sách.";
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
        if (phong.TenPhong.Trim().Length > 50)
            return BadRequest(new { message = "Tên phòng tối đa 50 ký tự." });
        if (phong.GiaThue is null or < 100_000) return BadRequest(new { message = "Giá thuê phải từ 100.000 đ trở lên." });
        string trangThai = phong.TrangThai ?? NghiepVu.Phong.Trong;
        if (trangThai != NghiepVu.Phong.Trong && trangThai != NghiepVu.Phong.BaoTri)
            return BadRequest(new { message = "Phòng mới chỉ có trạng thái Trống hoặc Bảo trì. Đặt cọc và đang thuê được cập nhật khi lập phiếu đặt hoặc hợp đồng." });
        var loi = await KiemTraThamChieu(phong.MaLoaiPhong, phong.MaCoSo);
        if (loi != null) return BadRequest(new { message = loi });
        if (await _context.Phongs.AnyAsync(p => p.MaCoSo == phong.MaCoSo && p.TenPhong == phong.TenPhong.Trim()))
            return BadRequest(new { message = "Tên phòng đã tồn tại trong cơ sở này." });
        phong = new Phong
        {
            TenPhong = phong.TenPhong.Trim(), MaLoaiPhong = phong.MaLoaiPhong, MaCoSo = phong.MaCoSo,
            Tang = phong.Tang, GiaThue = phong.GiaThue, TrangThai = trangThai, MoTa = phong.MoTa, HinhAnh = phong.HinhAnh
        };
        try
        {
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
            if (model.TenPhong?.Trim().Length > 50)
                return BadRequest(new { message = "Tên phòng tối đa 50 ký tự." });
            if (model.GiaThue.HasValue && model.GiaThue < 100_000)
                return BadRequest(new { message = "Giá thuê phải từ 100.000 đ trở lên." });
            var loi = await KiemTraThamChieu(model.MaLoaiPhong ?? item.MaLoaiPhong,
                model.MaCoSoProvided ? model.MaCoSo : item.MaCoSo);
            if (loi != null) return BadRequest(new { message = loi });
            string tenPhong = model.TenPhong?.Trim() ?? item.TenPhong;
            int? maCoSo = model.MaCoSoProvided ? model.MaCoSo : item.MaCoSo;
            if (tenPhong != item.TenPhong || maCoSo != item.MaCoSo ||
                (model.MaLoaiPhong.HasValue && model.MaLoaiPhong != item.MaLoaiPhong) ||
                (model.TangProvided && model.Tang != item.Tang))
            {
                bool coLichSu = await _context.HopDongs.AnyAsync(h => h.MaPhong == id)
                    || await _context.DatPhongs.AnyAsync(d => d.MaPhong == id);
                if (coLichSu)
                    return BadRequest(new { message = "Phòng đã có hợp đồng hoặc phiếu đặt. Không thể đổi tên, tầng, loại phòng hay cơ sở vì sẽ làm sai lịch sử của khách; các thông tin khác vẫn sửa được." });
            }
            // Bản ghi cũ có thể đã trùng tên trước khi có kiểm tra này. Vẫn cho phép
            // sửa trạng thái/thông tin khác; chỉ chặn khi tên hoặc cơ sở thực sự đổi.
            if ((tenPhong != item.TenPhong || maCoSo != item.MaCoSo) &&
                await _context.Phongs.AnyAsync(p => p.MaPhong != id && p.MaCoSo == maCoSo && p.TenPhong == tenPhong))
                return BadRequest(new { message = "Tên phòng đã tồn tại trong cơ sở này." });
            if (model.TrangThai != null)
            {
                bool coHopDong = await _context.HopDongs.AnyAsync(h => h.MaPhong == id && h.TrangThai == NghiepVu.HopDong.HieuLuc);
                bool coDatPhong = await _context.DatPhongs.AnyAsync(d => d.MaPhong == id && d.TrangThai == NghiepVu.DatPhong.DaDatCoc);
                if (coHopDong && model.TrangThai != NghiepVu.Phong.DangThue)
                    return BadRequest(new { message = "Phòng còn hợp đồng hiệu lực. Thanh lý hợp đồng trước khi đổi trạng thái phòng." });
                if (!coHopDong && coDatPhong && model.TrangThai != NghiepVu.Phong.DaDatCoc)
                    return BadRequest(new { message = "Phòng còn phiếu giữ chỗ. Hủy đặt phòng hoặc nhận phòng trước khi đổi trạng thái." });
                if (!coHopDong && !coDatPhong && model.TrangThai != NghiepVu.Phong.Trong && model.TrangThai != NghiepVu.Phong.BaoTri)
                    return BadRequest(new { message = "Chỉ chọn Trống hoặc Bảo trì. Trạng thái đặt cọc và đang thuê được cập nhật qua phiếu đặt và hợp đồng." });
            }

            // Chỉ ghi đè trường client thực sự gửi lên, giữ nguyên phần còn lại.
            if (model.MaLoaiPhong is int ml && ml > 0) item.MaLoaiPhong = ml;
            item.TenPhong  = model.TenPhong?.Trim() ?? item.TenPhong;
            if (model.TangProvided) item.Tang = model.Tang;
            item.GiaThue   = model.GiaThue   ?? item.GiaThue;
            item.TrangThai = model.TrangThai ?? item.TrangThai;
            item.MoTa      = model.MoTa      ?? item.MoTa;
            item.MaCoSo    = maCoSo;
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
    int? floor, site;
    [System.Text.Json.Serialization.JsonIgnore] public bool TangProvided { get; private set; }
    [System.Text.Json.Serialization.JsonIgnore] public bool MaCoSoProvided { get; private set; }
    public int? Tang { get => floor; set { floor = value; TangProvided = true; } }
    public decimal? GiaThue { get; set; }
    public string? TrangThai { get; set; }
    public string? MoTa { get; set; }
    public int? MaCoSo { get => site; set { site = value; MaCoSoProvided = true; } }
    public string? HinhAnh { get; set; }
}
