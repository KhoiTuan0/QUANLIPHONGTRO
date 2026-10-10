using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QLPT.WPF.Services;
using QLPT.WPF.Views.Dialogs;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace QLPT.WPF.ViewModels;

/// <summary>
/// Admin: gửi thông báo, nhắc nhở tự động, xem phản hồi.
/// Khách thuê: đọc thông báo, đánh dấu đã đọc, phản hồi chủ trọ.
/// </summary>
public partial class ThongBaoViewModel : BaseViewModel
{
    [ObservableProperty] private ObservableCollection<ThongBaoItem> _items = new();
    [ObservableProperty] private ThongBaoItem? _selectedItem;

    public bool IsAdmin    => Session.IsAdmin;
    public bool IsNotAdmin => !Session.IsAdmin;

    public ThongBaoViewModel() => _ = LoadAsync();

    [RelayCommand]
    private async Task LoadAsync() => await RunSafeAsync(async () =>
    {
        string url = Session.IsAdmin
            ? "/api/ThongBao"
            : $"/api/ThongBao/customer/{Session.MaTaiKhoan}";
        var el = await ApiClient.GetRawAsync(url);
        var list = new List<ThongBaoItem>();
        if (el.ValueKind == JsonValueKind.Array)
            foreach (var item in el.EnumerateArray())
            {
                string ngay = ApiClient.ElStr(item, "ngayGui");
                // Admin thấy mức độ tiếp nhận: đã đọc bao nhiêu / gửi bao nhiêu, có mấy phản hồi.
                string trangThai = Session.IsAdmin
                    ? $"Đã đọc {ApiClient.ElStr(item, "soDaDoc")}/{ApiClient.ElStr(item, "soNguoiNhan")}" +
                      (int.TryParse(ApiClient.ElStr(item, "soPhanHoi"), out int ph) && ph > 0 ? $" · {ph} phản hồi" : "")
                    : ApiClient.ElStr(item, "trangThai");
                list.Add(new ThongBaoItem
                {
                    MaThongBao = ApiClient.ElStr(item, "maThongBao"),
                    TieuDe     = ApiClient.ElStr(item, "tieuDe"),
                    NoiDung    = ApiClient.ElStr(item, "noiDung"),
                    Loai       = ApiClient.ElStr(item, "loai"),
                    NgayGui    = DateTime.TryParse(ngay, out var d) ? d.ToString("dd/MM/yyyy HH:mm") : ngay,
                    TrangThai  = trangThai,
                });
            }
        Items = new ObservableCollection<ThongBaoItem>(list);
    });

    [RelayCommand]
    private async Task GuiAsync()
    {
        var dlg = new ThongBaoDialog
            { Owner = System.Windows.Application.Current.MainWindow };
        if (dlg.ShowDialog() == true)
            await LoadAsync();
    }

    // Bước "Nhắc nhở" của quy trình: gửi nhắc nợ hóa đơn + hợp đồng sắp hết hạn.
    [RelayCommand]
    private async Task NhacNhoAsync()
    {
        if (!ConfirmDialog.Ask("Gửi nhắc nhở tự động",
                "Gửi thông báo nhắc tới mọi khách đang có hóa đơn còn nợ hoặc hợp đồng hết hạn trong 30 ngày tới. " +
                "Khách đã được nhắc hôm nay sẽ được bỏ qua.",
                confirmText: "Gửi nhắc nhở")) return;

        string? ketQua = null;
        await RunSafeAsync(async () =>
        {
            var r = await ApiClient.PostAsync("/api/ThongBao/nhac-nho", new { });
            ketQua = ApiClient.ElStr(r, "message");
        });
        if (ketQua != null)
        {
            ConfirmDialog.Info("Nhắc nhở", ketQua);
            await LoadAsync();
        }
    }

    [RelayCommand]
    private void XemPhanHoi()
    {
        if (SelectedItem == null) { ErrorMessage = "Chọn thông báo cần xem phản hồi."; return; }
        new DanhSachPhanHoiDialog(SelectedItem)
            { Owner = System.Windows.Application.Current.MainWindow }.ShowDialog();
    }

    [RelayCommand]
    private async Task DocAsync()
    {
        if (SelectedItem == null) { ErrorMessage = "Chọn thông báo cần đánh dấu đã đọc."; return; }
        await RunSafeAsync(async () =>
            await ApiClient.PutAsync(
                $"/api/ThongBao/{SelectedItem.MaThongBao}/doc/{Session.MaTaiKhoan}", new { }));
        if (string.IsNullOrEmpty(ErrorMessage))
            await LoadAsync();
    }

    [RelayCommand]
    private async Task PhanHoiAsync()
    {
        if (SelectedItem == null) { ErrorMessage = "Chọn thông báo cần phản hồi."; return; }
        var dlg = new PhanHoiDialog(SelectedItem)
            { Owner = System.Windows.Application.Current.MainWindow };
        if (dlg.ShowDialog() == true)
        {
            ConfirmDialog.Info("Đã gửi phản hồi", "Chủ trọ sẽ nhận được phản hồi của bạn.");
            await LoadAsync();
        }
    }
}

public class ThongBaoItem
{
    public string MaThongBao { get; set; } = "";
    public string TieuDe     { get; set; } = "";
    public string NoiDung    { get; set; } = "";
    public string Loai       { get; set; } = "";
    public string NgayGui    { get; set; } = "";
    public string TrangThai  { get; set; } = "";
}
