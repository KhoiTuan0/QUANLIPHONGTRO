using System.Windows;
using QLPT.WPF.Services;

namespace QLPT.WPF.Views.Dialogs;

public partial class ThuTienDialog : Window
{
    private readonly string _maHoaDon;
    private readonly decimal _conLai;

    public ThuTienDialog(string maHoaDon, decimal conLai)
    {
        InitializeComponent();
        _maHoaDon = maHoaDon;
        _conLai = conLai;
        TxtInfo.Text = $"Mã hóa đơn: {maHoaDon}  |  Còn lại: {VnMoney.FormatD(conLai)}";
        TxtSoTienThu.Text = VnMoney.Format(conLai);
    }

    private async void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        TxtError.Visibility = Visibility.Collapsed;

        if (!VnMoney.TryParse(TxtSoTienThu.Text, out decimal soTien) || soTien <= 0)
        { ShowError("Số tiền thu không hợp lệ."); return; }

        if (soTien > _conLai)
        { ShowError($"Số tiền thu không được vượt quá số còn lại ({VnMoney.FormatD(_conLai)})."); return; }

        try
        {
            IsEnabled = false;
            await ApiClient.PutAsync($"/api/HoaDon/{_maHoaDon}/thu-tien", new { soTienThu = soTien });
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
