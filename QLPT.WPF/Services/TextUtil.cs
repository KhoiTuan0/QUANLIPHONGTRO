using System.Globalization;
using System.Text;

namespace QLPT.WPF.Services;

public static class TextUtil
{
    /// <summary>
    /// Bỏ dấu tiếng Việt và hạ chữ thường để so khớp khi tìm kiếm —
    /// gõ "hop dong" vẫn khớp "Hợp đồng", "dien nuoc" khớp "Điện nước".
    /// </summary>
    public static string Normalize(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new StringBuilder(s.Length);
        foreach (var c in s.Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        return sb.ToString().Normalize(NormalizationForm.FormC)
                 .ToLowerInvariant().Replace('đ', 'd');
    }
}
