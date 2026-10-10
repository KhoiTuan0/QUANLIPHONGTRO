using System.Text.Json;
using System.Windows;
using QLPT.WPF.Services;
using QLPT.WPF.ViewModels;

namespace QLPT.WPF.Views.Dialogs;

/// <summary>Admin xem các phản hồi khách gửi cho một thông báo.</summary>
public partial class DanhSachPhanHoiDialog : Window
{
    private sealed record PhanHoi(string NguoiGui, string NoiDung, string NgayGui);

    public DanhSachPhanHoiDialog(ThongBaoItem item)
    {
        InitializeComponent();
        TxtTieuDe.Text = $"Thông báo #{item.MaThongBao}: {item.TieuDe}";
        Loaded += async (_, _) => await LoadAsync(item.MaThongBao);
    }

    private async Task LoadAsync(string maThongBao)
    {
        try
        {
            var el = await ApiClient.GetRawAsync($"/api/ThongBao/{maThongBao}/phan-hoi");
            var list = new List<PhanHoi>();
            if (el.ValueKind == JsonValueKind.Array)
                foreach (var x in el.EnumerateArray())
                {
                    string ngay = ApiClient.ElStr(x, "ngayGui");
                    list.Add(new PhanHoi(
                        ApiClient.ElStr(x, "nguoiGui"),
                        ApiClient.ElStr(x, "noiDung"),
                        DateTime.TryParse(ngay, out var d) ? d.ToString("dd/MM/yyyy HH:mm") : ngay));
                }
            LstPhanHoi.ItemsSource = list;
            TxtEmpty.Visibility = list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            TxtEmpty.Text = ex.Message;
            TxtEmpty.Visibility = Visibility.Visible;
        }
    }
}
