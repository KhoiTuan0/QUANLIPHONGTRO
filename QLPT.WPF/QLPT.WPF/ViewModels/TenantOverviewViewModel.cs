using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QLPT.WPF.Services;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace QLPT.WPF.ViewModels;

public partial class TenantOverviewViewModel : BaseViewModel
{
    // Personal info
    [ObservableProperty] private string _hoTen       = "";
    [ObservableProperty] private string _cccd        = "";
    [ObservableProperty] private string _soDienThoai = "";
    [ObservableProperty] private string _email       = "";
    [ObservableProperty] private string _diaChi      = "";

    // Tables
    [ObservableProperty] private ObservableCollection<TenantHopDong>  _hopDongs  = new();
    [ObservableProperty] private ObservableCollection<TenantHoaDon>   _hoaDons   = new();
    [ObservableProperty] private ObservableCollection<TenantThongBao> _thongBaos = new();

    public string TenChao => $"Xin chào, {Session.TenDangNhap}";

    public TenantOverviewViewModel() => _ = LoadAsync();

    [RelayCommand]
    private async Task LoadAsync() => await RunSafeAsync(async () =>
    {
        if (!Session.MaKhach.HasValue || !Session.MaTaiKhoan.HasValue)
        {
            ErrorMessage = "Không tìm thấy thông tin tài khoản.";
            return;
        }
        int maKhach     = Session.MaKhach.Value;
        int maTaiKhoan  = Session.MaTaiKhoan.Value;

        // Personal info
        var info = await ApiClient.GetRawAsync($"/api/KhachThue/{maKhach}");
        HoTen       = ApiClient.ElStr(info, "hoTen");
        Cccd        = ApiClient.ElStr(info, "cccd");
        SoDienThoai = ApiClient.ElStr(info, "soDienThoai");
        Email       = ApiClient.ElStr(info, "email");
        DiaChi      = ApiClient.ElStr(info, "diaChi");

        // Contracts
        var hdEl  = await ApiClient.GetRawAsync($"/api/HopDong/customer/{maKhach}");
        var hdList = new List<TenantHopDong>();
        if (hdEl.ValueKind == JsonValueKind.Array)
            foreach (var h in hdEl.EnumerateArray())
            {
                string maHd = ApiClient.ElStr(h, "maHopDong");
                // Ghi nhận hợp đồng của chính mình để lớp kiểm tra quyền cho xem hóa đơn bên dưới.
                if (int.TryParse(maHd, out int ownId)) Session.OwnContractIds.Add(ownId);
                hdList.Add(new TenantHopDong
                {
                    MaHopDong   = maHd,
                    MaPhong     = ApiClient.ElStr(h, "maPhong"),
                    NgayBatDau  = ApiClient.ElStr(h, "ngayBatDau"),
                    NgayKetThuc = ApiClient.ElStr(h, "ngayKetThuc"),
                    GiaThueThang= VnMoney.FormatD(ApiClient.ElStr(h, "giaThueThang")),
                    TrangThai   = ApiClient.ElStr(h, "trangThai"),
                });
            }
        HopDongs = new ObservableCollection<TenantHopDong>(hdList);

        // Invoices — API không có /api/HoaDon/customer/{maKhach}, nên gom theo từng hợp đồng
        // qua /api/HoaDon/hop-dong/{maHopDong} (endpoint này có thật).
        var hdnList = new List<TenantHoaDon>();
        foreach (var hopDong in hdList)
        {
            if (!int.TryParse(hopDong.MaHopDong, out int hdId)) continue;
            var hdnEl = await ApiClient.GetRawAsync($"/api/HoaDon/hop-dong/{hdId}");
            if (hdnEl.ValueKind != JsonValueKind.Array) continue;
            foreach (var h in hdnEl.EnumerateArray())
            {
                string thang = ApiClient.ElStr(h, "thang");
                string nam   = ApiClient.ElStr(h, "nam");
                hdnList.Add(new TenantHoaDon
                {
                    MaHoaDon  = ApiClient.ElStr(h, "maHoaDon"),
                    ThangNam  = string.IsNullOrEmpty(thang) ? "" : $"{thang}/{nam}",
                    TongTien  = VnMoney.FormatD(ApiClient.ElStr(h, "tongTien")),
                    DaThu     = VnMoney.FormatD(ApiClient.ElStr(h, "daThanhToan")),
                    ConLai    = VnMoney.FormatD(ApiClient.ElStr(h, "conLai")),
                    TrangThai = ApiClient.ElStr(h, "trangThai"),
                });
            }
        }
        HoaDons = new ObservableCollection<TenantHoaDon>(hdnList);

        // Notifications
        var tbEl  = await ApiClient.GetRawAsync($"/api/ThongBao/customer/{maTaiKhoan}");
        var tbList = new List<TenantThongBao>();
        if (tbEl.ValueKind == JsonValueKind.Array)
            foreach (var t in tbEl.EnumerateArray())
                tbList.Add(new TenantThongBao
                {
                    MaThongBao = ApiClient.ElStr(t, "maThongBao"),
                    TieuDe     = ApiClient.ElStr(t, "tieuDe"),
                    NgayGui    = ApiClient.ElStr(t, "ngayGui"),
                    TrangThai  = ApiClient.ElStr(t, "trangThai"),
                });
        ThongBaos = new ObservableCollection<TenantThongBao>(tbList);
    });
}

public class TenantHopDong
{
    public string MaHopDong    { get; set; } = "";
    public string MaPhong      { get; set; } = "";
    public string NgayBatDau   { get; set; } = "";
    public string NgayKetThuc  { get; set; } = "";
    public string GiaThueThang { get; set; } = "";
    public string TrangThai    { get; set; } = "";
}

public class TenantHoaDon
{
    public string MaHoaDon  { get; set; } = "";
    public string ThangNam  { get; set; } = "";
    public string TongTien  { get; set; } = "";
    public string DaThu     { get; set; } = "";
    public string ConLai    { get; set; } = "";
    public string TrangThai { get; set; } = "";
}

public class TenantThongBao
{
    public string MaThongBao { get; set; } = "";
    public string TieuDe     { get; set; } = "";
    public string NgayGui    { get; set; } = "";
    public string TrangThai  { get; set; } = "";
}
