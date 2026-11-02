using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QLPT.WPF.Services;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace QLPT.WPF.ViewModels;

public partial class CoSoViewModel : BaseViewModel
{
    [ObservableProperty] private ObservableCollection<CoSoItem> _items = new();
    [ObservableProperty] private CoSoItem? _selectedItem;

    public CoSoViewModel() => _ = LoadAsync();

    [RelayCommand]
    private async Task LoadAsync() => await RunSafeAsync(async () =>
    {
        var arr = await ApiClient.GetRawAsync("/api/CoSo");
        var list = new ObservableCollection<CoSoItem>();
        foreach (var el in arr.EnumerateArray())
            list.Add(new CoSoItem
            {
                MaCoSo = ApiClient.ElStr(el, "maCoSo"),
                TenCoSo = ApiClient.ElStr(el, "tenCoSo"),
                DiaChi = ApiClient.ElStr(el, "diaChi"),
                SoDienThoai = ApiClient.ElStr(el, "soDienThoai"),
                MoTa = ApiClient.ElStr(el, "moTa")
            });
        Items = list;
    });

    [RelayCommand]
    private async Task AddAsync()
    {
        var dlg = new Views.Dialogs.CoSoDialog(null);
        if (dlg.ShowDialog() == true) await LoadAsync();
    }

    [RelayCommand]
    private async Task EditAsync()
    {
        if (SelectedItem == null) { ErrorMessage = "Vui lòng chọn một dòng."; return; }
        var dlg = new Views.Dialogs.CoSoDialog(SelectedItem);
        if (dlg.ShowDialog() == true) await LoadAsync();
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (SelectedItem == null) { ErrorMessage = "Vui lòng chọn một dòng."; return; }
        if (!Views.Dialogs.ConfirmDialog.Delete(
                "Cơ sở này sẽ bị xóa khỏi hệ thống.",
                $"#{SelectedItem.MaCoSo} — {SelectedItem.TenCoSo}",
                "Xóa cơ sở")) return;
        await RunSafeAsync(async () =>
        {
            await ApiClient.DeleteAsync($"/api/CoSo/{SelectedItem.MaCoSo}");
            await LoadAsync();
        });
    }
}

public class CoSoItem
{
    public string MaCoSo { get; set; } = "";
    public string TenCoSo { get; set; } = "";
    public string DiaChi { get; set; } = "";
    public string SoDienThoai { get; set; } = "";
    public string MoTa { get; set; } = "";
}
