using QLPT.WPF.Services;
using QLPT.WPF.ViewModels;
using System.Windows;

namespace QLPT.WPF.Views.Dialogs;

public partial class CoSoDialog : Window
{
    private readonly CoSoItem? _item;

    public CoSoDialog(CoSoItem? item)
    {
        InitializeComponent();
        _item = item;
        if (item != null)
        {
            TxtTitle.Text = "Sửa cơ sở";
            TxtTen.Text = item.TenCoSo;
            TxtDiaChi.Text = item.DiaChi;
            TxtSdt.Text = item.SoDienThoai;
            TxtMoTa.Text = item.MoTa;
        }
    }

    private async void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        TxtError.Text = "";
        var ten = TxtTen.Text.Trim();
        if (string.IsNullOrEmpty(ten))
        {
            TxtError.Text = "Tên cơ sở không được để trống.";
            return;
        }
        var sdt = TxtSdt.Text.Trim();
        if (sdt.Length > 0 && !KiemTra.Sdt(sdt))
        {
            TxtError.Text = "Số điện thoại phải gồm 10 chữ số và bắt đầu bằng 0.";
            return;
        }

        BtnSave.IsEnabled = false;
        try
        {
            var body = new
            {
                tenCoSo = ten,
                diaChi = TxtDiaChi.Text.Trim(),
                soDienThoai = TxtSdt.Text.Trim(),
                moTa = TxtMoTa.Text.Trim()
            };

            if (_item == null)
                await ApiClient.PostAsync("/api/CoSo", body);
            else
                await ApiClient.PutAsync($"/api/CoSo/{_item.MaCoSo}", body);

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
