using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLPT.API.Models;
using QLPT.API.Services;
using System.Security.Claims;

namespace QLPT.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class DanhMucMoRongController : ControllerBase
{
    private readonly Klcn052QuanLyPhongTroContext _ctx;
    public DanhMucMoRongController(Klcn052QuanLyPhongTroContext ctx) => _ctx = ctx;

    // Giá trị đúng như đang lưu trong CSDL. Trước đây code so với "Hiệu lực"
    // nên mọi thống kê hợp đồng đều ra 0.
    private const string HopDongHieuLuc = "Đang hiệu lực";
    private const string HoaDonDaThanhToan = "Đã thanh toán";

    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard()
    {
        try
        {
            var tongPhong   = await _ctx.Phongs.CountAsync();
            var phongTrong  = await _ctx.Phongs.CountAsync(x => x.TrangThai == "Trống");
            var dangThue    = await _ctx.Phongs.CountAsync(x => x.TrangThai == "Đang thuê");
            var tongKhach   = await _ctx.KhachThues.CountAsync();
            // Người thực sự ở lấy từ cư trú; người đứng tên có thể thuê hộ và ở nơi khác.
            var trangThaiO  = new[] { NghiepVu.CuTru.DangO, NghiepVu.CuTru.TamVang };
            var khachCuTru  = await _ctx.CuTrus
                .Where(c => trangThaiO.Contains(c.TrangThaiCuTru!) && c.MaHopDongNavigation.TrangThai == HopDongHieuLuc)
                .Select(c => c.MaKhach).ToListAsync();
            var khachDangO  = khachCuTru.Distinct().Count();
            var hdHieuLuc   = await _ctx.HopDongs.CountAsync(x => x.TrangThai == HopDongHieuLuc);
            var cutoff      = DateOnly.FromDateTime(DateTime.Today.AddDays(30));
            var sapHetHan   = await _ctx.HopDongs.CountAsync(x =>
                x.TrangThai == HopDongHieuLuc && x.NgayKetThuc <= cutoff);
            // Công nợ = phần CÒN LẠI của mọi hóa đơn chưa tất toán.
            // Bản cũ cộng TongTien và bỏ sót hóa đơn "Thanh toán một phần".
            var noChuaThu   = await _ctx.HoaDons
                .Where(x => x.TrangThai != HoaDonDaThanhToan)
                .SumAsync(x => (decimal?)(x.ConLai ?? 0)) ?? 0;
            var thangNay    = DateTime.Now.Month;
            var namNay      = DateTime.Now.Year;
            var doanhThu    = await _ctx.ThanhToans
                .Where(x => x.NgayThanhToan.HasValue
                    && x.NgayThanhToan.Value.Month == thangNay
                    && x.NgayThanhToan.Value.Year == namNay)
                .SumAsync(x => (decimal?)(x.SoTien ?? 0)) ?? 0;

            return Ok(new {
                tongPhong, phongTrong, dangThue, tongKhach, khachDangO,
                hdHieuLuc, sapHetHan, noChuaThu, doanhThuThang = doanhThu
            });
        }
        catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }

    [HttpGet("cu-tru")]
    public async Task<IActionResult> GetCuTru([FromQuery] int? maHopDong)
    {
        try
        {
            var q = _ctx.CuTrus
                .Include(x => x.MaKhachNavigation)
                .AsQueryable();
            if (User.IsInRole("Manager") && int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int managerId))
                q = q.Where(x => x.MaHopDongNavigation.MaPhongNavigation.MaCoSo.HasValue && _ctx.QuanLyCoSos.Any(a =>
                    a.MaTaiKhoan == managerId && a.MaCoSo == x.MaHopDongNavigation.MaPhongNavigation.MaCoSo.Value));
            if (maHopDong.HasValue)
                q = q.Where(x => x.MaHopDong == maHopDong.Value);
            var list = await q.Select(x => new {
                x.MaCuTru, x.MaHopDong, x.MaKhach, x.NgayVao, x.NgayRoi,
                TrangThaiCuTru = x.TrangThaiCuTru == "Đã rời" ? NghiepVu.CuTru.DaRoiDi : x.TrangThaiCuTru,
                x.TrangThaiKhaiBao, x.NgayKhaiBao, x.GhiChu,
                KhaiBaoMoiNhat = x.KhaiBaoCuTrus.OrderByDescending(k => k.MaKhaiBao)
                    .Select(k => new { k.MaKhaiBao, k.DiaChiThuongTru, k.ThongTinBoSung,
                        k.TrangThai, k.LyDoTraLai, k.NgayGui, k.NgayXuLy }).FirstOrDefault(),
                HoTen = x.MaKhachNavigation != null ? x.MaKhachNavigation.HoTen : "",
                TenPhong = x.MaHopDongNavigation.MaPhongNavigation.TenPhong
            }).ToListAsync();
            return Ok(list);
        }
        catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }

    [HttpGet("cu-tru/customer/{maKhach:int}")]
    public async Task<IActionResult> GetCuTruByCustomer(int maKhach)
    {
        if (maKhach <= 0) return BadRequest(new { message = "Mã khách không hợp lệ." });
        var list = await _ctx.CuTrus.AsNoTracking()
            .Where(c => c.MaKhach == maKhach)
            .OrderByDescending(c => c.NgayVao)
            .Select(c => new {
                c.MaCuTru, c.MaHopDong, c.NgayVao, c.NgayRoi,
                c.TrangThaiCuTru, c.TrangThaiKhaiBao, c.NgayKhaiBao,
                KhaiBaoMoiNhat = c.KhaiBaoCuTrus.OrderByDescending(k => k.MaKhaiBao)
                    .Select(k => new { k.MaKhaiBao, k.DiaChiThuongTru, k.ThongTinBoSung,
                        k.TrangThai, k.LyDoTraLai, k.NgayGui, k.NgayXuLy }).FirstOrDefault(),
                TrangThaiHopDong = c.MaHopDongNavigation.TrangThai,
                TenPhong = c.MaHopDongNavigation.MaPhongNavigation.TenPhong
            }).ToListAsync();
        return Ok(list);
    }

    // Thêm người ở cùng vào hợp đồng. Chặn vượt số người tối đa của loại phòng và
    // chặn một người đang ở hai nơi cùng lúc.
    [HttpPost("cu-tru")]
    public async Task<IActionResult> AddCuTru([FromBody] CuTru model)
    {
        if (model.MaHopDong <= 0 || model.MaKhach <= 0)
            return BadRequest(new { success = false, message = "Thiếu mã hợp đồng hoặc mã khách." });
        if (model.TrangThaiCuTru != null && model.TrangThaiCuTru != NghiepVu.CuTru.DangO && model.TrangThaiCuTru != NghiepVu.CuTru.TamVang)
            return BadRequest(new { success = false, message = "Người mới vào phòng phải có trạng thái Đang ở hoặc Tạm vắng." });
        if (model.NgayRoi.HasValue)
            return BadRequest(new { success = false, message = "Chưa ghi ngày rời đi khi thêm người đang cư trú." });

        var hd = await _ctx.HopDongs
            .Include(h => h.MaPhongNavigation).ThenInclude(p => p.MaLoaiPhongNavigation)
            .FirstOrDefaultAsync(h => h.MaHopDong == model.MaHopDong);
        if (hd == null) return NotFound(new { success = false, message = $"Không tìm thấy hợp đồng #{model.MaHopDong}." });
        if (hd.TrangThai != HopDongHieuLuc)
            return BadRequest(new { success = false, message = "Chỉ thêm người ở cùng vào hợp đồng đang hiệu lực." });
        if (await _ctx.KhachThues.FindAsync(model.MaKhach) == null)
            return NotFound(new { success = false, message = $"Không tìm thấy khách thuê #{model.MaKhach}." });

        var dangO = new[] { NghiepVu.CuTru.DangO, NghiepVu.CuTru.TamVang };
        int? hdDangO = await _ctx.CuTrus
            .Where(c => c.MaKhach == model.MaKhach && dangO.Contains(c.TrangThaiCuTru!)
                        && c.MaHopDongNavigation.TrangThai == HopDongHieuLuc)
            .Select(c => (int?)c.MaHopDong).FirstOrDefaultAsync();
        if (hdDangO.HasValue)
            return BadRequest(new
            {
                success = false,
                message = hdDangO == model.MaHopDong
                    ? $"Khách #{model.MaKhach} đã có trong hợp đồng #{model.MaHopDong} rồi."
                    : $"Khách #{model.MaKhach} đang cư trú ở hợp đồng #{hdDangO}."
            });
        DateOnly ngayVao = model.NgayVao ?? DateOnly.FromDateTime(DateTime.Now);
        if (ngayVao < hd.NgayBatDau || ngayVao > hd.NgayKetThuc)
            return BadRequest(new { success = false, message = "Ngày vào phải nằm trong thời gian hợp đồng." });

        int? toiDa = hd.MaPhongNavigation?.MaLoaiPhongNavigation?.SoNguoiToiDa;
        if (toiDa.HasValue)
        {
            int hienTai = await _ctx.CuTrus.CountAsync(c => c.MaHopDong == hd.MaHopDong && dangO.Contains(c.TrangThaiCuTru!));
            if (hienTai >= toiDa.Value)
                return BadRequest(new { success = false, message = $"Phòng đã đủ {toiDa} người theo loại phòng." });
        }

        try
        {
            model = new CuTru
            {
                MaHopDong = model.MaHopDong, MaKhach = model.MaKhach, NgayVao = ngayVao,
                TrangThaiCuTru = model.TrangThaiCuTru, TrangThaiKhaiBao = model.TrangThaiKhaiBao,
                NgayKhaiBao = model.NgayKhaiBao, GhiChu = model.GhiChu
            };
            model.TrangThaiCuTru   ??= NghiepVu.CuTru.DangO;
            model.TrangThaiKhaiBao ??= NghiepVu.CuTru.ChuaKhaiBao;
            _ctx.CuTrus.Add(model);
            await _ctx.SaveChangesAsync();
            return Ok(new { success = true, message = "Đã thêm người ở cùng.", data = new { model.MaCuTru } });
        }
        catch (Exception ex) { return StatusCode(500, new { success = false, message = ex.InnerException?.Message ?? ex.Message }); }
    }

    [HttpPut("cu-tru/{id}")]
    public async Task<IActionResult> UpdateCuTru(int id, [FromBody] CuTru model)
    {
        try
        {
            var item = await _ctx.CuTrus.FindAsync(id);
            if (item == null) return NotFound(new { message = "Không tìm thấy cư trú" });
            // Dữ liệu cũ có nhãn "Đã rời"; chuẩn hiện tại là "Đã rời đi".
            string? trangThai = model.TrangThaiCuTru ?? item.TrangThaiCuTru;
            if (trangThai == "Đã rời") trangThai = NghiepVu.CuTru.DaRoiDi;
            if (trangThai != NghiepVu.CuTru.DangO && trangThai != NghiepVu.CuTru.TamVang && trangThai != NghiepVu.CuTru.DaRoiDi)
                return BadRequest(new { success = false, message = "Trạng thái cư trú không hợp lệ." });
            bool dangCuTru = trangThai == NghiepVu.CuTru.DangO || trangThai == NghiepVu.CuTru.TamVang;
            if (dangCuTru)
            {
                if (!await _ctx.HopDongs.AnyAsync(h => h.MaHopDong == item.MaHopDong && h.TrangThai == HopDongHieuLuc))
                    return BadRequest(new { success = false, message = "Hợp đồng đã kết thúc, không thể chuyển khách sang đang cư trú." });
                if (item.TrangThaiCuTru == NghiepVu.CuTru.DaRoiDi || item.TrangThaiCuTru == "Đã rời")
                    return BadRequest(new { success = false, message = "Dùng thêm người ở cùng để ghi lượt cư trú mới cho khách đã rời đi." });
                if (model.NgayRoi.HasValue)
                    return BadRequest(new { success = false, message = "Chỉ ghi ngày rời đi khi khách đã rời phòng." });
            }
            DateOnly? ngayRoi = dangCuTru ? null : model.NgayRoi ?? item.NgayRoi ?? DateOnly.FromDateTime(DateTime.Now);
            if (ngayRoi.HasValue && item.NgayVao.HasValue && ngayRoi < item.NgayVao)
                return BadRequest(new { success = false, message = "Ngày rời đi không được trước ngày vào." });
            if (model.TrangThaiKhaiBao != null && model.TrangThaiKhaiBao is not ("Chưa khai báo" or "Chờ duyệt" or "Đã khai báo"))
                return BadRequest(new { success = false, message = "Trạng thái khai báo không hợp lệ." });
            if (model.TrangThaiKhaiBao == "Chờ duyệt")
                return BadRequest(new { message = "Chỉ khách thuê được gửi hồ sơ cư trú vào hàng chờ duyệt." });
            if (item.TrangThaiKhaiBao == "Chờ duyệt" && model.TrangThaiKhaiBao != null)
                return BadRequest(new { message = "Hồ sơ khách gửi đang chờ duyệt. Hãy mở hồ sơ để xác nhận hoặc trả lại." });
            item.TrangThaiCuTru    = trangThai;
            item.TrangThaiKhaiBao  = model.TrangThaiKhaiBao  ?? item.TrangThaiKhaiBao;
            item.NgayKhaiBao       = model.TrangThaiKhaiBao == "Đã khai báo"
                ? model.NgayKhaiBao ?? DateOnly.FromDateTime(DateTime.Today)
                : model.NgayKhaiBao ?? item.NgayKhaiBao;
            item.NgayRoi           = ngayRoi;
            item.GhiChu            = model.GhiChu             ?? item.GhiChu;
            await _ctx.SaveChangesAsync();
            return Ok(new { success = true, message = "Cập nhật cư trú thành công" });
        }
        catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }

    public static string? ValidateKhaiBao(GuiKhaiBaoCuTruDto dto)
    {
        string address = dto.DiaChiThuongTru?.Trim() ?? "";
        if (address.Length is < 5 or > 300) return "Địa chỉ thường trú cần từ 5 đến 300 ký tự.";
        if (dto.ThongTinBoSung?.Trim().Length > 500) return "Thông tin bổ sung tối đa 500 ký tự.";
        return null;
    }

    // Khách nộp nội dung khai báo; mỗi lần gửi được giữ lại để kiểm tra lịch sử.
    [HttpPost("cu-tru/{id}/de-nghi")]
    public async Task<IActionResult> DeNghiKhaiBao(int id, [FromBody] GuiKhaiBaoCuTruDto dto)
    {
        var validation = ValidateKhaiBao(dto);
        if (validation != null) return BadRequest(new { message = validation });
        var item = await _ctx.CuTrus.Include(c => c.MaHopDongNavigation).ThenInclude(h => h.MaPhongNavigation)
            .FirstOrDefaultAsync(c => c.MaCuTru == id);
        if (item == null) return NotFound(new { message = "Không tìm thấy lượt cư trú." });
        if (User.IsInRole("Customer") && (!int.TryParse(User.FindFirstValue("MaKhach"), out var customerId) || item.MaKhach != customerId))
            return Forbid();
        if (item.MaHopDongNavigation.TrangThai != HopDongHieuLuc ||
            item.TrangThaiCuTru is NghiepVu.CuTru.DaRoiDi or "Đã rời")
            return BadRequest(new { message = "Chỉ gửi khai báo cho khách còn ở trong hợp đồng hiệu lực." });
        if (item.TrangThaiKhaiBao == "Chờ duyệt" ||
            await _ctx.KhaiBaoCuTrus.AnyAsync(k => k.MaCuTru == id && k.TrangThai == "Chờ duyệt"))
            return BadRequest(new { message = "Thông tin cư trú đã gửi và đang chờ quản lý xác nhận." });
        _ctx.KhaiBaoCuTrus.Add(new KhaiBaoCuTru { MaCuTru = id,
            DiaChiThuongTru = dto.DiaChiThuongTru.Trim(),
            ThongTinBoSung = string.IsNullOrWhiteSpace(dto.ThongTinBoSung) ? null : dto.ThongTinBoSung.Trim(),
            TrangThai = "Chờ duyệt", NgayGui = DateTime.UtcNow });
        item.TrangThaiKhaiBao = "Chờ duyệt";
        item.NgayKhaiBao = null;
        var nguoiNhan = await _ctx.TaiKhoans.Where(t => t.VaiTro == "Admin" && t.TrangThai == "Active")
            .Select(t => t.MaTaiKhoan).ToListAsync();
        if (item.MaHopDongNavigation.MaPhongNavigation.MaCoSo is int maCoSo)
        {
            var quanLy = await _ctx.TaiKhoans
                .Where(t => t.VaiTro == "Manager" && t.TrangThai == "Active"
                    && _ctx.QuanLyCoSos.Any(x => x.MaCoSo == maCoSo && x.MaTaiKhoan == t.MaTaiKhoan))
                .Select(t => t.MaTaiKhoan).ToListAsync();
            nguoiNhan.AddRange(quanLy);
        }
        NghiepVu.TaoThongBao(_ctx, $"Cư trú chờ xác nhận - {item.MaHopDongNavigation.MaPhongNavigation.TenPhong}",
            $"Khách thuê đã gửi đề nghị khai báo cư trú cho hợp đồng #{item.MaHopDong}. Mở mục Cư trú để kiểm tra và xác nhận.",
            NghiepVu.ThongBao.LoaiCaNhan, nguoiNhan.Distinct().ToList());
        await _ctx.SaveChangesAsync();
        return Ok(new { success = true, message = "Đã gửi thông tin cư trú, chờ quản lý kiểm tra." });
    }

    [HttpPost("cu-tru/{id}/xu-ly-khai-bao")]
    public async Task<IActionResult> XuLyKhaiBao(int id, [FromBody] XuLyKhaiBaoCuTruDto dto)
    {
        if (dto.ChapNhan == null || (!dto.ChapNhan.Value &&
            (string.IsNullOrWhiteSpace(dto.LyDoTraLai) || dto.LyDoTraLai.Trim().Length > 300)))
            return BadRequest(new { message = "Chọn xác nhận hoặc nhập lý do trả lại (tối đa 300 ký tự)." });
        var item = await _ctx.CuTrus.Include(c => c.MaHopDongNavigation)
            .FirstOrDefaultAsync(c => c.MaCuTru == id);
        if (item == null) return NotFound(new { message = "Không tìm thấy lượt cư trú." });
        var pending = await _ctx.KhaiBaoCuTrus.Where(k => k.MaCuTru == id && k.TrangThai == "Chờ duyệt")
            .OrderByDescending(k => k.MaKhaiBao).FirstOrDefaultAsync();
        if (pending == null || item.TrangThaiKhaiBao != "Chờ duyệt")
            return BadRequest(new { message = "Lượt cư trú không có hồ sơ khách gửi đang chờ duyệt." });
        if (dto.ChapNhan.Value && (item.MaHopDongNavigation.TrangThai != HopDongHieuLuc ||
            item.TrangThaiCuTru is NghiepVu.CuTru.DaRoiDi or "Đã rời"))
            return BadRequest(new { message = "Khách đã rời phòng hoặc hợp đồng không còn hiệu lực; chỉ có thể trả hồ sơ kèm lý do." });
        pending.TrangThai = dto.ChapNhan.Value ? "Đã xác nhận" : "Cần bổ sung";
        pending.LyDoTraLai = dto.ChapNhan.Value ? null : dto.LyDoTraLai!.Trim();
        pending.NgayXuLy = DateTime.UtcNow;
        pending.MaNguoiXuLy = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var reviewerId) ? reviewerId : null;
        item.TrangThaiKhaiBao = dto.ChapNhan.Value ? "Đã khai báo" : NghiepVu.CuTru.ChuaKhaiBao;
        item.NgayKhaiBao = dto.ChapNhan.Value ? DateOnly.FromDateTime(DateTime.Today) : null;
        var recipients = await _ctx.TaiKhoans.Where(t => t.MaKhach == item.MaKhach && t.VaiTro == "Customer" && t.TrangThai == "Active")
            .Select(t => t.MaTaiKhoan).ToListAsync();
        if (recipients.Count > 0)
            NghiepVu.TaoThongBao(_ctx, dto.ChapNhan.Value ? "Thông tin cư trú đã được xác nhận" : "Thông tin cư trú cần bổ sung",
                dto.ChapNhan.Value ? "Chủ trọ/quản lý đã xác nhận thông tin cư trú của bạn."
                    : $"Vui lòng bổ sung thông tin cư trú: {pending.LyDoTraLai}",
                NghiepVu.ThongBao.LoaiCaNhan, recipients);
        await _ctx.SaveChangesAsync();
        return Ok(new { success = true, message = dto.ChapNhan.Value ? "Đã xác nhận thông tin cư trú." : "Đã trả hồ sơ để khách bổ sung." });
    }
}

public sealed class GuiKhaiBaoCuTruDto
{
    public string DiaChiThuongTru { get; set; } = "";
    public string? ThongTinBoSung { get; set; }
}

public sealed class XuLyKhaiBaoCuTruDto
{
    public bool? ChapNhan { get; set; }
    public string? LyDoTraLai { get; set; }
}
