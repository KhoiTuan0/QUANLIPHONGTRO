using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using QLPT.WPF.Services;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;

namespace QLPT.WPF.ViewModels;

public partial class DashboardViewModel : BaseViewModel
{
    [ObservableProperty] private string _tongPhong = "-";
    [ObservableProperty] private string _phongTrong = "-";
    [ObservableProperty] private string _dangThue = "-";
    [ObservableProperty] private string _tongKhach = "-";   // số hồ sơ khách (kể cả khách cũ)
    [ObservableProperty] private string _khachDangO = "-";  // người đang thực sự ở
    [ObservableProperty] private string _hdHieuLuc = "-";
    [ObservableProperty] private string _sapHetHan = "-";
    [ObservableProperty] private string _congNo = "-";
    [ObservableProperty] private string _doanhThu = "-";

    [ObservableProperty]
    private List<HopDongSapHetHan> _hopDongSapHetHan = new();

    // ── Biểu đồ doanh thu theo kỳ (cột chồng: đã thu + còn nợ) ──
    [ObservableProperty] private ISeries[] _doanhThuSeries = Array.Empty<ISeries>();
    [ObservableProperty] private Axis[] _doanhThuX = Array.Empty<Axis>();
    [ObservableProperty] private Axis[] _doanhThuY = Array.Empty<Axis>();
    [ObservableProperty] private bool _coDoanhThu;
    [ObservableProperty] private string _tyLeThuKyGanNhat = "";

    // ── Biểu đồ tình trạng phòng (donut) ──
    [ObservableProperty] private ISeries[] _phongSeries = Array.Empty<ISeries>();
    [ObservableProperty] private List<ChuThichBieuDo> _phongChuThich = new();
    [ObservableProperty] private int _tongSoPhong;

    // Màu khung tooltip theo theme (LiveCharts không đọc DynamicResource)
    [ObservableProperty] private SolidColorPaint _tooltipNen = ChartTheme.NenTooltip;
    [ObservableProperty] private SolidColorPaint _tooltipChu = ChartTheme.ChuChinh;

    // Dữ liệu đã tải, giữ lại để đổi theme chỉ cần vẽ lại, không gọi API lần nữa.
    private List<KyDoanhThu> _ky = new();
    private readonly Dictionary<string, int> _demPhong = new();

    // Thứ tự cố định = màu cố định (slot 1..4), không đổi theo số lượng.
    private static readonly string[] TrangThaiPhong = { "Đang thuê", "Trống", "Đã đặt cọc", "Bảo trì" };

    public DashboardViewModel()
    {
        _ = LoadAsync();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        await RunSafeAsync(async () =>
        {
            var el = await ApiClient.GetRawAsync("/api/DanhMucMoRong/dashboard");
            string G(string n) => ApiClient.ElStr(el, n);
            TongPhong = G("tongPhong");
            PhongTrong = G("phongTrong");
            DangThue = G("dangThue");
            TongKhach = G("tongKhach");
            KhachDangO = G("khachDangO");
            HdHieuLuc = G("hdHieuLuc");
            SapHetHan = G("sapHetHan");
            CongNo = VnMoney.FormatD(G("noChuaThu"));
            DoanhThu = VnMoney.FormatD(G("doanhThuThang"));

            var sap = await ApiClient.GetRawAsync("/api/HopDong/sap-het-han?days=30");
            var list = new List<HopDongSapHetHan>();
            foreach (var h in sap.EnumerateArray())
            {
                // API sap-het-han trả trường phẳng tenPhong / hoTenKhach, không có object
                // navigation lồng bên trong — đọc kiểu cũ thì hai cột này luôn trống.
                string ngayKt = ApiClient.ElStr(h, "ngayKetThuc");
                list.Add(new HopDongSapHetHan
                {
                    MaHopDong = ApiClient.ElStr(h, "maHopDong"),
                    TenPhong = ApiClient.ElStr(h, "tenPhong"),
                    HoTenKhach = ApiClient.ElStr(h, "hoTenKhach"),
                    NgayKetThuc = DateTime.TryParse(ngayKt, out var d) ? d.ToString("dd/MM/yyyy") : ngayKt,
                    TrangThai = ApiClient.ElStr(h, "trangThai")
                });
            }
            HopDongSapHetHan = list;

            _ky = ThongKe.TheoKy(await ApiClient.GetRawAsync("/api/HoaDon"), soKy: 6);

            _demPhong.Clear();
            var phong = await ApiClient.GetRawAsync("/api/Phong");
            if (phong.ValueKind == JsonValueKind.Array)
                foreach (var p in phong.EnumerateArray())
                {
                    string tt = ApiClient.ElStr(p, "trangThai");
                    _demPhong[tt] = _demPhong.GetValueOrDefault(tt) + 1;
                }

            DungBieuDo();
        });
    }

    /// <summary>Dựng (lại) biểu đồ từ dữ liệu đã tải, theo màu của theme hiện tại.</summary>
    public void DungBieuDo()
    {
        TooltipNen = ChartTheme.NenTooltip;
        TooltipChu = ChartTheme.ChuChinh;

        // Doanh thu theo kỳ
        CoDoanhThu = _ky.Count > 0;
        DoanhThuSeries = ThongKe.SeriesDoanhThu(_ky);
        DoanhThuX = ChartTheme.TrucDanhMuc(_ky.Select(k => k.Nhan).ToArray());
        DoanhThuY = ChartTheme.TrucTien();
        var gan = _ky.LastOrDefault(k => k.Tong > 0);
        TyLeThuKyGanNhat = gan == null ? "" : $"Kỳ {gan.Nhan}: đã thu {gan.TyLeThu:P0}";

        // Tình trạng phòng — gồm cả trạng thái lạ (nếu có) ở cuối, để tổng luôn khớp.
        var nhom = TrangThaiPhong.Select(t => (Ten: t, SoLuong: _demPhong.GetValueOrDefault(t)))
            .Concat(_demPhong.Where(kv => !TrangThaiPhong.Contains(kv.Key)).Select(kv => (Ten: kv.Key, SoLuong: kv.Value)))
            .ToList();
        TongSoPhong = nhom.Sum(x => x.SoLuong);
        int tong = Math.Max(1, TongSoPhong);

        PhongSeries = nhom.Select((x, i) => (ISeries)new PieSeries<double>
        {
            Name = x.Ten,
            Values = new[] { (double)x.SoLuong },
            Fill = ChartTheme.Fill(Math.Min(i + 1, 4)),
            Stroke = ChartTheme.Gap(),
            InnerRadius = 52,
            HoverPushout = 6,
            ToolTipLabelFormatter = p => $"{p.Model:0} phòng · {p.Model / tong:P0}",
        }).ToArray();

        PhongChuThich = nhom.Select((x, i) => new ChuThichBieuDo(
            x.Ten, x.SoLuong.ToString(), $"{(double)x.SoLuong / tong:P0}",
            MauSlot(Math.Min(i + 1, 4)))).ToList();
    }

    private static Brush MauSlot(int slot) =>
        Application.Current?.Resources[$"ChartSeries{slot}Brush"] as Brush ?? Brushes.Gray;
}

/// <summary>Một dòng chú thích dựng bằng WPF (chữ rõ, theo theme) cạnh biểu đồ.</summary>
public sealed record ChuThichBieuDo(string Ten, string GiaTri, string TyLe, Brush Mau);

public class HopDongSapHetHan
{
    public string MaHopDong { get; set; } = "";
    public string TenPhong { get; set; } = "";
    public string HoTenKhach { get; set; } = "";
    public string NgayKetThuc { get; set; } = "";
    public string TrangThai { get; set; } = "";
}
