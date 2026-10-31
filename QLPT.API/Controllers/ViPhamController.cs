using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLPT.API.Models;
using QLPT.API.Services;

namespace QLPT.API.Controllers;

/// <summary>Quy trình: Vi phạm → Xử lý → Phản hồi/Thông báo → Nhắc nhở.</summary>
[Route("api/[controller]")]
[ApiController]
public class ViPhamController : ControllerBase
{
    private readonly Klcn052QuanLyPhongTroContext _ctx;
    public ViPhamController(Klcn052QuanLyPhongTroContext ctx) => _ctx = ctx;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            var list = await _ctx.ViPhams
                .Select(v => new
                {
                    maViPham     = v.MaViPham,
                    maPhong      = v.MaPhong,
                    tenPhong     = v.MaPhongNavigation.TenPhong,
                    maKhach      = v.MaKhach,
                    tenKhach     = v.MaKhachNavigation.HoTen,
                    noiDung      = v.NoiDung,
                    ngayViPham   = v.NgayViPham,
                    tienPhat     = v.TienPhat,
                    duocTinhHoaDon = v.DuocTinhHoaDon,
                    maHoaDon = _ctx.ChiTietHoaDons.Where(c => c.MaViPham == v.MaViPham).Select(c => (int?)c.MaHoaDon).FirstOrDefault(),
                    hinhThucXuLy = v.HinhThucXuLy,
                    trangThai    = v.TrangThai
                })
                .OrderByDescending(v => v.ngayViPham)
                .ToListAsync();
            return Ok(list);
        }
        catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }

    // WPF gửi { maHopDong, loaiViPham, moTa, ngayViPham, mucPhat }. Bản cũ bind thẳng
    // entity ViPham nên mọi trường đều rơi và MaPhong = MaKhach = 0 → lỗi khóa ngoại.
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] TaoViPhamDto dto)
    {
        int maPhong = dto.MaPhong ?? 0, maKhach = dto.MaKhach ?? 0;
        if (dto.MaHopDong is int maHd)
        {
            var hd = await _ctx.HopDongs.FindAsync(maHd);
            if (hd == null) return NotFound(new { success = false, message = $"Không tìm thấy hợp đồng #{maHd}." });
            if (dto.MaPhong is int phongDuocGui && phongDuocGui != hd.MaPhong)
                return BadRequest(new { success = false, message = "Phòng được gửi không thuộc hợp đồng đã chọn." });
            if (dto.MaKhach is int khachDuocGui && khachDuocGui != hd.MaKhachDaiDien)
                return BadRequest(new { success = false, message = "Khách được gửi không phải khách đại diện của hợp đồng đã chọn." });
            maPhong = hd.MaPhong;
            if (maKhach <= 0) maKhach = hd.MaKhachDaiDien;
        }
        if (maPhong <= 0 || maKhach <= 0)
            return BadRequest(new { success = false, message = "Cần mã hợp đồng (hoặc mã phòng và mã khách)." });

        // NoiDung là cột duy nhất mô tả vi phạm: ghép "loại: mô tả".
        string? noiDung = dto.NoiDung;
        if (string.IsNullOrWhiteSpace(noiDung))
            noiDung = string.Join(": ", new[] { dto.LoaiViPham, dto.MoTa }.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!.Trim()));
        if (string.IsNullOrWhiteSpace(noiDung))
            return BadRequest(new { success = false, message = "Nhập nội dung vi phạm." });

        decimal? phat = dto.TienPhat ?? dto.MucPhat;
        if (phat is < 0) return BadRequest(new { success = false, message = "Mức phạt không được âm." });

        try
        {
            var vp = new ViPham
            {
                MaPhong      = maPhong,
                MaKhach      = maKhach,
                NoiDung      = noiDung,
                NgayViPham   = dto.NgayViPham ?? DateTime.Now,
                TienPhat     = phat,
                HinhThucXuLy = dto.HinhThucXuLy,
                TrangThai    = NghiepVu.ViPham.ChuaXuLy,
                DuongDanAnh  = dto.DuongDanAnh
            };
            _ctx.ViPhams.Add(vp);
            await _ctx.SaveChangesAsync();
            return Ok(new { success = true, message = "Ghi nhận vi phạm thành công", data = new { vp.MaViPham } });
        }
        catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.InnerException?.Message ?? ex.Message }); }
    }

    // Bước "Xử lý": chốt hình thức xử lý + tiền phạt, rồi thông báo cho khách (bước "Thông báo").
    [HttpPut("{id}/xu-ly")]
    public async Task<IActionResult> XuLy(int id, [FromBody] XuLyViPhamDto dto)
    {
        var vp = await _ctx.ViPhams.Include(v => v.MaPhongNavigation).Include(v => v.MaKhachNavigation)
            .FirstOrDefaultAsync(v => v.MaViPham == id);
        if (vp == null) return NotFound(new { success = false, message = "Không tìm thấy vi phạm" });
        if (vp.TrangThai == NghiepVu.ViPham.DaXuLy)
            return BadRequest(new { success = false, message = "Vi phạm này đã được xử lý." });
        if (string.IsNullOrWhiteSpace(dto.HinhThucXuLy))
            return BadRequest(new { success = false, message = "Nhập hình thức xử lý (nhắc nhở, phạt tiền…)." });
        if (dto.TienPhat is < 0) return BadRequest(new { success = false, message = "Tiền phạt không được âm." });

        try
        {
            vp.HinhThucXuLy = dto.HinhThucXuLy.Trim();
            vp.TienPhat     = dto.TienPhat ?? vp.TienPhat;
            vp.TrangThai    = NghiepVu.ViPham.DaXuLy;

            // Xử lý chỉ chốt mức phạt; chưa có thao tác nhận tiền. Không ghi phiếu thu
            // trước khi thực sự thu, tránh doanh thu ảo và thu trùng nếu sau này đưa phạt vào hóa đơn.

            bool daGui = false;
            if (dto.GuiThongBao && await NghiepVu.TaiKhoanCuaKhachAsync(_ctx, vp.MaKhach) is int tk)
            {
                NghiepVu.TaoThongBao(_ctx, "Thông báo xử lý vi phạm",
                    $"Vi phạm ngày {vp.NgayViPham:dd/MM/yyyy} tại {vp.MaPhongNavigation?.TenPhong}: {vp.NoiDung}. " +
                    $"Hình thức xử lý: {vp.HinhThucXuLy}." +
                    (vp.TienPhat is > 0 ? $" Tiền phạt: {NghiepVu.Tien(vp.TienPhat)}." : ""),
                    NghiepVu.ThongBao.LoaiCaNhan, new[] { tk });
                daGui = true;
            }

            await _ctx.SaveChangesAsync();
            return Ok(new
            {
                success = true,
                message = daGui ? "Đã xử lý và gửi thông báo cho khách."
                       : dto.GuiThongBao ? "Đã xử lý. Khách chưa có tài khoản nên không gửi được thông báo."
                       : "Đã xử lý."
            });
        }
        catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.InnerException?.Message ?? ex.Message }); }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] ViPham model)
    {
        try
        {
            var item = await _ctx.ViPhams.FindAsync(id);
            if (item == null) return NotFound(new { message = "Không tìm thấy vi phạm" });
            if (item.TrangThai == NghiepVu.ViPham.DaXuLy)
                return BadRequest(new { message = "Vi phạm đã xử lý không thể sửa vì khách đã có thể nhận thông báo." });
            if (model.TrangThai != null && model.TrangThai != NghiepVu.ViPham.ChuaXuLy)
                return BadRequest(new { message = "Dùng chức năng Xử lý để chốt vi phạm và gửi thông báo." });
            if (model.TienPhat is < 0 || model.NoiDung != null &&
                (string.IsNullOrWhiteSpace(model.NoiDung) || model.NoiDung.Trim().Length > 500))
                return BadRequest(new { message = "Tiền phạt hoặc nội dung vi phạm không hợp lệ." });
            item.HinhThucXuLy  = model.HinhThucXuLy  ?? item.HinhThucXuLy;
            item.TienPhat      = model.TienPhat       ?? item.TienPhat;
            item.NoiDung       = model.NoiDung?.Trim() ?? item.NoiDung;
            await _ctx.SaveChangesAsync();
            return Ok(new { success = true, message = "Cập nhật thành công" });
        }
        catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var item = await _ctx.ViPhams.FindAsync(id);
            if (item == null) return NotFound(new { message = "Không tìm thấy vi phạm" });
            if (item.TrangThai == NghiepVu.ViPham.DaXuLy)
                return BadRequest(new { message = "Vi phạm đã xử lý không thể xóa để giữ lịch sử và thông báo đã gửi." });
            _ctx.ViPhams.Remove(item);
            await _ctx.SaveChangesAsync();
            return Ok(new { success = true, message = "Xóa thành công" });
        }
        catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }
}

public class TaoViPhamDto
{
    public int? MaHopDong { get; set; }
    public int? MaPhong { get; set; }
    public int? MaKhach { get; set; }
    public string? LoaiViPham { get; set; }
    public string? MoTa { get; set; }
    public string? NoiDung { get; set; }
    public DateTime? NgayViPham { get; set; }
    public decimal? MucPhat { get; set; }
    public decimal? TienPhat { get; set; }
    public string? HinhThucXuLy { get; set; }
    public string? DuongDanAnh { get; set; }
}

public class XuLyViPhamDto
{
    public string? HinhThucXuLy { get; set; }
    public decimal? TienPhat { get; set; }
    public bool GuiThongBao { get; set; } = true;
}
