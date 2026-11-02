using System.Globalization;
using System.Text.RegularExpressions;

namespace QLPT.WPF.Services;

/// <summary>
/// Các kiểm tra dữ liệu nhập dùng chung cho dialog. Mỗi hàm trả true khi hợp lệ,
/// để dialog tự hiện câu báo lỗi phù hợp ngữ cảnh.
/// </summary>
public static class KiemTra
{
    /// <summary>Mã (khóa) phải là số nguyên dương: "0", "-3", "abc" đều sai.</summary>
    public static bool Ma(string? s, out int ma) =>
        int.TryParse(s?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out ma) && ma > 0;

    /// <summary>Kỳ dạng yyyy-MM, tháng 01–12, năm 2000–2100.</summary>
    public static bool Ky(string? s, out int nam, out int thang)
    {
        nam = thang = 0;
        var m = Regex.Match(s?.Trim() ?? "", @"^(\d{4})-(\d{2})$");
        if (!m.Success) return false;
        nam = int.Parse(m.Groups[1].Value);
        thang = int.Parse(m.Groups[2].Value);
        return nam is >= 2000 and <= 2100 && thang is >= 1 and <= 12;
    }

    /// <summary>Số thập phân không âm, nhận cả "12.5" lẫn "12,5".</summary>
    public static bool SoKhongAm(string? s, out decimal v)
    {
        v = 0;
        string t = (s ?? "").Trim().Replace(',', '.');
        return t.Length > 0 &&
               decimal.TryParse(t, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out v) && v >= 0;
    }

    /// <summary>Số nguyên trong khoảng [min, max].</summary>
    public static bool SoNguyen(string? s, int min, int max, out int v) =>
        int.TryParse(s?.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out v) && v >= min && v <= max;

    public static bool Sdt(string? s)   => Regex.IsMatch(s?.Trim() ?? "", @"^0\d{9}$");
    public static bool Cccd(string? s)  => Regex.IsMatch(s?.Trim() ?? "", @"^\d{12}$");
    public static bool Email(string? s) => Regex.IsMatch(s?.Trim() ?? "", @"^[^@\s]+@[^@\s]+\.[^@\s]{2,}$");

    /// <summary>Họ tên: chữ cái (kể cả có dấu), khoảng trắng, dấu nháy/chấm/gạch; 2–100 ký tự.</summary>
    public static bool HoTen(string? s)
    {
        string t = s?.Trim() ?? "";
        return t.Length is >= 2 and <= 100 && Regex.IsMatch(t, @"^[\p{L}][\p{L}\s.'\-]*$");
    }

    /// <summary>Tên đăng nhập: 3–50 ký tự, chỉ chữ không dấu, số, dấu chấm, gạch dưới.</summary>
    public static bool TenDangNhap(string? s) => Regex.IsMatch(s?.Trim() ?? "", @"^[A-Za-z0-9._]{3,50}$");

    /// <summary>Ngày không được ở tương lai (so với hôm nay).</summary>
    public static bool KhongTuongLai(DateTime? d) => d.HasValue && d.Value.Date <= DateTime.Today;
}
