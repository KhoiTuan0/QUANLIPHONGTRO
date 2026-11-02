using System.Windows;
using QLPT.WPF.Services;

namespace QLPT.WPF.Views.Dialogs;

public partial class GiaHanDialog : Window
{
    private readonly string _maHopDong;

    public GiaHanDialog(string maHopDong)
    {
        InitializeComponent();
        _maHopDong = maHopDong;
        TxtMaHD.Text = $"Mã hợp đồng: {maHopDong}";
        DpNgayKetThucMoi.SelectedDate = DateTime.Today.AddYears(1);
    }

    private async void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        TxtError.Visibility = Visibility.Collapsed;

        if (DpNgayKetThucMoi.SelectedDate == null)
        { ShowError("Vui lòng chọn ngày kết thúc mới."); return; }

        if (DpNgayKetThucMoi.SelectedDate.Value <= DateTime.Today)
        { ShowError("Ngày kết thúc mới phải sau ngày hôm nay."); return; }

        decimal? giaThueMoi = null;
        if (!string.IsNullOrWhiteSpace(TxtGiaThueMoi.Text))
        {
            if (!VnMoney.TryParse(TxtGiaThueMoi.Text, out decimal gia) || gia <= 0)
            { ShowError("Giá thuê mới không hợp lệ."); return; }
            giaThueMoi = gia;
        }

        try
        {
            IsEnabled = false;
            object body = giaThueMoi.HasValue
                ? new { ngayKetThucMoi = DpNgayKetThucMoi.SelectedDate.Value.ToString("yyyy-MM-dd"), giaThueMoi = giaThueMoi.Value }
                : (object)new { ngayKetThucMoi = DpNgayKetThucMoi.SelectedDate.Value.ToString("yyyy-MM-dd") };
            await ApiClient.PostAsync($"/api/HopDong/{_maHopDong}/gia-han", body);
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
