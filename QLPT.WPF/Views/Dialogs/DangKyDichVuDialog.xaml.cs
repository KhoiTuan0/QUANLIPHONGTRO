using QLPT.WPF.Services;
using System.Windows;

namespace QLPT.WPF.Views.Dialogs;

public partial class DangKyDichVuDialog : Window
{
    public DangKyDichVuDialog(string maHopDong)
    {
        InitializeComponent();
        TxtMaHopDong.Text = maHopDong;
        DpNgayBatDau.SelectedDate = DateTime.Today;
    }

    private async void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        TxtError.Text = "";

        if (string.IsNullOrWhiteSpace(TxtMaHopDong.Text) ||
            !KiemTra.Ma(TxtMaHopDong.Text, out int maHopDong))
        {
            TxtError.Text = "Mã hợp đồng phải là số nguyên dương."; return;
        }
        if (string.IsNullOrWhiteSpace(TxtMaDichVu.Text) ||
            !KiemTra.Ma(TxtMaDichVu.Text, out int maDichVu))
        {
            TxtError.Text = "Mã dịch vụ phải là số nguyên dương."; return;
        }
        if (DpNgayBatDau.SelectedDate == null)
        {
            TxtError.Text = "Chọn ngày bắt đầu."; return;
        }

        BtnSave.IsEnabled = false;
        try
        {
            await ApiClient.PostAsync("/api/DangKyDichVu", new
            {
                maHopDong,
                maDichVu,
                ngayBatDau = DpNgayBatDau.SelectedDate.Value.ToString("yyyy-MM-dd"),
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
