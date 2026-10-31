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
                    TenPhong     = ApiClient.ElStr(item, "tenPhong"),
                    TenKhach     = ApiClient.ElStr(item, "tenKhach"),
                    NoiDung      = ApiClient.ElStr(item, "noiDung"),
                    NgayViPham   = DateTime.TryParse(ngay, out var d) ? d.ToString("dd/MM/yyyy") : ngay,
                    TienPhatSo   = phat,
                    MucPhat      = phat > 0 ? VnMoney.FormatD(phat) : "",
                    HinhThucXuLy = ApiClient.ElStr(item, "hinhThucXuLy"),
                    TrangThai    = ApiClient.ElStr(item, "trangThai"),
                    HoaDon = ApiClient.ElStr(item, "maHoaDon") is { Length: > 0 } invoiceId ? $"HĐ #{invoiceId}" : "Chưa đưa vào HĐ",
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
    private async Task DuaVaoHoaDonAsync()
    {
        if (SelectedItem == null || SelectedItem.TrangThai != "Đã xử lý" || SelectedItem.TienPhatSo <= 0)
        { ErrorMessage = "Chọn vi phạm đã xử lý và có tiền phạt lớn hơn 0."; return; }
        if (SelectedItem.HoaDon.StartsWith("HĐ #")) { ErrorMessage = "Khoản phạt đã nằm trong hóa đơn."; return; }
        if (new GanPhatHoaDonDialog(SelectedItem) { Owner = System.Windows.Application.Current.MainWindow }.ShowDialog() == true)
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
        if (SelectedItem.TrangThai == "Đã xử lý")
        { ErrorMessage = "Vi phạm đã xử lý không thể xóa vì đã có lịch sử hoặc thông báo cho khách."; return; }
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
    public string HoaDon { get; set; } = "";
    public string MaViPham     { get; set; } = "";
    public string TenPhong     { get; set; } = "";
    public string TenKhach     { get; set; } = "";
    public string NoiDung      { get; set; } = "";
    public string NgayViPham   { get; set; } = "";
    public decimal TienPhatSo  { get; set; }
    public string MucPhat      { get; set; } = "";
    public string HinhThucXuLy { get; set; } = "";
    public string TrangThai    { get; set; } = "";
}
