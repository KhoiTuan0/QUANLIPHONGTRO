using QLPT.WPF.Services;
using QLPT.WPF.ViewModels;
using System.Windows;

namespace QLPT.WPF.Views.Dialogs;

public partial class GanTaiSanDialog : Window
{
    private readonly TaiSanItem _taiSan;
    private readonly string _maPhong;

    public GanTaiSanDialog(TaiSanItem taiSan, string maPhong)
    {
        InitializeComponent();
        _taiSan = taiSan;
        _maPhong = maPhong;
        TxtInfo.Text = $"Tài sản: {taiSan.TenTaiSan}  |  Phòng: {maPhong}";
    }

    private async void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        TxtError.Text = "";
        if (!int.TryParse(TxtSoLuong.Text.Trim(), out var soLuong) || soLuong <= 0)
        {
            TxtError.Text = "Số lượng phải là số nguyên dương.";
            return;
        }

        if (!int.TryParse(_maPhong?.Trim(), out var maPhong) || maPhong <= 0)
        {
            TxtError.Text = "Mã phòng không hợp lệ.";
            return;
        }
        if (!int.TryParse(_taiSan.MaTaiSan?.Trim(), out var maTaiSan) || maTaiSan <= 0)
        {
            TxtError.Text = "Mã tài sản không hợp lệ.";
            return;
        }

        BtnSave.IsEnabled = false;
        try
        {
            // API nhận model TaiSanPhong tại /gan-phong; mã phải là số nguyên, không phải chuỗi.
            var body = new
            {
                maTaiSan  = maTaiSan,
                maPhong   = maPhong,
                soLuong   = soLuong,
                tinhTrang = TxtTinhTrang.Text.Trim()
            };
            await ApiClient.PostAsync("/api/TaiSan/gan-phong", body);
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
