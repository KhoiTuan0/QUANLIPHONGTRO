using System.Windows;
using QLPT.WPF.Services;
using QLPT.WPF.ViewModels;

namespace QLPT.WPF.Views.Dialogs;

public partial class GanPhatHoaDonDialog : Window
{
    private readonly ViPhamItem _item;
    public GanPhatHoaDonDialog(ViPhamItem item)
    {
        InitializeComponent(); _item = item;
        TxtInfo.Text = $"Vi phạm #{item.MaViPham} · {item.TenPhong} · {item.TenKhach}\n{item.NoiDung}\nKhoản phạt: {item.MucPhat}. Chỉ chọn hóa đơn đúng khách/phòng, chưa thu tiền. Thao tác tăng công nợ, chưa ghi nhận thu tiền.";
        Loaded += async (_, _) => {
            try {
                var list = await ApiClient.GetAsync<List<HoaDonPhatItem>>($"/api/ViPham/{item.MaViPham}/hoa-don-phu-hop");
                GridInvoices.ItemsSource = list;
                if (list?.Count == 0) TxtError.Text = "Chưa có hóa đơn phù hợp. Lập hóa đơn đúng kỳ, chưa thu tiền; kiểm tra vi phạm đã xử lý và chưa được tính trước đó.";
            } catch (Exception ex) { TxtError.Text = ex.Message; }
        };
    }
    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (GridInvoices.SelectedItem is not HoaDonPhatItem invoice) { TxtError.Text = "Chọn hóa đơn cần bổ sung."; return; }
        if (!ConfirmDialog.Ask("Bổ sung tiền phạt", $"Thêm { _item.MucPhat } vào hóa đơn #{invoice.MaHoaDon}. Tổng sau bổ sung: {invoice.SauDisplay}. Khoản phạt chỉ tính một lần.")) return;
        using var guard = new DialogSaveGuard(this);
        try {
            await ApiClient.PostAsync($"/api/ViPham/{_item.MaViPham}/hoa-don", new { maHoaDon = invoice.MaHoaDon });
            guard.Complete(); DialogResult = true;
        } catch (Exception ex) { TxtError.Text = ex.Message; }
    }
}
public class HoaDonPhatItem
{
    public int MaHoaDon { get; set; }
    public int Thang { get; set; }
    public int Nam { get; set; }
    public decimal? TongTien { get; set; }
    public decimal? TongSauKhiThem { get; set; }
    public string Ky => $"{Thang}/{Nam}";
    public string TongDisplay => VnMoney.FormatD(TongTien ?? 0);
    public string SauDisplay => VnMoney.FormatD(TongSauKhiThem ?? 0);
}
