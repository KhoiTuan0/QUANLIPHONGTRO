using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QLPT.WPF.Services;
using System.Collections.ObjectModel;

namespace QLPT.WPF.ViewModels;

public partial class DatPhongViewModel : BaseViewModel
{
    [ObservableProperty] private ObservableCollection<DatPhongItem> _items = new();
    [ObservableProperty] private DatPhongItem? _selectedItem;

    public DatPhongViewModel() => _ = LoadAsync();

    [RelayCommand]
    private async Task YeuCauHuyAsync()
    {
        new Views.Dialogs.YeuCauHuyDatPhongDialog(SelectedItem) { Owner = System.Windows.Application.Current.MainWindow }.ShowDialog();
        await LoadAsync();
    }

    [RelayCommand]
    private async Task LoadAsync() => await RunSafeAsync(async () =>
    {
        var arr = await ApiClient.GetRawAsync("/api/DatPhong");
        var list = new ObservableCollection<DatPhongItem>();
        foreach (var el in arr.EnumerateArray())
        {
            decimal.TryParse(ApiClient.ElStr(el, "tienCoc"), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var coc);
            list.Add(new DatPhongItem
            {
                MaDatPhong = ApiClient.ElStr(el, "maDatPhong"),
                MaPhong = ApiClient.ElStr(el, "maPhong"),
                TenPhong = LookupData.Display(ApiClient.ElStr(el, "tenPhong"), ApiClient.ElStr(el, "maPhong")),
                MaKhach = ApiClient.ElStr(el, "maKhach"),
                TenKhach = LookupData.Display(ApiClient.ElStr(el, "tenKhach"), ApiClient.ElStr(el, "maKhach")),
                NgayDat = ApiClient.ElStr(el, "ngayDat"),
                NgayDuKienNhan = ApiClient.ElStr(el, "ngayDuKienNhan"),
                TienCoc = coc,
                TienCocDisplay = VnMoney.FormatD(coc),
                TrangThai = ApiClient.ElStr(el, "trangThai")
            });
        }
        Items = list;
    });

    [RelayCommand]
    private async Task DatMoiAsync()
    {
        var dlg = new Views.Dialogs.DatPhongDialog();
        if (dlg.ShowDialog() == true) await LoadAsync();
    }

    [RelayCommand]
    private async Task NhanPhongAsync()
    {
        if (SelectedItem == null) { ErrorMessage = "Vui lòng chọn lượt đặt phòng cần nhận."; return; }
        if (SelectedItem.TrangThai != "Đã đặt cọc") { ErrorMessage = "Chỉ nhận phòng cho lượt đặt đang giữ chỗ."; return; }
        var dlg = new Views.Dialogs.HopDongDialog(SelectedItem.MaPhong, SelectedItem.MaKhach, SelectedItem.TienCoc);
        if (dlg.ShowDialog() == true) await LoadAsync();
    }

    [RelayCommand]
    private async Task HuyGiuChoAsync()
    {
        if (SelectedItem == null) { ErrorMessage = "Vui lòng chọn một dòng."; return; }
        if (SelectedItem.TrangThai != "Đã đặt cọc") { ErrorMessage = "Chỉ hủy được lượt đặt đang giữ chỗ."; return; }
        // Hủy giữ chỗ không phải xóa dữ liệu nên dùng tông cảnh báo thay vì tông xóa.
        if (!Views.Dialogs.ConfirmDialog.Show(
                "Xác nhận hủy giữ chỗ",
                "Lượt đặt phòng sẽ chuyển sang trạng thái \"Đã hủy\".",
                $"#{SelectedItem.MaDatPhong} — {SelectedItem.TenPhong} · {SelectedItem.TenKhach}",
                "Hủy giữ chỗ",
                MaterialDesignThemes.Wpf.PackIconKind.CalendarRemoveOutline,
                "#F59E0B", warn: true)) return;
        await RunSafeAsync(async () =>
        {
            await ApiClient.PutAsync($"/api/DatPhong/{SelectedItem.MaDatPhong}/huy", new { });
            await LoadAsync();
        });
    }
}

public class DatPhongItem
{
    public string MaDatPhong { get; set; } = "";
    public string MaPhong { get; set; } = "";
    public string TenPhong { get; set; } = "";
    public string MaKhach { get; set; } = "";
    public string TenKhach { get; set; } = "";
    public string NgayDat { get; set; } = "";
    public string NgayDuKienNhan { get; set; } = "";
    public decimal TienCoc { get; set; }
    public string TienCocDisplay { get; set; } = "";
    public string TrangThai { get; set; } = "";
}
