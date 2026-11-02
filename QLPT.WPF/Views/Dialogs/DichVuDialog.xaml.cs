using QLPT.WPF.Services;
using System.Windows;
using System.Windows.Controls;

namespace QLPT.WPF.Views.Dialogs;

public partial class DichVuDialog : Window
{
    public DichVuDialog()
    {
        InitializeComponent();
    }

    private async void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        TxtError.Text = "";
        var ten = TxtTen.Text.Trim();
        if (string.IsNullOrEmpty(ten))
        {
            TxtError.Text = "Tên dịch vụ không được để trống.";
            return;
        }

        decimal donGia = 0;
        if (!string.IsNullOrWhiteSpace(TxtDonGia.Text) && !VnMoney.TryParse(TxtDonGia.Text, out donGia))
        {
            TxtError.Text = "Đơn giá không hợp lệ.";
            return;
        }

        var trangThai = (CboTrangThai.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Đang áp dụng";

        BtnSave.IsEnabled = false;
        try
        {
            var body = new
            {
                tenDichVu = ten,
                donViTinh = TxtDonViTinh.Text.Trim(),
                donGia = donGia,
                trangThai = trangThai
            };
            await ApiClient.PostAsync("/api/DichVu", body);
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
