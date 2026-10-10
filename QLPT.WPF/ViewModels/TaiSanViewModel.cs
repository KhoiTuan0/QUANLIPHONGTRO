using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QLPT.WPF.Services;
using System.Collections.ObjectModel;

namespace QLPT.WPF.ViewModels;

public partial class TaiSanViewModel : BaseViewModel
{
    [ObservableProperty] private ObservableCollection<TaiSanItem> _allItems = new();
    [ObservableProperty] private ObservableCollection<TaiSanPhongItem> _phongItems = new();
    [ObservableProperty] private TaiSanItem? _selectedItem;
    [ObservableProperty] private TaiSanPhongItem? _selectedPhongItem;
    [ObservableProperty] private string _maPhongInput = "";

    public TaiSanViewModel() => _ = LoadAllAsync();

    [RelayCommand]
    private async Task LoadAllAsync() => await RunSafeAsync(async () =>
    {
        var arr = await ApiClient.GetRawAsync("/api/TaiSan");
        var list = new ObservableCollection<TaiSanItem>();
        foreach (var el in arr.EnumerateArray())
        {
            decimal.TryParse(ApiClient.ElStr(el, "giaTriThamKhao"), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var gia);
            list.Add(new TaiSanItem
            {
                MaTaiSan = ApiClient.ElStr(el, "maTaiSan"),
                TenTaiSan = ApiClient.ElStr(el, "tenTaiSan"),
                GiaTriThamKhao = gia,
                GiaTriDisplay = VnMoney.FormatD(gia)
            });
        }
        AllItems = list;
    });

    [RelayCommand]
    private async Task LoadPhongAsync() => await RunSafeAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(MaPhongInput))
        {
            ErrorMessage = "Vui lòng nhập mã phòng.";
            return;
        }
        var arr = await ApiClient.GetRawAsync($"/api/TaiSan/phong/{MaPhongInput.Trim()}");
        var list = new ObservableCollection<TaiSanPhongItem>();
        foreach (var el in arr.EnumerateArray())
        {
            int.TryParse(ApiClient.ElStr(el, "soLuong"), out var sl);
            list.Add(new TaiSanPhongItem
            {
                MaTaiSanPhong = ApiClient.ElStr(el, "maTaiSanPhong"),
                TenTaiSan = ApiClient.ElStr(el, "tenTaiSan"),
                SoLuong = sl,
                TinhTrang = ApiClient.ElStr(el, "tinhTrang")
            });
        }
        PhongItems = list;
    });

    [RelayCommand]
    private async Task AddTaiSanAsync()
    {
        var dlg = new Views.Dialogs.TaiSanDialog();
        if (dlg.ShowDialog() == true) await LoadAllAsync();
    }

    [RelayCommand]
    private async Task GanVaoPhongAsync()
    {
        if (SelectedItem == null) { ErrorMessage = "Vui lòng chọn tài sản mẫu."; return; }
        if (string.IsNullOrWhiteSpace(MaPhongInput)) { ErrorMessage = "Vui lòng nhập mã phòng."; return; }
        var dlg = new Views.Dialogs.GanTaiSanDialog(SelectedItem, MaPhongInput.Trim());
        if (dlg.ShowDialog() == true) await LoadPhongAsync();
    }
}

public class TaiSanItem
{
    public string MaTaiSan { get; set; } = "";
    public string TenTaiSan { get; set; } = "";
    public decimal GiaTriThamKhao { get; set; }
    public string GiaTriDisplay { get; set; } = "";
}

// TaiSanPhongItem is defined in BanGiaoViewModel.cs
