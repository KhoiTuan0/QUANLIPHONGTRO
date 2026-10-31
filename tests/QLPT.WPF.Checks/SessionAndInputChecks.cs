using System.ComponentModel;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using QLPT.WPF.Controls;
using QLPT.WPF.Services;
using QLPT.WPF.ViewModels;

internal static class SessionAndInputChecks
{
    private static readonly BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;
    public static void Run(Action<bool, string> check, FixtureHandler fake)
    {
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        Install(fake, cookie: true);
        var login = new LoginViewModel { Username = "fixture_admin", Password = "fixture_password" };
        fake.Overrides["/api/Auth/login"] = () => Json(new { maTaiKhoan = 1, vaiTro = "Admin", maKhach = (int?)null });
        bool loggedIn = false;
        login.LoginSuccess += () => loggedIn = true;
        Pump(login.LoginCommand.ExecuteAsync(null));
        check(loggedIn && Session.IsLoggedIn && Session.IsAdmin && ApiClient.HasSessionCookie, "login requires and accepts a server session cookie");

        Install(fake, cookie: false);
        loggedIn = false;
        Pump(login.LoginCommand.ExecuteAsync(null));
        check(!loggedIn && !Session.IsLoggedIn && login.StatusMessage.Contains("phiên đăng nhập"), "successful JSON without cookie cannot become a logged-in session");
        Install(fake, cookie: true);
        fake.Overrides["/api/Auth/login"] = () => Json(new { maTaiKhoan = 1, vaiTro = "Unsupported", maKhach = 100 });
        Pump(login.LoginCommand.ExecuteAsync(null));
        check(!Session.IsLoggedIn && login.IsStatusError, "invalid login role is rejected and authentication cleared");
        Install(fake, cookie: true);
        fake.Overrides["/api/Auth/login"] = () => Json(new { maTaiKhoan = 2, vaiTro = "Customer", maKhach = (int?)null });
        Pump(login.LoginCommand.ExecuteAsync(null));
        check(!Session.IsLoggedIn && login.IsStatusError, "desktop login rejects customer accounts");

        Install(fake, cookie: true);
        SetCustomer();
        Session.OwnContractIds.Add(11);
        int sent = fake.Requests.Count;
        check(Rejected(() => ApiClient.GetRawAsync("/api/KhachThue").GetAwaiter().GetResult()) && fake.Requests.Count == sent,
            "customer cannot request admin tenant list even before HTTP transport");
        check(Rejected(() => ApiClient.PostAsync("/api/HopDong", new { }).GetAwaiter().GetResult()) && fake.Requests.Count == sent,
            "customer cannot create admin records");
        check(Rejected(() => ApiClient.GetRawAsync("/api/KhachThue/101").GetAwaiter().GetResult()), "customer cannot read another tenant profile");
        check(Rejected(() => ApiClient.GetRawAsync("/api/HoaDon/hop-dong/12").GetAwaiter().GetResult()), "customer cannot read another contract invoices");
        ApiClient.GetRawAsync("/api/KhachThue/100").GetAwaiter().GetResult();
        ApiClient.GetRawAsync("/api/HoaDon/hop-dong/11").GetAwaiter().GetResult();
        ApiClient.PutAsync("/api/ThongBao/70/doc/2", new { }).GetAwaiter().GetResult();
        check(fake.Requests.Count == sent + 3, "customer reads own profile/invoices and marks own notice as read");
        check(Rejected(() => ApiClient.PutAsync("/api/ThongBao/70/doc/3", new { }).GetAwaiter().GetResult()), "customer cannot mark a notice read for another account");
        var nav = new MainViewModel(() => { });
        nav.NavigateToCommand.Execute("Cơ sở");
        check(nav.CurrentPage == null && nav.ErrorMessage.Length > 0, "customer navigation cannot instantiate an admin page");

        fake.Overrides["/api/KhachThue/100"] = () => Json(new { message = "SQL SELECT private_details" }, HttpStatusCode.InternalServerError);
        var serverError = Failure(() => ApiClient.GetRawAsync("/api/KhachThue/100").GetAwaiter().GetResult());
        check(serverError is ApiResponseException && !serverError.Message.Contains("SELECT"), "server failures do not display SQL details");
        fake.Overrides["/api/KhachThue/100"] = () => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("not JSON") });
        check(Failure(() => ApiClient.GetRawAsync("/api/KhachThue/100").GetAwaiter().GetResult()).Message.Contains("định dạng"), "malformed successful response reports a readable error");
        fake.Overrides["/api/KhachThue/100"] = () => Json(new { message = "expired fixture" }, HttpStatusCode.Unauthorized);
        var expired = Failure(() => ApiClient.GetRawAsync("/api/KhachThue/100").GetAwaiter().GetResult());
        check(!Session.IsLoggedIn && Session.OwnContractIds.Count == 0 && expired.Message.Contains("hết hạn"), "401 clears account and contract ownership");
        nav.NavigateToCommand.Execute("Thông báo");
        check(nav.CurrentPage == null && nav.ErrorMessage.Contains("hết hạn"), "expired session cannot navigate to more pages");

        Install(fake, cookie: true);
        SetCustomer();
        fake.Overrides["/api/KhachThue/100"] = () => Json(new { maKhach = 100, hoTen = "Đặng Văn Khôi", soDienThoai = "0900000100" });
        fake.Overrides["/api/HopDong/customer/100"] = () => Json(new[] {
            new { maHopDong = 11, maPhong = 1, tenPhong = "Phòng A", ngayBatDau = "2026-01-01", ngayKetThuc = "2027-01-01", giaThueThang = 2000000, trangThai = "Đang hiệu lực" },
            new { maHopDong = 12, maPhong = 2, tenPhong = "Phòng B", ngayBatDau = "2025-01-01", ngayKetThuc = "2026-01-01", giaThueThang = 2000000, trangThai = "Đã thanh lý" } });
        fake.Overrides["/api/HoaDon/customer/100"] = () => Json(new[] { new { maHoaDon = 91, maHopDong = 11, tenPhong = "Phòng A", thang = 10, nam = 2026, tongTien = 2000000, daThanhToan = 1000000, conLai = 1000000, trangThai = "Thanh toán một phần" } });
        fake.Overrides["/api/ThongBao/customer/2"] = () => Json(new[] { new { maThongBao = 70, tieuDe = "Fixture", ngayGui = "2026-10-14", trangThai = "Chưa đọc" } });
        Session.OwnContractIds.Add(999);
        fake.Requests.Clear();
        var overview = new TenantOverviewViewModel();
        check(fake.Requests.Count == 4 && !fake.Requests.Any(x => x.Contains("/hop-dong/")), "tenant overview uses four requests independent of number of contracts");
        check(overview.HopDongs.Count == 2 && overview.HoaDons.Single().TenPhong == "Phòng A" && overview.HopDongs[0].TenPhong == "Phòng A",
            "tenant overview displays contracts and invoices by room name");
        check(Session.OwnContractIds.SetEquals(new[] { 11, 12 }), "ownership is replaced by the latest response rather than accumulated");
        fake.Overrides["/api/HopDong/customer/100"] = () => Json(Array.Empty<object>());
        fake.Overrides["/api/HoaDon/customer/100"] = () => Json(Array.Empty<object>());
        Pump(overview.LoadCommand.ExecuteAsync(null));
        check(overview.HopDongs.Count == 0 && overview.HoaDons.Count == 0 && Session.OwnContractIds.Count == 0, "empty reload clears old contracts, invoices and ownership");
        fake.Overrides["/api/KhachThue/100"] = () => Json(new { message = "missing fixture" }, HttpStatusCode.NotFound);
        Pump(overview.LoadCommand.ExecuteAsync(null));
        check(overview.HoTen.Length == 0 && overview.ThongBaos.Count == 0 && overview.ErrorMessage.Length > 0, "failed reload clears old personal data and reports the error");
        var oldProfile = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        fake.Overrides["/api/KhachThue/100"] = () => oldProfile.Task;
        var oldLoad = overview.LoadCommand.ExecuteAsync(null);
        Session.MaKhach = 101;
        Session.MaTaiKhoan = 3;
        oldProfile.SetResult(Response(new { maKhach = 100, hoTen = "Old session fixture" }));
        Pump(oldLoad);
        check(overview.HoTen.Length == 0 && Session.OwnContractIds.Count == 0, "late tenant response cannot populate a different session");

        fake.Overrides.Clear();
        Install(fake, cookie: true);
        Session.MaTaiKhoan = 1;
        Session.VaiTro = "Admin";
        var picker = new EntityPicker { Source = LookupKind.KhachThue, SelectedId = "100" };
        picker.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        fake.FailTenants = true;
        Pump(picker.ReloadAsync());
        check(!picker.IsReady && picker.SelectedId.Length == 0, "lookup failure also clears IDs bound to asset and handover pages");
        fake.FailTenants = false;
        Pump(picker.ReloadAsync());
        var oldTenants = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        fake.Overrides["/api/KhachThue"] = () => oldTenants.Task;
        var oldReload = picker.ReloadAsync();
        picker.Source = LookupKind.Phong;
        oldTenants.SetResult(Response(new[] { new { maKhach = 500, hoTen = "Late fixture" } }));
        Pump(oldReload);
        check(picker.IsReady && picker.Source == LookupKind.Phong && ((ComboBox)picker.FindName("CbOptions")).Items.Count == 6,
            "late lookup response cannot replace a newer source");

        var money = new TextBox { Text = "500000" };
        MoneyInput.SetIsEnabled(money, true);
        Paste(money, "-500.000");
        check(money.Text == "500.000", "pasted negative money is rejected instead of converted to positive");
        Paste(money, "100abc200");
        check(money.Text == "500.000", "pasted text cannot concatenate digits into a different amount");
        Paste(money, "1234567890123456");
        check(money.Text == "500.000", "oversized pasted amount is rejected instead of truncated");
        Paste(money, "1.500.000 đ");
        check(money.Text == "1.500.000", "valid Vietnamese amount is pasted and formatted");
        money.Text = "-500000";
        check(VnMoney.Parse(money.Text) == -500000, "negative preset remains negative for validation");

        var window = new Window();
        using (var guard = new DialogSaveGuard(window))
        {
            var closing = new CancelEventArgs();
            OnClosing(window, closing);
            check(!window.IsEnabled && closing.Cancel, "pending save disables controls and cancels close");
            guard.Complete();
            closing = new CancelEventArgs();
            OnClosing(window, closing);
            check(window.IsEnabled && !closing.Cancel, "successful save restores controls and permits dialog result close");
        }
        using (new DialogSaveGuard(window)) { }
        check(window.IsEnabled, "failed save disposal restores the window");

        fake.Overrides.Clear();
        Install(fake, cookie: true);
        Session.MaTaiKhoan = 1;
        Session.MaKhach = 100;
        Session.VaiTro = "Admin";
        Session.OwnContractIds.Add(11);
        bool returned = false;
        var main = new MainViewModel(() => returned = !Session.IsLoggedIn && !ApiClient.HasSessionCookie);
        Pump(main.LogoutCommand.ExecuteAsync(null));
        check(returned && Session.OwnContractIds.Count == 0, "logout clears cookie and session before returning to login without restarting dotnet");
    }

    private static void SetCustomer() { Session.MaTaiKhoan = 2; Session.MaKhach = 100; Session.VaiTro = "Customer"; }
    private static void Install(FixtureHandler fake, bool cookie)
    {
        Session.Logout();
        var cookies = new CookieContainer();
        if (cookie) cookies.Add(new Uri(AppConfig.ApiBaseUrl), new Cookie("QLPT.Session", "fixture", "/"));
        typeof(ApiClient).GetField("_cookies", PrivateStatic)!.SetValue(null, cookies);
        typeof(ApiClient).GetField("_http", PrivateStatic)!.SetValue(null, new HttpClient(fake, false) { BaseAddress = new Uri(AppConfig.ApiBaseUrl) });
    }
    private static HttpResponseMessage Response(object data, HttpStatusCode code = HttpStatusCode.OK) => new(code) { Content = new StringContent(JsonSerializer.Serialize(data)) };
    private static Task<HttpResponseMessage> Json(object data, HttpStatusCode code = HttpStatusCode.OK) => Task.FromResult(Response(data, code));
    private static Exception Failure(Action action) { try { action(); } catch (Exception ex) { return ex; } throw new InvalidOperationException("Expected failure."); }
    private static bool Rejected(Action action) => Failure(action) is UnauthorizedAccessException;
    private static void Paste(TextBox box, string text)
    {
        box.SelectAll();
        var data = new DataObject(DataFormats.UnicodeText, text);
        box.RaiseEvent(new DataObjectPastingEventArgs(data, false, DataFormats.UnicodeText));
    }
    private static void OnClosing(Window window, CancelEventArgs args) => typeof(Window).GetMethod("OnClosing", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, new object[] { args });
    private static void Pump(Task task)
    {
        if (!task.IsCompleted)
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            var frame = new DispatcherFrame();
            var timeout = new DispatcherTimer(TimeSpan.FromSeconds(10), DispatcherPriority.Send, (_, _) => frame.Continue = false, dispatcher);
            task.ContinueWith(_ => dispatcher.BeginInvoke(() => frame.Continue = false), TaskScheduler.Default);
            Dispatcher.PushFrame(frame);
            timeout.Stop();
            if (!task.IsCompleted) throw new TimeoutException("WPF regression check did not finish.");
        }
        task.GetAwaiter().GetResult();
    }
}
