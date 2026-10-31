using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QLPT.WPF.Services;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows;

namespace QLPT.WPF.ViewModels;

public partial class SaoLuuViewModel : BaseViewModel
{
    private const string FileGuidance = "Tệp .bak nằm trên máy SQL Server. Quản lý và xóa tệp tại thư mục sao lưu của máy SQL Server; danh sách này là lịch sử sao lưu.";
    private const string UnavailableGuidance = "Sao lưu chưa sẵn sàng. Kiểm tra thư mục sao lưu mặc định của SQL Server hoặc Backups:SqlServerDirectory, quyền ghi của dịch vụ SQL Server và quyền đọc lịch sử trong msdb.";
    [ObservableProperty] private ObservableCollection<SaoLuuItem> _items = new();
    [ObservableProperty] private SaoLuuItem? _selectedItem;
    [ObservableProperty] private string _infoMessage = "";

    public SaoLuuViewModel() => _ = LoadAsync();

    [RelayCommand]
    private async Task LoadAsync() => await RunSafeAsync(async () =>
    {
        InfoMessage = FileGuidance;
        JsonElement el;
        try { el = await ApiClient.GetRawAsync("/api/SaoLuu"); }
        catch (ApiResponseException ex) when (ex.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
        { InfoMessage = UnavailableGuidance; Items = new(); return; }
        if (el.ValueKind != JsonValueKind.Array) throw new Exception("Máy chủ trả danh sách sao lưu không đúng định dạng.");

        var list = new List<SaoLuuItem>();
        if (el.ValueKind == JsonValueKind.Array)
            foreach (var item in el.EnumerateArray())
                list.Add(new SaoLuuItem
                {
                    Id        = ApiClient.ElStr(item, "id", "maSaoLuu"),
                    FileName  = ApiClient.ElStr(item, "fileName", "tenFile"),
                    NgayTao   = DateTime.TryParse(ApiClient.ElStr(item, "ngayTao", "createdAt"), out var created)
                        ? created.ToString("dd/MM/yyyy HH:mm") : ApiClient.ElStr(item, "ngayTao", "createdAt"),
                    KichThuoc = ApiClient.ElStr(item, "kichThuoc", "size"),
                    DuongDan  = ApiClient.ElStr(item, "duongDan"),
                    TrangThai = ApiClient.ElStr(item, "trangThai"),
                });
        Items = new ObservableCollection<SaoLuuItem>(list);
    });

    [RelayCommand]
    private async Task TaoSaoLuuAsync()
    {
        await RunSafeAsync(async () =>
        {
            InfoMessage = "";
            JsonElement result;
            try { result = await ApiClient.PostLongAsync("/api/SaoLuu/tao", new { }); }
            catch (ApiResponseException ex) when (ex.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
            {
                InfoMessage = UnavailableGuidance;
                return;
            }
            await LoadAsync();
            InfoMessage = $"{ApiClient.ElStr(result, "message")}\n{ApiClient.ElStr(result, "duongDan")}\n{InfoMessage}";
        });
    }

    [RelayCommand]
    private async Task KhoiPhucBanSaoAsync()
    {
        if (SelectedItem == null || !KiemTra.Ma(SelectedItem.Id, out _))
        { ErrorMessage = "Chọn một bản sao lưu trong danh sách."; return; }
        string id = SelectedItem.Id;
        string name = SelectedItem.FileName;
        if (MessageBox.Show($"Khôi phục {name} thành một CSDL mới trên máy SQL Server?\n\n" +
            "CSDL đang dùng sẽ không bị ghi đè. Cần đủ dung lượng đĩa và quyền khôi phục SQL.",
            "Khôi phục bản sao", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        await RunSafeAsync(async () =>
        {
            var result = await ApiClient.PostLongAsync($"/api/SaoLuu/{id}/khoi-phuc-ban-sao", new { });
            InfoMessage = ApiClient.ElStr(result, "message") +
                " Nếu cần dùng CSDL này, kiểm tra và đổi chuỗi kết nối API theo quy trình triển khai.";
        });
    }

    [RelayCommand]
    private void SaoChepDuongDan()
    {
        if (string.IsNullOrWhiteSpace(SelectedItem?.DuongDan))
        { ErrorMessage = "Chọn bản sao lưu có đường dẫn để sao chép."; return; }
        try { Clipboard.SetText(SelectedItem.DuongDan); ErrorMessage = ""; }
        catch { ErrorMessage = "Không thể sao chép đường dẫn vào bộ nhớ tạm."; }
    }
}

public class SaoLuuItem
{
    public string Id        { get; set; } = "";
    public string FileName  { get; set; } = "";
    public string NgayTao   { get; set; } = "";
    public string KichThuoc { get; set; } = "";
    public string DuongDan { get; set; } = "";
    public string TrangThai { get; set; } = "";
}
