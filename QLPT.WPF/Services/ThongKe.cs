using System.Globalization;
using System.Text.Json;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;

namespace QLPT.WPF.Services;

/// <summary>Một kỳ hóa đơn: tổng phát hành, đã thu, còn nợ.</summary>
public sealed record KyDoanhThu(int Nam, int Thang, decimal Tong, decimal DaThu, decimal ConNo)
{
    public string Nhan => $"{Thang}/{Nam}";
    public double TyLeThu => Tong == 0 ? 0 : (double)(DaThu / Tong);
}

public static class ThongKe
{
    private static decimal Dec(JsonElement e, string ten) =>
        decimal.TryParse(ApiClient.ElStr(e, ten), NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0;

    /// <summary>
    /// Gộp hóa đơn theo kỳ (tháng/năm), lấy N kỳ gần nhất. Tháng trống ở giữa vẫn được
    /// điền 0 để trục thời gian không "nhảy cóc" làm sai cảm nhận xu hướng.
    /// </summary>
    public static List<KyDoanhThu> TheoKy(JsonElement hoaDons, int soKy)
    {
        var nhom = new Dictionary<(int Nam, int Thang), (decimal Tong, decimal DaThu, decimal ConNo)>();
        if (hoaDons.ValueKind == JsonValueKind.Array)
            foreach (var h in hoaDons.EnumerateArray())
            {
                if (!int.TryParse(ApiClient.ElStr(h, "thang"), out int t) || !int.TryParse(ApiClient.ElStr(h, "nam"), out int n)) continue;
                var cu = nhom.GetValueOrDefault((n, t));
                nhom[(n, t)] = (cu.Tong + Dec(h, "tongTien"), cu.DaThu + Dec(h, "daThanhToan"), cu.ConNo + Dec(h, "conLai"));
            }
        if (nhom.Count == 0) return new();

        var dau = nhom.Keys.Min(k => k.Nam * 12 + k.Thang - 1);
        var cuoi = nhom.Keys.Max(k => k.Nam * 12 + k.Thang - 1);
        var ds = new List<KyDoanhThu>();
        for (int i = Math.Max(dau, cuoi - soKy + 1); i <= cuoi; i++)
        {
            int nam = i / 12, thang = i % 12 + 1;
            var v = nhom.GetValueOrDefault((nam, thang));
            ds.Add(new KyDoanhThu(nam, thang, v.Tong, v.DaThu, v.ConNo));
        }
        return ds;
    }

    /// <summary>
    /// Cột chồng 2 phần cho mỗi kỳ: "Đã thu" (slot 1) ở dưới, "Còn nợ" (slot 2) ở trên.
    /// Cột ≤ 24px, đầu cột bo 4px (khúc trên cùng), khe 2px màu nền giữa hai khúc.
    /// </summary>
    public static ISeries[] SeriesDoanhThu(IReadOnlyList<KyDoanhThu> ky) => new ISeries[]
    {
        new StackedColumnSeries<double>
        {
            Name = "Đã thu",
            Values = ky.Select(k => (double)k.DaThu).ToArray(),
            Fill = ChartTheme.Fill(1),
            Stroke = ChartTheme.Gap(),
            MaxBarWidth = 24,
            Rx = 0, Ry = 0,
            YToolTipLabelFormatter = p => ChartTheme.Tien(p.Model),
        },
        new StackedColumnSeries<double>
        {
            Name = "Còn nợ",
            Values = ky.Select(k => (double)k.ConNo).ToArray(),
            Fill = ChartTheme.Fill(2),
            Stroke = ChartTheme.Gap(),
            MaxBarWidth = 24,
            Rx = 4, Ry = 4,
            YToolTipLabelFormatter = p => ChartTheme.Tien(p.Model),
        },
    };
}
