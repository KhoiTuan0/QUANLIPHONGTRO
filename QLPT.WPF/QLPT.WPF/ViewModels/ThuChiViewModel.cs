using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QLPT.WPF.Services;
using System.Collections.ObjectModel;

namespace QLPT.WPF.ViewModels;

public partial class ThuChiViewModel : BaseViewModel
{
    [ObservableProperty] private ObservableCollection<ThuChiItem> _items = new();
    [ObservableProperty] private ThuChiItem? _selectedItem;

    public ThuChiViewModel() => _ = LoadAsync();

    [RelayCommand]
    private async Task LoadAsync() => await RunSafeAsync(async () =>
    {
        var arr = await ApiClient.GetRawAsync("/api/ThuChi");
        var list = new ObservableCollection<ThuChiItem>();
        foreach (var el in arr.EnumerateArray())
        {
            decimal.TryParse(ApiClient.ElStr(el, "soTien"), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var tien);
            list.Add(new ThuChiItem
            {
                MaThuChi = ApiClient.ElStr(el, "maPhieu"),
                Loai = ApiClient.ElStr(el, "loaiPhieu"),
                SoTien = tien,
                SoTienDisplay = VnMoney.FormatD(tien),
                MoTa = ApiClient.ElStr(el, "noiDung"),
                NgayGhiNhan = ApiClient.ElStr(el, "ngayLap"),
                NguoiGhiNhan = ApiClient.ElStr(el, "nguoiNopNhan")
            });
        }
        Items = list;
    });

    [RelayCommand]
    private async Task ThemAsync()
    {
        var dlg = new Views.Dialogs.ThuChiDialog();
        if (dlg.ShowDialog() == true) await LoadAsync();
    }

    [RelayCommand]
    private async Task XoaAsync()
    {
        if (SelectedItem == null) { ErrorMessage = "Vui lòng chọn một dòng."; return; }
        if (!Views.Dialogs.ConfirmDialog.Delete(
                "Phiếu thu chi này sẽ bị xóa khỏi sổ.",
                $"#{SelectedItem.MaThuChi} — {SelectedItem.Loai} · {SelectedItem.MoTa}",
                "Xóa phiếu")) return;
        await RunSafeAsync(async () =>
        {
            await ApiClient.DeleteAsync($"/api/ThuChi/{SelectedItem.MaThuChi}");
            await LoadAsync();
        });
    }
}

public class ThuChiItem
{
    public string MaThuChi { get; set; } = "";
    public string Loai { get; set; } = "";
    public decimal SoTien { get; set; }
    public string SoTienDisplay { get; set; } = "";
    public string MoTa { get; set; } = "";
    public string NgayGhiNhan { get; set; } = "";
    public string NguoiGhiNhan { get; set; } = "";
}
