using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLPT.API.Models;
using QLPT.API.Services;
using System.Data;
using System.Security.Claims;

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
            var query = _ctx.YeuCauSuaChuas.AsQueryable();
            if (User.IsInRole("Manager") && int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int managerId))
                query = query.Where(x => x.MaPhongNavigation.MaCoSo.HasValue &&
                    _ctx.QuanLyCoSos.Any(a => a.MaTaiKhoan == managerId && a.MaCoSo == x.MaPhongNavigation.MaCoSo.Value));
            var list = await query
                .Include(x => x.MaPhongNavigation)
                .Select(x => new {
                    x.MaYeuCau, x.MaPhong, x.MaKhach,
                    x.NoiDung, x.NgayBao, x.TrangThai,
                    x.ChiPhi, x.NgayHoanThanh, x.KhachXacNhan,
                    TenPhong = x.MaPhongNavigation != null ? x.MaPhongNavigation.TenPhong : "",
                    HoTenKhach = x.MaKhachNavigation != null ? x.MaKhachNavigation.HoTen : "",
                    MaDinhDanhHienVat = _ctx.LichSuTaiSans.Where(h => h.MaYeuCau == x.MaYeuCau)
                        .Select(h => h.MaTaiSanCaTheNavigation.MaDinhDanh).FirstOrDefault(),
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
        if (dto.MaHopDong is <= 0)
            return BadRequest(new { success = false, message = "Chọn hợp đồng trong danh sách." });
        if (dto.NgayYeuCau.HasValue && dto.NgayYeuCau.Value.Date != DateTime.Today)
            return BadRequest(new { success = false, message = "Yêu cầu sửa chữa mới phải ghi ngày hôm nay." });
        int maPhong = dto.MaPhong ?? 0, maKhach = dto.MaKhach ?? 0;
        if (dto.MaHopDong is int maHd)
        {
            var hd = await _ctx.HopDongs.FindAsync(maHd);
            if (hd == null) return NotFound(new { success = false, message = $"Không tìm thấy hợp đồng #{maHd}." });
            if (hd.TrangThai != NghiepVu.HopDong.HieuLuc)
                return BadRequest(new { success = false, message = "Chỉ tạo yêu cầu sửa chữa cho hợp đồng đang hiệu lực." });
            var homNay = DateOnly.FromDateTime(DateTime.Today);
            if (homNay < hd.NgayBatDau || homNay > hd.NgayKetThuc)
                return BadRequest(new { success = false, message = "Chỉ báo sửa chữa trong thời gian thuê của hợp đồng." });
            maPhong = hd.MaPhong;
            maKhach = hd.MaKhachDaiDien;
        }
        if (maPhong <= 0 || maKhach <= 0)
            return BadRequest(new { success = false, message = "Cần mã hợp đồng (hoặc mã phòng và mã khách)." });

        string? noiDung = dto.NoiDung ?? dto.MoTa;
        if (string.IsNullOrWhiteSpace(noiDung) || noiDung.Trim().Length > 500)
            return BadRequest(new { success = false, message = "Nội dung sửa chữa phải từ 1 đến 500 ký tự." });
        if (!await _ctx.Phongs.AnyAsync(p => p.MaPhong == maPhong))
            return NotFound(new { success = false, message = "Không tìm thấy phòng." });
        if (!await _ctx.KhachThues.AnyAsync(k => k.MaKhach == maKhach))
            return NotFound(new { success = false, message = "Không tìm thấy khách thuê." });
        if (!await NghiepVu.KhachDangThuePhongAsync(_ctx, maPhong, maKhach))
            return BadRequest(new { success = false, message = "Khách thuê không đang cư trú ở phòng đã chọn." });

        try
        {
            var yc = new YeuCauSuaChua
            {
                MaPhong   = maPhong,
                MaKhach   = maKhach,
                NoiDung   = noiDung.Trim(),
                NgayBao   = dto.NgayYeuCau ?? DateTime.Now,
                TrangThai = NghiepVu.SuaChua.ChoTiepNhan,
                KhachXacNhan = false
            };
            _ctx.YeuCauSuaChuas.Add(yc);
            await _ctx.SaveChangesAsync();
            return Ok(new { success = true, message = "Thêm yêu cầu thành công", data = new { yc.MaYeuCau } });
        }
        catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.InnerException?.Message ?? ex.Message }); }
    }

    [HttpPut("{id}")]
    [HttpPut("/api/YeuCauSuaChua/{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] SuaChuaUpdateDto model)
    {
        if (model.ChiPhi is < 0) return BadRequest(new { success = false, message = "Chi phí không được âm." });
        if (model.MaTaiSanCaThe is <= 0)
            return BadRequest(new { success = false, message = "Chọn hiện vật hợp lệ trong phòng." });
        if (model.TrangThai != null && model.TrangThai != NghiepVu.SuaChua.ChoTiepNhan
            && model.TrangThai != NghiepVu.SuaChua.DangSua && model.TrangThai != NghiepVu.SuaChua.DaHoanThanh)
            return BadRequest(new { success = false, message = "Trạng thái sửa chữa không hợp lệ." });
        try
        {
            await using var tx = _ctx.Database.CurrentTransaction == null
                ? await _ctx.Database.BeginTransactionAsync(IsolationLevel.Serializable) : null;
            var item = await _ctx.YeuCauSuaChuas.Include(y => y.MaPhongNavigation).FirstOrDefaultAsync(y => y.MaYeuCau == id);
            if (item == null) return NotFound(new { message = "Không tìm thấy yêu cầu" });
            string? trangThai = model.TrangThai ?? item.TrangThai;
            if (trangThai != NghiepVu.SuaChua.ChoTiepNhan && trangThai != NghiepVu.SuaChua.DangSua && trangThai != NghiepVu.SuaChua.DaHoanThanh)
                return BadRequest(new { success = false, message = "Trạng thái sửa chữa không hợp lệ." });
            if (item.TrangThai == NghiepVu.SuaChua.DaHoanThanh && trangThai != NghiepVu.SuaChua.DaHoanThanh)
                return BadRequest(new { success = false, message = "Yêu cầu đã hoàn thành. Tạo yêu cầu mới nếu cần sửa chữa tiếp." });
            if (model.KhachXacNhan.HasValue && model.KhachXacNhan != item.KhachXacNhan)
                return BadRequest(new { success = false, message = "Dùng chức năng khách xác nhận sau khi sửa chữa hoàn thành." });
            DateTime? ngayHoanThanh = model.NgayHoanThanh ?? item.NgayHoanThanh;
            if (trangThai == NghiepVu.SuaChua.DaHoanThanh) ngayHoanThanh ??= DateTime.Now;
            if (model.NgayHoanThanh.HasValue && trangThai != NghiepVu.SuaChua.DaHoanThanh)
                return BadRequest(new { success = false, message = "Chỉ ghi ngày hoàn thành khi sửa chữa đã hoàn thành." });
            if (ngayHoanThanh.HasValue && (ngayHoanThanh.Value.Date > DateTime.Today
                || (item.NgayBao.HasValue && ngayHoanThanh.Value.Date < item.NgayBao.Value.Date)))
                return BadRequest(new { success = false, message = "Ngày hoàn thành phải từ ngày báo sửa chữa đến hôm nay." });
            TaiSanCaThe? unit = null;
            if (model.MaTaiSanCaThe.HasValue)
            {
                if (trangThai != NghiepVu.SuaChua.DaHoanThanh || item.TrangThai == NghiepVu.SuaChua.DaHoanThanh)
                    return BadRequest(new { success = false, message = "Chỉ gắn hiện vật khi hoàn thành yêu cầu lần đầu." });
                unit = await _ctx.TaiSanCaThes.FirstOrDefaultAsync(x => x.MaTaiSanCaThe == model.MaTaiSanCaThe &&
                    x.MaPhong == item.MaPhong && x.TrangThai == "Đang sử dụng");
                if (unit == null)
                    return BadRequest(new { success = false, message = "Hiện vật không còn thuộc phòng của yêu cầu sửa chữa." });
                if (await _ctx.LichSuTaiSans.AnyAsync(x => x.MaYeuCau == id))
                    return BadRequest(new { success = false, message = "Yêu cầu đã được ghi vào lịch sử hiện vật." });
            }
            string dauHieu = $"(yêu cầu sửa chữa số {item.MaYeuCau})";
            var phieuChi = await _ctx.PhieuThuChis.FirstOrDefaultAsync(p => p.LoaiPhieu == NghiepVu.PhieuChi
                && p.NoiDung != null && p.NoiDung.Contains(dauHieu));
            if (phieuChi != null && model.ChiPhi.HasValue && model.ChiPhi != phieuChi.SoTien)
                return BadRequest(new { success = false, message = "Chi phí sửa chữa đã được hạch toán. Điều chỉnh phiếu chi trước khi sửa chi phí." });
            item.TrangThai = trangThai;
            item.ChiPhi = model.ChiPhi ?? item.ChiPhi;
            item.NgayHoanThanh = trangThai == NghiepVu.SuaChua.DaHoanThanh ? ngayHoanThanh : null;

            // Hạch toán một lần, kể cả khi chi phí được bổ sung sau lần hoàn thành miễn phí.
            if (item.TrangThai == NghiepVu.SuaChua.DaHoanThanh && item.ChiPhi is > 0 && phieuChi == null)
            {
                item.NgayHoanThanh ??= DateTime.Now;
                string noiDung = (item.NoiDung ?? "Sửa chữa").Trim();
                if (noiDung.Length > 120) noiDung = noiDung[..120] + "…";
                NghiepVu.GhiPhieu(_ctx, NghiepVu.PhieuChi,
                    $"{noiDung} {NghiepVu.ChuThuongDau(item.MaPhongNavigation?.TenPhong ?? $"phòng #{item.MaPhong}")} {dauHieu}",
                    item.ChiPhi.Value, null);
            }
            if (unit != null)
            {
                string summary = (item.NoiDung ?? "Sửa chữa hiện vật").Trim();
                if (summary.Length > 420) summary = summary[..420] + "…";
                _ctx.LichSuTaiSans.Add(new LichSuTaiSan
                {
                    MaTaiSanCaThe = unit.MaTaiSanCaThe, MaYeuCau = item.MaYeuCau,
                    LoaiSuKien = "Sửa chữa", NgaySuKien = item.NgayHoanThanh!.Value.Date,
                    ChiPhi = item.ChiPhi ?? 0, NoiDung = $"Yêu cầu sửa chữa #{item.MaYeuCau}: {summary}"
                });
            }
            else if (item.TrangThai == NghiepVu.SuaChua.DaHoanThanh)
            {
                // Phiếu chi có thể được bổ sung sau lần hoàn thành miễn phí.
                var linkedLog = await _ctx.LichSuTaiSans.FirstOrDefaultAsync(x => x.MaYeuCau == id);
                if (linkedLog != null) linkedLog.ChiPhi = item.ChiPhi ?? 0;
            }
            await _ctx.SaveChangesAsync();
            if (tx != null) await tx.CommitAsync();
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
            if (item.TrangThai != NghiepVu.SuaChua.ChoTiepNhan || item.ChiPhi is > 0 ||
                await _ctx.LichSuTaiSans.AnyAsync(x => x.MaYeuCau == id))
                return BadRequest(new { success = false, message = "Chỉ xóa được yêu cầu chờ tiếp nhận, chưa có chi phí hay lịch sử tài sản." });
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

public class SuaChuaUpdateDto
{
    public string? TrangThai { get; set; }
    public decimal? ChiPhi { get; set; }
    public DateTime? NgayHoanThanh { get; set; }
    public bool? KhachXacNhan { get; set; }
    public int? MaTaiSanCaThe { get; set; }
}
