using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLPT.API.Models;
using QLPT.API.Services;

namespace QLPT.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class SuaChuaController : ControllerBase
{
    private readonly Klcn052QuanLyPhongTroContext _ctx;
    public SuaChuaController(Klcn052QuanLyPhongTroContext ctx) => _ctx = ctx;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            var list = await _ctx.YeuCauSuaChuas
                .Include(x => x.MaPhongNavigation)
                .Select(x => new {
                    x.MaYeuCau, x.MaPhong, x.MaKhach,
                    x.NoiDung, x.NgayBao, x.TrangThai,
                    x.ChiPhi, x.NgayHoanThanh, x.KhachXacNhan,
                    TenPhong = x.MaPhongNavigation != null ? x.MaPhongNavigation.TenPhong : "",
                    // Bảng không lưu mã hợp đồng; suy ra hợp đồng gần nhất của phòng đó.
                    MaHopDong = _ctx.HopDongs.Where(h => h.MaPhong == x.MaPhong)
                        .OrderByDescending(h => h.NgayBatDau).Select(h => (int?)h.MaHopDong).FirstOrDefault()
                })
                .OrderByDescending(x => x.NgayBao)
                .ToListAsync();
            return Ok(list);
        }
        catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }

    // WPF gửi { maHopDong, moTa, ngayYeuCau } nhưng bảng YeuCauSuaChua cần MaPhong + MaKhach.
    // Bản cũ bind thẳng entity → MaPhong = MaKhach = 0 → luôn lỗi khóa ngoại.
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] TaoSuaChuaDto dto)
    {
        int maPhong = dto.MaPhong ?? 0, maKhach = dto.MaKhach ?? 0;
        if (dto.MaHopDong is int maHd)
        {
            var hd = await _ctx.HopDongs.FindAsync(maHd);
            if (hd == null) return NotFound(new { success = false, message = $"Không tìm thấy hợp đồng #{maHd}." });
            maPhong = hd.MaPhong;
            maKhach = hd.MaKhachDaiDien;
        }
        if (maPhong <= 0 || maKhach <= 0)
            return BadRequest(new { success = false, message = "Cần mã hợp đồng (hoặc mã phòng và mã khách)." });

        string? noiDung = dto.NoiDung ?? dto.MoTa;
        if (string.IsNullOrWhiteSpace(noiDung))
            return BadRequest(new { success = false, message = "Nhập nội dung cần sửa chữa." });

        try
        {
            var yc = new YeuCauSuaChua
            {
                MaPhong   = maPhong,
                MaKhach   = maKhach,
                NoiDung   = noiDung.Trim(),
                NgayBao   = dto.NgayYeuCau ?? DateTime.Now,
                TrangThai = NghiepVu.SuaChua.ChoTiepNhan
            };
            _ctx.YeuCauSuaChuas.Add(yc);
            await _ctx.SaveChangesAsync();
            return Ok(new { success = true, message = "Thêm yêu cầu thành công", data = new { yc.MaYeuCau } });
        }
        catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.InnerException?.Message ?? ex.Message }); }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] YeuCauSuaChua model)
    {
        try
        {
            var item = await _ctx.YeuCauSuaChuas.Include(y => y.MaPhongNavigation).FirstOrDefaultAsync(y => y.MaYeuCau == id);
            if (item == null) return NotFound(new { message = "Không tìm thấy yêu cầu" });
            if (model.ChiPhi is < 0) return BadRequest(new { success = false, message = "Chi phí không được âm." });
            bool daHoanThanhTruoc = item.TrangThai == NghiepVu.SuaChua.DaHoanThanh;
            item.TrangThai = model.TrangThai ?? item.TrangThai;
            item.ChiPhi = model.ChiPhi ?? item.ChiPhi;
            item.NgayHoanThanh = model.NgayHoanThanh ?? item.NgayHoanThanh;
            item.KhachXacNhan = model.KhachXacNhan ?? item.KhachXacNhan;

            // Hoàn thành lần đầu và có chi phí → ghi phiếu chi, đúng mẫu dữ liệu gốc
            // ("Sửa khóa cửa phòng B101 (yêu cầu sửa chữa số 2)"). Trước đây Sổ thu chi
            // không thấy khoản chi này. Bỏ qua nếu yêu cầu đã có phiếu (tránh ghi trùng).
            if (!daHoanThanhTruoc && item.TrangThai == NghiepVu.SuaChua.DaHoanThanh && item.ChiPhi is > 0)
            {
                item.NgayHoanThanh ??= DateTime.Now;
                string dauHieu = $"(yêu cầu sửa chữa số {item.MaYeuCau})";
                if (!await _ctx.PhieuThuChis.AnyAsync(p => p.NoiDung != null && p.NoiDung.Contains(dauHieu)))
                {
                    string noiDung = (item.NoiDung ?? "Sửa chữa").Trim();
                    if (noiDung.Length > 120) noiDung = noiDung[..120] + "…";
                    NghiepVu.GhiPhieu(_ctx, NghiepVu.PhieuChi,
                        $"{noiDung} {NghiepVu.ChuThuongDau(item.MaPhongNavigation?.TenPhong ?? $"phòng #{item.MaPhong}")} {dauHieu}",
                        item.ChiPhi.Value, null);
                }
            }
            await _ctx.SaveChangesAsync();
            return Ok(new { success = true, message = "Cập nhật thành công",
                            data = new { item.MaYeuCau, item.TrangThai, item.ChiPhi, item.NgayHoanThanh } });
        }
        catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var item = await _ctx.YeuCauSuaChuas.FindAsync(id);
            if (item == null) return NotFound(new { message = "Không tìm thấy yêu cầu" });
            _ctx.YeuCauSuaChuas.Remove(item);
            await _ctx.SaveChangesAsync();
            return Ok(new { success = true, message = "Xóa yêu cầu thành công" });
        }
        catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }
}

public class TaoSuaChuaDto
{
    public int? MaHopDong { get; set; }
    public int? MaPhong { get; set; }
    public int? MaKhach { get; set; }
    public string? MoTa { get; set; }
    public string? NoiDung { get; set; }
    public DateTime? NgayYeuCau { get; set; }
}
