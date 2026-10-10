using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QLPT.WPF.Services;

namespace QLPT.WPF.ViewModels;

public partial class LoginViewModel : BaseViewModel
{
    [ObservableProperty] private string _username = "";
    [ObservableProperty] private string _apiUrl = AppConfig.ApiBaseUrl;
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private bool _isStatusError;

    public event Action? LoginSuccess;

    public string Password { get; set; } = "";

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            StatusMessage = "Vui lòng nhập đầy đủ tên đăng nhập và mật khẩu.";
            IsStatusError = true;
            return;
        }

        await RunSafeAsync(async () =>
        {
            StatusMessage = "Đang đăng nhập...";
            IsStatusError = false;

            ApiClient.SetBaseUrl(ApiUrl.Trim());
            var el = await ApiClient.PostAsync("/api/Auth/login", new
            {
                username = Username.Trim(),
                password = Password
            });

            if (!int.TryParse(ApiClient.ElStr(el, "maTaiKhoan"), out int accountId) || accountId <= 0)
                throw new Exception("API đăng nhập không trả mã tài khoản hợp lệ.");

            string vt = ApiClient.ElStr(el, "vaiTro", "VaiTro");
            if (!string.Equals(vt, "Admin", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(vt, "Customer", StringComparison.OrdinalIgnoreCase))
                throw new Exception("API trả vai trò không hợp lệ.");

            string mk = ApiClient.ElStr(el, "maKhach", "MaKhach");
            int? customerId = int.TryParse(mk, out int mkv) && mkv > 0 ? mkv : null;

            if (vt.Equals("Customer", StringComparison.OrdinalIgnoreCase) && !customerId.HasValue)
                throw new Exception("Tài khoản Customer chưa được liên kết với khách thuê.");

            Session.MaTaiKhoan = accountId;
            Session.VaiTro = vt.Trim();
            Session.MaKhach = customerId;
            Session.TenDangNhap = Username.Trim();

            LoginSuccess?.Invoke();
        });

        if (!string.IsNullOrEmpty(ErrorMessage))
        {
            StatusMessage = ErrorMessage;
            IsStatusError = true;
            ApiClient.ResetAuthentication();
        }
    }

    [RelayCommand]
    private void Register()
    {
        try { ApiClient.SetBaseUrl(ApiUrl.Trim()); }
        catch (Exception ex) { StatusMessage = ex.Message; IsStatusError = true; return; }
        var win = new Views.RegisterWindow();
        win.ShowDialog();
    }
}
