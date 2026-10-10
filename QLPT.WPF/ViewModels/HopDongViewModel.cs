using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QLPT.WPF.Services;
using System.Collections.ObjectModel;

namespace QLPT.WPF.ViewModels;

public partial class HopDongViewModel : BaseViewModel
{
    [ObservableProperty] private ObservableCollection<HopDongItem> _items = new();
    [ObservableProperty] private HopDongItem? _selectedItem;
    [ObservableProperty] private ObservableCollection<HopDongItem> _sapHetHanItems = new();
    [ObservableProperty] private bool _showSapHetHan;

    public HopDongViewModel() => _ = LoadAsync();

    [RelayCommand]
    private async Task LoadAsync() => await RunSafeAsync(async () =>
    {
        var arr = await ApiClient.GetRawAsync("/api/HopDong");
        var list = new ObservableCollection<HopDongItem>();
        foreach (var el in arr.EnumerateArray())
            list.Add(MapItem(el));
        Items = list;
    });

    private static HopDongItem MapItem(System.Text.Json.JsonElement el)
    {
        decimal.TryParse(ApiClient.ElStr(el, "giaThueThang"), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var gia);
        decimal.TryParse(ApiClient.ElStr(el, "tienCoc"), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var coc);
        return new HopDongItem
        {
            MaHopDong = ApiClient.ElStr(el, "maHopDong"),
            MaPhong = ApiClient.ElStr(el, "maPhong"),
            MaKhachDaiDien = ApiClient.ElStr(el, "maKhachDaiDien"),
            NgayBatDau = ApiClient.ElStr(el, "ngayBatDau"),
            NgayKetThuc = ApiClient.ElStr(el, "ngayKetThuc"),
            GiaThueThang = gia,
            GiaThueThangDisplay = VnMoney.FormatD(gia),
            TienCoc = coc,
            TienCocDisplay = VnMoney.FormatD(coc),
            TrangThai = ApiClient.ElStr(el, "trangThai")
        };
    }

    [RelayCommand]
    private async Task TaoHopDongAsync()
    {
        var dlg = new Views.Dialogs.HopDongDialog();
        if (dlg.ShowDialog() == true) await LoadAsync();
    }

    [RelayCommand]
    private async Task GiaHanAsync()
    {
        if (SelectedItem == null) { ErrorMessage = "Vui lòng chọn một hợp đồng."; return; }
        var dlg = new Views.Dialogs.GiaHanDialog(SelectedItem.MaHopDong);
        if (dlg.ShowDialog() == true) await LoadAsync();
    }

    [RelayCommand]
    private async Task ThanhLyAsync()
    {
        if (SelectedItem == null) { ErrorMessage = "Vui lòng chọn một hợp đồng."; return; }
        // Fetch outstanding debt
        decimal tongNo = 0;
        await RunSafeAsync(async () =>
        {
            var arr = await ApiClient.GetRawAsync($"/api/HoaDon/hop-dong/{SelectedItem.MaHopDong}");
            foreach (var el in arr.EnumerateArray())
            {
                decimal.TryParse(ApiClient.ElStr(el, "conLai"), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var con);
                tongNo += con;
            }
        });
        var dlg = new Views.Dialogs.ThanhLyDialog(SelectedItem.MaHopDong, SelectedItem.TienCoc, tongNo);
        if (dlg.ShowDialog() == true) await LoadAsync();
    }

    [RelayCommand]
    private async Task XemSapHetHanAsync()
    {
        ShowSapHetHan = !ShowSapHetHan;
        if (ShowSapHetHan)
        {
            await RunSafeAsync(async () =>
            {
                var arr = await ApiClient.GetRawAsync("/api/HopDong/sap-het-han?days=30");
                var list = new ObservableCollection<HopDongItem>();
                foreach (var el in arr.EnumerateArray())
                    list.Add(MapItem(el));
                SapHetHanItems = list;
            });
        }
    }
}

public class HopDongItem
{
    public string MaHopDong { get; set; } = "";
    public string MaPhong { get; set; } = "";
    public string MaKhachDaiDien { get; set; } = "";
    public string NgayBatDau { get; set; } = "";
    public string NgayKetThuc { get; set; } = "";
    public decimal GiaThueThang { get; set; }
    public string GiaThueThangDisplay { get; set; } = "";
    public decimal TienCoc { get; set; }
    public string TienCocDisplay { get; set; } = "";
    public string TrangThai { get; set; } = "";
}
