using System.Text.RegularExpressions;
using System.Windows;
using QLPT.WPF.Services;

namespace QLPT.WPF.Views;

public partial class RegisterWindow : Window
{
    public RegisterWindow() => InitializeComponent();

    /// <summary>Tên vừa đăng ký thành công, để màn hình đăng nhập điền sẵn.</summary>
    public string? TenDaDangKy { get; private set; }

    private async void BtnRegister_Click(object sender, RoutedEventArgs e)
    {
        TxtMsg.Text = "";
        if (string.IsNullOrWhiteSpace(TxtUser.Text) || string.IsNullOrWhiteSpace(PbPass.Password) || string.IsNullOrWhiteSpace(TxtHoTen.Text))
        { TxtMsg.Text = "Vui lòng nhập Tên đăng nhập, Mật khẩu và Họ tên."; return; }
        if (!KiemTra.TenDangNhap(TxtUser.Text))
        { TxtMsg.Text = "Tên đăng nhập 3–50 ký tự, chỉ gồm chữ không dấu, số, dấu chấm hoặc gạch dưới."; return; }
        if (!KiemTra.HoTen(TxtHoTen.Text))
        { TxtMsg.Text = "Họ tên chỉ gồm chữ cái và khoảng trắng (2–100 ký tự)."; return; }
        if (PbPass.Password.Length < 6) { TxtMsg.Text = "Mật khẩu tối thiểu 6 ký tự."; return; }
        if (PbPass2.Password != PbPass.Password) { TxtMsg.Text = "Mật khẩu nhập lại không khớp."; return; }
        string cccd = TxtCccd.Text.Trim(), phone = TxtPhone.Text.Trim(), email = TxtEmail.Text.Trim();
        if (cccd.Length > 0 && !Regex.IsMatch(cccd, @"^[0-9]{12}$")) { TxtMsg.Text = "CCCD phải gồm 12 chữ số."; return; }
        if (phone.Length > 0 && !Regex.IsMatch(phone, @"^0[0-9]{9}$")) { TxtMsg.Text = "Số điện thoại phải gồm 10 số, bắt đầu 0."; return; }
        if (email.Length > 0 && !Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$")) { TxtMsg.Text = "Email không đúng định dạng."; return; }

        try
        {
            await ApiClient.PostAsync("/api/Auth/register", new
            {
                tenDangNhap = TxtUser.Text.Trim(),
                matKhau = PbPass.Password,
                hoTen = TxtHoTen.Text.Trim(),
                soDienThoai = phone,
                cccd = cccd,
                email = email,
                diaChi = ""
            });
            MessageBox.Show("Đăng ký thành công! Hãy đăng nhập.", "Thành công",
                MessageBoxButton.OK, MessageBoxImage.Information);
            TenDaDangKy = TxtUser.Text.Trim();
            Close();
        }
        catch (Exception ex) { TxtMsg.Text = ex.Message; }
    }

    private void BtnBack_Click(object sender, RoutedEventArgs e) => Close();
}
