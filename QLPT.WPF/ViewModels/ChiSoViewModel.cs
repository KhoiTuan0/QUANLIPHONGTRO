using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QLPT.WPF.Services;
using System.Collections.ObjectModel;

namespace QLPT.WPF.ViewModels;

public partial class ChiSoViewModel : BaseViewModel
{
    [ObservableProperty] private ObservableCollection<ChiSoItem> _items = new();
    [ObservableProperty] private ChiSoItem? _selectedItem;

    public ChiSoViewModel() => _ = LoadAsync();

    [RelayCommand]
    private async Task LoadAsync() => await RunSafeAsync(async () =>
    {
        var arr = await ApiClient.GetRawAsync("/api/ChiSoDienNuoc");
        var list = new ObservableCollection<ChiSoItem>();
        foreach (var el in arr.EnumerateArray())
        {
            decimal.TryParse(ApiClient.ElStr(el, "chiSoCu"), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var cu);
            decimal.TryParse(ApiClient.ElStr(el, "chiSoMoi"), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var moi);
            var thang = ApiClient.ElStr(el, "thang");
            var nam = ApiClient.ElStr(el, "nam");
            list.Add(new ChiSoItem
            {
                MaChiSo = ApiClient.ElStr(el, "maChiSo"),
                MaPhong = ApiClient.ElStr(el, "maPhong"),
                TenPhong = ApiClient.ElStr(el, "tenPhong"),
                KyGhi = ApiClient.ElStr(el, "ngayGhi"),
                ThangNam = string.IsNullOrEmpty(thang) ? "" : $"T{thang}/{nam}",
                Loai = ApiClient.ElStr(el, "loaiChiSo"),
                ChiSoCu = cu,
                ChiSoMoi = moi,
                SoTieu = moi - cu
            });
        }
        Items = list;
    });

    [RelayCommand]
    private async Task ThemChiSoAsync()
    {
        var dlg = new Views.Dialogs.ChiSoDialog();
        if (dlg.ShowDialog() == true) await LoadAsync();
    }

    [RelayCommand]
    private async Task XoaAsync()
    {
        if (SelectedItem == null) { ErrorMessage = "Vui lòng chọn một dòng."; return; }
        if (!Views.Dialogs.ConfirmDialog.Delete(
                "Bản ghi chỉ số điện nước này sẽ bị xóa.",
                $"#{SelectedItem.MaChiSo} — {SelectedItem.TenPhong} · {SelectedItem.Loai} · {SelectedItem.ThangNam}",
                "Xóa chỉ số")) return;
        await RunSafeAsync(async () =>
        {
            await ApiClient.DeleteAsync($"/api/ChiSoDienNuoc/{SelectedItem.MaChiSo}");
            await LoadAsync();
        });
    }
}

public class ChiSoItem
{
    public string MaChiSo { get; set; } = "";
    public string MaPhong { get; set; } = "";
    public string TenPhong { get; set; } = "";
    public string KyGhi { get; set; } = "";
    public string ThangNam { get; set; } = "";
    public string Loai { get; set; } = "";
    public decimal ChiSoCu { get; set; }
    public decimal ChiSoMoi { get; set; }
    public decimal SoTieu { get; set; }
}
