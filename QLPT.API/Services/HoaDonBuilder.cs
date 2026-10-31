using Microsoft.EntityFrameworkCore;
using QLPT.API.Models;

namespace QLPT.API.Services;

/// <summary>
/// Lập hóa đơn tháng theo quy trình: Ghi chỉ số → Tính tiền dịch vụ → Tạo hóa đơn.
/// Định dạng dòng chi tiết bám theo hóa đơn đang có trong CSDL, ví dụ:
///   Tiền phòng tháng 9/2026                              1 × 2.500.000
///   Tiền điện tháng 9/2026 (chỉ số 1787 -> 1885 kWh)    98 × 3.500
///   Phí Internet                                         1 × 100.000
/// Điện/Nước tính theo số tiêu thụ; các dịch vụ khác tính phí cố định.
/// Bản cũ (tinh-tien-ocr) cộng Điện/Nước hai lần: một lần theo chỉ số, một lần
/// nữa như phí cố định trong vòng lặp dịch vụ.
/// </summary>
public static class HoaDonBuilder
{
    public const decimal DonGiaDienMacDinh = 3500;
    public const decimal DonGiaNuocMacDinh = 20000;
    public const int SoNgayHanThanhToan = 7;

    public sealed record KetQua(List<ChiTietHoaDon> ChiTiet, decimal TongTien, string? Loi);

    private static ChiTietHoaDon Dong(string noiDung, decimal soLuong, decimal donGia) => new()
    {
        NoiDung   = noiDung,
        SoLuong   = soLuong,
        DonGia    = donGia,
        ThanhTien = Math.Round(soLuong * donGia, 0)
    };

    public static async Task<KetQua> TinhAsync(Klcn052QuanLyPhongTroContext ctx, HopDong hd,
        int thang, int nam, decimal? donGiaDien = null, decimal? donGiaNuoc = null)
    {
        string? loiKy = KiemTraKy(hd, thang, nam);
        if (loiKy != null) return new KetQua([], 0, loiKy);
        var dauKy = new DateOnly(nam, thang, 1);
        var cuoiKy = new DateOnly(nam, thang, DateTime.DaysInMonth(nam, thang));
        decimal tienPhong = hd.GiaThueThang
            ?? await ctx.Phongs.Where(p => p.MaPhong == hd.MaPhong).Select(p => p.GiaThue).FirstOrDefaultAsync()
            ?? 0;
        // A past month still includes services that were used then and stopped later.
        var dangKy = await ctx.DangKyDichVus.AsNoTracking().Include(d => d.MaDichVuNavigation)
            .Where(d => d.MaHopDong == hd.MaHopDong
                && (d.TrangThai == NghiepVu.DichVu.DangSuDung || d.TrangThai == NghiepVu.DichVu.DaKetThuc)
                && (!d.NgayBatDau.HasValue || d.NgayBatDau <= cuoiKy)
                && (!d.NgayKetThuc.HasValue || d.NgayKetThuc >= dauKy))
            .ToListAsync();
        var danhMuc = await ctx.DichVus.AsNoTracking().ToListAsync();
        var chiSo = await ctx.ChiSoDienNuocs.AsNoTracking()
            .Where(c => c.MaPhong == hd.MaPhong && c.Thang == thang && c.Nam == nam)
            .ToListAsync();
        return TinhTuDuLieu(hd, thang, nam, dangKy, danhMuc, chiSo, tienPhong, donGiaDien, donGiaNuoc);
    }

    public static string? KiemTraKy(HopDong hd, int thang, int nam)
    {
        if (thang is < 1 or > 12 || nam is < 2000 or > 9999) return "Kỳ hóa đơn không hợp lệ.";
        int ky = nam * 12 + thang;
        if (ky < hd.NgayBatDau.Year * 12 + hd.NgayBatDau.Month || ky > hd.NgayKetThuc.Year * 12 + hd.NgayKetThuc.Month)
            return "Kỳ hóa đơn phải nằm trong thời gian hợp đồng.";
        return null;
    }

    public static KetQua TinhTuDuLieu(HopDong hd, int thang, int nam, IEnumerable<DangKyDichVu> cacDangKy,
        IEnumerable<DichVu> cacDichVu, IEnumerable<ChiSoDienNuoc> cacChiSo, decimal? giaPhong = null,
        decimal? donGiaDien = null, decimal? donGiaNuoc = null)
    {
        string? loiKy = KiemTraKy(hd, thang, nam);
        if (loiKy != null) return new KetQua([], 0, loiKy);
        string ky = $"{thang}/{nam}";
        var dong = new List<ChiTietHoaDon>();
        decimal tienPhong = hd.GiaThueThang ?? giaPhong ?? 0;
        if (tienPhong <= 0) return new KetQua([], 0, "Giá thuê hợp đồng chưa hợp lệ.");
        if (donGiaDien is < 0 || donGiaNuoc is < 0) return new KetQua([], 0, "Đơn giá điện, nước không được âm.");
        var dauKy = new DateOnly(nam, thang, 1);
        var cuoiKy = new DateOnly(nam, thang, DateTime.DaysInMonth(nam, thang));
        var lienQuan = cacDangKy.Where(d => d.MaHopDong == hd.MaHopDong
            && (d.TrangThai == NghiepVu.DichVu.DangSuDung || d.TrangThai == NghiepVu.DichVu.DaKetThuc)
            && (!d.NgayBatDau.HasValue || d.NgayBatDau <= cuoiKy)
            && (!d.NgayKetThuc.HasValue || d.NgayKetThuc >= dauKy)).ToList();
        if (lienQuan.Any(d => (d.NgayBatDau.HasValue && d.NgayKetThuc < d.NgayBatDau)
            || (d.TrangThai == NghiepVu.DichVu.DaKetThuc && !d.NgayKetThuc.HasValue)))
            return new KetQua([], 0, "Ngày sử dụng dịch vụ chưa hợp lệ. Kiểm tra ngày bắt đầu và kết thúc đăng ký.");
        // Monthly flat fees are charged once per service, including a stop/restart within the month.
        var dangKy = lienQuan.OrderByDescending(d => d.NgayBatDau).ThenByDescending(d => d.MaDangKy)
            .GroupBy(d => d.MaDichVu).Select(g => g.First()).ToList();
        var danhMuc = cacDichVu.ToList();
        var chiSo = cacChiSo.Where(c => c.MaPhong == hd.MaPhong && c.Thang == thang && c.Nam == nam).ToList();
        dong.Add(Dong($"Tiền phòng tháng {ky}", 1, tienPhong));

        // 2. Điện / Nước theo chỉ số. Đã đăng ký mà chưa ghi chỉ số thì chặn, để
        //    không phát hành hóa đơn thiếu tiền điện nước mà không ai hay.
        var thieu = new List<string>();
        foreach (var (ten, laLoai, donVi, ghiDe, macDinh) in new (string, Func<string?, bool>, string, decimal?, decimal)[]
        {
            ("Điện", NghiepVu.LaDien, "kWh", donGiaDien, DonGiaDienMacDinh),
            ("Nước", NghiepVu.LaNuoc, "m³",  donGiaNuoc, DonGiaNuocMacDinh),
        })
        {
            var dk = dangKy.FirstOrDefault(d => laLoai(d.MaDichVuNavigation?.TenDichVu));
            var cs = chiSo.FirstOrDefault(c => laLoai(c.LoaiChiSo));
            if (cs == null)
            {
                if (dk != null) thieu.Add(ten);
                continue;
            }

            decimal cu  = cs.ChiSoCu ?? 0;
            decimal moi = cs.ChiSoMoi ?? cs.ChiSoXacNhan ?? cu;
            if (cu < 0 || moi < cu)
                return new KetQua(dong, 0, $"Chỉ số {ten.ToLower()} tháng {ky} không hợp lệ. Sửa chỉ số trước khi lập hóa đơn.");
            decimal tieuThu = moi - cu;
            decimal gia = ghiDe is > 0 ? ghiDe.Value
                : dk?.DonGiaApDung
                  ?? dk?.MaDichVuNavigation?.DonGia
                  ?? danhMuc.FirstOrDefault(d => laLoai(d.TenDichVu))?.DonGia
                  ?? macDinh;
            if (gia < 0) return new KetQua(dong, 0, $"Đơn giá {ten.ToLower()} không được âm.");

            try { dong.Add(Dong($"Tiền {ten.ToLower()} tháng {ky} (chỉ số {cu:0.##} -> {moi:0.##} {donVi})", tieuThu, gia)); }
            catch (OverflowException) { return new KetQua(dong, 0, "Tiền dịch vụ vượt giới hạn tính toán."); }
        }
        if (thieu.Count > 0)
            return new KetQua(dong, 0,
                $"Chưa ghi chỉ số {string.Join(" và ", thieu)} tháng {ky} cho phòng của hợp đồng #{hd.MaHopDong}. " +
                "Hãy ghi chỉ số trước khi lập hóa đơn.");

        // 3. Dịch vụ cố định (Internet, rác, giữ xe…) — bỏ qua Điện/Nước đã tính ở trên.
        foreach (var dk in dangKy)
        {
            string? tenDv = dk.MaDichVuNavigation?.TenDichVu;
            if (NghiepVu.LaDien(tenDv) || NghiepVu.LaNuoc(tenDv)) continue;
            decimal gia = dk.DonGiaApDung ?? dk.MaDichVuNavigation?.DonGia ?? 0;
            if (gia < 0) return new KetQua(dong, 0, $"Đơn giá dịch vụ {tenDv} không được âm.");
            if (gia == 0) continue;
            dong.Add(Dong($"Phí {tenDv}", 1, gia));
        }

        const decimal maxMoney = 9999999999999999.99m;
        try
        {
            decimal tong = dong.Sum(d => d.ThanhTien ?? 0);
            if (dong.Any(d => d.ThanhTien > maxMoney || d.DonGia > maxMoney || d.SoLuong > maxMoney) || tong > maxMoney)
                return new KetQua(dong, 0, "Tổng tiền hóa đơn vượt giới hạn lưu trữ.");
            return new KetQua(dong, tong, null);
        }
        catch (OverflowException) { return new KetQua(dong, 0, "Tổng tiền hóa đơn vượt giới hạn tính toán."); }
    }

    /// <summary>Tạo hóa đơn (chưa SaveChanges). Trả lỗi nghiệp vụ nếu không lập được.</summary>
    public static async Task<(HoaDon? HoaDon, string? Loi)> LapAsync(Klcn052QuanLyPhongTroContext ctx, HopDong hd,
        int thang, int nam, decimal? donGiaDien = null, decimal? donGiaNuoc = null)
    {
        string? loiKy = KiemTraKy(hd, thang, nam);
        if (loiKy != null) return (null, loiKy);
        if (hd.TrangThai != NghiepVu.HopDong.HieuLuc)
            return (null, $"Hợp đồng #{hd.MaHopDong} không còn hiệu lực (trạng thái: {hd.TrangThai}).");
        if (await ctx.HoaDons.AnyAsync(h => h.MaHopDong == hd.MaHopDong && h.Thang == thang && h.Nam == nam))
            return (null, $"Hóa đơn tháng {thang}/{nam} của hợp đồng #{hd.MaHopDong} đã tồn tại.");

        var kq = await TinhAsync(ctx, hd, thang, nam, donGiaDien, donGiaNuoc);
        if (kq.Loi != null) return (null, kq.Loi);

        return (TaoHoaDon(ctx, hd.MaHopDong, thang, nam, kq.ChiTiet), null);
    }

    /// <summary>Gắn hóa đơn với danh sách dòng chi tiết vào context (chưa SaveChanges).</summary>
    public static HoaDon TaoHoaDon(Klcn052QuanLyPhongTroContext ctx, int maHopDong, int thang, int nam,
                                   IEnumerable<ChiTietHoaDon> chiTiet)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var list = chiTiet.ToList();
        decimal tong = list.Sum(c => c.ThanhTien ?? 0);
        var hoaDon = new HoaDon
        {
            MaHopDong    = maHopDong,
            Thang        = thang,
            Nam          = nam,
            NgayLap      = today,
            HanThanhToan = today.AddDays(SoNgayHanThanhToan),
            TongTien     = tong,
            DaThanhToan  = 0,
            ConLai       = tong,
            TrangThai    = NghiepVu.HoaDon.ChuaThanhToan
        };
        foreach (var ct in list) hoaDon.ChiTietHoaDons.Add(ct);
        ctx.HoaDons.Add(hoaDon);
        return hoaDon;
    }

    /// <summary>Hóa đơn → dữ liệu trả về client (tránh vòng tham chiếu navigation).</summary>
    public static object ToDto(HoaDon h) => new
    {
        maHoaDon    = h.MaHoaDon,
        maHopDong   = h.MaHopDong,
        thang       = h.Thang,
        nam         = h.Nam,
        tongTien    = h.TongTien,
        daThanhToan = h.DaThanhToan,
        conLai      = h.ConLai,
        trangThai   = h.TrangThai,
        chiTiet     = h.ChiTietHoaDons.Select(c => new { c.MaViPham, c.NoiDung, c.SoLuong, c.DonGia, c.ThanhTien })
    };
}
