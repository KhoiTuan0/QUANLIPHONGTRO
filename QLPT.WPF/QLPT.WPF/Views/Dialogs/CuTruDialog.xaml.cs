using System.Windows;
using QLPT.WPF.Services;

namespace QLPT.WPF.Views.Dialogs;

public partial class CuTruDialog : Window
{
    /// <param name="maHopDong">Điền sẵn từ dòng đang chọn (có thể rỗng).</param>
    public CuTruDialog(string? maHopDong)
    {
        InitializeComponent();
        TxtMaHopDong.Text = maHopDong ?? "";
        DpNgayVao.SelectedDate = DateTime.Today;
        Loaded += (_, _) => (string.IsNullOrEmpty(TxtMaHopDong.Text) ? TxtMaHopDong : TxtMaKhach).Focus();
    }

    private async void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        TxtError.Visibility = Visibility.Collapsed;

        if (!KiemTra.Ma(TxtMaHopDong.Text, out int maHopDong))
        { ShowError("Mã hợp đồng phải là số nguyên dương."); return; }

        if (!KiemTra.Ma(TxtMaKhach.Text, out int maKhach))
        { ShowError("Mã khách phải là số nguyên dương."); return; }

        if (DpNgayVao.SelectedDate == null)
        { ShowError("Vui lòng chọn ngày vào."); return; }

        try
        {
            IsEnabled = false;
            await ApiClient.PostAsync("/api/DanhMucMoRong/cu-tru", new
            {
                maHopDong,
                maKhach,
                ngayVao = DpNgayVao.SelectedDate.Value.ToString("yyyy-MM-dd"),
                trangThaiCuTru = "Đang ở",
                trangThaiKhaiBao = "Chưa khai báo"
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
