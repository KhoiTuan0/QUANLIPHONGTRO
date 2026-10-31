using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLPT.API.Models;
using QLPT.API.Services;
using System.Security.Claims;

namespace QLPT.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class HopDongController : ControllerBase
{
    private readonly Klcn052QuanLyPhongTroContext _ctx;
    public HopDongController(Klcn052QuanLyPhongTroContext ctx) => _ctx = ctx;

    // Khớp đúng giá trị trong CSDL; dùng "Hiệu lực" sẽ không khớp bản ghi nào.
    private const string HieuLuc = NghiepVu.HopDong.HieuLuc;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            var query = _ctx.HopDongs.AsQueryable();
            if (User.IsInRole("Manager") && int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int managerId))
                query = query.Where(h => h.MaPhongNavigation.MaCoSo.HasValue &&
                    _ctx.QuanLyCoSos.Any(a => a.MaTaiKhoan == managerId && a.MaCoSo == h.MaPhongNavigation.MaCoSo.Value));
            var list = await query
                .Include(h => h.MaPhongNavigation)
                .Include(h => h.MaKhachDaiDienNavigation)
                .Select(h => new {
                    maHopDong       = h.MaHopDong,
                    maPhong         = h.MaPhong,
                    tenPhong        = h.MaPhongNavigation != null ? h.MaPhongNavigation.TenPhong : "",
                    maKhachDaiDien  = h.MaKhachDaiDien,
                    hoTenKhach      = h.MaKhachDaiDienNavigation != null ? h.MaKhachDaiDienNavigation.HoTen : "",
                    ngayKy          = h.NgayKy,
                    ngayBatDau      = h.NgayBatDau,
                    ngayKetThuc     = h.NgayKetThuc,
                    giaThueThang    = h.GiaThueThang,
                    tienCoc         = h.TienCoc,
                    trangThai       = h.TrangThai,
                    ghiChu          = h.GhiChu
                })
                .OrderByDescending(h => h.ngayBatDau)
                .ToListAsync();
            return Ok(list);
        }
        catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        try
        {
            var h = await _ctx.HopDongs
                .Include(x => x.MaPhongNavigation)
                .Include(x => x.MaKhachDaiDienNavigation)
                .Where(x => x.MaHopDong == id)
                .Select(x => new {
                    maHopDong      = x.MaHopDong,
                    maPhong        = x.MaPhong,
                    tenPhong       = x.MaPhongNavigation != null ? x.MaPhongNavigation.TenPhong : "",
                    maKhachDaiDien = x.MaKhachDaiDien,
                    hoTenKhach     = x.MaKhachDaiDienNavigation != null ? x.MaKhachDaiDienNavigation.HoTen : "",
                    ngayKy         = x.NgayKy,
                    ngayBatDau     = x.NgayBatDau,
                    ngayKetThuc    = x.NgayKetThuc,
                    giaThueThang   = x.GiaThueThang,
                    tienCoc        = x.TienCoc,
                    trangThai      = x.TrangThai,
                    ghiChu         = x.GhiChu
                })
                .FirstOrDefaultAsync();
            if (h == null) return NotFound(new { message = "Không tìm thấy hợp đồng" });
            return Ok(h);
        }
        catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }

    [HttpGet("sap-het-han")]
    public async Task<IActionResult> SapHetHan([FromQuery] int days = 30)
    {
        // Gồm cả hợp đồng còn đánh dấu hiệu lực nhưng đã quá ngày kết thúc:
        // chủ trọ cần xử lý/gia hạn chúng, không để chúng biến mất khỏi nhắc việc.
        if (days < 0 || days > 3650)
            return BadRequest(new { success = false, message = "Số ngày phải từ 0 đến 3650." });
        try
        {
            var cutoff = DateOnly.FromDateTime(DateTime.Today.AddDays(days));
            var query = _ctx.HopDongs.AsNoTracking().AsQueryable();
            if (User.IsInRole("Manager") && int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int managerId))
                query = query.Where(h => h.MaPhongNavigation.MaCoSo.HasValue &&
                    _ctx.QuanLyCoSos.Any(a => a.MaTaiKhoan == managerId && a.MaCoSo == h.MaPhongNavigation.MaCoSo.Value));
            var list = await query
                .Include(h => h.MaPhongNavigation)
                .Include(h => h.MaKhachDaiDienNavigation)
                .Where(h => h.TrangThai == HieuLuc && h.NgayKetThuc <= cutoff)
                .Select(h => new {
                    maHopDong      = h.MaHopDong,
                    maPhong        = h.MaPhong,
                    tenPhong       = h.MaPhongNavigation != null ? h.MaPhongNavigation.TenPhong : "",
                    maKhachDaiDien = h.MaKhachDaiDien,
                    hoTenKhach     = h.MaKhachDaiDienNavigation != null ? h.MaKhachDaiDienNavigation.HoTen : "",
                    ngayBatDau     = h.NgayBatDau,
                    ngayKetThuc    = h.NgayKetThuc,
                    giaThueThang   = h.GiaThueThang,
                    tienCoc        = h.TienCoc,
                    trangThai      = h.TrangThai
                })
                .OrderBy(h => h.ngayKetThuc)
                .ToListAsync();
            return Ok(list);
        }
        catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }

    [HttpGet("customer/{maKhach}")]
    public async Task<IActionResult> GetByCustomer(int maKhach)
    {
        try
        {
            var list = await _ctx.HopDongs
                .Where(h => h.MaKhachDaiDien == maKhach)
                .Select(h => new {
                    maHopDong    = h.MaHopDong,
                    maPhong      = h.MaPhong,
                    tenPhong     = h.MaPhongNavigation.TenPhong,
                    maKhachDaiDien = h.MaKhachDaiDien,
                    hoTenKhach   = h.MaKhachDaiDienNavigation.HoTen,
                    ngayKy       = h.NgayKy,
                    ngayBatDau   = h.NgayBatDau,
                    ngayKetThuc  = h.NgayKetThuc,
                    giaThueThang = h.GiaThueThang,
                    tienCoc      = h.TienCoc,
                    trangThai    = h.TrangThai,
                    ghiChu       = h.GhiChu
                })
                .ToListAsync();
            return Ok(list);
        }
        catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }

    // Nhận phòng = lập hợp đồng. Theo quy trình: Đặt phòng → Nhận phòng → Hợp đồng thuê,
    // nên ngoài việc lưu hợp đồng còn phải: chuyển phòng sang "Đang thuê", chốt phiếu đặt
    // phòng sang "Đã nhận phòng", ghi người đại diện vào cư trú, đăng ký điện/nước để
    // hóa đơn tháng sau tính được, và ghi phần tiền cọc chưa thu vào sổ thu chi.
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] HopDong model)
    {
        if (model.NgayBatDau == default || model.NgayKetThuc == default)
            return BadRequest(new { success = false, message = "Chọn ngày bắt đầu và ngày kết thúc hợp đồng." });
        if (model.MaPhong <= 0 || model.MaKhachDaiDien <= 0)
            return BadRequest(new { success = false, message = "Thiếu mã phòng hoặc mã khách đại diện." });
        if (model.NgayKetThuc <= model.NgayBatDau)
            return BadRequest(new { success = false, message = "Ngày kết thúc phải sau ngày bắt đầu." });
        if (model.GiaThueThang is <= 0 || model.TienCoc is < 0)
            return BadRequest(new { success = false, message = "Giá thuê phải lớn hơn 0, tiền cọc không được âm." });
        if (model.NgayKy == default(DateOnly))
            return BadRequest(new { success = false, message = "Ngày ký hợp đồng không hợp lệ." });

        await using var tx = _ctx.Database.CurrentTransaction == null
            ? await _ctx.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable) : null;
        var phong = await _ctx.Phongs.FromSqlInterpolated($"SELECT * FROM dbo.Phong WITH (UPDLOCK,HOLDLOCK) WHERE MaPhong={model.MaPhong}").FirstOrDefaultAsync();
        if (phong == null) return NotFound(new { success = false, message = $"Không tìm thấy phòng #{model.MaPhong}." });
        if (phong.TrangThai != NghiepVu.Phong.Trong && phong.TrangThai != NghiepVu.Phong.DaDatCoc)
            return BadRequest(new { success = false, message = $"{phong.TenPhong} đang ở trạng thái '{phong.TrangThai}', chưa thể lập hợp đồng mới." });
        if ((model.GiaThueThang ?? phong.GiaThue) is not > 0)
            return BadRequest(new { success = false, message = "Chưa có giá thuê hợp lệ cho phòng. Nhập giá thuê lớn hơn 0." });
        // Báo lỗi dễ hiểu trước khi vấp index lọc UX_HopDong_PhongDangHieuLuc.
        if (await _ctx.HopDongs.AnyAsync(h => h.MaPhong == model.MaPhong && h.TrangThai == HieuLuc))
            return BadRequest(new { success = false, message = $"{phong.TenPhong} đã có hợp đồng đang hiệu lực." });

        var khach = await _ctx.KhachThues.FindAsync(model.MaKhachDaiDien);
        if (khach == null) return NotFound(new { success = false, message = $"Không tìm thấy khách thuê #{model.MaKhachDaiDien}." });

        // Người ký có thể thuê hộ; cư trú chỉ gắn với người thực sự ở phòng.
        int maKhachCuTru = model.MaKhachCuTru ?? model.MaKhachDaiDien;
        if (await _ctx.KhachThues.FindAsync(maKhachCuTru) == null)
            return NotFound(new { success = false, message = $"Không tìm thấy người ở thực tế #{maKhachCuTru}." });
        int? hdDangO = await _ctx.CuTrus
            .Where(c => c.MaKhach == maKhachCuTru
                        && (c.TrangThaiCuTru == NghiepVu.CuTru.DangO || c.TrangThaiCuTru == NghiepVu.CuTru.TamVang)
                        && c.MaHopDongNavigation.TrangThai == HieuLuc)
            .Select(c => (int?)c.MaHopDong).FirstOrDefaultAsync();
        if (hdDangO.HasValue)
            return BadRequest(new
            {
                success = false,
                message = $"Người ở thực tế #{maKhachCuTru} đang cư trú tại hợp đồng #{hdDangO}. " +
                          "Chọn người khác hoặc ghi nhận rời phòng cũ trước."
            });
        int? toiDa = await _ctx.LoaiPhongs.Where(l => l.MaLoaiPhong == phong.MaLoaiPhong)
            .Select(l => l.SoNguoiToiDa).FirstOrDefaultAsync();
        if (toiDa is <= 0)
            return BadRequest(new { success = false, message = "Loại phòng chưa cho phép người ở; kiểm tra sức chứa." });
        var datPhong = await _ctx.DatPhongs
            .Where(d => d.MaPhong == model.MaPhong && d.TrangThai == NghiepVu.DatPhong.DaDatCoc)
            .OrderByDescending(d => d.NgayDat)
            .FirstOrDefaultAsync();
        if (datPhong != null && datPhong.MaKhach != model.MaKhachDaiDien)
            return BadRequest(new
            {
                success = false,
                message = $"{phong.TenPhong} đang được giữ chỗ cho khách #{datPhong.MaKhach} (phiếu đặt #{datPhong.MaDatPhong}). " +
                          "Hãy hủy giữ chỗ trước, hoặc lập hợp đồng cho đúng khách đã đặt."
            });
        if (phong.TrangThai == NghiepVu.Phong.DaDatCoc && datPhong == null)
            return BadRequest(new { success = false, message = "Phòng được đánh dấu đã đặt cọc nhưng không có phiếu giữ chỗ. Kiểm tra dữ liệu đặt phòng trước." });
        if (datPhong != null && await _ctx.YeuCauHuyDatPhongs.AnyAsync(r => r.MaDatPhong == datPhong.MaDatPhong && r.TrangThai == "Chờ duyệt"))
            return BadRequest(new { message = "Phiếu đặt đang có yêu cầu hủy. Xử lý yêu cầu trước khi nhận phòng." });
        decimal tienCoc = model.TienCoc ?? datPhong?.TienCoc ?? 0;
        if (tienCoc < (datPhong?.TienCoc ?? 0))
            return BadRequest(new { success = false, message = $"Tiền cọc hợp đồng không được thấp hơn tiền đã thu khi đặt phòng ({NghiepVu.Tien(datPhong?.TienCoc)})." });

        // Chỉ nhận dữ liệu nhập của hợp đồng; các bảng liên quan do server tạo theo nghiệp vụ.
        model = new HopDong
        {
            MaPhong = model.MaPhong, MaKhachDaiDien = model.MaKhachDaiDien,
            NgayKy = model.NgayKy, NgayBatDau = model.NgayBatDau, NgayKetThuc = model.NgayKetThuc,
            GiaThueThang = model.GiaThueThang, TienCoc = tienCoc, GhiChu = model.GhiChu
        };

        try
        {
            model.TrangThai      = HieuLuc;
            model.NgayKy       ??= DateOnly.FromDateTime(DateTime.Now);
            model.GiaThueThang ??= phong.GiaThue;
            _ctx.HopDongs.Add(model);

            phong.TrangThai = NghiepVu.Phong.DangThue;
            if (datPhong != null) datPhong.TrangThai = NghiepVu.DatPhong.DaNhanPhong;

            model.CuTrus.Add(new CuTru
            {
                MaKhach          = maKhachCuTru,
                NgayVao          = model.NgayBatDau,
                TrangThaiCuTru   = NghiepVu.CuTru.DangO,
                TrangThaiKhaiBao = NghiepVu.CuTru.ChuaKhaiBao
            });

            // Điện, nước là bắt buộc thực tế; dịch vụ khác do khách tự đăng ký thêm.
            foreach (var dv in (await _ctx.DichVus.ToListAsync())
                         .Where(d => NghiepVu.LaDien(d.TenDichVu) || NghiepVu.LaNuoc(d.TenDichVu)))
                model.DangKyDichVus.Add(new DangKyDichVu
                {
                    MaDichVu     = dv.MaDichVu,
                    NgayBatDau   = model.NgayBatDau,
                    DonGiaApDung = dv.DonGia,
                    TrangThai    = NghiepVu.DichVu.DangSuDung
                });

            // Tiền cọc đã thu lúc đặt phòng thì không ghi lại lần nữa.
            decimal conPhaiThu = (model.TienCoc ?? 0) - (datPhong?.TienCoc ?? 0);
            NghiepVu.GhiPhieu(_ctx, NghiepVu.PhieuThu,
                datPhong == null
                    ? $"Thu tiền đặt cọc hợp đồng {NghiepVu.ChuThuongDau(phong.TenPhong)}"
                    : $"Thu bổ sung tiền cọc hợp đồng {NghiepVu.ChuThuongDau(phong.TenPhong)}",
                conPhaiThu, khach.HoTen);

            // Nếu khách có tài khoản mobile, nhắc cung cấp thông tin cư trú sau khi nhận phòng.
            if (await NghiepVu.TaiKhoanCuaKhachAsync(_ctx, model.MaKhachDaiDien) is int taiKhoanKhach)
                NghiepVu.TaoThongBao(_ctx, $"Cập nhật cư trú phòng {phong.TenPhong}",
                    $"Hợp đồng phòng {phong.TenPhong} đã được lập. Vui lòng kiểm tra thông tin người ở cùng và gửi khai báo cư trú để quản lý xác nhận.",
                    NghiepVu.ThongBao.LoaiCaNhan, new[] { taiKhoanKhach });

            await _ctx.SaveChangesAsync();
            if (tx != null) await tx.CommitAsync();
            return Ok(new { success = true, message = "Tạo hợp đồng thành công", data = new { model.MaHopDong } });
        }
        catch (Exception ex)
        {
            if (tx != null) await tx.RollbackAsync();
            return StatusCode(500, new { success = false, message = ex.InnerException?.Message ?? ex.Message });
        }
    }

    // Gia hạn: lưu lịch sử vào GiaHanHopDong rồi mới đổi ngày kết thúc / giá thuê.
    [HttpPost("{id}/gia-han")]
    public async Task<IActionResult> GiaHan(int id, [FromBody] GiaHanDto dto)
    {
        var hd = await _ctx.HopDongs.FindAsync(id);
        if (hd == null) return NotFound(new { success = false, message = "Không tìm thấy hợp đồng." });
        if (hd.TrangThai != HieuLuc)
            return BadRequest(new { success = false, message = $"Chỉ gia hạn được hợp đồng đang hiệu lực (hiện: {hd.TrangThai})." });
        if (dto.NgayKetThucMoi <= hd.NgayKetThuc)
            return BadRequest(new { success = false, message = $"Ngày kết thúc mới phải sau ngày kết thúc hiện tại ({hd.NgayKetThuc:dd/MM/yyyy})." });
        if (dto.GiaThueMoi is <= 0)
            return BadRequest(new { success = false, message = "Giá thuê mới phải lớn hơn 0." });

        await using var tx = await _ctx.Database.BeginTransactionAsync();
        try
        {
            _ctx.GiaHanHopDongs.Add(new GiaHanHopDong
            {
                MaHopDong      = hd.MaHopDong,
                NgayGiaHan     = DateOnly.FromDateTime(DateTime.Now),
                NgayKetThucCu  = hd.NgayKetThuc,
                NgayKetThucMoi = dto.NgayKetThucMoi,
                GiaThueMoi     = dto.GiaThueMoi,
                GhiChu         = dto.GhiChu
            });

            hd.NgayKetThuc = dto.NgayKetThucMoi;
            if (dto.GiaThueMoi.HasValue) hd.GiaThueThang = dto.GiaThueMoi;

            if (await NghiepVu.TaiKhoanCuaKhachAsync(_ctx, hd.MaKhachDaiDien) is int tk)
                NghiepVu.TaoThongBao(_ctx, $"Hợp đồng #{hd.MaHopDong} đã được gia hạn",
                    $"Hợp đồng của bạn được gia hạn đến ngày {dto.NgayKetThucMoi:dd/MM/yyyy}." +
                    (dto.GiaThueMoi.HasValue ? $" Giá thuê mới: {NghiepVu.Tien(dto.GiaThueMoi)}/tháng." : ""),
                    NghiepVu.ThongBao.LoaiCaNhan, new[] { tk });

            await _ctx.SaveChangesAsync();
            await tx.CommitAsync();
            return Ok(new { success = true, message = $"Đã gia hạn đến {dto.NgayKetThucMoi:dd/MM/yyyy}." });
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            return StatusCode(500, new { success = false, message = ex.InnerException?.Message ?? ex.Message });
        }
    }

    // Cộng một khoản phạt vào hóa đơn gần nhất của hợp đồng (chưa có hóa đơn nào thì lập
    // hóa đơn kỳ hiện tại) để khoản đó nằm trong công nợ. Chưa SaveChanges — chung transaction.
    private async Task GhiPhatVaoHoaDonAsync(int maHopDong, decimal soTien, DateOnly homNay)
    {
        var hoaDon = await _ctx.HoaDons.Where(h => h.MaHopDong == maHopDong)
            .OrderByDescending(h => h.Nam).ThenByDescending(h => h.Thang).FirstOrDefaultAsync();
        if (hoaDon == null)
        {
            hoaDon = new HoaDon
            {
                MaHopDong    = maHopDong,
                Thang        = homNay.Month,
                Nam          = homNay.Year,
                NgayLap      = homNay,
                HanThanhToan = homNay.AddDays(HoaDonBuilder.SoNgayHanThanhToan),
                TongTien     = 0,
                DaThanhToan  = 0,
                ConLai       = 0,
            };
            _ctx.HoaDons.Add(hoaDon);
        }
        hoaDon.ChiTietHoaDons.Add(new ChiTietHoaDon
        {
            NoiDung   = $"Phạt khi thanh lý hợp đồng #{maHopDong} (tiền cọc không đủ trừ)",
            DonGia    = soTien,
            SoLuong   = 1,
            ThanhTien = soTien
        });
        hoaDon.TongTien = (hoaDon.TongTien ?? 0) + soTien;
        hoaDon.ConLai   = (hoaDon.ConLai ?? 0) + soTien;
        hoaDon.TrangThai = (hoaDon.DaThanhToan ?? 0) > 0 ? NghiepVu.HoaDon.MotPhan : NghiepVu.HoaDon.ChuaThanhToan;
    }

    // Thanh lý (trả phòng): quyết toán TongQuyetToan = tiền cọc đem quyết toán − TienNo − TienPhat.
    // ThanhLy.TienHoanCoc lưu số tiền thực hoàn cho khách (= phần cọc còn dư, ≥ 0).
    // Tiền cọc được dùng để cấn trừ nợ hóa đơn trước, rồi trừ phạt; phần dư hoàn cho khách
    // (ghi phiếu chi). Nợ được tính lại ở server, không tin con số client gửi lên.
    // Sau đó đóng hợp đồng, trả phòng về "Trống", kết thúc cư trú và dịch vụ.
    [HttpPost("{id}/thanh-ly")]
    public async Task<IActionResult> ThanhLyHopDong(int id, [FromBody] ThanhLyDto dto)
    {
        var hd = await _ctx.HopDongs
            .Include(h => h.MaPhongNavigation)
            .Include(h => h.MaKhachDaiDienNavigation)
            .FirstOrDefaultAsync(h => h.MaHopDong == id);
        if (hd == null) return NotFound(new { success = false, message = "Không tìm thấy hợp đồng." });
        if (hd.TrangThai != HieuLuc)
            return BadRequest(new { success = false, message = $"Hợp đồng không còn hiệu lực (hiện: {hd.TrangThai})." });
        if (await _ctx.ThanhLies.AnyAsync(t => t.MaHopDong == id))
            return BadRequest(new { success = false, message = "Hợp đồng này đã được thanh lý trước đó." });

        decimal tienCoc = hd.TienCoc ?? 0;
        decimal phat    = dto.TienPhat ?? 0;
        if (dto.TienHoanCoc < 0 || dto.TienHoanCoc > tienCoc)
            return BadRequest(new { success = false, message = $"Tiền hoàn cọc phải từ 0 đến tiền cọc ({NghiepVu.Tien(tienCoc)})." });
        if (phat < 0)
            return BadRequest(new { success = false, message = "Tiền phạt không được âm." });

        var hoaDonNo = await _ctx.HoaDons
            .Where(h => h.MaHopDong == id && h.TrangThai != NghiepVu.HoaDon.DaThanhToan && (h.ConLai ?? 0) > 0)
            .OrderBy(h => h.Nam).ThenBy(h => h.Thang)
            .ToListAsync();
        decimal tienNo = hoaDonNo.Sum(h => h.ConLai ?? 0);
        decimal tongQuyetToan = dto.TienHoanCoc - tienNo - phat;

        await using var tx = await _ctx.Database.BeginTransactionAsync();
        try
        {
            var homNay = DateOnly.FromDateTime(DateTime.Now);
            decimal quy = dto.TienHoanCoc;

            // 1. Cấn trừ nợ hóa đơn (cũ trước) bằng tiền cọc. Không ghi phiếu thu vì
            //    tiền cọc đã vào sổ lúc nhận; chỉ ghi ThanhToan để hóa đơn tất toán.
            foreach (var h in hoaDonNo)
            {
                if (quy <= 0) break;
                decimal tra = Math.Min(quy, h.ConLai ?? 0);
                _ctx.ThanhToans.Add(new ThanhToan
                {
                    MaHoaDon      = h.MaHoaDon,
                    NgayThanhToan = DateTime.Now,
                    SoTien        = tra,
                    PhuongThuc    = "Cấn trừ tiền cọc",
                    GhiChu        = $"Thanh lý hợp đồng #{id}"
                });
                h.DaThanhToan = (h.DaThanhToan ?? 0) + tra;
                h.ConLai      = (h.ConLai ?? 0) - tra;
                h.TrangThai   = h.ConLai <= 0 ? NghiepVu.HoaDon.DaThanhToan : NghiepVu.HoaDon.MotPhan;
                quy -= tra;
            }

            // 2. Trừ phạt vào phần cọc còn lại; 3. phần dư hoàn trả khách.
            decimal phatTruCoc = Math.Min(quy, phat);
            quy -= phatTruCoc;
            string tenPhong = hd.MaPhongNavigation?.TenPhong ?? $"phòng #{hd.MaPhong}";
            NghiepVu.GhiPhieu(_ctx, NghiepVu.PhieuChi,
                $"Hoàn tiền đặt cọc {NghiepVu.ChuThuongDau(tenPhong)} (thanh lý HĐ #{id})",
                quy, hd.MaKhachDaiDienNavigation?.HoTen);

            // 4. Phần phạt cọc không đủ trừ: trước đây bị bỏ mất (khách được báo là còn nợ
            //    nhưng công nợ không có). Giờ cộng vào hóa đơn để hiện trong công nợ và thu tiếp.
            decimal phatConThieu = phat - phatTruCoc;
            if (phatConThieu > 0)
                await GhiPhatVaoHoaDonAsync(id, phatConThieu, homNay);

            _ctx.ThanhLies.Add(new ThanhLy
            {
                MaHopDong     = id,
                NgayThanhLy   = homNay,
                TienNo        = tienNo,
                TienPhat      = phat,
                // Theo đúng nghĩa dữ liệu gốc (HĐ #9, #10): số tiền THỰC hoàn cho khách,
                // không phải toàn bộ cọc đem ra quyết toán.
                TienHoanCoc   = quy,
                TongQuyetToan = tongQuyetToan,
                GhiChu        = dto.GhiChu
            });

            hd.TrangThai = NghiepVu.HopDong.DaThanhLy;
            if (hd.MaPhongNavigation != null) hd.MaPhongNavigation.TrangThai = NghiepVu.Phong.Trong;

            foreach (var ct in await _ctx.CuTrus.Where(c => c.MaHopDong == id &&
                         (c.TrangThaiCuTru == NghiepVu.CuTru.DangO || c.TrangThaiCuTru == NghiepVu.CuTru.TamVang)).ToListAsync())
            {
                ct.TrangThaiCuTru = NghiepVu.CuTru.DaRoiDi;
                // CHECK constraint: NgayRoi >= NgayVao
                ct.NgayRoi = ct.NgayVao.HasValue && ct.NgayVao.Value > homNay ? ct.NgayVao : homNay;
            }

            foreach (var dk in await _ctx.DangKyDichVus.Where(d => d.MaHopDong == id &&
                         d.TrangThai == NghiepVu.DichVu.DangSuDung).ToListAsync())
            {
                dk.TrangThai   = NghiepVu.DichVu.DaKetThuc;
                dk.NgayKetThuc = homNay;
            }

            foreach (var bt in await _ctx.BaoTraPhongs.Where(b => b.MaHopDong == id &&
                         b.TrangThai == NghiepVu.BaoTra.ChoXuLy).ToListAsync())
                bt.TrangThai = NghiepVu.BaoTra.DaHoanTat;

            if (await NghiepVu.TaiKhoanCuaKhachAsync(_ctx, hd.MaKhachDaiDien) is int tk)
                NghiepVu.TaoThongBao(_ctx, $"Hợp đồng #{id} đã được thanh lý",
                    $"Quyết toán: tiền cọc {NghiepVu.Tien(dto.TienHoanCoc)}, cấn trừ nợ hóa đơn {NghiepVu.Tien(tienNo)}, " +
                    $"phạt {NghiepVu.Tien(phat)}. " +
                    (tongQuyetToan >= 0
                        ? $"Số tiền hoàn lại cho bạn: {NghiepVu.Tien(tongQuyetToan)}."
                        : $"Bạn còn phải thanh toán thêm: {NghiepVu.Tien(-tongQuyetToan)}."),
                    NghiepVu.ThongBao.LoaiCaNhan, new[] { tk });

            await _ctx.SaveChangesAsync();
            await tx.CommitAsync();
            return Ok(new
            {
                success = true,
                message = tongQuyetToan >= 0
                    ? $"Đã thanh lý. Hoàn lại khách {NghiepVu.Tien(tongQuyetToan)}."
                    : $"Đã thanh lý. Khách còn nợ {NghiepVu.Tien(-tongQuyetToan)}.",
                data = new { tienNo, tienPhat = phat, tienHoanCoc = quy, tongQuyetToan }
            });
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            return StatusCode(500, new { success = false, message = ex.InnerException?.Message ?? ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] HopDong model)
    {
        try
        {
            var item = await _ctx.HopDongs.FindAsync(id);
            if (item == null) return NotFound(new { message = "Không tìm thấy hợp đồng" });
            if (model.TrangThai != null && model.TrangThai != item.TrangThai)
                return BadRequest(new { success = false, message = "Dùng chức năng thanh lý để kết thúc hợp đồng và cập nhật phòng, cư trú, tiền cọc." });
            if (model.NgayKetThuc != default && model.NgayKetThuc != item.NgayKetThuc)
                return BadRequest(new { success = false, message = "Dùng chức năng gia hạn để đổi ngày kết thúc và lưu lịch sử hợp đồng." });
            if (model.GiaThueThang is <= 0)
                return BadRequest(new { success = false, message = "Giá thuê phải lớn hơn 0." });
            if (item.TrangThai != HieuLuc && model.GiaThueThang.HasValue && model.GiaThueThang != item.GiaThueThang)
                return BadRequest(new { success = false, message = "Không sửa giá thuê của hợp đồng đã kết thúc." });
            item.GhiChu       = model.GhiChu       ?? item.GhiChu;
            item.GiaThueThang = model.GiaThueThang ?? item.GiaThueThang;
            await _ctx.SaveChangesAsync();
            return Ok(new { success = true, message = "Cập nhật thành công" });
        }
        catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }
}

public class GiaHanDto
{
    public DateOnly NgayKetThucMoi { get; set; }
    public decimal? GiaThueMoi { get; set; }
    public string? GhiChu { get; set; }
}

public class ThanhLyDto
{
    public decimal TienHoanCoc { get; set; }
    /// <summary>Client gửi để hiển thị; server tự tính lại từ hóa đơn còn nợ.</summary>
    public decimal? TienNo { get; set; }
    public decimal? TienPhat { get; set; }
    public string? GhiChu { get; set; }
}
