using System.Windows;
using System.Windows.Controls;
using QLPT.WPF.Services;

namespace QLPT.WPF.Views.Dialogs;

public partial class ThuChiDialog : Window
{
    public ThuChiDialog()
    {
        InitializeComponent();
        DpNgayGhiNhan.SelectedDate = DateTime.Today;
    }

    private async void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        TxtError.Visibility = Visibility.Collapsed;

        if (CbLoai.SelectedItem is not ComboBoxItem loaiItem)
        { ShowError("Vui lòng chọn loại."); return; }

        if (!VnMoney.TryParse(TxtSoTien.Text, out decimal soTien) || soTien <= 0)
        { ShowError("Số tiền không hợp lệ."); return; }

        if (string.IsNullOrWhiteSpace(TxtMoTa.Text))
        { ShowError("Vui lòng nhập mô tả."); return; }

        if (DpNgayGhiNhan.SelectedDate == null)
        { ShowError("Vui lòng chọn ngày ghi nhận."); return; }
        if (!KiemTra.KhongTuongLai(DpNgayGhiNhan.SelectedDate))
        { ShowError("Ngày ghi nhận không được sau hôm nay."); return; }

        // Optional maHopDong
        int? maHopDong = null;
        if (!string.IsNullOrWhiteSpace(TxtMaHopDong.Text))
        {
            if (!KiemTra.Ma(TxtMaHopDong.Text, out int mhd))
            { ShowError("Mã hợp đồng phải là số nguyên dương."); return; }
            maHopDong = mhd;
        }

        try
        {
            IsEnabled = false;
            // Khớp entity PhieuThuChi. Bảng không có cột mã hợp đồng nên ghi vào phần ghi chú.
            await ApiClient.PostAsync("/api/ThuChi", new
            {
                loaiPhieu = loaiItem.Content?.ToString() ?? "Thu",
                soTien,
                noiDung   = TxtMoTa.Text.Trim(),
                ngayLap   = DpNgayGhiNhan.SelectedDate.Value.ToString("yyyy-MM-dd"),
                ghiChu    = maHopDong.HasValue ? $"Hợp đồng #{maHopDong.Value}" : null,
                trangThai = "Hoàn tất"
            });
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
