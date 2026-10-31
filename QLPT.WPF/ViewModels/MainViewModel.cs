using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using QLPT.WPF.Services;
using QLPT.WPF.Views.Pages;

namespace QLPT.WPF.ViewModels;

public partial class MainViewModel : BaseViewModel
{
    private readonly Action _returnToLogin;
    public MainViewModel(Action? returnToLogin = null) => _returnToLogin = returnToLogin ?? (() => ((App)System.Windows.Application.Current).ReturnToLogin());
    [ObservableProperty] private object? _currentPage;
    [ObservableProperty] private string _currentPageTitle = "Dashboard";
    [ObservableProperty] private string _currentPageGroup = "";
    [ObservableProperty] private bool _isMenuOpen = true;
    [ObservableProperty] private bool _isDarkMode = false;
    [ObservableProperty] private PackIconKind _themeIcon = PackIconKind.WeatherNight;

    public string UserInfo    => $"{Session.TenDangNhap} ({Session.VaiTro})";
    public string UserName    => Session.TenDangNhap;
    public string UserRole    => Session.IsAdmin ? "Chủ trọ" : Session.IsManager ? "Quản lý cơ sở" : Session.IsBackupOperator ? "Phụ trách sao lưu" : "Khách thuê";
    public string UserInitial => Session.TenDangNhap.Length > 0
        ? Session.TenDangNhap[0].ToString().ToUpper() : "?";
    public bool IsAdmin => Session.IsAdmin;

    // Trang khởi đầu do MainWindow chọn (để đồng bộ highlight menu + breadcrumb).

    partial void OnIsDarkModeChanged(bool value)
    {
        ThemeService.Apply(value);
        ThemeIcon = value ? PackIconKind.WeatherSunny : PackIconKind.WeatherNight;
    }

    [RelayCommand]
    private void ToggleTheme() => IsDarkMode = !IsDarkMode;

    [RelayCommand]
    private void NavigateTo(string page)
    {
        if (!Session.IsLoggedIn)
        { ErrorMessage = "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại."; return; }
        if (Session.IsManager && page is not ("Phòng trọ" or "Khách thuê" or "Đặt phòng" or "Hợp đồng" or "Cư trú" or "Chỉ số điện nước" or "Tài sản" or "Sửa chữa" or "Thông báo"))
        { ErrorMessage = "Quản lý chỉ xem và xử lý nghiệp vụ vận hành tại cơ sở được phân công."; return; }
        if (Session.IsBackupOperator && page != "Sao lưu")
        { ErrorMessage = "Người phụ trách sao lưu chỉ được mở mục Sao lưu."; return; }
        if (!Session.IsAdmin && !Session.IsManager && !Session.IsBackupOperator && page is not ("Thông tin của tôi" or "Thông báo"))
        { ErrorMessage = "Tài khoản khách thuê chỉ xem được thông tin và thông báo của mình."; return; }
        ErrorMessage = "";
        CurrentPageTitle = page;
        CurrentPage = page switch
        {
            "Dashboard"         => new DashboardPage(),
            "Cơ sở"            => new CoSoPage(),
            "Loại phòng"       => new LoaiPhongPage(),
            "Phòng trọ"        => new PhongPage(),
            "Dịch vụ"          => new DichVuPage(),
            "Tài sản"          => new TaiSanPage(),
            "Khách thuê"       => new KhachThuePage(),
            "Tài khoản"        => new TaiKhoanPage(),
            "Đặt phòng"        => new DatPhongPage(),
            "Hợp đồng"         => new HopDongPage(),
            "Báo trả phòng"    => new BaoTraPage(),
            "Cư trú"           => new CuTruPage(),
            "Bàn giao tài sản" => new BanGiaoPage(),
            "Chỉ số điện nước" => new ChiSoPage(),
            "Hóa đơn"          => new HoaDonPage(),
            "Sổ thu chi"       => new ThuChiPage(),
            "Dịch vụ hợp đồng" => new DangKyDichVuPage(),
            "Vi phạm"          => new ViPhamPage(),
            "Sửa chữa"         => new SuaChuaPage(),
            "Thông báo"        => new ThongBaoPage(),
            "Báo cáo"          => new BaoCaoPage(),
            "Sao lưu"          => new SaoLuuPage(),
            "Thông tin của tôi"=> new TenantOverviewPage(),
            _ => new DashboardPage()
        };
    }

    [RelayCommand]
    private void ToggleMenu() => IsMenuOpen = !IsMenuOpen;

    [RelayCommand]
    private async Task LogoutAsync()
    {
        try { await ApiClient.PostAsync("/api/Auth/logout", new { }); } catch { }
        ApiClient.ResetAuthentication();
        _returnToLogin();
    }
}
