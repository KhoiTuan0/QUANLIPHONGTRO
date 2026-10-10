using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QLPT.WPF.Services;
using System.Collections.ObjectModel;

namespace QLPT.WPF.ViewModels;

public partial class TaiKhoanViewModel : BaseViewModel
{
    [ObservableProperty] private ObservableCollection<TaiKhoanItem> _items = new();
    [ObservableProperty] private TaiKhoanItem? _selectedItem;

    public TaiKhoanViewModel() => _ = LoadAsync();

    [RelayCommand]
    private async Task LoadAsync() => await RunSafeAsync(async () =>
    {
        var arr = await ApiClient.GetRawAsync("/api/TaiKhoan");
        var list = new ObservableCollection<TaiKhoanItem>();
        foreach (var el in arr.EnumerateArray())
            list.Add(new TaiKhoanItem
            {
                MaTaiKhoan = ApiClient.ElStr(el, "maTaiKhoan"),
                TenDangNhap = ApiClient.ElStr(el, "tenDangNhap"),
                VaiTro = ApiClient.ElStr(el, "vaiTro"),
                MaKhach = ApiClient.ElStr(el, "maKhach")
            });
        Items = list;
    });

    [RelayCommand]
    private async Task AddAsync()
    {
        var dlg = new Views.Dialogs.TaiKhoanDialog(null);
        if (dlg.ShowDialog() == true) await LoadAsync();
    }

    [RelayCommand]
    private async Task EditAsync()
    {
        if (SelectedItem == null) { ErrorMessage = "Vui lòng chọn một dòng."; return; }
        var dlg = new Views.Dialogs.TaiKhoanDialog(SelectedItem);
        if (dlg.ShowDialog() == true) await LoadAsync();
    }
}

public class TaiKhoanItem
{
    public string MaTaiKhoan { get; set; } = "";
    public string TenDangNhap { get; set; } = "";
    public string VaiTro { get; set; } = "";
    public string MaKhach { get; set; } = "";
}
