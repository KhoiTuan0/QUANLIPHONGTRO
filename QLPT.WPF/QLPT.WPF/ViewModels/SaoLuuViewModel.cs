using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QLPT.WPF.Services;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace QLPT.WPF.ViewModels;

public partial class SaoLuuViewModel : BaseViewModel
{
    [ObservableProperty] private ObservableCollection<SaoLuuItem> _items = new();
    [ObservableProperty] private SaoLuuItem? _selectedItem;
    [ObservableProperty] private string _infoMessage = "";

    public SaoLuuViewModel() => _ = LoadAsync();

    [RelayCommand]
    private async Task LoadAsync() => await RunSafeAsync(async () =>
    {
        InfoMessage = "";
        var (el, ok, friendly) = await TryGetBackupListAsync();
        if (!ok) { InfoMessage = friendly; Items = new(); return; }

        var list = new List<SaoLuuItem>();
        if (el.ValueKind == JsonValueKind.Array)
            foreach (var item in el.EnumerateArray())
                list.Add(new SaoLuuItem
                {
                    Id        = ApiClient.ElStr(item, "id", "maSaoLuu"),
                    FileName  = ApiClient.ElStr(item, "fileName", "tenFile"),
                    NgayTao   = ApiClient.ElStr(item, "ngayTao", "createdAt"),
                    KichThuoc = ApiClient.ElStr(item, "kichThuoc", "size"),
                });
        Items = new ObservableCollection<SaoLuuItem>(list);
    });

    [RelayCommand]
    private async Task TaoSaoLuuAsync()
    {
        await RunSafeAsync(async () =>
        {
            InfoMessage = "";
            bool done = false;
            foreach (var url in new[] { "/api/SaoLuu/tao", "/api/Backup/create" })
            {
                try { await ApiClient.PostAsync(url, new { }); done = true; break; }
                catch (Exception ex) when (IsNotFound(ex)) { /* try next */ }
            }
            if (!done) InfoMessage = "Chức năng sao lưu chưa được hỗ trợ bởi API.";
        });
        if (string.IsNullOrEmpty(ErrorMessage) && string.IsNullOrEmpty(InfoMessage))
            await LoadAsync();
    }

    [RelayCommand]
    private async Task XoaAsync()
    {
        if (SelectedItem == null) { ErrorMessage = "Chọn bản sao lưu cần xóa."; return; }
        await RunSafeAsync(async () =>
        {
            InfoMessage = "";
            bool done = false;
            foreach (var url in new[]
                { $"/api/SaoLuu/{SelectedItem.Id}", $"/api/Backup/{SelectedItem.Id}" })
            {
                try { await ApiClient.DeleteAsync(url); done = true; break; }
                catch (Exception ex) when (IsNotFound(ex)) { /* try next */ }
            }
            if (!done) InfoMessage = "Chức năng sao lưu chưa được hỗ trợ bởi API.";
        });
        if (string.IsNullOrEmpty(ErrorMessage))
            await LoadAsync();
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static async Task<(JsonElement el, bool ok, string friendly)>
        TryGetBackupListAsync()
    {
        foreach (var url in new[] { "/api/SaoLuu", "/api/Backup" })
        {
            try
            {
                var el = await ApiClient.GetRawAsync(url);
                return (el, true, "");
            }
            catch (Exception ex) when (IsNotFound(ex)) { /* try next */ }
            catch { /* other error — fall through to friendly */ }
        }
        return (default, false, "Chức năng sao lưu chưa được hỗ trợ bởi API.");
    }

    private static bool IsNotFound(Exception ex)
        => ex.Message.Contains("404") || ex.Message.Contains("Not Found",
               StringComparison.OrdinalIgnoreCase);
}

public class SaoLuuItem
{
    public string Id        { get; set; } = "";
    public string FileName  { get; set; } = "";
    public string NgayTao   { get; set; } = "";
    public string KichThuoc { get; set; } = "";
}
