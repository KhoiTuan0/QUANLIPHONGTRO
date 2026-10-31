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

        if (!PickMaHopDong.TryGetSelectedId(out int maHopDong))
        {
            TxtError.Text = "Vui lòng chọn hợp đồng từ danh sách đã tải."; return;
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
        if (!VnMoney.TryParse(TxtMucPhat.Text, out decimal mucPhat) || mucPhat < 0)
        {
            TxtError.Text = "Mức phạt phải là số không âm."; return;
        }

        BtnSave.IsEnabled = false;
        try
        {
            using var saveGuard = new DialogSaveGuard(this);
            await ApiClient.PostAsync("/api/ViPham", new
            {
                maHopDong,
                loaiViPham = TxtLoaiViPham.Text.Trim(),
                moTa       = TxtMoTa.Text.Trim(),
                ngayViPham = DpNgayViPham.SelectedDate.Value.ToString("yyyy-MM-dd"),
                mucPhat,
            });
            saveGuard.Complete();
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
