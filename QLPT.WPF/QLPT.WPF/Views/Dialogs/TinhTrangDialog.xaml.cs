using System.Windows;
using System.Windows.Controls;
using QLPT.WPF.Services;

namespace QLPT.WPF.Views.Dialogs;

public partial class TinhTrangDialog : Window
{
    private readonly string _maTaiSanPhong;

    public TinhTrangDialog(string maTaiSanPhong, string tenTaiSan, string tinhTrangHienTai)
    {
        InitializeComponent();
        _maTaiSanPhong = maTaiSanPhong;
        TxtTenTaiSan.Text = $"Tài sản: {tenTaiSan}  |  Hiện tại: {tinhTrangHienTai}";

        // Pre-select current tình trạng
        foreach (ComboBoxItem item in CbTinhTrang.Items)
        {
            if (item.Content?.ToString() == tinhTrangHienTai)
            {
                CbTinhTrang.SelectedItem = item;
                break;
            }
        }
    }

    private async void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        TxtError.Visibility = Visibility.Collapsed;

        if (CbTinhTrang.SelectedItem is not ComboBoxItem selected)
        { ShowError("Vui lòng chọn tình trạng."); return; }

        string tinhTrang = selected.Content?.ToString() ?? "";

        try
        {
            IsEnabled = false;
            await ApiClient.PutAsync($"/api/TaiSan/phong/{_maTaiSanPhong}", new { tinhTrang });
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
