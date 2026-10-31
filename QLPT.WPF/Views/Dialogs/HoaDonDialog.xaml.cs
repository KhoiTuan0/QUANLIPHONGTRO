using System.Windows;
using QLPT.WPF.Services;

namespace QLPT.WPF.Views.Dialogs;

public partial class HoaDonDialog : Window
{
    public HoaDonDialog()
    {
        InitializeComponent();
        TxtThangNam.Text = DateTime.Today.ToString("yyyy-MM");
    }

    private void Cach_Changed(object sender, RoutedEventArgs e)
    {
        if (TxtTongTien == null) return; // sự kiện bắn trong lúc InitializeComponent
        TxtTongTien.IsEnabled = RbNhapTay.IsChecked == true;
    }

    private async void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        TxtError.Visibility = Visibility.Collapsed;

        if (!PickMaHopDong.TryGetSelectedId(out int maHopDong))
        { ShowError("Vui lòng chọn hợp đồng từ danh sách đã tải."); return; }

        string thangNam = TxtThangNam.Text.Trim();
        if (!KiemTra.Ky(thangNam, out int nam, out int thang))
        { ShowError("Kỳ hóa đơn không đúng định dạng yyyy-MM (VD: 2026-10, tháng 01–12)."); return; }
        if (new DateTime(nam, thang, 1) > DateTime.Today)
        { ShowError("Không lập hóa đơn cho kỳ chưa tới."); return; }

        bool nhapTay = RbNhapTay.IsChecked == true;
        decimal tongTien = 0;
        if (nhapTay && (!VnMoney.TryParse(TxtTongTien.Text, out tongTien) || tongTien <= 0))
        { ShowError("Tổng tiền không hợp lệ."); return; }

        try
        {
            using var saveGuard = new DialogSaveGuard(this);
            // Hai đường tách bạch. Bản cũ gọi /generate rồi tự rơi xuống lưu tay khi có lỗi,
            // nên lỗi "chưa ghi chỉ số" bị nuốt và hóa đơn thiếu tiền điện nước vẫn được tạo.
            if (nhapTay)
                await ApiClient.PostAsync("/api/HoaDon", new { maHopDong, thangNam, tongTien });
            else
                await ApiClient.PostAsync("/api/HoaDon/generate", new { maHopDong, thangNam });
            saveGuard.Complete();
            DialogResult = true;
        }
        catch (Exception ex) { ShowError(ex.Message); }
    }

    private void ShowError(string msg)
    {
        TxtError.Text = msg;
        TxtError.Visibility = Visibility.Visible;
    }
}
