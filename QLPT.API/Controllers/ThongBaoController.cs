using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLPT.API.Models;
using QLPT.API.Services;

namespace QLPT.API.Controllers;

/// <summary>
/// Thông báo, phản hồi và nhắc nhở.
///
/// Phản hồi của khách được lưu thành một bản ghi ThongBao loại "Phản hồi" gửi tới các
/// tài khoản Admin, tiêu đề dạng "Phản hồi TB#12 — Nguyễn Văn An". Cách này dùng lại
/// bảng sẵn có vì không được thêm cột mới vào CSDL; tiền tố "Phản hồi TB#{id} — " là
/// khóa liên kết phản hồi với thông báo gốc.
/// </summary>
[Route("api/[controller]")]
[ApiController]
public class ThongBaoController : ControllerBase
{
    private readonly Klcn052QuanLyPhongTroContext _ctx;
    public ThongBaoController(Klcn052QuanLyPhongTroContext ctx) => _ctx = ctx;

    private static string TienToPhanHoi(int maThongBao) => $"Phản hồi TB#{maThongBao} — ";

    private static int? MaThongBaoGoc(string? tieuDe)
    {
        if (tieuDe == null || !tieuDe.StartsWith("Phản hồi TB#")) return null;
        var rest = tieuDe["Phản hồi TB#".Length..];
        int end = rest.IndexOf(' ');
        return int.TryParse(end < 0 ? rest : rest[..end], out int id) ? id : null;
    }

    // Danh sách cho admin: thông báo đã gửi (không lẫn phản hồi), kèm số người nhận / đã đọc / phản hồi.
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            var tieuDePhanHoi = await _ctx.ThongBaos
                .Where(t => t.LoaiThongBao == NghiepVu.ThongBao.LoaiPhanHoi)
                .Select(t => t.TieuDe).ToListAsync();
            var demPhanHoi = tieuDePhanHoi
                .Select(MaThongBaoGoc).Where(id => id.HasValue)
                .GroupBy(id => id!.Value).ToDictionary(g => g.Key, g => g.Count());

            var list = await _ctx.ThongBaos
                .Where(t => t.LoaiThongBao != NghiepVu.ThongBao.LoaiPhanHoi)
                .Select(t => new
                {
                    maThongBao  = t.MaThongBao,
                    tieuDe      = t.TieuDe,
                    noiDung     = t.NoiDung,
                    loai        = t.LoaiThongBao,
                    ngayGui     = t.NgayTao,
                    trangThai   = t.TrangThai,
                    soNguoiNhan = t.ThongBaoNguoiNhans.Count(),
                    soDaDoc     = t.ThongBaoNguoiNhans.Count(n => n.DaDoc)
                })
                .OrderByDescending(t => t.ngayGui)
                .ToListAsync();

            return Ok(list.Select(t => new
            {
                t.maThongBao, t.tieuDe, t.noiDung, t.loai, t.ngayGui, t.trangThai,
                t.soNguoiNhan, t.soDaDoc,
                soPhanHoi = demPhanHoi.GetValueOrDefault(t.maThongBao)
            }));
        }
        catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }

    [HttpGet("customer/{maTaiKhoan}")]
    public async Task<IActionResult> GetByAccount(int maTaiKhoan)
    {
        try
        {
            var list = await _ctx.ThongBaoNguoiNhans
                .Where(n => n.MaTaiKhoan == maTaiKhoan)
                .Include(n => n.MaThongBaoNavigation)
                .OrderByDescending(n => n.MaThongBaoNavigation != null ? n.MaThongBaoNavigation.NgayTao : null)
                .Select(n => new {
                    maThongBao = n.MaThongBao,
                    tieuDe     = n.MaThongBaoNavigation != null ? n.MaThongBaoNavigation.TieuDe : "",
                    noiDung    = n.MaThongBaoNavigation != null ? n.MaThongBaoNavigation.NoiDung : "",
                    loai       = n.MaThongBaoNavigation != null ? n.MaThongBaoNavigation.LoaiThongBao : "",
                    ngayGui    = n.MaThongBaoNavigation != null ? n.MaThongBaoNavigation.NgayTao : null,
                    trangThai  = n.DaDoc == true ? "Đã đọc" : "Chưa đọc"
                })
                .ToListAsync();
            return Ok(list);
        }
        catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] GuiThongBaoDto model)
    {
        if (string.IsNullOrWhiteSpace(model.TieuDe) || string.IsNullOrWhiteSpace(model.NoiDung))
            return BadRequest(new { success = false, message = "Cần tiêu đề và nội dung." });

        try
        {
            var taiKhoans = model.GuiChoTatCa
                ? await _ctx.TaiKhoans
                    .Where(t => t.TrangThai == NghiepVu.TaiKhoanActive && t.VaiTro == "Customer")
                    .Select(t => t.MaTaiKhoan).ToListAsync()
                : (model.DanhSachMaTaiKhoan ?? new());
            if (taiKhoans.Count == 0)
                return BadRequest(new { success = false, message = "Chưa chọn người nhận." });

            var tb = NghiepVu.TaoThongBao(_ctx, model.TieuDe.Trim(), model.NoiDung.Trim(),
                                          model.LoaiThongBao ?? "Hệ thống", taiKhoans);
            await _ctx.SaveChangesAsync();
            return Ok(new { success = true, message = $"Đã gửi tới {taiKhoans.Distinct().Count()} người.", maThongBao = tb!.MaThongBao });
        }
        catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.InnerException?.Message ?? ex.Message }); }
    }

    [HttpPut("{maThongBao}/doc/{maTaiKhoan}")]
    public async Task<IActionResult> MarkRead(int maThongBao, int maTaiKhoan)
    {
        try
        {
            var item = await _ctx.ThongBaoNguoiNhans
                .FirstOrDefaultAsync(n => n.MaThongBao == maThongBao && n.MaTaiKhoan == maTaiKhoan);
            if (item == null) return NotFound(new { message = "Không tìm thấy" });
            item.DaDoc  = true;
            item.NgayDoc = DateTime.Now;
            await _ctx.SaveChangesAsync();
            return Ok(new { success = true, message = "Đã đánh dấu đã đọc" });
        }
        catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }

    // Khách phản hồi một thông báo đã nhận.
    [HttpPost("{id}/phan-hoi")]
    public async Task<IActionResult> PhanHoi(int id, [FromBody] PhanHoiDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.NoiDung))
            return BadRequest(new { success = false, message = "Nhập nội dung phản hồi." });

        var nhan = await _ctx.ThongBaoNguoiNhans
            .Include(n => n.MaThongBaoNavigation)
            .Include(n => n.MaTaiKhoanNavigation).ThenInclude(t => t.MaKhachNavigation)
            .FirstOrDefaultAsync(n => n.MaThongBao == id && n.MaTaiKhoan == dto.MaTaiKhoan);
        if (nhan == null)
            return BadRequest(new { success = false, message = "Bạn không phải người nhận thông báo này." });

        var admins = await _ctx.TaiKhoans
            .Where(t => t.VaiTro == "Admin" && t.TrangThai == NghiepVu.TaiKhoanActive)
            .Select(t => t.MaTaiKhoan).ToListAsync();
        if (admins.Count == 0)
            return BadRequest(new { success = false, message = "Không có tài khoản quản trị nào để nhận phản hồi." });

        try
        {
            var tk = nhan.MaTaiKhoanNavigation;
            string nguoiGui = tk?.MaKhachNavigation?.HoTen ?? tk?.TenDangNhap ?? $"TK#{dto.MaTaiKhoan}";

            // Phản hồi xong coi như đã đọc thông báo gốc.
            nhan.DaDoc = true;
            nhan.NgayDoc ??= DateTime.Now;

            NghiepVu.TaoThongBao(_ctx, TienToPhanHoi(id) + nguoiGui, dto.NoiDung.Trim(),
                                 NghiepVu.ThongBao.LoaiPhanHoi, admins);
            await _ctx.SaveChangesAsync();
            return Ok(new { success = true, message = "Đã gửi phản hồi tới chủ trọ." });
        }
        catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.InnerException?.Message ?? ex.Message }); }
    }

    // Admin xem các phản hồi của một thông báo.
    [HttpGet("{id}/phan-hoi")]
    public async Task<IActionResult> DanhSachPhanHoi(int id)
    {
        try
        {
            string tienTo = TienToPhanHoi(id);
            var list = await _ctx.ThongBaos
                .Where(t => t.LoaiThongBao == NghiepVu.ThongBao.LoaiPhanHoi && t.TieuDe!.StartsWith(tienTo))
                .OrderByDescending(t => t.NgayTao)
                .Select(t => new
                {
                    maPhanHoi = t.MaThongBao,
                    nguoiGui  = t.TieuDe!.Substring(tienTo.Length),
                    noiDung   = t.NoiDung,
                    ngayGui   = t.NgayTao
                })
                .ToListAsync();
            return Ok(list);
        }
        catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }

    // Bước "Nhắc nhở" của quy trình: gửi nhắc tới từng khách đang có hóa đơn còn nợ
    // và/hoặc hợp đồng sắp hết hạn. Mỗi khách gộp thành một thông báo; ai đã được nhắc
    // hôm nay thì bỏ qua để bấm nhiều lần không bị spam.
    [HttpPost("nhac-nho")]
    public async Task<IActionResult> NhacNho([FromQuery] int soNgay = 30)
    {
        try
        {
            var homNay = DateOnly.FromDateTime(DateTime.Now);
            var moc    = homNay.AddDays(soNgay);

            var no = await _ctx.HoaDons
                .Where(h => h.TrangThai != NghiepVu.HoaDon.DaThanhToan && (h.ConLai ?? 0) > 0
                            && h.MaHopDongNavigation.TrangThai == NghiepVu.HopDong.HieuLuc)
                .OrderBy(h => h.Nam).ThenBy(h => h.Thang)
                .Select(h => new { h.Thang, h.Nam, h.ConLai, h.HanThanhToan, maKhach = h.MaHopDongNavigation.MaKhachDaiDien })
                .ToListAsync();
            var sap = await _ctx.HopDongs
                .Where(h => h.TrangThai == NghiepVu.HopDong.HieuLuc && h.NgayKetThuc <= moc)
                .Select(h => new { h.MaHopDong, h.NgayKetThuc, maKhach = h.MaKhachDaiDien })
                .ToListAsync();

            var khachIds = no.Select(x => x.maKhach).Concat(sap.Select(x => x.maKhach)).Distinct().ToList();
            var taiKhoan = await _ctx.TaiKhoans
                .Where(t => t.MaKhach != null && khachIds.Contains(t.MaKhach.Value) && t.TrangThai == NghiepVu.TaiKhoanActive)
                .ToDictionaryAsync(t => t.MaKhach!.Value, t => t.MaTaiKhoan);

            var dauNgay = DateTime.Today;
            var daNhacHomNay = (await _ctx.ThongBaoNguoiNhans
                .Where(n => n.MaThongBaoNavigation.LoaiThongBao == NghiepVu.ThongBao.LoaiNhacNho
                            && n.MaThongBaoNavigation.NgayTao >= dauNgay)
                .Select(n => n.MaTaiKhoan).ToListAsync()).ToHashSet();

            int daGui = 0, boQua = 0, khongTaiKhoan = 0;
            foreach (int maKhach in khachIds)
            {
                if (!taiKhoan.TryGetValue(maKhach, out int tk)) { khongTaiKhoan++; continue; }
                if (daNhacHomNay.Contains(tk)) { boQua++; continue; }

                var dong = new List<string>();
                var hdNo = no.Where(x => x.maKhach == maKhach).ToList();
                foreach (var h in hdNo)
                    dong.Add($"• Hóa đơn tháng {h.Thang}/{h.Nam} còn nợ {NghiepVu.Tien(h.ConLai)}" +
                             (h.HanThanhToan.HasValue ? $" (hạn {h.HanThanhToan:dd/MM/yyyy})." : "."));
                var hdSap = sap.Where(x => x.maKhach == maKhach).ToList();
                foreach (var h in hdSap)
                    dong.Add($"• Hợp đồng #{h.MaHopDong} hết hạn ngày {h.NgayKetThuc:dd/MM/yyyy} — liên hệ chủ trọ nếu muốn gia hạn.");

                string tieuDe = hdNo.Count > 0 && hdSap.Count > 0 ? "Nhắc thanh toán và gia hạn hợp đồng"
                              : hdNo.Count > 0 ? "Nhắc thanh toán hóa đơn"
                              : "Nhắc hợp đồng sắp hết hạn";
                NghiepVu.TaoThongBao(_ctx, tieuDe, string.Join("\n", dong), NghiepVu.ThongBao.LoaiNhacNho, new[] { tk });
                daGui++;
            }

            await _ctx.SaveChangesAsync();
            return Ok(new
            {
                success = true,
                message = daGui == 0 && boQua == 0 && khongTaiKhoan == 0
                    ? "Không có khách nào cần nhắc."
                    : $"Đã nhắc {daGui} khách." +
                      (boQua > 0 ? $" {boQua} khách đã được nhắc hôm nay nên bỏ qua." : "") +
                      (khongTaiKhoan > 0 ? $" {khongTaiKhoan} khách chưa có tài khoản." : ""),
                data = new { daGui, boQua, khongTaiKhoan, soHoaDonNo = no.Count, soHopDongSapHetHan = sap.Count }
            });
        }
        catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.InnerException?.Message ?? ex.Message }); }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var item = await _ctx.ThongBaos.Include(t => t.ThongBaoNguoiNhans).FirstOrDefaultAsync(t => t.MaThongBao == id);
            if (item == null) return NotFound(new { message = "Không tìm thấy thông báo" });
            _ctx.ThongBaoNguoiNhans.RemoveRange(item.ThongBaoNguoiNhans);
            _ctx.ThongBaos.Remove(item);
            await _ctx.SaveChangesAsync();
            return Ok(new { success = true, message = "Xóa thành công" });
        }
        catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }
}

public class GuiThongBaoDto
{
    public string TieuDe { get; set; } = "";
    public string NoiDung { get; set; } = "";
    public string? LoaiThongBao { get; set; }
    public bool GuiChoTatCa { get; set; }
    public List<int>? DanhSachMaTaiKhoan { get; set; }
}

public class PhanHoiDto
{
    public int MaTaiKhoan { get; set; }
    public string? NoiDung { get; set; }
}
