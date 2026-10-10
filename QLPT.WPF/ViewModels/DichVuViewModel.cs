using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QLPT.WPF.Services;
using System.Collections.ObjectModel;

namespace QLPT.WPF.ViewModels;

public partial class DichVuViewModel : BaseViewModel
{
    [ObservableProperty] private ObservableCollection<DichVuItem> _items = new();
    [ObservableProperty] private DichVuItem? _selectedItem;

    public DichVuViewModel() => _ = LoadAsync();

    [RelayCommand]
    private async Task LoadAsync() => await RunSafeAsync(async () =>
    {
        var arr = await ApiClient.GetRawAsync("/api/DichVu");
        var list = new ObservableCollection<DichVuItem>();
        foreach (var el in arr.EnumerateArray())
        {
            decimal.TryParse(ApiClient.ElStr(el, "donGia"), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var donGia);
            list.Add(new DichVuItem
            {
                MaDichVu = ApiClient.ElStr(el, "maDichVu"),
                TenDichVu = ApiClient.ElStr(el, "tenDichVu"),
                DonViTinh = ApiClient.ElStr(el, "donViTinh"),
                DonGia = donGia,
                DonGiaDisplay = VnMoney.FormatD(donGia),
                TrangThai = ApiClient.ElStr(el, "trangThai")
            });
        }
        Items = list;
    });

    [RelayCommand]
    private async Task AddAsync()
    {
        var dlg = new Views.Dialogs.DichVuDialog();
        if (dlg.ShowDialog() == true) await LoadAsync();
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (SelectedItem == null) { ErrorMessage = "Vui lòng chọn một dòng."; return; }
        if (!Views.Dialogs.ConfirmDialog.Delete(
                "Dịch vụ này sẽ bị xóa khỏi bảng giá.",
                $"#{SelectedItem.MaDichVu} — {SelectedItem.TenDichVu}",
                "Xóa dịch vụ")) return;
        await RunSafeAsync(async () =>
        {
            await ApiClient.DeleteAsync($"/api/DichVu/{SelectedItem.MaDichVu}");
            await LoadAsync();
        });
    }
}

public class DichVuItem
{
    public string MaDichVu { get; set; } = "";
    public string TenDichVu { get; set; } = "";
    public string DonViTinh { get; set; } = "";
    public decimal DonGia { get; set; }
    public string DonGiaDisplay { get; set; } = "";
    public string TrangThai { get; set; } = "";
}
