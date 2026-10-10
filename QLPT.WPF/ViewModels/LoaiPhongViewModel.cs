using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QLPT.WPF.Services;
using System.Collections.ObjectModel;

namespace QLPT.WPF.ViewModels;

public partial class LoaiPhongViewModel : BaseViewModel
{
    [ObservableProperty] private ObservableCollection<LoaiPhongItem> _items = new();
    [ObservableProperty] private LoaiPhongItem? _selectedItem;

    public LoaiPhongViewModel() => _ = LoadAsync();

    [RelayCommand]
    private async Task LoadAsync() => await RunSafeAsync(async () =>
    {
        var arr = await ApiClient.GetRawAsync("/api/LoaiPhong");
        var list = new ObservableCollection<LoaiPhongItem>();
        foreach (var el in arr.EnumerateArray())
        {
            decimal.TryParse(ApiClient.ElStr(el, "giaMacDinh"), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var gia);
            decimal.TryParse(ApiClient.ElStr(el, "dienTich"), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var dien);
            int.TryParse(ApiClient.ElStr(el, "soNguoiToiDa"), out var soNguoi);
            list.Add(new LoaiPhongItem
            {
                MaLoaiPhong = ApiClient.ElStr(el, "maLoaiPhong"),
                TenLoaiPhong = ApiClient.ElStr(el, "tenLoaiPhong"),
                DienTich = dien,
                SoNguoiToiDa = soNguoi,
                GiaMacDinh = gia,
                GiaMacDinhDisplay = VnMoney.FormatD(gia),
                MoTa = ApiClient.ElStr(el, "moTa")
            });
        }
        Items = list;
    });

    [RelayCommand]
    private async Task AddAsync()
    {
        var dlg = new Views.Dialogs.LoaiPhongDialog(null);
        if (dlg.ShowDialog() == true) await LoadAsync();
    }

    [RelayCommand]
    private async Task EditAsync()
    {
        if (SelectedItem == null) { ErrorMessage = "Vui lòng chọn một dòng."; return; }
        var dlg = new Views.Dialogs.LoaiPhongDialog(SelectedItem);
        if (dlg.ShowDialog() == true) await LoadAsync();
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (SelectedItem == null) { ErrorMessage = "Vui lòng chọn một dòng."; return; }
        if (!Views.Dialogs.ConfirmDialog.Delete(
                "Loại phòng này sẽ bị xóa. Các phòng đang dùng loại này có thể bị ảnh hưởng.",
                $"#{SelectedItem.MaLoaiPhong} — {SelectedItem.TenLoaiPhong}",
                "Xóa loại phòng")) return;
        await RunSafeAsync(async () =>
        {
            await ApiClient.DeleteAsync($"/api/LoaiPhong/{SelectedItem.MaLoaiPhong}");
            await LoadAsync();
        });
    }
}

public class LoaiPhongItem
{
    public string MaLoaiPhong { get; set; } = "";
    public string TenLoaiPhong { get; set; } = "";
    public decimal DienTich { get; set; }
    public int SoNguoiToiDa { get; set; }
    public decimal GiaMacDinh { get; set; }
    public string GiaMacDinhDisplay { get; set; } = "";
    public string MoTa { get; set; } = "";
}
