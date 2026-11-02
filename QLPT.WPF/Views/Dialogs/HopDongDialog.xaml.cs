using System.Windows;
using QLPT.WPF.Services;

namespace QLPT.WPF.Views.Dialogs;

public partial class HopDongDialog : Window
{
    public HopDongDialog()
    {
        InitializeComponent();
        DpNgayBatDau.SelectedDate = DateTime.Today;
        DpNgayKetThuc.SelectedDate = DateTime.Today.AddYears(1);
    }

    private async void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        TxtError.Visibility = Visibility.Collapsed;

        if (!KiemTra.Ma(TxtMaPhong.Text, out int maPhong))
        { ShowError("Mã phòng phải là số nguyên dương."); return; }

        if (!KiemTra.Ma(TxtMaKhachDaiDien.Text, out int maKhach))
        { ShowError("Mã khách đại diện phải là số nguyên dương."); return; }

        if (DpNgayBatDau.SelectedDate == null)
        { ShowError("Vui lòng chọn ngày bắt đầu."); return; }

        if (DpNgayKetThuc.SelectedDate == null)
        { ShowError("Vui lòng chọn ngày kết thúc."); return; }

        if (DpNgayKetThuc.SelectedDate <= DpNgayBatDau.SelectedDate)
        { ShowError("Ngày kết thúc phải sau ngày bắt đầu."); return; }

        if (!VnMoney.TryParse(TxtGiaThueThang.Text, out decimal giaThue) || giaThue <= 0)
        { ShowError("Giá thuê/tháng không hợp lệ."); return; }

        if (!VnMoney.TryParse(TxtTienCoc.Text, out decimal tienCoc) || tienCoc < 0)
        { ShowError("Tiền cọc không hợp lệ."); return; }

        try
        {
            IsEnabled = false;
            await ApiClient.PostAsync("/api/HopDong", new
            {
                maPhong,
                maKhachDaiDien = maKhach,
                ngayBatDau = DpNgayBatDau.SelectedDate.Value.ToString("yyyy-MM-dd"),
                ngayKetThuc = DpNgayKetThuc.SelectedDate.Value.ToString("yyyy-MM-dd"),
                giaThueThang = giaThue,
                tienCoc,
                trangThai = "Đang hiệu lực"
            });
            DialogResult = true;
        }
        catch (Exception ex) { ShowError(ex.Message); }
        finally { IsEnabled = true; }
    }

    private void ShowError(string msg)
    {
        TxtError.Text = msg;
        TxtError.Visibility = Visibility.Visible;
    }
}
