using System.Windows;
using QLPT.WPF.Services;
using QLPT.WPF.ViewModels;

namespace QLPT.WPF.Views.Dialogs;

/// <summary>Khách thuê trả lời một thông báo đã nhận.</summary>
public partial class PhanHoiDialog : Window
{
    private readonly string _maThongBao;

    public PhanHoiDialog(ThongBaoItem item)
    {
        InitializeComponent();
        _maThongBao = item.MaThongBao;
        TxtTieuDe.Text = item.TieuDe;
        TxtNoiDungGoc.Text = item.NoiDung;
        Loaded += (_, _) => TxtNoiDung.Focus();
    }

    private async void BtnSend_Click(object sender, RoutedEventArgs e)
    {
        TxtError.Text = "";
        if (string.IsNullOrWhiteSpace(TxtNoiDung.Text)) { TxtError.Text = "Nhập nội dung phản hồi."; return; }

        BtnSend.IsEnabled = false;
        try
        {
            await ApiClient.PostAsync($"/api/ThongBao/{_maThongBao}/phan-hoi", new
            {
                maTaiKhoan = Session.MaTaiKhoan,
                noiDung = TxtNoiDung.Text.Trim()
            });
            DialogResult = true;
        }
        catch (Exception ex)
        {
            TxtError.Text = ex.Message;
            BtnSend.IsEnabled = true;
        }
    }
}
