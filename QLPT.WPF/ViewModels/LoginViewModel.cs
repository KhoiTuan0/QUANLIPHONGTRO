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
            if (vt is not ("Admin" or "Manager" or "BackupOperator"))
                throw new Exception("Bản desktop dành cho chủ trọ, quản lý và người phụ trách sao lưu. Khách thuê sử dụng ứng dụng mobile.");

            string mk = ApiClient.ElStr(el, "maKhach", "MaKhach");
            int? customerId = int.TryParse(mk, out int mkv) && mkv > 0 ? mkv : null;

            if (!ApiClient.HasSessionCookie)
                throw new Exception("Chưa nhận được phiên đăng nhập từ máy chủ. Vui lòng đăng nhập lại.");

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

}
