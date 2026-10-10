using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QLPT.WPF.Services;
using System.Collections.ObjectModel;

namespace QLPT.WPF.ViewModels;

public partial class KhachThueViewModel : BaseViewModel
{
    [ObservableProperty] private ObservableCollection<KhachThueItem> _items = new();
    [ObservableProperty] private KhachThueItem? _selectedItem;
    [ObservableProperty] private string _searchText = "";

    private List<KhachThueItem> _allItems = new();

    partial void OnSearchTextChanged(string value) => FilterItems();

    public KhachThueViewModel() => _ = LoadAsync();

    [RelayCommand]
    private async Task LoadAsync() => await RunSafeAsync(async () =>
    {
        var arr = await ApiClient.GetRawAsync("/api/KhachThue");
        var list = new List<KhachThueItem>();
        foreach (var el in arr.EnumerateArray())
            list.Add(new KhachThueItem
            {
                MaKhach = ApiClient.ElStr(el, "maKhach"),
                HoTen = ApiClient.ElStr(el, "hoTen"),
                Cccd = ApiClient.ElStr(el, "cccd"),
                SoDienThoai = ApiClient.ElStr(el, "soDienThoai"),
                GioiTinh = ApiClient.ElStr(el, "gioiTinh"),
                Email = ApiClient.ElStr(el, "email"),
                DiaChi = ApiClient.ElStr(el, "diaChi")
            });
        _allItems = list;
        FilterItems();
    });

    private void FilterItems()
    {
        var q = SearchText.Trim().ToLower();
        var filtered = string.IsNullOrEmpty(q)
            ? _allItems
            : _allItems.Where(x =>
                x.HoTen.Contains(q, StringComparison.CurrentCultureIgnoreCase) ||
                x.Cccd.Contains(q, StringComparison.CurrentCultureIgnoreCase) ||
                x.SoDienThoai.Contains(q, StringComparison.CurrentCultureIgnoreCase) ||
                x.Email.Contains(q, StringComparison.CurrentCultureIgnoreCase)).ToList();
        Items = new ObservableCollection<KhachThueItem>(filtered);
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        var dlg = new Views.Dialogs.KhachThueDialog(null);
        if (dlg.ShowDialog() == true) await LoadAsync();
    }

    [RelayCommand]
    private async Task EditAsync()
    {
        if (SelectedItem == null) { ErrorMessage = "Vui lòng chọn một dòng."; return; }
        var dlg = new Views.Dialogs.KhachThueDialog(SelectedItem);
        if (dlg.ShowDialog() == true) await LoadAsync();
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (SelectedItem == null) { ErrorMessage = "Vui lòng chọn một dòng."; return; }
        // Xóa mềm: hồ sơ vẫn nằm trong database, chỉ ẩn khỏi danh sách đang hoạt động.
        if (!Views.Dialogs.ConfirmDialog.Show(
                "Xác nhận xóa mềm",
                "Khách thuê sẽ bị ẩn khỏi danh sách nhưng hồ sơ vẫn được giữ trong hệ thống.",
                $"#{SelectedItem.MaKhach} — {SelectedItem.HoTen}",
                "Xóa mềm",
                MaterialDesignThemes.Wpf.PackIconKind.AccountOffOutline,
                "#EF4444", warn: false)) return;
        await RunSafeAsync(async () =>
        {
            await ApiClient.PutAsync($"/api/KhachThue/{SelectedItem.MaKhach}/xoa-mem", new { });
            await LoadAsync();
        });
    }
}

public class KhachThueItem
{
    public string MaKhach { get; set; } = "";
    public string HoTen { get; set; } = "";
    public string Cccd { get; set; } = "";
    public string SoDienThoai { get; set; } = "";
    public string GioiTinh { get; set; } = "";
    public string Email { get; set; } = "";
    public string DiaChi { get; set; } = "";
}
