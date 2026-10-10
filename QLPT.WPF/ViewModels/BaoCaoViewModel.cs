using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using QLPT.WPF.Services;
using QLPT.WPF.Views.Dialogs;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace QLPT.WPF.ViewModels;

public partial class BaoCaoViewModel : BaseViewModel
{
    // Dashboard statistics
    [ObservableProperty] private string _tongPhong  = "-";
    [ObservableProperty] private string _phongTrong = "-";
    [ObservableProperty] private string _dangThue   = "-";
    [ObservableProperty] private string _tongKhach  = "-";
    [ObservableProperty] private string _khachDangO = "-";
    [ObservableProperty] private string _hdHieuLuc  = "-";
    [ObservableProperty] private string _sapHetHan  = "-";
    [ObservableProperty] private string _congNo     = "-";
    [ObservableProperty] private string _doanhThu   = "-";

    // Expiring contracts table (tab 2)
    [ObservableProperty]
    private ObservableCollection<HopDongSapHetHanBc> _hopDongSapHetHan = new();

    // Unpaid invoices table (tab 3)
    [ObservableProperty]
    private ObservableCollection<HoaDonChuaThu> _hoaDonChuaThu = new();

    // Doanh thu theo kỳ: biểu đồ (tab Tổng quan) + bảng số liệu đi kèm (tab riêng)
    [ObservableProperty] private ISeries[] _doanhThuSeries = Array.Empty<ISeries>();
    [ObservableProperty] private Axis[] _doanhThuX = Array.Empty<Axis>();
    [ObservableProperty] private Axis[] _doanhThuY = Array.Empty<Axis>();
    [ObservableProperty] private ObservableCollection<DongDoanhThu> _doanhThuTheoKy = new();
    [ObservableProperty] private bool _coDoanhThu;

    // Công nợ theo phòng: thanh ngang, nợ nhiều nhất ở trên cùng
    [ObservableProperty] private ISeries[] _congNoSeries = Array.Empty<ISeries>();
    [ObservableProperty] private Axis[] _congNoX = Array.Empty<Axis>();
    [ObservableProperty] private Axis[] _congNoY = Array.Empty<Axis>();
    [ObservableProperty] private bool _coCongNo;

    [ObservableProperty] private SolidColorPaint _tooltipNen = ChartTheme.NenTooltip;
    [ObservableProperty] private SolidColorPaint _tooltipChu = ChartTheme.ChuChinh;

    // Số liệu thô của tab Tổng quan, để xuất Excel ra dạng số thay vì chuỗi đã định dạng.
    private readonly List<(string ChiTieu, decimal GiaTri, string DonVi)> _tongQuan = new();
    private List<KyDoanhThu> _ky = new();
    private bool _daTai;

    private const int SoPhongHienThi = 8;

    public BaoCaoViewModel() => _ = LoadAsync();

    private static decimal Dec(string s) =>
        decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0;
    private static DateTime? Ngay(string s) => DateTime.TryParse(s, out var d) ? d : null;
    private static string HienNgay(DateTime? d) => d?.ToString("dd/MM/yyyy") ?? "";

    [RelayCommand]
    private async Task LoadAsync() => await RunSafeAsync(async () =>
    {
        // --- Tab 1: dashboard stats ---
        var dash = await ApiClient.GetRawAsync("/api/DanhMucMoRong/dashboard");
        string G(string n) => ApiClient.ElStr(dash, n);
        TongPhong  = G("tongPhong");
        PhongTrong = G("phongTrong");
        DangThue   = G("dangThue");
        TongKhach  = G("tongKhach");
        KhachDangO = G("khachDangO");
        HdHieuLuc  = G("hdHieuLuc");
        SapHetHan  = G("sapHetHan");
        CongNo     = VnMoney.FormatD(G("noChuaThu"));
        DoanhThu   = VnMoney.FormatD(G("doanhThuThang"));

        _tongQuan.Clear();
        _tongQuan.Add(("Tổng số phòng",            Dec(G("tongPhong")),     "phòng"));
        _tongQuan.Add(("Phòng trống",              Dec(G("phongTrong")),    "phòng"));
        _tongQuan.Add(("Phòng đang thuê",          Dec(G("dangThue")),      "phòng"));
        _tongQuan.Add(("Khách đang ở",             Dec(G("khachDangO")),    "người"));
        _tongQuan.Add(("Hồ sơ khách thuê (kể cả khách cũ)", Dec(G("tongKhach")), "hồ sơ"));
        _tongQuan.Add(("Hợp đồng đang hiệu lực",   Dec(G("hdHieuLuc")),     "hợp đồng"));
        _tongQuan.Add(("Hợp đồng sắp hết hạn (30 ngày)", Dec(G("sapHetHan")), "hợp đồng"));
        _tongQuan.Add(("Công nợ chưa thu",         Dec(G("noChuaThu")),     "đồng"));
        _tongQuan.Add(($"Doanh thu tháng {DateTime.Now:MM/yyyy}", Dec(G("doanhThuThang")), "đồng"));

        // --- Tab 2: expiring contracts ---
        // API trả trường phẳng tenPhong / hoTenKhach (không có object navigation lồng),
        // bản cũ đọc kiểu lồng nên cột Khách thuê luôn trống và Phòng hiện số mã.
        var sap = await ApiClient.GetRawAsync("/api/HopDong/sap-het-han?days=30");
        var hdList = new List<HopDongSapHetHanBc>();
        if (sap.ValueKind == JsonValueKind.Array)
            foreach (var h in sap.EnumerateArray())
            {
                var bd = Ngay(ApiClient.ElStr(h, "ngayBatDau"));
                var kt = Ngay(ApiClient.ElStr(h, "ngayKetThuc"));
                hdList.Add(new HopDongSapHetHanBc
                {
                    MaHopDong   = ApiClient.ElStr(h, "maHopDong"),
                    TenPhong    = ApiClient.ElStr(h, "tenPhong"),
                    HoTenKhach  = ApiClient.ElStr(h, "hoTenKhach"),
                    NgayBatDau  = HienNgay(bd),
                    NgayKetThuc = HienNgay(kt),
                    BatDau      = bd,
                    KetThuc     = kt,
                    ConLaiNgay  = kt.HasValue ? (int)(kt.Value.Date - DateTime.Today).TotalDays : null,
                    TrangThai   = ApiClient.ElStr(h, "trangThai"),
                });
            }
        HopDongSapHetHan = new ObservableCollection<HopDongSapHetHanBc>(hdList.OrderBy(x => x.KetThuc));

        // --- Tab 3: unpaid invoices ---
        var hd = await ApiClient.GetRawAsync("/api/HoaDon");
        var unpaidList = new List<HoaDonChuaThu>();
        if (hd.ValueKind == JsonValueKind.Array)
            foreach (var item in hd.EnumerateArray())
            {
                decimal conLai = Dec(ApiClient.ElStr(item, "conLai"));
                if (conLai <= 0) continue;

                string phong = "", khach = "";
                if (item.TryGetProperty("maHopDongNavigation", out var hopDong) && hopDong.ValueKind == JsonValueKind.Object)
                {
                    if (hopDong.TryGetProperty("maPhongNavigation", out var p) && p.ValueKind == JsonValueKind.Object)
                        phong = ApiClient.ElStr(p, "tenPhong");
                    if (hopDong.TryGetProperty("maKhachDaiDienNavigation", out var k) && k.ValueKind == JsonValueKind.Object)
                        khach = ApiClient.ElStr(k, "hoTen");
                }

                decimal tong = Dec(ApiClient.ElStr(item, "tongTien"));
                decimal daThu = Dec(ApiClient.ElStr(item, "daThanhToan"));
                var han = Ngay(ApiClient.ElStr(item, "hanThanhToan"));
                unpaidList.Add(new HoaDonChuaThu
                {
                    MaHoaDon  = ApiClient.ElStr(item, "maHoaDon"),
                    MaHopDong = ApiClient.ElStr(item, "maHopDong"),
                    TenPhong  = phong,
                    HoTenKhach = khach,
                    // API trả "thang"/"nam" tách riêng và "daThanhToan", không có "thangNam"/"daThu".
                    ThangNam  = $"{ApiClient.ElStr(item, "thang")}/{ApiClient.ElStr(item, "nam")}",
                    TongTien  = VnMoney.FormatD(tong),
                    DaThu     = VnMoney.FormatD(daThu),
                    ConLai    = VnMoney.FormatD(conLai),
                    HanThanhToan = HienNgay(han),
                    TrangThai = han.HasValue && han.Value.Date < DateTime.Today
                        ? $"Quá hạn {(DateTime.Today - han.Value.Date).Days} ngày"
                        : ApiClient.ElStr(item, "trangThai"),
                    TongTienSo = tong,
                    DaThuSo    = daThu,
                    ConLaiSo   = conLai,
                    Han        = han,
                });
            }
        HoaDonChuaThu = new ObservableCollection<HoaDonChuaThu>(unpaidList.OrderBy(x => x.Han));

        // --- Doanh thu theo kỳ (12 kỳ gần nhất) ---
        _ky = ThongKe.TheoKy(hd, soKy: 12);
        DoanhThuTheoKy = new ObservableCollection<DongDoanhThu>(_ky.AsEnumerable().Reverse().Select(k => new DongDoanhThu
        {
            Ky     = k.Nhan,
            Tong   = VnMoney.FormatD(k.Tong),
            DaThu  = VnMoney.FormatD(k.DaThu),
            ConNo  = VnMoney.FormatD(k.ConNo),
            TyLe   = k.Tong == 0 ? "—" : k.TyLeThu.ToString("P0"),
            Goc    = k,
        }));

        DungBieuDo();
        _daTai = true;
    });

    /// <summary>Dựng (lại) biểu đồ từ dữ liệu đã tải, theo màu của theme hiện tại.</summary>
    public void DungBieuDo()
    {
        TooltipNen = ChartTheme.NenTooltip;
        TooltipChu = ChartTheme.ChuChinh;

        CoDoanhThu = _ky.Count > 0;
        DoanhThuSeries = ThongKe.SeriesDoanhThu(_ky);
        DoanhThuX = ChartTheme.TrucDanhMuc(_ky.Select(k => k.Nhan).ToArray());
        DoanhThuY = ChartTheme.TrucTien();

        // Công nợ theo phòng: một series nên một màu (slot 1) cho mọi thanh — không tô đậm
        // dần theo độ lớn, vì chiều dài thanh đã nói lên điều đó. Quá 8 phòng thì gộp "Khác".
        var theoPhong = HoaDonChuaThu
            .GroupBy(h => string.IsNullOrWhiteSpace(h.TenPhong) ? $"HĐ #{h.MaHopDong}" : h.TenPhong)
            .Select(g => (Ten: g.Key, No: g.Sum(x => x.ConLaiSo), SoHd: g.Count()))
            .OrderByDescending(x => x.No)
            .ToList();
        if (theoPhong.Count > SoPhongHienThi)
        {
            var conLai = theoPhong.Skip(SoPhongHienThi - 1).ToList();
            theoPhong = theoPhong.Take(SoPhongHienThi - 1)
                .Append(($"Khác ({conLai.Count} phòng)", conLai.Sum(x => x.No), conLai.Sum(x => x.SoHd)))
                .ToList();
        }
        // Trục dọc của biểu đồ thanh ngang đếm từ dưới lên: đảo để phòng nợ nhiều nhất nằm trên cùng.
        theoPhong.Reverse();

        CoCongNo = theoPhong.Count > 0;
        var nhan = theoPhong.Select(x => x.Ten).ToArray();
        CongNoSeries = new ISeries[]
        {
            new RowSeries<double>
            {
                Name = "Còn nợ",
                Values = theoPhong.Select(x => (double)x.No).ToArray(),
                Fill = ChartTheme.Fill(1),
                MaxBarWidth = 18,
                Rx = 4, Ry = 4,
                // Xếp hạng ít dòng: ghi giá trị ở đầu thanh để đọc ngay không cần rê chuột.
                DataLabelsPaint = ChartTheme.ChuPhu,
                DataLabelsSize = 11,
                DataLabelsPosition = DataLabelsPosition.End,
                DataLabelsFormatter = p => ChartTheme.TienGon(p.Model),
                YToolTipLabelFormatter = p => ChartTheme.Tien(p.Model),
                XToolTipLabelFormatter = p => p.Index < nhan.Length ? nhan[p.Index] : "",
            }
        };
        CongNoX = ChartTheme.TrucTien();
        CongNoY = ChartTheme.TrucDanhMuc(nhan);
    }

    [RelayCommand]
    private async Task XuatExcelAsync()
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title      = "Xuất báo cáo ra Excel",
            Filter     = "Excel (*.xlsx)|*.xlsx",
            FileName   = $"BaoCao_QLPT_{DateTime.Now:yyyyMMdd_HHmm}.xlsx",
            DefaultExt = ".xlsx"
        };
        if (dlg.ShowDialog() != true) return;
        string path = dlg.FileName;

        bool ok = false;
        await RunSafeAsync(async () =>
        {
            if (!_daTai) await LoadAsync();
            var bangs = TaoBangXuat();
            try
            {
                await Task.Run(() => ExcelExporter.Xuat(path, "BÁO CÁO QUẢN LÝ PHÒNG TRỌ", bangs));
            }
            catch (IOException)
            {
                throw new Exception("Không ghi được file — có thể file đang mở trong Excel. Hãy đóng file hoặc chọn tên khác.");
            }
            ok = true;
        });

        if (ok && ConfirmDialog.Ask("Đã xuất báo cáo",
                "Báo cáo gồm 4 sheet: Tổng quan, Hợp đồng sắp hết hạn, Hóa đơn chưa thu, Doanh thu theo kỳ.",
                Path.GetFileName(path), "Mở file"))
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true }); }
            catch (Exception ex) { ErrorMessage = $"Đã lưu file nhưng không mở được: {ex.Message}"; }
        }
    }

    // Mã ghi ra Excel dạng số để sắp xếp đúng (10 sau 9, không phải sau 1).
    private static object SoHoacChu(string s) => int.TryParse(s, out int n) ? n : s;

    private List<ExcelExporter.Bang> TaoBangXuat()
    {
        var K = ExcelExporter.Kieu.Chu;
        var S = ExcelExporter.Kieu.So;
        var T = ExcelExporter.Kieu.Tien;
        var N = ExcelExporter.Kieu.Ngay;

        return new()
        {
            new()
            {
                TenSheet = "Tổng quan",
                TieuDe   = "Tổng quan",
                Cot      = new ExcelExporter.Cot[] { new("Chỉ tiêu", K), new("Giá trị", S), new("Đơn vị", K) },
                Dong     = _tongQuan.Select(x => new object?[] { x.ChiTieu, x.GiaTri, x.DonVi }).ToList(),
            },
            new()
            {
                TenSheet = "HĐ sắp hết hạn",
                TieuDe   = "Hợp đồng hết hạn trong 30 ngày",
                Cot      = new ExcelExporter.Cot[]
                {
                    new("Mã HĐ", S), new("Phòng", K), new("Khách thuê", K),
                    new("Ngày bắt đầu", N), new("Ngày hết hạn", N), new("Còn lại (ngày)", S), new("Trạng thái", K)
                },
                Dong = HopDongSapHetHan.Select(x => new object?[]
                {
                    SoHoacChu(x.MaHopDong),
                    x.TenPhong, x.HoTenKhach, x.BatDau, x.KetThuc, x.ConLaiNgay, x.TrangThai
                }).ToList(),
            },
            new()
            {
                TenSheet = "Hóa đơn chưa thu",
                TieuDe   = "Hóa đơn chưa thu",
                DongTong = true,
                Cot      = new ExcelExporter.Cot[]
                {
                    new("Mã HĐơn", S), new("Mã HĐ", S), new("Phòng", K), new("Khách thuê", K), new("Kỳ", K),
                    new("Tổng tiền", T), new("Đã thu", T), new("Còn lại", T), new("Hạn thanh toán", N), new("Trạng thái", K)
                },
                Dong = HoaDonChuaThu.Select(x => new object?[]
                {
                    SoHoacChu(x.MaHoaDon),
                    SoHoacChu(x.MaHopDong),
                    x.TenPhong, x.HoTenKhach, x.ThangNam,
                    x.TongTienSo, x.DaThuSo, x.ConLaiSo, x.Han, x.TrangThai
                }).ToList(),
            },
            new()
            {
                TenSheet = "Doanh thu theo kỳ",
                TieuDe   = "Doanh thu theo kỳ (12 kỳ gần nhất)",
                DongTong = true,
                Cot      = new ExcelExporter.Cot[]
                {
                    new("Kỳ", K), new("Tổng phát hành", T), new("Đã thu", T), new("Còn nợ", T),
                    new("Tỷ lệ thu", ExcelExporter.Kieu.PhanTram)
                },
                Dong = _ky.AsEnumerable().Reverse().Select(k => new object?[]
                {
                    k.Nhan, k.Tong, k.DaThu, k.ConNo, k.Tong == 0 ? null : k.TyLeThu
                }).ToList(),
            },
        };
    }
}

public class HopDongSapHetHanBc
{
    public string MaHopDong   { get; set; } = "";
    public string TenPhong    { get; set; } = "";
    public string HoTenKhach  { get; set; } = "";
    public string NgayBatDau  { get; set; } = "";
    public string NgayKetThuc { get; set; } = "";
    public string TrangThai   { get; set; } = "";
    public DateTime? BatDau   { get; set; }
    public DateTime? KetThuc  { get; set; }
    public int? ConLaiNgay    { get; set; }
}

public class DongDoanhThu
{
    public string Ky    { get; set; } = "";
    public string Tong  { get; set; } = "";
    public string DaThu { get; set; } = "";
    public string ConNo { get; set; } = "";
    public string TyLe  { get; set; } = "";
    public KyDoanhThu? Goc { get; set; }
}

public class HoaDonChuaThu
{
    public string MaHoaDon     { get; set; } = "";
    public string MaHopDong    { get; set; } = "";
    public string TenPhong     { get; set; } = "";
    public string HoTenKhach   { get; set; } = "";
    public string ThangNam     { get; set; } = "";
    public string TongTien     { get; set; } = "";
    public string DaThu        { get; set; } = "";
    public string ConLai       { get; set; } = "";
    public string HanThanhToan { get; set; } = "";
    public string TrangThai    { get; set; } = "";
    public decimal TongTienSo  { get; set; }
    public decimal DaThuSo     { get; set; }
    public decimal ConLaiSo    { get; set; }
    public DateTime? Han       { get; set; }
}
