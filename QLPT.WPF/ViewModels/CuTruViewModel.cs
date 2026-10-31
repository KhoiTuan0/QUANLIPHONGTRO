using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QLPT.WPF.Services;
using System.Collections.ObjectModel;

namespace QLPT.WPF.ViewModels;

public partial class CuTruViewModel : BaseViewModel
{
    public bool IsAdmin => Session.IsAdmin;
    [ObservableProperty] private string _maHopDong = "";
    [ObservableProperty] private ObservableCollection<CuTruItem> _items = new();
    [ObservableProperty] private CuTruItem? _selectedItem;

    // Ô nhập mã hợp đồng đã bỏ khỏi giao diện; API trả toàn bộ cư trú khi không lọc,
    // nên mở trang là tải luôn và dùng ô tìm kiếm của bảng để lọc theo mã HĐ.
    public CuTruViewModel() => _ = TaiAsync();

    [RelayCommand]
    private async Task TaiAsync()
    {
        await RunSafeAsync(async () =>
        {
            string url = string.IsNullOrWhiteSpace(MaHopDong)
                ? "/api/DanhMucMoRong/cu-tru"
                : $"/api/DanhMucMoRong/cu-tru?maHopDong={MaHopDong.Trim()}";
            var arr = await ApiClient.GetRawAsync(url);
            var list = new ObservableCollection<CuTruItem>();
            foreach (var el in arr.EnumerateArray())
            {
                // Try to get hoTen from navigation property
                string hoTen = "";
                if (el.TryGetProperty("maKhachNavigation", out var nav) && nav.ValueKind == System.Text.Json.JsonValueKind.Object)
                    hoTen = ApiClient.ElStr(nav, "hoTen");
                if (string.IsNullOrEmpty(hoTen)) hoTen = ApiClient.ElStr(el, "hoTen");
                var declaration = el.TryGetProperty("khaiBaoMoiNhat", out var latest) &&
                    latest.ValueKind == System.Text.Json.JsonValueKind.Object ? latest : default;
                list.Add(new CuTruItem
                {
                    MaCuTru = ApiClient.ElStr(el, "maCuTru"),
                    MaHopDong = ApiClient.ElStr(el, "maHopDong"),
                    TenPhong = LookupData.Display(ApiClient.ElStr(el, "tenPhong"), ApiClient.ElStr(el, "maHopDong")),
                    MaKhach = ApiClient.ElStr(el, "maKhach"),
                    HoTen = hoTen,
                    NgayVao = ApiClient.ElStr(el, "ngayVao"),
                    NgayRoi = ApiClient.ElStr(el, "ngayRoi"),
                    NgayKhaiBao = DateTime.TryParse(ApiClient.ElStr(el, "ngayKhaiBao"), out var ngayKb)
                        ? ngayKb.ToString("dd/MM/yyyy") : "",
                    TrangThaiCuTru = ApiClient.ElStr(el, "trangThaiCuTru"),
                    TrangThaiKhaiBao = ApiClient.ElStr(el, "trangThaiKhaiBao"),
                    MaKhaiBao = ApiClient.ElStr(declaration, "maKhaiBao"),
                    DiaChiThuongTru = ApiClient.ElStr(declaration, "diaChiThuongTru"),
                    ThongTinBoSung = ApiClient.ElStr(declaration, "thongTinBoSung"),
                    TrangThaiHoSo = ApiClient.ElStr(declaration, "trangThai"),
                    LyDoTraLai = ApiClient.ElStr(declaration, "lyDoTraLai"),
                    NgayGui = ApiClient.ElStr(declaration, "ngayGui")
                });
            }
            Items = list;
        });
    }

    [RelayCommand]
    private async Task ThemNguoiAsync()
    {
        // Điền sẵn hợp đồng của dòng đang chọn — thao tác thường gặp là thêm người vào cùng phòng.
        var dlg = new Views.Dialogs.CuTruDialog(SelectedItem?.MaHopDong ?? MaHopDong)
            { Owner = System.Windows.Application.Current.MainWindow };
        if (dlg.ShowDialog() == true) await TaiAsync();
    }

    [RelayCommand]
    private async Task KhaiBaoAsync()
    {
        if (SelectedItem == null) { ErrorMessage = "Vui lòng chọn một hồ sơ cư trú."; return; }
        if (SelectedItem.TrangThaiKhaiBao != "Chờ duyệt" || string.IsNullOrWhiteSpace(SelectedItem.MaKhaiBao))
        { ErrorMessage = "Dòng này chưa có thông tin khách gửi đang chờ duyệt."; return; }
        var dlg = new Views.Dialogs.KhaiBaoCuTruDialog(SelectedItem)
            { Owner = System.Windows.Application.Current.MainWindow };
        if (dlg.ShowDialog() == true) await TaiAsync();
    }

    [RelayCommand]
    private async Task TamVangAsync() => await CapNhatCuTruAsync("Tạm vắng");

    [RelayCommand]
    private async Task TroLaiAsync() => await CapNhatCuTruAsync("Đang ở");

    private async Task CapNhatCuTruAsync(string trangThai)
    {
        if (SelectedItem == null) { ErrorMessage = "Vui lòng chọn người đang cư trú."; return; }
        if (SelectedItem.TrangThaiCuTru is "Đã rời đi" or "Đã rời") { ErrorMessage = "Khách đã rời đi. Thêm lượt cư trú mới nếu khách quay lại."; return; }
        var id = SelectedItem.MaCuTru;
        await RunSafeAsync(async () =>
        {
            await ApiClient.PutAsync($"/api/DanhMucMoRong/cu-tru/{id}", new { trangThaiCuTru = trangThai });
            await TaiAsync();
        });
    }

    [RelayCommand]
    private async Task RoiDiAsync()
    {
        if (SelectedItem == null) { ErrorMessage = "Vui lòng chọn một dòng."; return; }
        if (SelectedItem.TrangThaiCuTru is "Đã rời đi" or "Đã rời") { ErrorMessage = "Khách đã rời đi rồi."; return; }
        await RunSafeAsync(async () =>
        {
            await ApiClient.PutAsync($"/api/DanhMucMoRong/cu-tru/{SelectedItem.MaCuTru}",
                new { trangThaiCuTru = "Đã rời đi", ngayRoi = DateTime.Today.ToString("yyyy-MM-dd") });
            await TaiAsync();
        });
    }
}

public class CuTruItem
{
    public string MaCuTru { get; set; } = "";
    public string MaHopDong { get; set; } = "";
    public string TenPhong { get; set; } = "";
    public string MaKhach { get; set; } = "";
    public string HoTen { get; set; } = "";
    public string NgayVao { get; set; } = "";
    public string NgayRoi { get; set; } = "";
    public string NgayKhaiBao { get; set; } = "";
    public string TrangThaiCuTru { get; set; } = "";
    public string TrangThaiKhaiBao { get; set; } = "";
    public string MaKhaiBao { get; set; } = "";
    public string DiaChiThuongTru { get; set; } = "";
    public string ThongTinBoSung { get; set; } = "";
    public string TrangThaiHoSo { get; set; } = "";
    public string LyDoTraLai { get; set; } = "";
    public string NgayGui { get; set; } = "";
}
