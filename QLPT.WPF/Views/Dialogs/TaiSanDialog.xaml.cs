using QLPT.WPF.Services;
using System.Windows;

namespace QLPT.WPF.Views.Dialogs;

public partial class TaiSanDialog : Window
{
    public TaiSanDialog()
    {
        InitializeComponent();
    }

    private async void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        TxtError.Text = "";
        var ten = TxtTen.Text.Trim();
        if (string.IsNullOrEmpty(ten))
        {
            TxtError.Text = "Tên tài sản không được để trống.";
            return;
        }

        decimal giaTri = 0;
        if (!string.IsNullOrWhiteSpace(TxtGiaTri.Text) && !VnMoney.TryParse(TxtGiaTri.Text, out giaTri))
        {
            TxtError.Text = "Giá trị tham khảo không hợp lệ.";
            return;
        }

        BtnSave.IsEnabled = false;
        try
        {
            var body = new
            {
                tenTaiSan = ten,
                giaTriThamKhao = giaTri
            };
            await ApiClient.PostAsync("/api/TaiSan", body);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            TxtError.Text = ex.Message;
        }
        finally
        {
            BtnSave.IsEnabled = true;
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
