using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QLPT.WPF.Services;
using QLPT.WPF.Views.Dialogs;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace QLPT.WPF.ViewModels;

/// <summary>Quy trình: Vi phạm → Xử lý → Thông báo cho khách.</summary>
public partial class ViPhamViewModel : BaseViewModel
{
    [ObservableProperty] private ObservableCollection<ViPhamItem> _items = new();
    [ObservableProperty] private ViPhamItem? _selectedItem;

    public ViPhamViewModel() => _ = LoadAsync();

    [RelayCommand]
    private async Task LoadAsync() => await RunSafeAsync(async () =>
    {
        var el = await ApiClient.GetRawAsync("/api/ViPham");
        var list = new List<ViPhamItem>();
        if (el.ValueKind == JsonValueKind.Array)
            foreach (var item in el.EnumerateArray())
            {
                decimal.TryParse(ApiClient.ElStr(item, "tienPhat"),
                    System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var phat);
                string ngay = ApiClient.ElStr(item, "ngayViPham");
                list.Add(new ViPhamItem
                {
                    MaViPham     = ApiClient.ElStr(item, "maViPham"),
                    MaHopDong    = ApiClient.ElStr(item, "maHopDong"),
                    TenPhong     = ApiClient.ElStr(item, "tenPhong"),
                    TenKhach     = ApiClient.ElStr(item, "tenKhach"),
                    NoiDung      = ApiClient.ElStr(item, "noiDung"),
                    NgayViPham   = DateTime.TryParse(ngay, out var d) ? d.ToString("dd/MM/yyyy") : ngay,
                    TienPhatSo   = phat,
                    MucPhat      = phat > 0 ? VnMoney.FormatD(phat) : "",
                    HinhThucXuLy = ApiClient.ElStr(item, "hinhThucXuLy"),
                    TrangThai    = ApiClient.ElStr(item, "trangThai"),
                });
            }
        Items = new ObservableCollection<ViPhamItem>(list);
    });

    [RelayCommand]
    private async Task ThemAsync()
    {
        var dlg = new ViPhamDialog
            { Owner = System.Windows.Application.Current.MainWindow };
        if (dlg.ShowDialog() == true)
            await LoadAsync();
    }

    [RelayCommand]
    private async Task XuLyAsync()
    {
        if (SelectedItem == null) { ErrorMessage = "Chọn vi phạm cần xử lý."; return; }
        if (SelectedItem.TrangThai == "Đã xử lý") { ErrorMessage = "Vi phạm này đã được xử lý."; return; }
        var dlg = new XuLyViPhamDialog(SelectedItem)
            { Owner = System.Windows.Application.Current.MainWindow };
        if (dlg.ShowDialog() == true)
            await LoadAsync();
    }

    [RelayCommand]
    private async Task XoaAsync()
    {
        if (SelectedItem == null) { ErrorMessage = "Chọn vi phạm cần xóa."; return; }
        if (!ConfirmDialog.Delete("Bản ghi vi phạm này sẽ bị xóa.",
                $"#{SelectedItem.MaViPham} — {SelectedItem.NoiDung}", "Xóa vi phạm")) return;
        await RunSafeAsync(async () =>
            await ApiClient.DeleteAsync($"/api/ViPham/{SelectedItem.MaViPham}"));
        if (string.IsNullOrEmpty(ErrorMessage))
            await LoadAsync();
    }
}

public class ViPhamItem
{
    public string MaViPham     { get; set; } = "";
    public string MaHopDong    { get; set; } = "";
    public string TenPhong     { get; set; } = "";
    public string TenKhach     { get; set; } = "";
    public string NoiDung      { get; set; } = "";
    public string NgayViPham   { get; set; } = "";
    public decimal TienPhatSo  { get; set; }
    public string MucPhat      { get; set; } = "";
    public string HinhThucXuLy { get; set; } = "";
    public string TrangThai    { get; set; } = "";
}
