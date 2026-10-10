using QLPT.WPF.Services;
using System.Windows;

namespace QLPT.WPF.Views.Dialogs;

public partial class SuaChuaDialog : Window
{
    public SuaChuaDialog()
    {
        InitializeComponent();
        DpNgayYeuCau.SelectedDate = DateTime.Today;
    }

    private async void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        TxtError.Text = "";

        if (!KiemTra.Ma(TxtMaHopDong.Text, out int maHopDong))
        {
            TxtError.Text = "Mã hợp đồng phải là số nguyên dương."; return;
        }
        if (string.IsNullOrWhiteSpace(TxtMoTa.Text))
        {
            TxtError.Text = "Mô tả yêu cầu không được để trống."; return;
        }
        if (DpNgayYeuCau.SelectedDate == null)
        {
            TxtError.Text = "Chọn ngày yêu cầu."; return;
        }
        if (!KiemTra.KhongTuongLai(DpNgayYeuCau.SelectedDate))
        {
            TxtError.Text = "Ngày yêu cầu không được sau hôm nay."; return;
        }

        BtnSave.IsEnabled = false;
        try
        {
            await ApiClient.PostAsync("/api/SuaChua", new
            {
                maHopDong,
                moTa       = TxtMoTa.Text.Trim(),
                ngayYeuCau = DpNgayYeuCau.SelectedDate.Value.ToString("yyyy-MM-dd"),
            });
            DialogResult = true;
        }
        catch (Exception ex)
        {
            TxtError.Text = ex.Message;
            BtnSave.IsEnabled = true;
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
        => DialogResult = false;
}
