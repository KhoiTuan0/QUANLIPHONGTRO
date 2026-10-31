using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using LiveChartsCore.SkiaSharpView;
using QLPT.WPF.Services;
using QLPT.WPF.ViewModels;
using QLPT.WPF.Views;

internal static class Program
{
    static readonly string Log = Path.Combine(AppContext.BaseDirectory, "wpf-live-results.txt");
    static readonly string ApiBaseUrl = string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("QLPT_TEST_API_URL"))
        ? "http://localhost:5180"
        : Environment.GetEnvironmentVariable("QLPT_TEST_API_URL")!.Trim();
    static int checks;
    static void Check(bool ok, string label)
    {
        if (!ok) throw new InvalidOperationException(label);
        checks++;
        File.AppendAllText(Log, "PASS " + label + Environment.NewLine);
    }
    [STAThread]
    static void Main()
    {
        File.WriteAllText(Log, $"WPF live checks against {ApiBaseUrl} and isolated test database\n");
        var app = new QLPT.WPF.App();
        app.InitializeComponent();
        // Bootstrap the production views through test accounts, without a second login dialog.
        var startup = typeof(QLPT.WPF.App).GetMethod("App_Startup", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        app.Startup -= (StartupEventHandler)Delegate.CreateDelegate(typeof(StartupEventHandler), app, startup);
        LiveChartsCore.LiveCharts.Configure(c => c.UseDefaults());
        app.DispatcherUnhandledException += (_, e) =>
        {
            File.AppendAllText(Log, "FAIL dispatcher " + e.Exception + "\n");
            e.Handled = true;
            app.Shutdown(1);
            app.Dispatcher.BeginInvokeShutdown(DispatcherPriority.Send);
        };
        app.Dispatcher.BeginInvoke(async () =>
        {
            try
            {
                using var fixture = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "fixture.json")));
                var f = fixture.RootElement;
                Check(System.Text.RegularExpressions.Regex.IsMatch(f.GetProperty("database").GetString() ?? "", @"^QLPT_Test_\d{8}_[a-f0-9]{8}$"), "fixture uses an isolated SQL copy");
                var customerLogin = new LoginViewModel
                {
                    ApiUrl = ApiBaseUrl,
                    Username = f.GetProperty("tenant").GetString()!,
                    Password = f.GetProperty("password").GetString()!
                };
                bool customerAdmitted = false;
                customerLogin.LoginSuccess += () => customerAdmitted = true;
                await customerLogin.LoginCommand.ExecuteAsync(null);
                Check(!customerAdmitted && customerLogin.IsStatusError && !Session.IsLoggedIn && !ApiClient.HasSessionCookie,
                    "customer account cannot enter the desktop app");
                await Login(f.GetProperty("admin").GetString()!, f.GetProperty("password").GetString()!);
                var main = new MainWindow { Title = "QLPT — KIỂM THỬ CSDL RIÊNG" };
                app.MainWindow = main;
                main.Closed += (_, _) => { app.Shutdown(); app.Dispatcher.BeginInvokeShutdown(DispatcherPriority.Send); };
                main.Show();
                var vm = (MainViewModel)main.DataContext;
                foreach (string page in new[] { "Dashboard", "Cơ sở", "Loại phòng", "Phòng trọ", "Dịch vụ", "Tài sản", "Khách thuê", "Tài khoản", "Đặt phòng", "Hợp đồng", "Báo trả phòng", "Cư trú", "Bàn giao tài sản", "Chỉ số điện nước", "Hóa đơn", "Sổ thu chi", "Dịch vụ hợp đồng", "Vi phạm", "Sửa chữa", "Thông báo", "Báo cáo", "Sao lưu" })
                {
                    vm.NavigateToCommand.Execute(page);
                    await app.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                    if (vm.CurrentPage is FrameworkElement view && view.DataContext is BaseViewModel model) await Ready(model);
                    Check(vm.CurrentPage != null && vm.ErrorMessage.Length == 0, "live admin page: " + page);
                }
                vm.NavigateToCommand.Execute("Hợp đồng");
                if (vm.CurrentPage is FrameworkElement last && last.DataContext is BaseViewModel lastModel) await Ready(lastModel);
                bool returned = false;
                await new MainViewModel(() => returned = true).LogoutCommand.ExecuteAsync(null);
                Check(returned && !Session.IsLoggedIn && !ApiClient.HasSessionCookie, "real logout clears the cookie and returns without restarting");
                File.AppendAllText(Log, $"PASS {checks} WPF live checks. Main window remains open for UI inspection.\n");
            }
            catch (Exception e)
            {
                File.AppendAllText(Log, "FAIL " + e + "\n");
                app.Shutdown(1);
                app.Dispatcher.BeginInvokeShutdown(DispatcherPriority.Send);
            }
        });
        Dispatcher.Run();
    }
    static async Task Login(string username, string password)
    {
        var login = new LoginViewModel { ApiUrl = ApiBaseUrl, Username = username, Password = password };
        bool succeeded = false;
        login.LoginSuccess += () => succeeded = true;
        await login.LoginCommand.ExecuteAsync(null);
        Check(succeeded && Session.IsLoggedIn && ApiClient.HasSessionCookie, "WPF LoginViewModel authenticates against real API");
    }
    static async Task Ready(BaseViewModel vm)
    {
        var until = DateTime.UtcNow.AddSeconds(40);
        while (vm.IsBusy && DateTime.UtcNow < until) await Task.Delay(50);
        if (vm.IsBusy) throw new TimeoutException(vm.GetType().Name + " did not finish loading");
        if (vm.ErrorMessage.Length > 0) throw new InvalidOperationException(vm.GetType().Name + ": " + vm.ErrorMessage);
    }
}
