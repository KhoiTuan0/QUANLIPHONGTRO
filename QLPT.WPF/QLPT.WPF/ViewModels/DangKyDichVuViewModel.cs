using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QLPT.WPF.Services;
using QLPT.WPF.Views.Dialogs;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace QLPT.WPF.ViewModels;

public partial class DangKyDichVuViewModel : BaseViewModel
{
    [ObservableProperty] private ObservableCollection<DangKyDichVuItem> _items = new();
    [ObservableProperty] private DangKyDichVuItem? _selectedItem;
    [ObservableProperty] private string _maHopDong = "";

    // API trả toàn bộ đăng ký khi không lọc; mở trang là thấy ngay, lọc bằng ô tìm kiếm.
    public DangKyDichVuViewModel() => _ = LoadAsync();

    [RelayCommand]
    private async Task LoadAsync() => await RunSafeAsync(async () =>
    {
        string url = string.IsNullOrWhiteSpace(MaHopDong)
            ? "/api/DangKyDichVu"
            : $"/api/DangKyDichVu?maHopDong={MaHopDong.Trim()}";
        var el = await ApiClient.GetRawAsync(url);
        var list = new List<DangKyDichVuItem>();
        if (el.ValueKind == JsonValueKind.Array)
            foreach (var item in el.EnumerateArray())
                list.Add(new DangKyDichVuItem
                {
                    MaDangKy   = ApiClient.ElStr(item, "maDangKy"),
                    MaHopDong  = ApiClient.ElStr(item, "maHopDong"),
                    MaDichVu   = ApiClient.ElStr(item, "maDichVu"),
                    TenDichVu  = ApiClient.ElStr(item, "tenDichVu"),
                    NgayBatDau = ApiClient.ElStr(item, "ngayBatDau"),
                    NgayKetThuc= ApiClient.ElStr(item, "ngayKetThuc"),
                    TrangThai  = ApiClient.ElStr(item, "trangThai"),
                });
        Items = new ObservableCollection<DangKyDichVuItem>(list);
    });

    [RelayCommand]
    private async Task DangKyAsync()
    {
        var dlg = new DangKyDichVuDialog(SelectedItem?.MaHopDong ?? MaHopDong)
            { Owner = System.Windows.Application.Current.MainWindow };
        if (dlg.ShowDialog() == true)
            await LoadAsync();
    }

    [RelayCommand]
    private async Task NgungAsync()
    {
        if (SelectedItem == null) { ErrorMessage = "Chọn dịch vụ cần ngừng."; return; }
        if (!ConfirmDialog.Ask("Ngừng dịch vụ",
                "Dịch vụ sẽ kết thúc từ hôm nay và không còn được tính vào hóa đơn các tháng sau.",
                $"HĐ #{SelectedItem.MaHopDong} — {SelectedItem.TenDichVu}", "Ngừng dịch vụ")) return;
        await RunSafeAsync(async () =>
            await ApiClient.PutAsync($"/api/DangKyDichVu/{SelectedItem.MaDangKy}/ngung", new { }));
        if (string.IsNullOrEmpty(ErrorMessage))
            await LoadAsync();
    }
}

public class DangKyDichVuItem
{
    public string MaDangKy    { get; set; } = "";
    public string MaHopDong   { get; set; } = "";
    public string MaDichVu    { get; set; } = "";
    public string TenDichVu   { get; set; } = "";
    public string NgayBatDau  { get; set; } = "";
    public string NgayKetThuc { get; set; } = "";
    public string TrangThai   { get; set; } = "";
}
