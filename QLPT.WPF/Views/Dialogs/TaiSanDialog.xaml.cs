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
        if (!string.IsNullOrWhiteSpace(TxtGiaTri.Text) && (!VnMoney.TryParse(TxtGiaTri.Text, out giaTri) || giaTri < 0))
        {
            TxtError.Text = "Giá trị tham khảo phải là số không âm.";
            return;
        }

        BtnSave.IsEnabled = false;
        try
        {
            using var saveGuard = new DialogSaveGuard(this);
            var body = new
            {
                tenTaiSan = ten,
                giaTriThamKhao = giaTri
            };
            await ApiClient.PostAsync("/api/TaiSan", body);
            saveGuard.Complete();
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
