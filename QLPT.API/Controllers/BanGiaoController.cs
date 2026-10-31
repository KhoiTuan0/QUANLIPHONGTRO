using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLPT.API.Models;
using QLPT.API.Services;

namespace QLPT.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BanGiaoController : ControllerBase
{
    public const string NhanPhong = "Nhận phòng";
    public const string TraPhong = "Trả phòng";
    private const decimal MaxDecimal = 9999999999999999.99m;
    private readonly Klcn052QuanLyPhongTroContext _ctx;
    private readonly ILogger<BanGiaoController>? _logger;

    public BanGiaoController(Klcn052QuanLyPhongTroContext ctx, ILogger<BanGiaoController>? logger = null)
    {
        _ctx = ctx;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int? maHopDong = null)
    {
        if (maHopDong is <= 0)
            return BadRequest(new { success = false, message = "Chọn hợp đồng trong danh sách." });
        try
        {
            var query = _ctx.BanGiaoTaiSans.AsNoTracking();
            if (User.IsInRole("Manager") && int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int managerId))
                query = query.Where(b => b.MaPhongNavigation.MaCoSo.HasValue &&
                    _ctx.QuanLyCoSos.Any(a => a.MaTaiKhoan == managerId && a.MaCoSo == b.MaPhongNavigation.MaCoSo.Value));
            if (maHopDong.HasValue) query = query.Where(b => b.MaHopDong == maHopDong.Value);
            return Ok(await query.OrderByDescending(b => b.NgayBanGiao).ThenByDescending(b => b.MaBanGiao)
                .Select(b => new
                {
                    b.MaBanGiao, b.MaHopDong, b.MaPhong, b.LoaiBanGiao, b.NgayBanGiao,
                    b.ChiSoDien, b.ChiSoNuoc, b.GhiChu,
                    TenPhong = b.MaPhongNavigation.TenPhong,
                    MaKhachDaiDien = b.MaHopDongNavigation.MaKhachDaiDien,
                    HoTenKhach = b.MaHopDongNavigation.MaKhachDaiDienNavigation.HoTen,
                    TongChiPhiPhat = b.ChiTietBanGiaos.Sum(c => c.ChiPhiPhat ?? 0),
                    ChiTietBanGiaos = b.ChiTietBanGiaos.OrderBy(c => c.MaChiTietBanGiao).Select(c => new
                    {
                        c.MaChiTietBanGiao, c.MaBanGiao, c.MaTaiSan, c.SoLuong, c.TinhTrang, c.ChiPhiPhat, c.GhiChu,
                        TenTaiSan = c.MaTaiSanNavigation.TenTaiSan,
                        MaTaiSanNavigation = new { c.MaTaiSanNavigation.MaTaiSan, c.MaTaiSanNavigation.TenTaiSan, c.MaTaiSanNavigation.DonViTinh }
                    }).ToList()
                }).ToListAsync());
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Không đọc được biên bản bàn giao");
            return StatusCode(500, new { success = false, message = "Không tải được danh sách bàn giao. Vui lòng thử lại." });
        }
    }

    [HttpGet("hop-dong/{maHopDong:int}")]
    [HttpGet("/api/DanhMucMoRong/ban-giao/{maHopDong:int}")]
    public Task<IActionResult> GetByContract(int maHopDong) => GetAll(maHopDong);

    public static string? Validate(BanGiaoCreateDto model)
    {
        if (model.MaHopDong <= 0 || model.MaPhong <= 0) return "Chọn hợp đồng và phòng trong danh sách.";
        if (model.LoaiBanGiao != NhanPhong && model.LoaiBanGiao != TraPhong) return "Loại bàn giao phải là Nhận phòng hoặc Trả phòng.";
        if (model.NgayBanGiao == default || model.NgayBanGiao > DateOnly.FromDateTime(DateTime.Today))
            return "Ngày bàn giao không được để trống hoặc sau hôm nay.";
        if (!HopLeDecimal(model.ChiSoDien) || !HopLeDecimal(model.ChiSoNuoc))
            return "Chỉ số điện, nước phải không âm, tối đa 2 chữ số thập phân và nằm trong giới hạn lưu trữ.";
        if (model.GhiChu?.Trim().Length > 300) return "Ghi chú biên bản tối đa 300 ký tự.";
        var details = model.ChiTietBanGiaos ?? [];
        if (details.Count > 200) return "Mỗi biên bản tối đa 200 loại tài sản.";
        var ids = new HashSet<int>();
        foreach (var detail in details)
        {
            if (detail == null || detail.MaTaiSan <= 0) return "Chọn tài sản trong danh sách phòng.";
            if (!ids.Add(detail.MaTaiSan)) return "Một tài sản chỉ xuất hiện một lần trên biên bản.";
            if (detail.SoLuong < 0 || (model.LoaiBanGiao == NhanPhong && detail.SoLuong == 0))
                return "Số lượng không được âm; tài sản nhận phòng phải có số lượng lớn hơn 0.";
            if (!HopLeDecimal(detail.ChiPhiPhat)) return "Chi phí phạt không được âm hoặc có quá 2 chữ số thập phân.";
            if (model.LoaiBanGiao == NhanPhong && detail.ChiPhiPhat > 0) return "Chỉ ghi chi phí phạt tài sản khi trả phòng.";
            if (string.IsNullOrWhiteSpace(detail.TinhTrang) || detail.TinhTrang.Trim().Length > 100)
                return "Tình trạng tài sản phải từ 1 đến 100 ký tự.";
            if (detail.GhiChu?.Trim().Length > 300) return "Ghi chú tài sản tối đa 300 ký tự.";
        }
        return null;
    }

    private static bool HopLeDecimal(decimal value) => value >= 0 && value <= MaxDecimal && decimal.Round(value, 2) == value;

    [HttpPost]
    [HttpPost("/api/DanhMucMoRong/ban-giao")]
    public async Task<IActionResult> Create([FromBody] BanGiaoCreateDto model)
    {
        var error = Validate(model);
        if (error != null) return BadRequest(new { success = false, message = error });
        try
        {
            // Serializes creation for the same contract, including the check for a duplicate document.
            await using var tx = await _ctx.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var contract = await _ctx.HopDongs.FindAsync(model.MaHopDong);
            if (contract == null) return NotFound(new { success = false, message = "Không tìm thấy hợp đồng." });
            if (contract.MaPhong != model.MaPhong) return BadRequest(new { success = false, message = "Phòng không thuộc hợp đồng đã chọn." });
            if (contract.TrangThai != NghiepVu.HopDong.HieuLuc)
                return BadRequest(new { success = false, message = "Lập biên bản bàn giao trước khi thanh lý hợp đồng." });
            if (model.NgayBanGiao < contract.NgayBatDau)
                return BadRequest(new { success = false, message = "Ngày bàn giao không được trước ngày bắt đầu hợp đồng." });
            if (await _ctx.BanGiaoTaiSans.AnyAsync(b => b.MaHopDong == model.MaHopDong && b.LoaiBanGiao == model.LoaiBanGiao))
                return BadRequest(new { success = false, message = "Hợp đồng đã có biên bản loại này. Xem và in biên bản đã lưu." });
            if (model.LoaiBanGiao == NhanPhong && await _ctx.BanGiaoTaiSans.AnyAsync(b => b.MaHopDong == model.MaHopDong && b.LoaiBanGiao == TraPhong))
                return BadRequest(new { success = false, message = "Hợp đồng đã có biên bản trả phòng, không thể lập thêm biên bản nhận phòng." });
            var previous = await _ctx.BanGiaoTaiSans.Where(b => b.MaHopDong == model.MaHopDong && b.LoaiBanGiao == NhanPhong)
                .OrderByDescending(b => b.NgayBanGiao).FirstOrDefaultAsync();
            if (model.LoaiBanGiao == TraPhong && previous?.NgayBanGiao > model.NgayBanGiao)
                return BadRequest(new { success = false, message = "Ngày trả phòng không được trước ngày nhận phòng." });
            var assigned = await _ctx.TaiSanPhongs.Where(t => t.MaPhong == model.MaPhong).ToListAsync();
            var details = model.ChiTietBanGiaos ?? [];
            if (assigned.Count != details.Count || details.Any(d => assigned.All(a => a.MaTaiSan != d.MaTaiSan)))
                return BadRequest(new { success = false, message = "Biên bản phải gồm đúng các tài sản đang gán vào phòng. Tải lại danh sách tài sản." });
            foreach (var detail in details)
            {
                var asset = assigned.Single(a => a.MaTaiSan == detail.MaTaiSan);
                if (detail.SoLuong > (asset.SoLuong ?? 0))
                    return BadRequest(new { success = false, message = "Số lượng bàn giao không được vượt số lượng tài sản đang gán vào phòng." });
            }
            var readings = await _ctx.ChiSoDienNuocs.Where(c => c.MaPhong == model.MaPhong && c.NgayGhi <= model.NgayBanGiao)
                .OrderByDescending(c => c.NgayGhi).ThenByDescending(c => c.MaChiSo).ToListAsync();
            decimal electric = Math.Max(previous?.ChiSoDien ?? 0, readings.FirstOrDefault(c => NghiepVu.LaDien(c.LoaiChiSo))?.ChiSoMoi ?? 0);
            decimal water = Math.Max(previous?.ChiSoNuoc ?? 0, readings.FirstOrDefault(c => NghiepVu.LaNuoc(c.LoaiChiSo))?.ChiSoMoi ?? 0);
            if (model.ChiSoDien < electric || model.ChiSoNuoc < water)
                return BadRequest(new { success = false, message = $"Chỉ số bàn giao không được thấp hơn số đã ghi: điện {electric}, nước {water}." });
            var handover = new BanGiaoTaiSan
            {
                MaHopDong = model.MaHopDong, MaPhong = model.MaPhong, LoaiBanGiao = model.LoaiBanGiao,
                NgayBanGiao = model.NgayBanGiao, ChiSoDien = model.ChiSoDien, ChiSoNuoc = model.ChiSoNuoc,
                GhiChu = model.GhiChu?.Trim(),
                ChiTietBanGiaos = details.Select(d => new ChiTietBanGiao
                {
                    MaTaiSan = d.MaTaiSan, SoLuong = d.SoLuong, TinhTrang = d.TinhTrang!.Trim(),
                    ChiPhiPhat = d.ChiPhiPhat, GhiChu = d.GhiChu?.Trim()
                }).ToList()
            };
            _ctx.BanGiaoTaiSans.Add(handover);
            await _ctx.SaveChangesAsync();
            await tx.CommitAsync();
            return Ok(new
            {
                success = true,
                message = "Đã lưu biên bản bàn giao. Nhập tổng chi phí phạt từ biên bản khi thanh lý hợp đồng.",
                data = new { handover.MaBanGiao, tongChiPhiPhat = details.Sum(d => d.ChiPhiPhat) }
            });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Không lưu được biên bản bàn giao hợp đồng {MaHopDong}", model.MaHopDong);
            return StatusCode(500, new { success = false, message = "Không lưu được biên bản bàn giao. Vui lòng thử lại." });
        }
    }
}

public class BanGiaoCreateDto
{
    public int MaHopDong { get; set; }
    public int MaPhong { get; set; }
    public string? LoaiBanGiao { get; set; }
    public DateOnly NgayBanGiao { get; set; }
    public decimal ChiSoDien { get; set; }
    public decimal ChiSoNuoc { get; set; }
    public string? GhiChu { get; set; }
    public List<ChiTietBanGiaoCreateDto>? ChiTietBanGiaos { get; set; }
}

public class ChiTietBanGiaoCreateDto
{
    public int MaTaiSan { get; set; }
    public int SoLuong { get; set; }
    public string? TinhTrang { get; set; }
    public decimal ChiPhiPhat { get; set; }
    public string? GhiChu { get; set; }
}
