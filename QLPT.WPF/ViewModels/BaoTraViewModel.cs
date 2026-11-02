using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QLPT.WPF.Services;
using System.Collections.ObjectModel;

namespace QLPT.WPF.ViewModels;

public partial class BaoTraViewModel : BaseViewModel
{
    [ObservableProperty] private ObservableCollection<BaoTraItem> _items = new();
    [ObservableProperty] private BaoTraItem? _selectedItem;

    public BaoTraViewModel() => _ = LoadAsync();

    [RelayCommand]
    private async Task LoadAsync() => await RunSafeAsync(async () =>
    {
        var arr = await ApiClient.GetRawAsync("/api/BaoTraPhong");
        var list = new ObservableCollection<BaoTraItem>();
        foreach (var el in arr.EnumerateArray())
            list.Add(new BaoTraItem
            {
                MaBaoTra = ApiClient.ElStr(el, "maBaoTra"),
                MaHopDong = ApiClient.ElStr(el, "maHopDong"),
                MaKhach = ApiClient.ElStr(el, "maKhach"),
                NgayBao = ApiClient.ElStr(el, "ngayBao"),
                NgayDuKienTra = ApiClient.ElStr(el, "ngayDuKienTra"),
                LyDo = ApiClient.ElStr(el, "lyDo"),
                TrangThai = ApiClient.ElStr(el, "trangThai")
            });
        Items = list;
    });

    [RelayCommand]
    private async Task ThemAsync()
    {
        var dlg = new Views.Dialogs.BaoTraDialog();
        if (dlg.ShowDialog() == true) await LoadAsync();
    }
}

public class BaoTraItem
{
    public string MaBaoTra { get; set; } = "";
    public string MaHopDong { get; set; } = "";
    public string MaKhach { get; set; } = "";
    public string NgayBao { get; set; } = "";
    public string NgayDuKienTra { get; set; } = "";
    public string LyDo { get; set; } = "";
    public string TrangThai { get; set; } = "";
}
