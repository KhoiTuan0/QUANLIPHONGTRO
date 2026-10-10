using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QLPT.WPF.Services;
using QLPT.WPF.Views.Dialogs;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace QLPT.WPF.ViewModels;

public partial class SuaChuaViewModel : BaseViewModel
{
    [ObservableProperty] private ObservableCollection<SuaChuaItem> _items = new();
    [ObservableProperty] private SuaChuaItem? _selectedItem;

    public SuaChuaViewModel() => _ = LoadAsync();

    [RelayCommand]
    private async Task LoadAsync() => await RunSafeAsync(async () =>
    {
        var el = await ApiClient.GetRawAsync("/api/SuaChua");
        var list = new List<SuaChuaItem>();
        if (el.ValueKind == JsonValueKind.Array)
            foreach (var item in el.EnumerateArray())
                list.Add(new SuaChuaItem
                {
                    MaSuaChua     = ApiClient.ElStr(item, "maYeuCau"),
                    MaHopDong     = ApiClient.ElStr(item, "maHopDong"),
                    MoTa          = ApiClient.ElStr(item, "noiDung"),
                    NgayYeuCau    = ApiClient.ElStr(item, "ngayBao"),
                    NgayHoanThanh = ApiClient.ElStr(item, "ngayHoanThanh"),
                    ChiPhi        = VnMoney.FormatD(ApiClient.ElStr(item, "chiPhi")),
                    TrangThai     = ApiClient.ElStr(item, "trangThai"),
                });
        Items = new ObservableCollection<SuaChuaItem>(list);
    });

    [RelayCommand]
    private async Task ThemAsync()
    {
        var dlg = new SuaChuaDialog
            { Owner = System.Windows.Application.Current.MainWindow };
        if (dlg.ShowDialog() == true)
            await LoadAsync();
    }

    [RelayCommand]
    private async Task CapNhatAsync()
    {
        if (SelectedItem == null) { ErrorMessage = "Chọn yêu cầu cần cập nhật."; return; }
        var dlg = new SuaChuaCapNhatDialog(SelectedItem)
            { Owner = System.Windows.Application.Current.MainWindow };
        if (dlg.ShowDialog() == true)
            await LoadAsync();
    }

    [RelayCommand]
    private async Task XoaAsync()
    {
        if (SelectedItem == null) { ErrorMessage = "Chọn yêu cầu cần xóa."; return; }
        await RunSafeAsync(async () =>
            await ApiClient.DeleteAsync($"/api/SuaChua/{SelectedItem.MaSuaChua}"));
        if (string.IsNullOrEmpty(ErrorMessage))
            await LoadAsync();
    }
}

public class SuaChuaItem
{
    public string MaSuaChua     { get; set; } = "";
    public string MaHopDong     { get; set; } = "";
    public string MoTa          { get; set; } = "";
    public string NgayYeuCau    { get; set; } = "";
    public string NgayHoanThanh { get; set; } = "";
    public string ChiPhi        { get; set; } = "";
    public string TrangThai     { get; set; } = "";
}
