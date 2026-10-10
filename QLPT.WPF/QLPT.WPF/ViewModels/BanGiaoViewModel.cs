using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QLPT.WPF.Services;
using System.Collections.ObjectModel;

namespace QLPT.WPF.ViewModels;

public partial class BanGiaoViewModel : BaseViewModel
{
    [ObservableProperty] private string _maPhong = "";
    [ObservableProperty] private ObservableCollection<TaiSanPhongItem> _items = new();
    [ObservableProperty] private TaiSanPhongItem? _selectedItem;

    [RelayCommand]
    private async Task TaiAsync()
    {
        if (string.IsNullOrWhiteSpace(MaPhong)) { ErrorMessage = "Nhập mã phòng."; return; }
        await RunSafeAsync(async () =>
        {
            var arr = await ApiClient.GetRawAsync($"/api/TaiSan/phong/{MaPhong.Trim()}");
            var list = new ObservableCollection<TaiSanPhongItem>();
            foreach (var el in arr.EnumerateArray())
            {
                string tenTaiSan = ApiClient.ElStr(el, "tenTaiSan");
                if (string.IsNullOrEmpty(tenTaiSan))
                {
                    if (el.TryGetProperty("taiSan", out var ts) && ts.ValueKind == System.Text.Json.JsonValueKind.Object)
                        tenTaiSan = ApiClient.ElStr(ts, "tenTaiSan");
                }
                int.TryParse(ApiClient.ElStr(el, "soLuong"), out var sl);
                list.Add(new TaiSanPhongItem
                {
                    MaTaiSanPhong = ApiClient.ElStr(el, "maTaiSanPhong"),
                    TenTaiSan = tenTaiSan,
                    SoLuong = sl,
                    TinhTrang = ApiClient.ElStr(el, "tinhTrang")
                });
            }
            Items = list;
        });
    }

    [RelayCommand]
    private async Task CapNhatTinhTrangAsync()
    {
        if (SelectedItem == null) { ErrorMessage = "Vui lòng chọn một tài sản."; return; }
        var dlg = new Views.Dialogs.TinhTrangDialog(SelectedItem.MaTaiSanPhong, SelectedItem.TenTaiSan, SelectedItem.TinhTrang);
        if (dlg.ShowDialog() == true) await TaiAsync();
    }
}

public class TaiSanPhongItem
{
    public string MaTaiSanPhong { get; set; } = "";
    public string TenTaiSan { get; set; } = "";
    public int SoLuong { get; set; }
    public string TinhTrang { get; set; } = "";
}
