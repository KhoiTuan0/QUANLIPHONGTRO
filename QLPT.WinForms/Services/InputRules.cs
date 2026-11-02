using System.Globalization;

namespace QLPT.WinForms.Services;

public static class InputRules
{
    public static int PositiveId(string text, string label)
    {
        if (!int.TryParse(text.Trim(), out int value) || value <= 0)
            throw new ArgumentException($"{label} phải là số nguyên dương.");
        return value;
    }

    public static decimal NonNegativeMoney(string text, string label)
    {
        if (!VnMoney.TryParse(text, out decimal value) || value < 0)
            throw new ArgumentException($"{label} phải là số tiền không âm.");
        return value;
    }

    public static decimal NonNegativeNumber(string text, string label)
    {
        string normalized = text.Trim().Replace(',', '.');
        if (!System.Text.RegularExpressions.Regex.IsMatch(normalized, @"^[0-9]+(?:\.[0-9]{1,2})?$") ||
            !decimal.TryParse(normalized, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal value))
            throw new ArgumentException($"{label} phải là số không âm, tối đa 2 chữ số thập phân; không gõ dấu phân tách hàng nghìn.");
        return value;
    }

    public static (int Month, int Year) Period(string month, string year)
    {
        if (!int.TryParse(month.Trim(), out int m) || m < 1 || m > 12)
            throw new ArgumentException("Tháng phải từ 1 đến 12.");
        if (!int.TryParse(year.Trim(), out int y) || y < 1900 || y > 2100)
            throw new ArgumentException("Năm phải từ 1900 đến 2100.");
        return (m, y);
    }

    public static DateTime EndAfterStart(string end, DateTime start)
    {
        if (!DateTime.TryParseExact(end.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date) || date <= start.Date)
            throw new ArgumentException("Ngày kết thúc phải đúng dạng yyyy-MM-dd và sau ngày bắt đầu.");
        return date;
    }
}
