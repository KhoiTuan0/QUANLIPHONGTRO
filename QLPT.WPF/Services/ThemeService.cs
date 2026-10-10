using System.Windows;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;

namespace QLPT.WPF.Services;

public static class ThemeService
{
    public static bool IsDark { get; private set; }
    public static event Action? ThemeChanged;

    public static void Apply(bool dark)
    {
        IsDark = dark;

        var palette = new PaletteHelper();
        var theme = palette.GetTheme();
        theme.SetBaseTheme(dark ? BaseTheme.Dark : BaseTheme.Light);
        palette.SetTheme(theme);

        // Sidebar màu chủ đạo: xanh thương hiệu ở chế độ sáng, xanh navy đậm ở chế độ tối
        // (xanh sáng #4BA3F5 của dark mode quá nhạt để đặt chữ trắng lên).
        Set("Win11SidebarBrush",       dark ? "#0F2744" : "#0B57B0");
        Set("Win11ContentBrush",       dark ? "#141414" : "#F5F5F5");
        Set("Win11SurfaceBrush",       dark ? "#242424" : "#FFFFFF");
        Set("Win11TextPrimaryBrush",   dark ? "#EDEDED" : "#111111");
        Set("Win11TextSecondaryBrush", dark ? "#888888" : "#888888");
        Set("Win11BorderBrush",        dark ? "#303030" : "#E5E5E5");
        Set("Win11AccentBrush",        dark ? "#4BA3F5" : "#0066CC");
        Set("Win11AccentLightBrush",   dark ? "#152035" : "#EEF4FF");
        Set("Win11HoverBrush",         dark ? "#18FFFFFF" : "#0A000000");

        // Biểu đồ: cùng 4 sắc độ nhưng bước riêng cho nền tối (không tự đảo màu).
        Set("ChartSeries1Brush", dark ? "#3987E5" : "#2A78D6");
        Set("ChartSeries2Brush", dark ? "#D95926" : "#EB6834");
        Set("ChartSeries3Brush", dark ? "#199E70" : "#1BAF7A");
        Set("ChartSeries4Brush", dark ? "#C98500" : "#EDA100");
        Set("ChartGridBrush",    dark ? "#333331" : "#ECECEA");
        Set("ChartAxisBrush",    dark ? "#45453F" : "#C3C2B7");
        Set("ChartMutedBrush",   "#898781");

        ThemeChanged?.Invoke();
    }

    private static void Set(string key, string hex)
    {
        Application.Current.Resources[key] =
            new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
    }
}
