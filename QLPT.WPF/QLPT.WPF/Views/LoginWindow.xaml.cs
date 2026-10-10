using System.Windows;
using System.Windows.Media;
using QLPT.WPF.Services;
using QLPT.WPF.ViewModels;

namespace QLPT.WPF.Views;

public partial class LoginWindow : Window
{
    private readonly LoginViewModel _vm = new();
    private bool _loggingIn;

    public LoginWindow()
    {
        InitializeComponent();
        TxtApiUrl.Text = AppConfig.ApiBaseUrl;
        _vm.LoginSuccess += () =>
        {
            DialogResult = true;
            Close();
        };
        PbPassword.KeyDown += async (s, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Enter) await DoLogin();
        };
    }

    private async void BtnLogin_Click(object sender, RoutedEventArgs e) => await DoLogin();

    private async Task DoLogin()
    {
        if (_loggingIn) return;
        _loggingIn = true;
        BtnLogin.IsEnabled = false;
        LoadingBar.Visibility = Visibility.Visible;
        TxtStatus.Text = "";

        _vm.ApiUrl = TxtApiUrl.Text;
        _vm.Username = TxtUsername.Text;
        _vm.Password = PbPassword.Password;

        await _vm.LoginCommand.ExecuteAsync(null);

        if (!string.IsNullOrEmpty(_vm.StatusMessage))
        {
            TxtStatus.Text = _vm.StatusMessage;
            TxtStatus.Foreground = _vm.IsStatusError ? Brushes.Crimson : Brushes.Gray;
        }

        _loggingIn = false;
        BtnLogin.IsEnabled = true;
        LoadingBar.Visibility = Visibility.Collapsed;
    }

    private void BtnRegister_Click(object sender, RoutedEventArgs e)
    {
        try { ApiClient.SetBaseUrl(TxtApiUrl.Text.Trim()); }
        catch (Exception ex) { TxtStatus.Text = ex.Message; TxtStatus.Foreground = Brushes.Crimson; return; }
        var win = new RegisterWindow();
        win.Owner = this;
        win.ShowDialog();
        // Điền sẵn tên vừa đăng ký, chỉ còn gõ mật khẩu → tránh gõ sai tên.
        if (!string.IsNullOrEmpty(win.TenDaDangKy))
        {
            TxtUsername.Text = win.TenDaDangKy;
            PbPassword.Clear();
            PbPassword.Focus();
            TxtStatus.Text = "Đăng ký thành công. Nhập mật khẩu để đăng nhập.";
            TxtStatus.Foreground = Brushes.Gray;
        }
    }
}
