using QLPT.WPF.Services;
using System.Windows;
using System.Windows.Controls;

namespace QLPT.WPF.Views.Dialogs;

public partial class ThongBaoDialog : Window
{
    public ThongBaoDialog()
    {
        InitializeComponent();
    }

    private async void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        TxtError.Text = "";

        if (string.IsNullOrWhiteSpace(TxtTieuDe.Text))
        {
            TxtError.Text = "Nhập tiêu đề."; return;
        }
        if (string.IsNullOrWhiteSpace(TxtNoiDung.Text))
        {
            TxtError.Text = "Nhập nội dung."; return;
        }

        var loai = (CbLoai.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Hệ thống";

        // Trước đây gõ sai mã (VD "abc") thì bị bỏ qua và thông báo bị gửi cho TẤT CẢ.
        // Giờ: để trống = gửi tất cả khách (có hỏi lại); có nhập thì mọi mã phải hợp lệ.
        var nguoiNhan = new List<int>();
        foreach (var phan in TxtMaTaiKhoanNhan.Text.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (!KiemTra.Ma(phan, out int ma))
            {
                TxtError.Text = $"Mã tài khoản \"{phan}\" không hợp lệ. Nhập các số nguyên dương, cách nhau bằng dấu phẩy.";
                return;
            }
            if (!nguoiNhan.Contains(ma)) nguoiNhan.Add(ma);
        }
        bool guiTatCa = nguoiNhan.Count == 0;
        if (guiTatCa && !ConfirmDialog.Ask("Gửi cho tất cả?",
                "Chưa nhập mã người nhận nên thông báo sẽ được gửi tới mọi tài khoản khách thuê.",
                TxtTieuDe.Text.Trim(), "Gửi tất cả", warn: true))
            return;

        BtnSave.IsEnabled = false;
        try
        {
            // Khớp GuiThongBaoDto: trước đây gửi "loai"/"maTaiKhoanNhan" nên thông báo
            // được lưu nhưng không tạo dòng người nhận nào — không ai thấy.
            await ApiClient.PostAsync("/api/ThongBao", new
            {
                tieuDe = TxtTieuDe.Text.Trim(),
                noiDung = TxtNoiDung.Text.Trim(),
                loaiThongBao = loai,
                guiChoTatCa = guiTatCa,
                danhSachMaTaiKhoan = guiTatCa ? null : nguoiNhan
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
