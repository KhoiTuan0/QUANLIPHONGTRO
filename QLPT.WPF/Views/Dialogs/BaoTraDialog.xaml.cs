using System.Windows;
using QLPT.WPF.Services;

namespace QLPT.WPF.Views.Dialogs;

public partial class BaoTraDialog : Window
{
    public BaoTraDialog()
    {
        InitializeComponent();
    }

    private async void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        TxtError.Visibility = Visibility.Collapsed;

        if (!KiemTra.Ma(TxtMaHopDong.Text, out int maHopDong))
        { ShowError("Mã hợp đồng phải là số nguyên dương."); return; }

        if (!KiemTra.Ma(TxtMaKhach.Text, out int maKhach))
        { ShowError("Mã khách phải là số nguyên dương."); return; }

        if (string.IsNullOrWhiteSpace(TxtLyDo.Text))
        { ShowError("Vui lòng nhập lý do."); return; }

        try
        {
            IsEnabled = false;
            await ApiClient.PostAsync("/api/BaoTraPhong", new
            {
                maHopDong,
                maKhach,
                lyDo = TxtLyDo.Text.Trim()
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
