using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QLPT.WPF.Services;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace QLPT.WPF.ViewModels;

public partial class TenantOverviewViewModel : BaseViewModel
{
    [ObservableProperty] private string _hoTen = "";
    [ObservableProperty] private string _cccd = "";
    [ObservableProperty] private string _soDienThoai = "";
    [ObservableProperty] private string _email = "";
    [ObservableProperty] private string _diaChi = "";
    [ObservableProperty] private ObservableCollection<TenantHopDong> _hopDongs = new();
    [ObservableProperty] private ObservableCollection<TenantHoaDon> _hoaDons = new();
    [ObservableProperty] private ObservableCollection<TenantThongBao> _thongBaos = new();

    public string TenChao => $"Xin chào, {Session.TenDangNhap}";
    public TenantOverviewViewModel() => _ = LoadAsync();

    [RelayCommand]
    private async Task LoadAsync() => await RunSafeAsync(async () =>
    {
        ClearView();
        Session.OwnContractIds.Clear();
        OnPropertyChanged(nameof(TenChao));
        if (!Session.MaKhach.HasValue || !Session.MaTaiKhoan.HasValue)
        { ErrorMessage = "Không tìm thấy thông tin tài khoản. Vui lòng đăng nhập lại."; return; }
        int customerId = Session.MaKhach.Value, accountId = Session.MaTaiKhoan.Value;
        var profileTask = ApiClient.GetRawAsync($"/api/KhachThue/{customerId}");
        var contractsTask = ApiClient.GetRawAsync($"/api/HopDong/customer/{customerId}");
        var invoicesTask = ApiClient.GetRawAsync($"/api/HoaDon/customer/{customerId}");
        var noticesTask = ApiClient.GetRawAsync($"/api/ThongBao/customer/{accountId}");
        await Task.WhenAll(profileTask, contractsTask, invoicesTask, noticesTask);
        // Không đưa phản hồi muộn của phiên cũ vào màn hình của phiên mới.
        if (Session.MaKhach != customerId || Session.MaTaiKhoan != accountId) return;

        var contracts = ArrayRows(contractsTask.Result, "hợp đồng").Select(h => new TenantHopDong
        {
            MaHopDong = ApiClient.ElStr(h, "maHopDong"),
            MaPhong = ApiClient.ElStr(h, "maPhong"),
            TenPhong = LookupData.Display(ApiClient.ElStr(h, "tenPhong"), ApiClient.ElStr(h, "maPhong")),
            NgayBatDau = DateDisplay(ApiClient.ElStr(h, "ngayBatDau")),
            NgayKetThuc = DateDisplay(ApiClient.ElStr(h, "ngayKetThuc")),
            GiaThueThang = VnMoney.FormatD(ApiClient.ElStr(h, "giaThueThang")),
            TrangThai = ApiClient.ElStr(h, "trangThai")
        }).ToList();
        var invoices = ArrayRows(invoicesTask.Result, "hóa đơn").Select(h => new TenantHoaDon
        {
            MaHoaDon = ApiClient.ElStr(h, "maHoaDon"),
            TenPhong = ApiClient.ElStr(h, "tenPhong"),
            ThangNam = $"{ApiClient.ElStr(h, "thang")}/{ApiClient.ElStr(h, "nam")}",
            TongTien = VnMoney.FormatD(ApiClient.ElStr(h, "tongTien")),
            DaThu = VnMoney.FormatD(ApiClient.ElStr(h, "daThanhToan")),
            ConLai = VnMoney.FormatD(ApiClient.ElStr(h, "conLai")),
            TrangThai = ApiClient.ElStr(h, "trangThai")
        }).ToList();
        var notices = ArrayRows(noticesTask.Result, "thông báo").Select(t => new TenantThongBao
        {
            MaThongBao = ApiClient.ElStr(t, "maThongBao"),
            TieuDe = ApiClient.ElStr(t, "tieuDe"),
            NgayGui = DateDisplay(ApiClient.ElStr(t, "ngayGui")),
            TrangThai = ApiClient.ElStr(t, "trangThai")
        }).ToList();
        var profile = profileTask.Result;
        HoTen = ApiClient.ElStr(profile, "hoTen");
        Cccd = ApiClient.ElStr(profile, "cccd");
        SoDienThoai = ApiClient.ElStr(profile, "soDienThoai");
        Email = ApiClient.ElStr(profile, "email");
        DiaChi = ApiClient.ElStr(profile, "diaChi");
        HopDongs = new(contracts);
        HoaDons = new(invoices);
        ThongBaos = new(notices);
        foreach (var h in contracts)
            if (int.TryParse(h.MaHopDong, out var id) && id > 0) Session.OwnContractIds.Add(id);
    });

    private void ClearView()
    {
        HoTen = Cccd = SoDienThoai = Email = DiaChi = "";
        HopDongs = new();
        HoaDons = new();
        ThongBaos = new();
    }

    private static IEnumerable<JsonElement> ArrayRows(JsonElement data, string label) => data.ValueKind == JsonValueKind.Array
        ? data.EnumerateArray().ToArray() : throw new Exception($"Danh sách {label} từ máy chủ không đúng định dạng. Vui lòng thử lại.");
    private static string DateDisplay(string value) => DateTime.TryParse(value, out var date) ? date.ToString("dd/MM/yyyy") : value;
}

public class TenantHopDong
{
    public string MaHopDong { get; set; } = "";
    public string MaPhong { get; set; } = "";
    public string TenPhong { get; set; } = "";
    public string NgayBatDau { get; set; } = "";
    public string NgayKetThuc { get; set; } = "";
    public string GiaThueThang { get; set; } = "";
    public string TrangThai { get; set; } = "";
}

public class TenantHoaDon
{
    public string MaHoaDon { get; set; } = "";
    public string TenPhong { get; set; } = "";
    public string ThangNam { get; set; } = "";
    public string TongTien { get; set; } = "";
    public string DaThu { get; set; } = "";
    public string ConLai { get; set; } = "";
    public string TrangThai { get; set; } = "";
}

public class TenantThongBao
{
    public string MaThongBao { get; set; } = "";
    public string TieuDe { get; set; } = "";
    public string NgayGui { get; set; } = "";
    public string TrangThai { get; set; } = "";
}
