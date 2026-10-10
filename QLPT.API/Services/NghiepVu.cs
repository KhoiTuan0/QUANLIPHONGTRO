using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using QLPT.API.Models;
// Lớp lồng NghiepVu.ThongBao (hằng trạng thái) che mất entity cùng tên.
using ThongBaoModel = QLPT.API.Models.ThongBao;

namespace QLPT.API.Services;

/// <summary>
/// Quy ước nghiệp vụ dùng chung. Các chuỗi trạng thái lấy đúng như dữ liệu đang
/// có trong CSDL (index lọc UX_HopDong_PhongDangHieuLuc cũng dựa trên chúng),
/// nên mọi controller phải dùng các hằng này thay vì tự gõ lại.
/// </summary>
public static class NghiepVu
{
    public static class Phong
    {
        public const string Trong = "Trống";
        public const string DaDatCoc = "Đã đặt cọc";
        public const string DangThue = "Đang thuê";
        public const string BaoTri = "Bảo trì";
    }

    public static class HopDong
    {
        public const string HieuLuc = "Đang hiệu lực";
        public const string DaThanhLy = "Đã thanh lý";
    }

    public static class DatPhong
    {
        public const string DaDatCoc = "Đã đặt cọc";
        public const string DaHuy = "Đã hủy";
        public const string DaNhanPhong = "Đã nhận phòng";
    }

    public static class HoaDon
    {
        public const string ChuaThanhToan = "Chưa thanh toán";
        public const string MotPhan = "Thanh toán một phần";
        public const string DaThanhToan = "Đã thanh toán";
    }

    public static class CuTru
    {
        public const string DangO = "Đang ở";
        public const string TamVang = "Tạm vắng";
        public const string DaRoiDi = "Đã rời đi";
        public const string ChuaKhaiBao = "Chưa khai báo";
    }

    public static class DichVu
    {
        public const string DangSuDung = "Đang sử dụng";
        public const string DaKetThuc = "Đã kết thúc";
    }

    public static class BaoTra
    {
        public const string ChoXuLy = "Chờ xử lý";
        public const string DaHoanTat = "Đã hoàn tất";
    }

    public static class ViPham
    {
        public const string ChuaXuLy = "Chưa xử lý";
        public const string DaXuLy = "Đã xử lý";
    }

    public static class SuaChua
    {
        public const string ChoTiepNhan = "Chờ tiếp nhận";
        public const string DangSua = "Đang sửa";
        public const string DaHoanThanh = "Đã hoàn thành";
    }

    public static class ThongBao
    {
        public const string DaGui = "Đã gửi";
        public const string LoaiNhacNho = "Nhắc nhở";
        public const string LoaiPhanHoi = "Phản hồi";
        public const string LoaiCaNhan = "Cá nhân";
    }

    public const string TaiKhoanActive = "Active";
    public const string PhieuThu = "Thu";
    public const string PhieuChi = "Chi";
    public const string PhieuHoanTat = "Hoàn tất";

    // ───────────────────────── Tiện ích chuỗi ─────────────────────────

    /// <summary>Bỏ dấu + hạ chữ thường để so tên dịch vụ ("Điện", "điện", "Dien" như nhau).</summary>
    public static string Norm(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var sb = new StringBuilder();
        foreach (var c in s.Trim().Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        return sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant().Replace('đ', 'd');
    }

    public static bool LaDien(string? tenDichVu) => Norm(tenDichVu) == "dien";
    public static bool LaNuoc(string? tenDichVu) => Norm(tenDichVu) == "nuoc";

    /// <summary>"Phòng B102" → "phòng B102", để ghép vào giữa câu như dữ liệu sổ quỹ đang có.</summary>
    public static string ChuThuongDau(string? s) =>
        string.IsNullOrEmpty(s) ? "" : char.ToLower(s[0]) + s[1..];

    public static string Tien(decimal? v) =>
        (v ?? 0).ToString("#,##0", CultureInfo.GetCultureInfo("vi-VN")) + "đ";

    // ───────────────────────── Sổ thu chi ─────────────────────────

    /// <summary>Ghi một phiếu vào sổ thu chi (chưa SaveChanges — để nằm chung transaction với nghiệp vụ gốc).</summary>
    public static void GhiPhieu(Klcn052QuanLyPhongTroContext ctx, string loai, string noiDung,
                                decimal soTien, string? nguoiNopNhan, string? ghiChu = null)
    {
        if (soTien <= 0) return;
        ctx.PhieuThuChis.Add(new PhieuThuChi
        {
            LoaiPhieu    = loai,
            NgayLap      = DateTime.Now,
            NoiDung      = noiDung,
            SoTien       = soTien,
            NguoiNopNhan = nguoiNopNhan,
            GhiChu       = ghiChu,
            TrangThai    = PhieuHoanTat
        });
    }

    // ───────────────────────── Thông báo ─────────────────────────

    /// <summary>Tài khoản đăng nhập gắn với một khách thuê (mỗi khách tối đa 1 tài khoản).</summary>
    public static Task<int?> TaiKhoanCuaKhachAsync(Klcn052QuanLyPhongTroContext ctx, int maKhach) =>
        ctx.TaiKhoans.Where(t => t.MaKhach == maKhach).Select(t => (int?)t.MaTaiKhoan).FirstOrDefaultAsync();

    /// <summary>Tạo thông báo kèm danh sách người nhận (chưa SaveChanges).</summary>
    public static ThongBaoModel? TaoThongBao(Klcn052QuanLyPhongTroContext ctx, string tieuDe, string noiDung,
                                             string loai, IEnumerable<int> maTaiKhoans)
    {
        var nguoiNhan = maTaiKhoans.Distinct().ToList();
        if (nguoiNhan.Count == 0) return null;

        var tb = new ThongBaoModel
        {
            TieuDe       = tieuDe,
            NoiDung      = noiDung,
            LoaiThongBao = loai,
            NgayTao      = DateTime.Now,
            TrangThai    = ThongBao.DaGui
        };
        foreach (var id in nguoiNhan)
            tb.ThongBaoNguoiNhans.Add(new ThongBaoNguoiNhan { MaTaiKhoan = id, DaDoc = false });
        ctx.ThongBaos.Add(tb);
        return tb;
    }
}
