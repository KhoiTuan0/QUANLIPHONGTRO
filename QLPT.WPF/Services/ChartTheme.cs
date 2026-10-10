using System.Windows;
using System.Windows.Media;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace QLPT.WPF.Services;

/// <summary>
/// Cầu nối giữa theme WPF (DynamicResource) và LiveCharts2 (vẽ bằng SkiaSharp).
/// LiveCharts không đọc DynamicResource, nên mỗi lần dựng biểu đồ ta lấy màu hiện tại
/// từ App.Resources; khi đổi sáng/tối, ViewModel dựng lại biểu đồ (xem ChartThemeBinding).
///
/// Quy ước theo skill dataviz: màu phân loại theo thứ tự cố định (slot 1..4), chữ trên
/// biểu đồ dùng màu chữ của app (không dùng màu series), lưới mảnh 1px, trục tiền bắt đầu từ 0.
/// </summary>
public static class ChartTheme
{
    private static SKColor C(string key)
    {
        if (Application.Current?.Resources[key] is SolidColorBrush b)
            return new SKColor(b.Color.R, b.Color.G, b.Color.B, b.Color.A);
        return SKColors.Gray;
    }

    /// <summary>Màu phân loại theo thứ tự cố định: 1 xanh, 2 cam, 3 xanh ngọc, 4 vàng.</summary>
    public static SolidColorPaint Fill(int slot) => new(C($"ChartSeries{slot}Brush"));

    /// <summary>Viền màu nền: tạo khe 2px giữa các phần chạm nhau (khúc cột chồng, lát donut).</summary>
    public static SolidColorPaint Gap(float doDay = 2) => new(C("Win11SurfaceBrush")) { StrokeThickness = doDay };

    public static SolidColorPaint ChuPhu   => new(C("Win11TextSecondaryBrush"));
    public static SolidColorPaint ChuChinh => new(C("Win11TextPrimaryBrush"));
    public static SolidColorPaint ChuMo    => new(C("ChartMutedBrush"));
    public static SolidColorPaint Luoi     => new(C("ChartGridBrush")) { StrokeThickness = 1 };
    public static SolidColorPaint DuongDay => new(C("ChartAxisBrush")) { StrokeThickness = 1 };
    public static SolidColorPaint NenTooltip => new(C("Win11SurfaceBrush"));

    // ───────────────────────── Định dạng số ─────────────────────────

    /// <summary>Tiền đầy đủ cho tooltip: 3.173.000đ.</summary>
    public static string Tien(double v) => VnMoney.FormatD((decimal)v);

    /// <summary>Tiền gọn cho trục / nhãn: 0 · 500k · 1,5tr · 25tr · 1,2 tỷ.</summary>
    public static string TienGon(double v)
    {
        double a = Math.Abs(v);
        var vi = System.Globalization.CultureInfo.GetCultureInfo("vi-VN");
        if (a >= 1e9) return (v / 1e9).ToString("0.#", vi) + " tỷ";
        if (a >= 1e6) return (v / 1e6).ToString("0.#", vi) + "tr";
        if (a >= 1e3) return (v / 1e3).ToString("0", vi) + "k";
        return v.ToString("0", vi);
    }

    // ───────────────────────── Trục ─────────────────────────

    /// <summary>Trục danh mục (kỳ, tên phòng): không kẻ lưới, chữ màu phụ.</summary>
    public static Axis[] TrucDanhMuc(IList<string> nhan) => new[]
    {
        new Axis
        {
            Labels = nhan,
            LabelsPaint = ChuPhu,
            TextSize = 12,
            ShowSeparatorLines = false,
            TicksPaint = null,
            ZeroPaint = null,
        }
    };

    /// <summary>Trục tiền: bắt đầu từ 0, lưới mảnh, nhãn gọn.</summary>
    public static Axis[] TrucTien() => new[]
    {
        new Axis
        {
            Labeler = TienGon,
            LabelsPaint = ChuMo,
            TextSize = 11,
            MinLimit = 0,
            SeparatorsPaint = Luoi,
            TicksPaint = null,
            ZeroPaint = DuongDay,
        }
    };
}

/// <summary>
/// Gắn việc "dựng lại biểu đồ khi đổi theme" vào vòng đời của trang: đăng ký khi trang
/// hiện, hủy khi trang rời đi — tránh ViewModel cũ bị sự kiện tĩnh giữ lại mỗi lần chuyển trang.
/// </summary>
public static class ChartThemeBinding
{
    public static void Attach(FrameworkElement page, Action dungLai)
    {
        void OnTheme() => page.Dispatcher.Invoke(dungLai);
        page.Loaded   += (_, _) => ThemeService.ThemeChanged += OnTheme;
        page.Unloaded += (_, _) => ThemeService.ThemeChanged -= OnTheme;
    }
}
