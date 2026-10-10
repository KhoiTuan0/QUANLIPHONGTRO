using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QLPT.WPF.Services;
using System.Collections.ObjectModel;

namespace QLPT.WPF.ViewModels;

public partial class CuTruViewModel : BaseViewModel
{
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
                list.Add(new CuTruItem
                {
                    MaCuTru = ApiClient.ElStr(el, "maCuTru"),
                    MaHopDong = ApiClient.ElStr(el, "maHopDong"),
                    MaKhach = ApiClient.ElStr(el, "maKhach"),
                    HoTen = hoTen,
                    NgayVao = ApiClient.ElStr(el, "ngayVao"),
                    NgayRoi = ApiClient.ElStr(el, "ngayRoi"),
                    TrangThaiCuTru = ApiClient.ElStr(el, "trangThaiCuTru"),
                    TrangThaiKhaiBao = ApiClient.ElStr(el, "trangThaiKhaiBao")
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
    private async Task CapNhatTrangThaiAsync(string trangThai)
    {
        if (SelectedItem == null) { ErrorMessage = "Vui lòng chọn một dòng."; return; }
        await RunSafeAsync(async () =>
        {
            await ApiClient.PutAsync($"/api/DanhMucMoRong/cu-tru/{SelectedItem.MaCuTru}", new { trangThaiKhaiBao = trangThai });
            await TaiAsync();
        });
    }

    [RelayCommand]
    private async Task KhaiBaoAsync() => await CapNhatTrangThaiAsync("Đã khai báo");

    [RelayCommand]
    private async Task TamVangAsync() => await CapNhatTrangThaiAsync("Tạm vắng");

    [RelayCommand]
    private async Task TroLaiAsync() => await CapNhatTrangThaiAsync("Trở lại");

    [RelayCommand]
    private async Task RoiDiAsync()
    {
        if (SelectedItem == null) { ErrorMessage = "Vui lòng chọn một dòng."; return; }
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
    public string MaKhach { get; set; } = "";
    public string HoTen { get; set; } = "";
    public string NgayVao { get; set; } = "";
    public string NgayRoi { get; set; } = "";
    public string TrangThaiCuTru { get; set; } = "";
    public string TrangThaiKhaiBao { get; set; } = "";
}
