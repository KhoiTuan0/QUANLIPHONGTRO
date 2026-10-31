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
        if (!string.IsNullOrWhiteSpace(PickMaHopDong.SelectedId))
        {
            if (!PickMaHopDong.TryGetSelectedId(out int mhd))
            { ShowError("Vui lòng chọn hợp đồng từ danh sách đã tải."); return; }
            maHopDong = mhd;
        }

        try
        {
            using var saveGuard = new DialogSaveGuard(this);
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
            saveGuard.Complete();
            DialogResult = true;
        }
        catch (Exception ex) { ShowError(ex.Message); }
    }

    private void ShowError(string msg)
    {
        TxtError.Text = msg;
        TxtError.Visibility = Visibility.Visible;
    }
}
