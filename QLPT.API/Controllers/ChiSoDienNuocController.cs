using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLPT.API.Models;
using QLPT.API.Services;
using System.Security.Claims;

namespace QLPT.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ChiSoDienNuocController : ControllerBase
{
    private readonly Klcn052QuanLyPhongTroContext _ctx;
    public ChiSoDienNuocController(Klcn052QuanLyPhongTroContext ctx) => _ctx = ctx;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int? maPhong, [FromQuery] int? thang, [FromQuery] int? nam)
    {
        if (maPhong is <= 0 || thang is < 1 or > 12 || nam is < 1900 or > 2100)
            return BadRequest(new { message = "Phòng hoặc kỳ chỉ số không hợp lệ." });
        try
        {
            var query = _ctx.ChiSoDienNuocs.AsNoTracking().AsQueryable();
            if (User.IsInRole("Manager") && int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int managerId))
                query = query.Where(x => x.MaPhongNavigation.MaCoSo.HasValue && _ctx.QuanLyCoSos.Any(a =>
                    a.MaTaiKhoan == managerId && a.MaCoSo == x.MaPhongNavigation.MaCoSo.Value));
            if (maPhong.HasValue) query = query.Where(x => x.MaPhong == maPhong.Value);
            if (thang.HasValue) query = query.Where(x => x.Thang == thang.Value);
            if (nam.HasValue) query = query.Where(x => x.Nam == nam.Value);
            var list = await query
                .Include(x => x.MaPhongNavigation)
                .Select(x => new {
                    x.MaChiSo, x.MaPhong, x.LoaiChiSo, x.Thang, x.Nam,
                    x.ChiSoCu, x.ChiSoMoi, x.NgayGhi, x.ChiSoXacNhan, x.ChiSoOcr, x.DuongDanAnh,
                    TenPhong = x.MaPhongNavigation != null ? x.MaPhongNavigation.TenPhong : ""
                })
                .OrderByDescending(x => x.Nam).ThenByDescending(x => x.Thang)
                .ToListAsync();
            return Ok(list);
        }
        catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }

    [HttpGet("cuoi/{maPhong}/{loaiChiSo}")]
    public async Task<IActionResult> GetPrevious(int maPhong, string loaiChiSo, [FromQuery] int thang, [FromQuery] int nam)
    {
        if (maPhong <= 0 || loaiChiSo is not ("Điện" or "Nước") || thang is < 1 or > 12 || nam is < 1900 or > 2100)
            return BadRequest(new { message = "Phòng, loại chỉ số hoặc kỳ không hợp lệ." });
        try
        {
            var value = await _ctx.ChiSoDienNuocs.AsNoTracking()
                .Where(x => x.MaPhong == maPhong && x.LoaiChiSo == loaiChiSo && (x.Nam < nam || x.Nam == nam && x.Thang < thang))
                .OrderByDescending(x => x.Nam).ThenByDescending(x => x.Thang)
                .Select(x => x.ChiSoMoi ?? x.ChiSoXacNhan ?? 0).FirstOrDefaultAsync();
            return Ok(new { chiSoMoi = value });
        }
        catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }

    public static string? Validate(string? type, int month, int year, decimal? oldValue, decimal? newValue, decimal? confirmed, decimal? ocr)
    {
        if (type is not ("Điện" or "Nước")) return "Loại chỉ số phải là Điện hoặc Nước.";
        if (month is < 1 or > 12 || year is < 1900 or > 2100) return "Kỳ chỉ số không hợp lệ.";
        if (oldValue is null or < 0 || newValue is null or < 0) return "Chỉ số cũ và mới phải là số không âm.";
        if (newValue < oldValue) return "Chỉ số mới không được nhỏ hơn chỉ số cũ.";
        if (new[] { oldValue, newValue, confirmed, ocr }.Any(x => x is < 0 or > 9999999999999999.99m || x.HasValue && decimal.Round(x.Value, 2) != x.Value))
            return "Chỉ số phải không âm, tối đa 2 chữ số thập phân và nằm trong giới hạn lưu trữ.";
        if (confirmed.HasValue && confirmed != newValue) return "Chỉ số xác nhận phải trùng chỉ số mới đã đối chiếu.";
        return null;
    }

    Task<bool> HasInvoice(int roomId, int month, int year) => _ctx.HoaDons.AnyAsync(h => h.MaHopDongNavigation.MaPhong == roomId && h.Thang == month && h.Nam == year);

    async Task<string?> ValidateContinuity(int roomId, string type, int month, int year, decimal oldValue, decimal newValue)
    {
        var previous = await _ctx.ChiSoDienNuocs.AsNoTracking()
            .Where(x => x.MaPhong == roomId && x.LoaiChiSo == type && (x.Nam < year || x.Nam == year && x.Thang < month))
            .OrderByDescending(x => x.Nam).ThenByDescending(x => x.Thang).FirstOrDefaultAsync();
        if (previous is not null && (previous.ChiSoMoi ?? previous.ChiSoXacNhan) is decimal last && last != oldValue)
            return $"Chỉ số cũ phải bằng chỉ số cuối kỳ trước ({last:0.##}).";
        var next = await _ctx.ChiSoDienNuocs.AsNoTracking()
            .Where(x => x.MaPhong == roomId && x.LoaiChiSo == type && (x.Nam > year || x.Nam == year && x.Thang > month))
            .OrderBy(x => x.Nam).ThenBy(x => x.Thang).FirstOrDefaultAsync();
        if (next?.ChiSoCu is decimal start && start != newValue)
            return $"Chỉ số mới phải khớp chỉ số đầu kỳ sau đã ghi ({start:0.##}).";
        return null;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ChiSoDienNuoc model)
    {
        if (model.MaPhong <= 0) return BadRequest(new { success = false, message = "Vui lòng chọn phòng hợp lệ." });
        var error = Validate(model.LoaiChiSo, model.Thang, model.Nam, model.ChiSoCu, model.ChiSoMoi, model.ChiSoXacNhan, model.ChiSoOcr);
        if (error != null) return BadRequest(new { success = false, message = error });
        try
        {
            var room = await _ctx.Phongs.AsNoTracking()
                .Where(x => x.MaPhong == model.MaPhong)
                .Select(x => new { x.TrangThai }).FirstOrDefaultAsync();
            if (room == null) return NotFound(new { message = "Không tìm thấy phòng." });
            if (room.TrangThai != NghiepVu.Phong.DangThue ||
                !await _ctx.HopDongs.AnyAsync(h => h.MaPhong == model.MaPhong && h.TrangThai == NghiepVu.HopDong.HieuLuc))
                return BadRequest(new { success = false, message = "Chỉ được ghi chỉ số cho phòng đang thuê có hợp đồng hiệu lực. Phòng đã trả hoặc hợp đồng đã thanh lý không thể ghi thêm." });
            if (await _ctx.ChiSoDienNuocs.AnyAsync(x => x.MaPhong == model.MaPhong && x.LoaiChiSo == model.LoaiChiSo && x.Thang == model.Thang && x.Nam == model.Nam))
                return BadRequest(new { success = false, message = "Kỳ này đã có chỉ số. Hãy chọn Sửa chỉ số thay vì ghi lại." });
            if (await HasInvoice(model.MaPhong, model.Thang, model.Nam)) return BadRequest(new { message = "Kỳ này đã phát hành hóa đơn; không thể thay đổi dữ liệu tính tiền." });
            error = await ValidateContinuity(model.MaPhong, model.LoaiChiSo, model.Thang, model.Nam, model.ChiSoCu!.Value, model.ChiSoMoi!.Value);
            if (error != null) return BadRequest(new { message = error });
            model = new ChiSoDienNuoc { MaPhong = model.MaPhong, LoaiChiSo = model.LoaiChiSo, Thang = model.Thang, Nam = model.Nam,
                ChiSoCu = model.ChiSoCu, ChiSoMoi = model.ChiSoMoi, ChiSoXacNhan = model.ChiSoXacNhan ?? model.ChiSoMoi,
                ChiSoOcr = model.ChiSoOcr, DuongDanAnh = model.DuongDanAnh, NgayGhi = model.NgayGhi ?? DateOnly.FromDateTime(DateTime.Today) };
            _ctx.ChiSoDienNuocs.Add(model);
            await _ctx.SaveChangesAsync();
            return Ok(new { success = true, message = "Thêm chỉ số thành công", data = model });
        }
        catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] ChiSoUpdateDto model)
    {
        try
        {
            var item = await _ctx.ChiSoDienNuocs.FindAsync(id);
            if (item == null) return NotFound(new { message = "Không tìm thấy chỉ số" });
            if (await HasInvoice(item.MaPhong, item.Thang, item.Nam)) return BadRequest(new { message = "Kỳ này đã phát hành hóa đơn; không thể sửa chỉ số đã dùng để tính tiền." });
            decimal? oldValue = model.ChiSoCu ?? item.ChiSoCu, newValue = model.ChiSoMoi ?? model.ChiSoXacNhan ?? item.ChiSoMoi;
            var error = Validate(item.LoaiChiSo, item.Thang, item.Nam, oldValue, newValue, model.ChiSoXacNhan, model.ChiSoOcr ?? item.ChiSoOcr);
            if (error != null) return BadRequest(new { message = error });
            error = await ValidateContinuity(item.MaPhong, item.LoaiChiSo, item.Thang, item.Nam, oldValue!.Value, newValue!.Value);
            if (error != null) return BadRequest(new { message = error });
            item.ChiSoCu = oldValue; item.ChiSoMoi = newValue; item.ChiSoXacNhan = newValue;
            item.ChiSoOcr = model.ChiSoOcr ?? item.ChiSoOcr; item.DuongDanAnh = model.DuongDanAnh ?? item.DuongDanAnh;
            item.NgayGhi = model.NgayGhi ?? DateOnly.FromDateTime(DateTime.Today);
            await _ctx.SaveChangesAsync();
            return Ok(new { success = true, message = "Cập nhật thành công", data = item });
        }
        catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var item = await _ctx.ChiSoDienNuocs.FindAsync(id);
            if (item == null) return NotFound(new { message = "Không tìm thấy chỉ số" });
            if (await HasInvoice(item.MaPhong, item.Thang, item.Nam)) return BadRequest(new { message = "Kỳ này đã phát hành hóa đơn; không thể xóa chỉ số đã dùng để tính tiền." });
            _ctx.ChiSoDienNuocs.Remove(item);
            await _ctx.SaveChangesAsync();
            return Ok(new { success = true, message = "Xóa thành công" });
        }
        catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }
}

public sealed class ChiSoUpdateDto
{
    public decimal? ChiSoCu { get; set; }
    public decimal? ChiSoMoi { get; set; }
    public decimal? ChiSoXacNhan { get; set; }
    public decimal? ChiSoOcr { get; set; }
    public string? DuongDanAnh { get; set; }
    public DateOnly? NgayGhi { get; set; }
}
