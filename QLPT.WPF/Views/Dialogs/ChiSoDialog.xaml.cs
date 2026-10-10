using System.Windows;
using System.Windows.Controls;
using QLPT.WPF.Services;

namespace QLPT.WPF.Views.Dialogs;

public partial class ChiSoDialog : Window
{
    public ChiSoDialog()
    {
        InitializeComponent();
        TxtThangNam.Text = DateTime.Today.ToString("yyyy-MM");
    }

    private async void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        TxtError.Visibility = Visibility.Collapsed;

        if (!KiemTra.Ma(TxtMaPhong.Text, out int maPhong))
        { ShowError("Mã phòng phải là số nguyên dương."); return; }

        if (CbLoai.SelectedItem is not ComboBoxItem loaiItem)
        { ShowError("Vui lòng chọn loại chỉ số."); return; }

        if (!KiemTra.Ky(TxtThangNam.Text, out int nam, out int thang))
        { ShowError("Tháng năm không đúng định dạng yyyy-MM (VD: 2026-10, tháng 01–12)."); return; }
        if (new DateTime(nam, thang, 1) > DateTime.Today)
        { ShowError("Không ghi chỉ số cho kỳ chưa tới."); return; }

        if (!KiemTra.SoKhongAm(TxtChiSoCu.Text, out decimal cu))
        { ShowError("Chỉ số cũ phải là số không âm."); return; }

        if (!KiemTra.SoKhongAm(TxtChiSoMoi.Text, out decimal moi))
        { ShowError("Chỉ số mới phải là số không âm."); return; }

        if (moi < cu)
        { ShowError("Chỉ số mới phải lớn hơn hoặc bằng chỉ số cũ."); return; }

        if (moi > 9_999_999)
        { ShowError("Chỉ số quá lớn (tối đa 9.999.999)."); return; }

        try
        {
            IsEnabled = false;
            // API bind vào entity ChiSoDienNuoc: cần loaiChiSo / thang / nam tách rời,
            // gửi "loai" hay "thangNam" thì bị bỏ qua và insert lỗi vì LoaiChiSo null.
            await ApiClient.PostAsync("/api/ChiSoDienNuoc", new
            {
                maPhong,
                loaiChiSo = loaiItem.Content?.ToString() ?? "Điện",
                nam,
                thang,
                chiSoCu  = cu,
                chiSoMoi = moi,
                ngayGhi  = DateTime.Today.ToString("yyyy-MM-dd")
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
