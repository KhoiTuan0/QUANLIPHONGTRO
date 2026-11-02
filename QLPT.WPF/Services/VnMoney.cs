using System.Globalization;

namespace QLPT.WPF.Services;

public static class VnMoney
{
    public static readonly CultureInfo Vi = new("vi-VN");

    public static string Format(object? value)
    {
        if (value == null) return "";
        if (value is decimal amount) return amount.ToString(amount % 1 == 0 ? "N0" : "N2", Vi);
        if (value is int or long or double or float) return Convert.ToDecimal(value, CultureInfo.InvariantCulture).ToString("N0", Vi);
        string s = value is string str ? str : Convert.ToString(value, Vi) ?? "";
        if (string.IsNullOrWhiteSpace(s)) return "";
        if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ||
            decimal.TryParse(s, NumberStyles.Any, Vi, out d))
            return d.ToString(d % 1 == 0 ? "N0" : "N2", Vi);
        return s;
    }

    public static string FormatD(object? value)
    {
        string f = Format(value);
        return string.IsNullOrEmpty(f) ? "" : f + " đ";
    }

    public static bool TryParse(string? s, out decimal value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(s)) return false;
        string t = s.Trim().Replace("đ", "").Replace("₫", "").Replace(" ", "");
        if (t.Contains('.') && t.Contains(','))
            t = t.Replace(".", "").Replace(',', '.');
        else if (t.Count(c => c == '.') > 1)
            t = t.Replace(".", "");
        else if (t.Count(c => c == ',') > 1)
            t = t.Replace(",", "");
        else if (t.Contains('.') && !t.Contains(','))
        {
            var parts = t.Split('.');
            if (parts.Length == 2 && parts[1].Length == 3 && parts[0].Length <= 4)
                t = t.Replace(".", "");
        }
        else if (t.Contains(',') && !t.Contains('.'))
        {
            var parts = t.Split(',');
            if (parts.Length == 2 && parts[1].Length == 3)
                t = t.Replace(",", "");
            else t = t.Replace(',', '.');
        }
        return decimal.TryParse(t, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
    }

    public static decimal Parse(string? s, decimal fallback = 0)
        => TryParse(s, out var v) ? v : fallback;
}
