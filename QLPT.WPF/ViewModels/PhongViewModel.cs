using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QLPT.WPF.Services;
using System.Collections.ObjectModel;

namespace QLPT.WPF.ViewModels;

public partial class PhongViewModel : BaseViewModel
{
    [ObservableProperty] private ObservableCollection<PhongItem> _items = new();
    [ObservableProperty] private PhongItem? _selectedItem;
    [ObservableProperty] private string _filterTrangThai = "Tất cả";

    public List<string> TrangThaiOptions { get; } = new()
    {
        "Tất cả", "Trống", "Đã đặt cọc", "Đang thuê", "Bảo trì"
    };

    partial void OnFilterTrangThaiChanged(string value) => _ = LoadAsync();

    public PhongViewModel() => _ = LoadAsync();

    [RelayCommand]
    private async Task LoadAsync() => await RunSafeAsync(async () =>
    {
        string url = FilterTrangThai == "Tất cả"
            ? "/api/Phong"
            : $"/api/Phong?trangThai={Uri.EscapeDataString(FilterTrangThai)}";
        var arr = await ApiClient.GetRawAsync(url);
        var list = new ObservableCollection<PhongItem>();
        foreach (var el in arr.EnumerateArray())
        {
            decimal.TryParse(ApiClient.ElStr(el, "giaThue"), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var gia);
            int.TryParse(ApiClient.ElStr(el, "maLoaiPhong"), out var maLoai);
            int.TryParse(ApiClient.ElStr(el, "maCoSo"), out var maCoSo);
            int.TryParse(ApiClient.ElStr(el, "tang"), out var tang);
            list.Add(new PhongItem
            {
                MaPhong = ApiClient.ElStr(el, "maPhong"),
                TenPhong = ApiClient.ElStr(el, "tenPhong"),
                MaLoaiPhong = maLoai,
                GiaThue = gia,
                GiaThueDisplay = VnMoney.FormatD(gia),
                TrangThai = ApiClient.ElStr(el, "trangThai"),
                MaCoSo = maCoSo,
                Tang = tang,
                MoTa = ApiClient.ElStr(el, "moTa")
            });
        }
        Items = list;
    });

    [RelayCommand]
    private async Task AddAsync()
    {
        var dlg = new Views.Dialogs.PhongDialog(null);
        if (dlg.ShowDialog() == true) await LoadAsync();
    }

    [RelayCommand]
    private async Task EditAsync()
    {
        if (SelectedItem == null) { ErrorMessage = "Vui lòng chọn một dòng."; return; }
        var dlg = new Views.Dialogs.PhongDialog(SelectedItem);
        if (dlg.ShowDialog() == true) await LoadAsync();
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (SelectedItem == null) { ErrorMessage = "Vui lòng chọn một dòng."; return; }
        if (!Views.Dialogs.ConfirmDialog.Delete(
                "Phòng này sẽ bị xóa khỏi hệ thống.",
                $"#{SelectedItem.MaPhong} — {SelectedItem.TenPhong}",
                "Xóa phòng")) return;
        await RunSafeAsync(async () =>
        {
            await ApiClient.DeleteAsync($"/api/Phong/{SelectedItem.MaPhong}");
            await LoadAsync();
        });
    }
}

public class PhongItem
{
    public string MaPhong { get; set; } = "";
    public string TenPhong { get; set; } = "";
    public int MaLoaiPhong { get; set; }
    public decimal GiaThue { get; set; }
    public string GiaThueDisplay { get; set; } = "";
    public string TrangThai { get; set; } = "";
    public int MaCoSo { get; set; }
    public int Tang { get; set; }
    public string MoTa { get; set; } = "";
}
