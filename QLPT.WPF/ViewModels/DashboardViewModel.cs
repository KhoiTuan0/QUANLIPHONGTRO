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
    public List<string> KhoangOptions { get; } = new() { "3 tháng", "6 tháng", "12 tháng", "Tùy chọn" };
    public List<string> HetHanOptions { get; } = new() { "7 ngày", "30 ngày", "60 ngày", "90 ngày", "Tùy chọn" };
    [ObservableProperty] private string _khoangThoiGian = "6 tháng";
    [ObservableProperty] private DateTime? _ngayTu = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-5);
    [ObservableProperty] private DateTime? _ngayDen = DateTime.Today;
    [ObservableProperty] private int _soNgaySapHetHan = 30;
    [ObservableProperty] private string _mocHetHan = "30 ngày";
    [ObservableProperty] private string _soNgayTuyChon = "30";
    [ObservableProperty] private string _moTaHetHan = "Trong vòng 30 ngày tới";
    public bool HienSoNgayTuyChon => MocHetHan == "Tùy chọn";

    // ── Biểu đồ tình trạng phòng (donut) ──
    [ObservableProperty] private ISeries[] _phongSeries = Array.Empty<ISeries>();
    [ObservableProperty] private List<ChuThichBieuDo> _phongChuThich = new();
    [ObservableProperty] private int _tongSoPhong;
    [ObservableProperty] private string _phongDangRe = "Rê chuột lên biểu đồ để xem chi tiết";

    // Màu khung tooltip theo theme (LiveCharts không đọc DynamicResource)
    [ObservableProperty] private SolidColorPaint _tooltipNen = ChartTheme.NenTooltip;
    [ObservableProperty] private SolidColorPaint _tooltipChu = ChartTheme.ChuChinh;

    // Dữ liệu đã tải, giữ lại để đổi theme chỉ cần vẽ lại, không gọi API lần nữa.
    private List<KyDoanhThu> _ky = new();
    private JsonElement _hoaDons;
    private readonly Dictionary<string, int> _demPhong = new();

    // Thứ tự cố định = màu cố định (slot 1..4), không đổi theo số lượng.
    private static readonly string[] TrangThaiPhong = { "Đang thuê", "Trống", "Đã đặt cọc", "Bảo trì" };

    public DashboardViewModel()
    {
        _ = LoadAsync();
    }

    partial void OnKhoangThoiGianChanged(string value)
    {
        int soThang = value switch { "3 tháng" => 3, "6 tháng" => 6, "12 tháng" => 12, _ => 0 };
        if (soThang > 0)
        {
            NgayTu = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(1 - soThang);
            NgayDen = DateTime.Today;
        }
        CapNhatKy();
    }
    partial void OnNgayTuChanged(DateTime? value) { if (KhoangThoiGian == "Tùy chọn") CapNhatKy(); }
    partial void OnNgayDenChanged(DateTime? value) { if (KhoangThoiGian == "Tùy chọn") CapNhatKy(); }
    partial void OnMocHetHanChanged(string value)
    {
        OnPropertyChanged(nameof(HienSoNgayTuyChon));
        if (value == "Tùy chọn") ApDungSoNgayTuyChon();
        else if (int.TryParse(value.Split(' ')[0], out int days)) SoNgaySapHetHan = days;
    }
    partial void OnSoNgayTuyChonChanged(string value)
    {
        if (HienSoNgayTuyChon) ApDungSoNgayTuyChon();
    }
    private void ApDungSoNgayTuyChon()
    {
        if (!int.TryParse(SoNgayTuyChon, out int days) || days is < 1 or > 365)
        {
            ErrorMessage = "Số ngày sắp hết hạn tùy chọn phải từ 1 đến 365.";
            return;
        }
        ErrorMessage = "";
        SoNgaySapHetHan = days;
    }
    partial void OnSoNgaySapHetHanChanged(int value)
    {
        MoTaHetHan = $"Trong vòng {value} ngày tới";
        _ = LoadAsync();
    }

    private void CapNhatKy()
    {
        if (_hoaDons.ValueKind != JsonValueKind.Array || NgayTu == null || NgayDen == null) return;
        if (NgayTu > NgayDen || NgayDen.Value.Year * 12 + NgayDen.Value.Month
            - NgayTu.Value.Year * 12 - NgayTu.Value.Month >= 120)
        {
            ErrorMessage = "Khoảng thống kê phải từ ngày bắt đầu đến ngày kết thúc và tối đa 120 tháng.";
            return;
        }
        ErrorMessage = "";
        _ky = ThongKe.TheoKhoang(_hoaDons, NgayTu.Value, NgayDen.Value);
        DungBieuDo();
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
            CongNo = VnMoney.FormatD(G("noChuaThu"));
            DoanhThu = VnMoney.FormatD(G("doanhThuThang"));

            var sap = await ApiClient.GetRawAsync($"/api/HopDong/sap-het-han?days={SoNgaySapHetHan}");
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
            SapHetHan = list.Count.ToString();

            _hoaDons = await ApiClient.GetRawAsync("/api/HoaDon");
            CapNhatKy();

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
        CoDoanhThu = _ky.Any(k => k.Tong > 0);
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
        PhongDangRe = "Rê chuột lên biểu đồ để xem chi tiết";
    }

    public void HienPhongDangRe(string? trangThai)
    {
        var muc = PhongChuThich.FirstOrDefault(x => x.Ten == trangThai);
        PhongDangRe = muc == null ? "Rê chuột lên biểu đồ để xem chi tiết"
            : $"{muc.Ten}: {muc.GiaTri} phòng · {muc.TyLe}";
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
