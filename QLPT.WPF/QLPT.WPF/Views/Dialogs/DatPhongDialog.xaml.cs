using System.Windows;
using QLPT.WPF.Services;

namespace QLPT.WPF.Views.Dialogs;

public partial class DatPhongDialog : Window
{
    public DatPhongDialog()
    {
        InitializeComponent();
    }

    private async void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        TxtError.Visibility = Visibility.Collapsed;

        if (!KiemTra.Ma(TxtMaPhong.Text, out int maPhong))
        { ShowError("Mã phòng phải là số nguyên dương."); return; }

        if (!KiemTra.Ma(TxtMaKhach.Text, out int maKhach))
        { ShowError("Mã khách phải là số nguyên dương."); return; }

        if (!VnMoney.TryParse(TxtTienCoc.Text, out decimal tienCoc) || tienCoc < 0)
        { ShowError("Tiền cọc không hợp lệ."); return; }

        try
        {
            IsEnabled = false;
            await ApiClient.PostAsync("/api/DatPhong", new
            {
                maPhong,
                maKhach,
                ngayDat = DateTime.Today.ToString("yyyy-MM-dd"),
                tienCoc
            });
            DialogResult = true;
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
        finally
        {
            IsEnabled = true;
        }
    }

    private void ShowError(string msg)
    {
        TxtError.Text = msg;
        TxtError.Visibility = Visibility.Visible;
    }
}
