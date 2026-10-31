using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Security.Claims;
using QLPT.API.Models;
using QLPT.API.Services;

namespace QLPT.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class TaiSanController : ControllerBase
{
    private const decimal MaxMoney = 9999999999999999.99m;
    private readonly Klcn052QuanLyPhongTroContext _ctx;
    public TaiSanController(Klcn052QuanLyPhongTroContext ctx) => _ctx = ctx;

    private static bool ValidMoney(decimal amount) => amount >= 0 && amount <= MaxMoney && decimal.Round(amount, 2) == amount;

    [HttpGet("ca-the")]
    public async Task<IActionResult> GetCaTheAll()
    {
        var query = _ctx.TaiSanCaThes.AsNoTracking().AsQueryable();
        if (User.IsInRole("Manager"))
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int managerId))
                return Forbid();
            var sites = await _ctx.QuanLyCoSos.AsNoTracking().Where(x => x.MaTaiKhoan == managerId)
                .Select(x => x.MaCoSo).ToListAsync();
            query = query.Where(x => x.MaPhongNavigation != null && x.MaPhongNavigation.MaCoSo.HasValue &&
                sites.Contains(x.MaPhongNavigation.MaCoSo.Value));
        }
        return Ok(await query.OrderBy(x => x.MaTaiSanCaThe)
            .Select(x => new { x.MaTaiSanCaThe, x.MaDinhDanh, x.MaTaiSan,
                TenTaiSan = x.MaTaiSanNavigation.TenTaiSan, x.MaPhong,
                TenPhong = x.MaPhongNavigation == null ? "" : x.MaPhongNavigation.TenPhong,
                TenCoSo = x.MaPhongNavigation == null || x.MaPhongNavigation.MaCoSoNavigation == null ? "" :
                    x.MaPhongNavigation.MaCoSoNavigation.TenCoSo,
                x.TinhTrang, x.TrangThai, x.NgayMua, x.GiaMua, x.GhiChu,
                SoLanSua = x.LichSu.Count(h => h.LoaiSuKien == "Sửa chữa"),
                TongChiPhiSua = x.LichSu.Where(h => h.LoaiSuKien == "Sửa chữa").Sum(h => (decimal?)h.ChiPhi) ?? 0m })
            .ToListAsync());
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        try { return Ok(await _ctx.TaiSans.ToListAsync()); }
        catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }

    [HttpGet("ton-kho")]
    public async Task<IActionResult> GetTonKho()
    {
        var counts = await _ctx.TaiSanCaThes.AsNoTracking()
            .GroupBy(x => new { x.MaTaiSan, x.TrangThai })
            .Select(g => new { g.Key.MaTaiSan, g.Key.TrangThai, SoLuong = g.Count() })
            .ToListAsync();
        return Ok(counts);
    }

    // Một phiếu nhập tạo từng hiện vật riêng, mỗi chiếc được SQL Server cấp mã TS duy nhất.
    [HttpPost("nhap-kho")]
    public async Task<IActionResult> NhapKho([FromBody] NhapKhoTaiSanDto dto)
    {
        if (dto.MaTaiSan <= 0 || dto.SoLuong is < 1 or > 500 ||
            dto.NgayMua is { Year: < 2000 } || dto.NgayMua?.Date > DateTime.Today ||
            dto.GiaMua is decimal price && !ValidMoney(price) ||
            string.IsNullOrWhiteSpace(dto.TinhTrang) || dto.TinhTrang.Trim().Length > 100 ||
            dto.GhiChu?.Length > 500)
            return BadRequest(new { message = "Mẫu tài sản, số lượng (1–500), ngày mua, giá mỗi chiếc hoặc tình trạng không hợp lệ." });
        if (!await _ctx.TaiSans.AnyAsync(x => x.MaTaiSan == dto.MaTaiSan))
            return NotFound(new { message = "Không tìm thấy mẫu tài sản." });

        var units = new List<TaiSanCaThe>(dto.SoLuong);
        for (int i = 0; i < dto.SoLuong; i++)
        {
            var unit = new TaiSanCaThe
            {
                MaTaiSan = dto.MaTaiSan, TrangThai = "Trong kho", TinhTrang = dto.TinhTrang.Trim(),
                NgayMua = dto.NgayMua?.Date, GiaMua = dto.GiaMua, GhiChu = dto.GhiChu?.Trim()
            };
            unit.LichSu.Add(new LichSuTaiSan { LoaiSuKien = "Nhập kho", NgaySuKien = DateTime.Today,
                ChiPhi = 0, NoiDung = $"Nhập kho; lô {dto.SoLuong} hiện vật cùng mẫu." });
            units.Add(unit);
        }
        await using var tx = _ctx.Database.CurrentTransaction == null ? await _ctx.Database.BeginTransactionAsync() : null;
        _ctx.TaiSanCaThes.AddRange(units);
        await _ctx.SaveChangesAsync();
        if (tx != null) await tx.CommitAsync();
        return Ok(new { success = true, soLuong = units.Count,
            maDinhDanh = units.Select(x => x.MaDinhDanh).ToArray() });
    }

    // Xuất nhiều chiếc cùng mẫu: chọn các hiện vật tồn kho lâu nhất, giữ nguyên mã và lịch sử.
    [HttpPost("xuat-kho")]
    public async Task<IActionResult> XuatKho([FromBody] XuatKhoTaiSanDto dto)
    {
        if (dto.MaTaiSan <= 0 || dto.MaPhong <= 0 || dto.SoLuong is < 1 or > 500)
            return BadRequest(new { message = "Chọn mẫu, phòng và số lượng từ 1 đến 500." });
        await using var tx = _ctx.Database.CurrentTransaction == null
            ? await _ctx.Database.BeginTransactionAsync(IsolationLevel.Serializable) : null;
        var room = await _ctx.Phongs.FindAsync(dto.MaPhong);
        if (room == null) return NotFound(new { message = "Không tìm thấy phòng nhận." });
        if (room.TrangThai is not (NghiepVu.Phong.Trong or NghiepVu.Phong.BaoTri) ||
            await _ctx.HopDongs.AnyAsync(x => x.MaPhong == dto.MaPhong && x.TrangThai == NghiepVu.HopDong.HieuLuc))
            return BadRequest(new { message = "Chỉ chuyển hiện vật vào phòng chưa có hợp đồng hiệu lực." });
        var units = await _ctx.TaiSanCaThes
            .Where(x => x.MaTaiSan == dto.MaTaiSan && x.TrangThai == "Trong kho" && x.MaPhong == null && x.MaTaiSanPhong == null)
            .OrderBy(x => x.NgayMua == null).ThenBy(x => x.NgayMua)
            .ThenBy(x => x.MaTaiSanCaThe).Take(dto.SoLuong).ToListAsync();
        if (units.Count != dto.SoLuong)
            return BadRequest(new { message = $"Kho chỉ còn {units.Count} hiện vật đủ điều kiện chuyển." });
        var group = await _ctx.TaiSanPhongs.SingleOrDefaultAsync(x => x.MaPhong == dto.MaPhong && x.MaTaiSan == dto.MaTaiSan);
        if (group == null)
        {
            group = new TaiSanPhong { MaPhong = dto.MaPhong, MaTaiSan = dto.MaTaiSan,
                SoLuong = 0, TinhTrang = units[0].TinhTrang };
            _ctx.TaiSanPhongs.Add(group);
            await _ctx.SaveChangesAsync();
        }
        group.SoLuong = (group.SoLuong ?? 0) + units.Count;
        foreach (var unit in units)
        {
            unit.MaPhong = dto.MaPhong;
            unit.MaTaiSanPhong = group.MaTaiSanPhong;
            unit.TrangThai = "Đang sử dụng";
            _ctx.LichSuTaiSans.Add(new LichSuTaiSan { MaTaiSanCaThe = unit.MaTaiSanCaThe,
                LoaiSuKien = "Điều chuyển", NgaySuKien = DateTime.Today, ChiPhi = 0,
                NoiDung = $"Đưa từ kho vào {await RoomLocationAsync(room)}." });
        }
        await _ctx.SaveChangesAsync();
        if (tx != null) await tx.CommitAsync();
        return Ok(new { success = true, soLuong = units.Count,
            maDinhDanh = units.Select(x => x.MaDinhDanh).ToArray() });
    }

    [HttpGet("phong/{maPhong}")]
    public async Task<IActionResult> GetByPhong(int maPhong)
    {
        try
        {
            var list = await _ctx.TaiSanPhongs
                .Where(x => x.MaPhong == maPhong)
                .Include(x => x.MaTaiSanNavigation)
                .Select(x => new {
                    x.MaTaiSanPhong, x.MaPhong, x.MaTaiSan, x.SoLuong, x.TinhTrang, x.GhiChu,
                    TenTaiSan = x.MaTaiSanNavigation != null ? x.MaTaiSanNavigation.TenTaiSan : "",
                    TenPhong = x.MaPhongNavigation != null ? x.MaPhongNavigation.TenPhong : ""
                })
                .ToListAsync();
            return Ok(list);
        }
        catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }

    // Tài sản mẫu có thể xuất hiện nhiều lần, nhưng mỗi hiện vật có mã và lịch sử riêng.
    [HttpGet("ca-the/phong/{maPhong:int}")]
    public async Task<IActionResult> GetCaTheTheoPhong(int maPhong)
    {
        if (!await _ctx.Phongs.AnyAsync(p => p.MaPhong == maPhong)) return NotFound(new { message = "Không tìm thấy phòng." });
        return Ok(await _ctx.TaiSanCaThes.AsNoTracking()
            .Where(x => x.MaPhong == maPhong && x.TrangThai != "Đã thanh lý")
            .OrderBy(x => x.MaTaiSanCaThe)
            .Select(x => new { x.MaTaiSanCaThe, x.MaDinhDanh, x.MaTaiSan, TenTaiSan = x.MaTaiSanNavigation.TenTaiSan,
                x.MaPhong, x.TinhTrang, x.TrangThai, x.NgayMua, x.GiaMua, x.GhiChu,
                SoLanSua = x.LichSu.Count(h => h.LoaiSuKien == "Sửa chữa"),
                TongChiPhiSua = x.LichSu.Where(h => h.LoaiSuKien == "Sửa chữa").Sum(h => (decimal?)h.ChiPhi) ?? 0m })
            .ToListAsync());
    }

    [HttpGet("ca-the/{id:int}/lich-su")]
    public async Task<IActionResult> GetLichSu(int id)
    {
        if (!await _ctx.TaiSanCaThes.AnyAsync(x => x.MaTaiSanCaThe == id)) return NotFound(new { message = "Không tìm thấy hiện vật." });
        return Ok(await _ctx.LichSuTaiSans.AsNoTracking().Where(x => x.MaTaiSanCaThe == id)
            .OrderByDescending(x => x.NgaySuKien).ThenByDescending(x => x.MaLichSu)
            .Select(x => new { x.MaLichSu, x.LoaiSuKien, x.NgaySuKien, x.ChiPhi, x.NoiDung, x.MaYeuCau }).ToListAsync());
    }

    [HttpPost("ca-the/{id:int}/lich-su")]
    public async Task<IActionResult> AddLichSu(int id, [FromBody] LichSuTaiSanDto dto)
    {
        var item = await _ctx.TaiSanCaThes.FindAsync(id);
        if (item == null) return NotFound(new { message = "Không tìm thấy hiện vật." });
        if (item.TrangThai == "Đã thanh lý") return BadRequest(new { message = "Hiện vật đã thanh lý, không thể thêm sự kiện." });
        if (dto.LoaiSuKien == "Thanh lý" && item.MaPhong is int roomWithAsset &&
            await _ctx.HopDongs.AnyAsync(x => x.MaPhong == roomWithAsset && x.TrangThai == NghiepVu.HopDong.HieuLuc))
            return BadRequest(new { message = "Phòng đang có hợp đồng hiệu lực. Cần bàn giao bổ sung hoặc kết thúc hợp đồng trước khi thanh lý hiện vật." });
        if (dto.LoaiSuKien is not ("Sửa chữa" or "Kiểm kê" or "Bảo dưỡng" or "Thanh lý") ||
            string.IsNullOrWhiteSpace(dto.NoiDung) || dto.NoiDung.Trim().Length > 500 || !ValidMoney(dto.ChiPhi) ||
            dto.NgaySuKien.Date > DateTime.Today || dto.NgaySuKien.Year < 2000)
            return BadRequest(new { message = "Loại, ngày, chi phí hoặc nội dung sự kiện không hợp lệ." });
        if (dto.MaYeuCau.HasValue && !await _ctx.YeuCauSuaChuas.AnyAsync(y => y.MaYeuCau == dto.MaYeuCau && y.MaPhong == item.MaPhong))
            return BadRequest(new { message = "Yêu cầu sửa chữa không thuộc phòng của hiện vật." });
        var log = new LichSuTaiSan { MaTaiSanCaThe = id, LoaiSuKien = dto.LoaiSuKien, NgaySuKien = dto.NgaySuKien.Date,
            ChiPhi = dto.ChiPhi, NoiDung = dto.NoiDung.Trim(), MaYeuCau = dto.MaYeuCau };
        _ctx.LichSuTaiSans.Add(log);
        if (dto.LoaiSuKien == "Thanh lý")
        {
            if (item.MaTaiSanPhong is int groupId)
            {
                var group = await _ctx.TaiSanPhongs.FindAsync(groupId);
                if (group != null) group.SoLuong = Math.Max(0, (group.SoLuong ?? 0) - 1);
            }
            item.TrangThai = "Đã thanh lý"; item.MaPhong = null; item.MaTaiSanPhong = null; item.TinhTrang = "Đã thanh lý";
        }
        await _ctx.SaveChangesAsync();
        return Ok(new { success = true, data = new { log.MaLichSu, log.MaTaiSanCaThe } });
    }

    [HttpPut("ca-the/{id:int}/tinh-trang")]
    public async Task<IActionResult> UpdateTinhTrangCaThe(int id, [FromBody] TinhTrangCaTheDto dto)
    {
        var item = await _ctx.TaiSanCaThes.FindAsync(id);
        if (item == null) return NotFound(new { message = "Không tìm thấy hiện vật." });
        if (item.TrangThai == "Đã thanh lý") return BadRequest(new { message = "Hiện vật đã thanh lý." });
        if (string.IsNullOrWhiteSpace(dto.TinhTrang) || dto.TinhTrang.Trim().Length > 100)
            return BadRequest(new { message = "Tình trạng tối đa 100 ký tự và không được trống." });
        if (dto.NgayMua?.Date > DateTime.Today || dto.GiaMua is decimal price && !ValidMoney(price) || dto.GhiChu?.Length > 500)
            return BadRequest(new { message = "Ngày mua, giá mua hoặc ghi chú không hợp lệ." });
        if (!User.IsInRole("Admin") && (dto.NgayMua.HasValue || dto.GiaMua.HasValue))
            return StatusCode(403, new { message = "Chỉ chủ trọ được sửa ngày mua hoặc giá mua của hiện vật." });
        var changes = new List<string>();
        if (dto.NgayMua.HasValue && dto.NgayMua.Value.Date != item.NgayMua?.Date)
            changes.Add($"Ngày mua: {(item.NgayMua.HasValue ? item.NgayMua.Value.ToString("dd/MM/yyyy") : "chưa ghi")} → {dto.NgayMua.Value:dd/MM/yyyy}");
        if (dto.GiaMua.HasValue && dto.GiaMua.Value != item.GiaMua)
            changes.Add($"Giá mua: {(item.GiaMua.HasValue ? item.GiaMua.Value.ToString("N0") : "chưa ghi")} → {dto.GiaMua.Value:N0} đ");
        if (dto.TinhTrang.Trim() != item.TinhTrang)
            changes.Add($"Tình trạng: {item.TinhTrang} → {dto.TinhTrang.Trim()}");
        item.TinhTrang = dto.TinhTrang.Trim();
        if (dto.NgayMua.HasValue) item.NgayMua = dto.NgayMua.Value.Date;
        if (dto.GiaMua.HasValue) item.GiaMua = dto.GiaMua;
        if (dto.GhiChu != null) item.GhiChu = dto.GhiChu.Trim();
        _ctx.LichSuTaiSans.Add(new LichSuTaiSan { MaTaiSanCaThe = id, LoaiSuKien = "Kiểm kê", NgaySuKien = DateTime.Today,
            NoiDung = changes.Count == 0 ? $"Đã kiểm kê; tình trạng: {item.TinhTrang}." : string.Join("; ", changes), ChiPhi = 0 });
        await _ctx.SaveChangesAsync();
        return Ok(new { success = true });
    }

    // Đưa lại chính hiện vật trong kho vào phòng, giữ nguyên mã và toàn bộ lịch sử.
    [HttpPost("ca-the/{id:int}/gan-phong")]
    public async Task<IActionResult> GanCaTheVaoPhong(int id, [FromBody] GanCaTheVaoPhongDto dto)
    {
        if (id <= 0 || dto.MaPhong <= 0)
            return BadRequest(new { message = "Chọn hiện vật trong kho và phòng nhận." });
        await using var tx = _ctx.Database.CurrentTransaction == null
            ? await _ctx.Database.BeginTransactionAsync(IsolationLevel.Serializable) : null;
        var unit = await _ctx.TaiSanCaThes.FindAsync(id);
        if (unit == null) return NotFound(new { message = "Không tìm thấy hiện vật." });
        if (unit.TrangThai != "Trong kho" || unit.MaPhong != null || unit.MaTaiSanPhong != null)
            return BadRequest(new { message = "Chỉ hiện vật còn trong kho mới được đưa vào phòng." });
        var room = await _ctx.Phongs.FindAsync(dto.MaPhong);
        if (room == null) return NotFound(new { message = "Không tìm thấy phòng nhận." });
        if (room.TrangThai is not (NghiepVu.Phong.Trong or NghiepVu.Phong.BaoTri) ||
            await _ctx.HopDongs.AnyAsync(x => x.MaPhong == room.MaPhong && x.TrangThai == NghiepVu.HopDong.HieuLuc))
            return BadRequest(new { message = "Chỉ đưa hiện vật vào phòng chưa có hợp đồng hiệu lực. Phòng đang thuê cần quy trình bàn giao bổ sung." });

        var group = await _ctx.TaiSanPhongs.SingleOrDefaultAsync(x => x.MaPhong == room.MaPhong && x.MaTaiSan == unit.MaTaiSan);
        if (group == null)
        {
            group = new TaiSanPhong { MaPhong = room.MaPhong, MaTaiSan = unit.MaTaiSan,
                SoLuong = 0, TinhTrang = unit.TinhTrang };
            _ctx.TaiSanPhongs.Add(group);
            await _ctx.SaveChangesAsync();
        }
        group.SoLuong = (group.SoLuong ?? 0) + 1;
        unit.MaPhong = room.MaPhong;
        unit.MaTaiSanPhong = group.MaTaiSanPhong;
        unit.TrangThai = "Đang sử dụng";
        _ctx.LichSuTaiSans.Add(new LichSuTaiSan { MaTaiSanCaThe = id, LoaiSuKien = "Điều chuyển",
            NgaySuKien = DateTime.Today, ChiPhi = 0,
            NoiDung = $"Đưa từ kho vào {await RoomLocationAsync(room)}." });
        await _ctx.SaveChangesAsync();
        if (tx != null) await tx.CommitAsync();
        return Ok(new { success = true, message = "Đã đưa hiện vật vào phòng, giữ nguyên mã và lịch sử.",
            data = new { unit.MaTaiSanCaThe, unit.MaDinhDanh, unit.MaPhong } });
    }

    [HttpPost("ca-the/{id:int}/go-kho")]
    public async Task<IActionResult> GoCaTheVeKho(int id)
    {
        if (id <= 0) return BadRequest(new { message = "Chọn hiện vật cần gỡ khỏi phòng." });
        await using var tx = _ctx.Database.CurrentTransaction == null
            ? await _ctx.Database.BeginTransactionAsync(IsolationLevel.Serializable) : null;
        var unit = await _ctx.TaiSanCaThes.FindAsync(id);
        if (unit == null) return NotFound(new { message = "Không tìm thấy hiện vật." });
        if (unit.TrangThai != "Đang sử dụng" || unit.MaPhong == null || unit.MaTaiSanPhong == null)
            return BadRequest(new { message = "Chỉ hiện vật đang ở trong phòng mới được gỡ về kho." });
        var room = await _ctx.Phongs.FindAsync(unit.MaPhong.Value);
        var group = await _ctx.TaiSanPhongs.FindAsync(unit.MaTaiSanPhong.Value);
        if (room == null || group == null || group.MaPhong != room.MaPhong || group.MaTaiSan != unit.MaTaiSan || group.SoLuong is null or <= 0)
            return BadRequest(new { message = "Dữ liệu hiện vật và số lượng trong phòng chưa khớp; hãy kiểm kê trước khi điều chuyển." });
        if (room.TrangThai is not (NghiepVu.Phong.Trong or NghiepVu.Phong.BaoTri) ||
            await _ctx.HopDongs.AnyAsync(x => x.MaPhong == room.MaPhong && x.TrangThai == NghiepVu.HopDong.HieuLuc))
            return BadRequest(new { message = "Phòng đang có hợp đồng hiệu lực. Cần bàn giao hoặc thanh lý trước khi gỡ hiện vật." });

        unit.MaPhong = null;
        unit.MaTaiSanPhong = null;
        unit.TrangThai = "Trong kho";
        group.SoLuong--;
        _ctx.LichSuTaiSans.Add(new LichSuTaiSan { MaTaiSanCaThe = id, LoaiSuKien = "Điều chuyển",
            NgaySuKien = DateTime.Today, ChiPhi = 0,
            NoiDung = $"Gỡ từ {await RoomLocationAsync(room)} về kho." });
        await _ctx.SaveChangesAsync();
        if (group.SoLuong == 0)
        {
            _ctx.TaiSanPhongs.Remove(group);
            await _ctx.SaveChangesAsync();
        }
        if (tx != null) await tx.CommitAsync();
        return Ok(new { success = true, message = "Đã gỡ hiện vật về kho, giữ nguyên mã và lịch sử." });
    }

    // Cập nhật tình trạng / số lượng một tài sản đang gán trong phòng (bàn giao, kiểm kê).
    [HttpPut("phong/{id}")]
    public async Task<IActionResult> UpdateTaiSanPhong(int id, [FromBody] TaiSanPhongUpdateDto dto)
    {
        var item = await _ctx.TaiSanPhongs.FindAsync(id);
        if (item == null) return NotFound(new { success = false, message = "Không tìm thấy tài sản trong phòng." });
        if (dto.SoLuong is <= 0) return BadRequest(new { success = false, message = "Số lượng phải lớn hơn 0. Dùng gỡ tài sản nếu phòng không còn tài sản này." });
        if (dto.SoLuong.HasValue && dto.SoLuong != item.SoLuong &&
            await _ctx.HopDongs.AnyAsync(x => x.MaPhong == item.MaPhong && x.TrangThai == NghiepVu.HopDong.HieuLuc))
            return BadRequest(new { message = "Phòng đang có hợp đồng hiệu lực. Chưa thể đổi số lượng tài sản nếu chưa có bàn giao bổ sung." });
        try
        {
            item.TinhTrang = dto.TinhTrang ?? item.TinhTrang;
            int newCount = dto.SoLuong ?? item.SoLuong ?? 0;
            var units = await _ctx.TaiSanCaThes.Where(x => x.MaTaiSanPhong == id && x.TrangThai == "Đang sử dụng")
                .OrderByDescending(x => x.MaTaiSanCaThe).ToListAsync();
            if (newCount < units.Count &&
                (dto.MaTaiSanCaTheGoiKho == null || dto.MaTaiSanCaTheGoiKho.Distinct().Count() != units.Count - newCount ||
                 dto.MaTaiSanCaTheGoiKho.Any(unitId => units.All(unit => unit.MaTaiSanCaThe != unitId))))
                return BadRequest(new { success = false, message = "Chọn đúng mã từng hiện vật sẽ gỡ khỏi phòng để giữ lịch sử sửa chữa chính xác." });
            if (newCount > units.Count)
                for (int i = units.Count; i < newCount; i++)
                {
                    var unit = new TaiSanCaThe { MaTaiSan = item.MaTaiSan, MaPhong = item.MaPhong,
                        MaTaiSanPhong = id, TinhTrang = dto.TinhTrang ?? "Chưa kiểm kê" };
                    unit.LichSu.Add(new LichSuTaiSan { LoaiSuKien = "Ghi nhận", NgaySuKien = DateTime.Today,
                        ChiPhi = 0, NoiDung = "Ghi nhận hiện vật mới trong phòng." });
                    _ctx.TaiSanCaThes.Add(unit);
                }
            else if (newCount < units.Count)
                foreach (var unit in units.Where(unit => dto.MaTaiSanCaTheGoiKho!.Contains(unit.MaTaiSanCaThe)))
                { unit.TrangThai = "Trong kho"; unit.MaPhong = null; unit.MaTaiSanPhong = null;
                    _ctx.LichSuTaiSans.Add(new LichSuTaiSan { MaTaiSanCaThe = unit.MaTaiSanCaThe, LoaiSuKien = "Kiểm kê",
                        NgaySuKien = DateTime.Today, NoiDung = "Gỡ khỏi phòng khi điều chỉnh số lượng.", ChiPhi = 0 }); }
            item.SoLuong   = dto.SoLuong   ?? item.SoLuong;
            item.GhiChu    = dto.GhiChu    ?? item.GhiChu;
            await _ctx.SaveChangesAsync();
            return Ok(new { success = true, message = "Đã cập nhật tình trạng tài sản." });
        }
        catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.InnerException?.Message ?? ex.Message }); }
    }

    // Gỡ tài sản khỏi phòng (không xóa tài sản mẫu trong danh mục).
    [HttpDelete("phong/{id}")]
    public async Task<IActionResult> GoKhoiPhong(int id)
    {
        var item = await _ctx.TaiSanPhongs.FindAsync(id);
        if (item == null) return NotFound(new { success = false, message = "Không tìm thấy tài sản trong phòng." });
        if (await _ctx.HopDongs.AnyAsync(x => x.MaPhong == item.MaPhong && x.TrangThai == NghiepVu.HopDong.HieuLuc))
            return BadRequest(new { message = "Phòng đang có hợp đồng hiệu lực. Chưa thể gỡ tài sản nếu chưa có bàn giao bổ sung." });
        try
        {
            var units = await _ctx.TaiSanCaThes.Where(x => x.MaTaiSanPhong == id).ToListAsync();
            foreach (var unit in units)
            {
                unit.MaTaiSanPhong = null; unit.MaPhong = null; unit.TrangThai = "Trong kho";
                _ctx.LichSuTaiSans.Add(new LichSuTaiSan { MaTaiSanCaThe = unit.MaTaiSanCaThe,
                    LoaiSuKien = "Kiểm kê", NgaySuKien = DateTime.Today, ChiPhi = 0, NoiDung = "Gỡ khỏi phòng; lưu hiện vật trong kho." });
            }
            _ctx.TaiSanPhongs.Remove(item);
            await _ctx.SaveChangesAsync();
            return Ok(new { success = true, message = "Đã gỡ tài sản khỏi phòng." });
        }
        catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.InnerException?.Message ?? ex.Message }); }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] TaiSan model)
    {
        if (string.IsNullOrWhiteSpace(model.TenTaiSan))
            return BadRequest(new { success = false, message = "Tên tài sản không được để trống." });
        if (model.TenTaiSan.Trim().Length > 100)
            return BadRequest(new { success = false, message = "Tên tài sản tối đa 100 ký tự." });
        if (model.GiaTriThamKhao is < 0)
            return BadRequest(new { success = false, message = "Giá trị tham khảo không được âm." });
        model = new TaiSan { TenTaiSan = model.TenTaiSan.Trim(), DonViTinh = model.DonViTinh, GiaTriThamKhao = model.GiaTriThamKhao, MoTa = model.MoTa };
        try
        {
            if (await _ctx.TaiSans.AnyAsync(t => t.TenTaiSan == model.TenTaiSan))
                return BadRequest(new { success = false, message = "Tên tài sản đã có trong danh mục." });
            _ctx.TaiSans.Add(model);
            await _ctx.SaveChangesAsync();
            return Ok(new { success = true, message = "Thêm tài sản thành công", data = model });
        }
        catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] TaiSan model)
    {
        try
        {
            var item = await _ctx.TaiSans.FindAsync(id);
            if (item == null) return NotFound(new { message = "Không tìm thấy tài sản" });
            if (string.IsNullOrWhiteSpace(model.TenTaiSan))
                return BadRequest(new { success = false, message = "Tên tài sản không được để trống." });
            if (model.TenTaiSan.Trim().Length > 100)
                return BadRequest(new { success = false, message = "Tên tài sản tối đa 100 ký tự." });
            if (model.GiaTriThamKhao is < 0)
                return BadRequest(new { success = false, message = "Giá trị tham khảo không được âm." });
            if (await _ctx.TaiSans.AnyAsync(t => t.MaTaiSan != id && t.TenTaiSan == model.TenTaiSan.Trim()))
                return BadRequest(new { success = false, message = "Tên tài sản đã có trong danh mục." });
            item.TenTaiSan = model.TenTaiSan.Trim();
            item.DonViTinh = model.DonViTinh;
            item.GiaTriThamKhao = model.GiaTriThamKhao;
            item.MoTa = model.MoTa;
            await _ctx.SaveChangesAsync();
            return Ok(new { success = true, message = "Cập nhật thành công", data = item });
        }
        catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }

    [HttpPost("gan-phong")]
    public async Task<IActionResult> GanVaoPhong([FromBody] TaiSanPhong model)
    {
        if (model.MaPhong <= 0 || model.MaTaiSan <= 0)
            return BadRequest(new { success = false, message = "Chọn phòng và tài sản trong danh sách." });
        if (model.SoLuong is <= 0)
            return BadRequest(new { success = false, message = "Số lượng phải lớn hơn 0." });
        try
        {
            if (!await _ctx.Phongs.AnyAsync(p => p.MaPhong == model.MaPhong))
                return NotFound(new { success = false, message = "Không tìm thấy phòng." });
            if (await _ctx.HopDongs.AnyAsync(x => x.MaPhong == model.MaPhong && x.TrangThai == NghiepVu.HopDong.HieuLuc))
                return BadRequest(new { message = "Phòng đang có hợp đồng hiệu lực. Chưa thể thêm tài sản nếu chưa có bàn giao bổ sung." });
            if (!await _ctx.TaiSans.AnyAsync(t => t.MaTaiSan == model.MaTaiSan))
                return NotFound(new { success = false, message = "Không tìm thấy tài sản." });
            if (await _ctx.TaiSanPhongs.AnyAsync(t => t.MaPhong == model.MaPhong && t.MaTaiSan == model.MaTaiSan))
                return BadRequest(new { success = false, message = "Tài sản đã được gán vào phòng này. Cập nhật số lượng ở bản ghi hiện có." });
            model = new TaiSanPhong
            {
                MaPhong = model.MaPhong, MaTaiSan = model.MaTaiSan, SoLuong = model.SoLuong ?? 1,
                TinhTrang = model.TinhTrang, GhiChu = model.GhiChu
            };
            await using var tx = _ctx.Database.CurrentTransaction == null ? await _ctx.Database.BeginTransactionAsync() : null;
            _ctx.TaiSanPhongs.Add(model);
            await _ctx.SaveChangesAsync();
            for (int i = 0; i < model.SoLuong; i++)
            {
                var unit = new TaiSanCaThe { MaTaiSan = model.MaTaiSan, MaPhong = model.MaPhong,
                    MaTaiSanPhong = model.MaTaiSanPhong, TinhTrang = model.TinhTrang ?? "Chưa kiểm kê" };
                unit.LichSu.Add(new LichSuTaiSan { LoaiSuKien = "Ghi nhận", NgaySuKien = DateTime.Today,
                    ChiPhi = 0, NoiDung = "Gán hiện vật vào phòng." });
                _ctx.TaiSanCaThes.Add(unit);
            }
            await _ctx.SaveChangesAsync();
            if (tx != null) await tx.CommitAsync();
            return Ok(new { success = true, message = "Gán tài sản vào phòng thành công", data = model });
        }
        catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }

    private async Task<string> RoomLocationAsync(Phong room)
    {
        string? siteName = await _ctx.CoSos.AsNoTracking()
            .Where(x => x.MaCoSo == room.MaCoSo)
            .Select(x => x.TenCoSo).FirstOrDefaultAsync();
        return $"phòng {room.TenPhong} (mã phòng {room.MaPhong}), " +
            $"cơ sở {(siteName ?? "chưa xác định")} (mã cơ sở {room.MaCoSo})";
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var item = await _ctx.TaiSans.FindAsync(id);
            if (item == null) return NotFound(new { message = "Không tìm thấy tài sản" });
            if (await _ctx.TaiSanCaThes.AnyAsync(x => x.MaTaiSan == id) ||
                await _ctx.TaiSanPhongs.AnyAsync(x => x.MaTaiSan == id) ||
                await _ctx.ChiTietBanGiaos.AnyAsync(x => x.MaTaiSan == id))
                return BadRequest(new { message = "Mẫu tài sản đã được gán phòng, bàn giao hoặc có hiện vật; không thể xóa lịch sử quản lý." });
            _ctx.TaiSans.Remove(item);
            await _ctx.SaveChangesAsync();
            return Ok(new { success = true, message = "Xóa tài sản thành công" });
        }
        catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }
}

public class TaiSanPhongUpdateDto
{
    public string? TinhTrang { get; set; }
    public int? SoLuong { get; set; }
    public string? GhiChu { get; set; }
    public List<int>? MaTaiSanCaTheGoiKho { get; set; }
}

public sealed class LichSuTaiSanDto
{
    public string LoaiSuKien { get; set; } = "";
    public DateTime NgaySuKien { get; set; }
    public decimal ChiPhi { get; set; }
    public string NoiDung { get; set; } = "";
    public int? MaYeuCau { get; set; }
}

public sealed class TinhTrangCaTheDto
{
    public string TinhTrang { get; set; } = "";
    public DateTime? NgayMua { get; set; }
    public decimal? GiaMua { get; set; }
    public string? GhiChu { get; set; }
}

public sealed class GanCaTheVaoPhongDto
{
    public int MaPhong { get; set; }
}

public sealed class NhapKhoTaiSanDto
{
    public int MaTaiSan { get; set; }
    public int SoLuong { get; set; }
    public DateTime? NgayMua { get; set; }
    public decimal? GiaMua { get; set; }
    public string TinhTrang { get; set; } = "Tốt";
    public string? GhiChu { get; set; }
}

public sealed class XuatKhoTaiSanDto
{
    public int MaTaiSan { get; set; }
    public int MaPhong { get; set; }
    public int SoLuong { get; set; }
}
