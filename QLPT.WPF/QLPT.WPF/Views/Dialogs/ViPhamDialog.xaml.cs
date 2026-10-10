using QLPT.WPF.Services;
using System.Windows;

namespace QLPT.WPF.Views.Dialogs;

public partial class ViPhamDialog : Window
{
    public ViPhamDialog()
    {
        InitializeComponent();
        DpNgayViPham.SelectedDate = DateTime.Today;
    }

    private async void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        TxtError.Text = "";

        if (!KiemTra.Ma(TxtMaHopDong.Text, out int maHopDong))
        {
            TxtError.Text = "Mã hợp đồng phải là số nguyên dương."; return;
        }
        if (string.IsNullOrWhiteSpace(TxtLoaiViPham.Text))
        {
            TxtError.Text = "Nhập loại vi phạm."; return;
        }
        if (DpNgayViPham.SelectedDate == null)
        {
            TxtError.Text = "Chọn ngày vi phạm."; return;
        }
        if (!KiemTra.KhongTuongLai(DpNgayViPham.SelectedDate))
        {
            TxtError.Text = "Ngày vi phạm không được sau hôm nay."; return;
        }
        if (!VnMoney.TryParse(TxtMucPhat.Text, out decimal mucPhat))
        {
            TxtError.Text = "Mức phạt phải là số hợp lệ."; return;
        }

        BtnSave.IsEnabled = false;
        try
        {
            await ApiClient.PostAsync("/api/ViPham", new
            {
                maHopDong,
                loaiViPham = TxtLoaiViPham.Text.Trim(),
                moTa       = TxtMoTa.Text.Trim(),
                ngayViPham = DpNgayViPham.SelectedDate.Value.ToString("yyyy-MM-dd"),
                mucPhat,
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
