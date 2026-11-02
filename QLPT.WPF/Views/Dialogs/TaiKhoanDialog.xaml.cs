using QLPT.WPF.Services;
using QLPT.WPF.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace QLPT.WPF.Views.Dialogs;

public partial class TaiKhoanDialog : Window
{
    private readonly TaiKhoanItem? _item;
    private readonly bool _isEdit;

    public TaiKhoanDialog(TaiKhoanItem? item)
    {
        InitializeComponent();
        _item = item;
        _isEdit = item != null;

        if (_isEdit)
        {
            TxtTitle.Text = "Sửa tài khoản";
            TxtTenDangNhap.Text = item!.TenDangNhap;
            TxtTenDangNhap.IsReadOnly = true;
            // Cho admin đặt lại mật khẩu khi khách quên / gõ nhầm lúc đăng ký.
            MaterialDesignThemes.Wpf.HintAssist.SetHint(TxtMatKhau, "Mật khẩu mới (để trống = giữ nguyên)");
            TxtMaKhach.Text = item.MaKhach;

            foreach (ComboBoxItem ci in CboVaiTro.Items)
            {
                if (ci.Content?.ToString() == item.VaiTro)
                {
                    CboVaiTro.SelectedItem = ci;
                    break;
                }
            }
        }
    }

    private async void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        TxtError.Text = "";

        var tenDangNhap = TxtTenDangNhap.Text.Trim();
        var vaiTro = (CboVaiTro.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Customer";

        if (!_isEdit)
        {
            if (string.IsNullOrEmpty(tenDangNhap))
            {
                TxtError.Text = "Tên đăng nhập không được để trống.";
                return;
            }
            if (!KiemTra.TenDangNhap(tenDangNhap))
            {
                TxtError.Text = "Tên đăng nhập 3–50 ký tự, chỉ gồm chữ không dấu, số, dấu chấm hoặc gạch dưới.";
                return;
            }
            if (string.IsNullOrEmpty(TxtMatKhau.Password))
            {
                TxtError.Text = "Mật khẩu không được để trống.";
                return;
            }
            if (TxtMatKhau.Password.Length < 6)
            {
                TxtError.Text = "Mật khẩu tối thiểu 6 ký tự.";
                return;
            }
        }

        // Trước đây gõ sai mã khách thì bị bỏ qua, rồi API báo "phải gắn mã khách" khó hiểu.
        int? maKhach = null;
        if (!string.IsNullOrWhiteSpace(TxtMaKhach.Text))
        {
            if (!KiemTra.Ma(TxtMaKhach.Text, out var mk))
            {
                TxtError.Text = "Mã khách thuê phải là số nguyên dương (xem ở trang Khách thuê).";
                return;
            }
            maKhach = mk;
        }
        if (!_isEdit && vaiTro == "Customer" && maKhach == null)
        {
            TxtError.Text = "Tài khoản khách thuê phải nhập mã khách thuê.";
            return;
        }

        BtnSave.IsEnabled = false;
        try
        {
            if (_isEdit)
            {
                string? matKhauMoi = string.IsNullOrEmpty(TxtMatKhau.Password) ? null : TxtMatKhau.Password;
                if (matKhauMoi != null && matKhauMoi.Length < 6)
                {
                    TxtError.Text = "Mật khẩu mới tối thiểu 6 ký tự.";
                    return;
                }
                var body = new { vaiTro = vaiTro, matKhau = matKhauMoi };
                await ApiClient.PutAsync($"/api/TaiKhoan/{_item!.MaTaiKhoan}", body);
                if (matKhauMoi != null)
                    ConfirmDialog.Info("Đã đặt lại mật khẩu",
                        "Báo cho chủ tài khoản đăng nhập bằng mật khẩu mới.", _item.TenDangNhap, isError: false);
            }
            else
            {
                var body = new
                {
                    tenDangNhap = tenDangNhap,
                    matKhau = TxtMatKhau.Password,
                    vaiTro = vaiTro,
                    maKhach = maKhach
                };
                await ApiClient.PostAsync("/api/TaiKhoan", body);
            }
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
