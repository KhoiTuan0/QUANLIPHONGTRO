using QLPT.WPF.Services;
using QLPT.WPF.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace QLPT.WPF.Views.Dialogs;

public partial class DichVuDialog : Window
{
    private readonly DichVuItem? _item;

    public DichVuDialog(DichVuItem? item = null)
    {
        InitializeComponent();
        _item = item;
        if (item == null) return;
        TxtTitle.Text = "Sửa dịch vụ";
        TxtTen.Text = item.TenDichVu;
        TxtDonViTinh.Text = item.DonViTinh;
        TxtDonGia.Text = item.DonGia.ToString(System.Globalization.CultureInfo.InvariantCulture);
        CboTrangThai.SelectedIndex = item.TrangThai == "Ngừng" ? 1 : 0;
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
            using var saveGuard = new DialogSaveGuard(this);
            var body = new
            {
                tenDichVu = ten,
                donViTinh = TxtDonViTinh.Text.Trim(),
                donGia = donGia,
                trangThai = trangThai
            };
            if (_item == null)
                await ApiClient.PostAsync("/api/DichVu", body);
            else
                await ApiClient.PutAsync($"/api/DichVu/{_item.MaDichVu}", body);
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
