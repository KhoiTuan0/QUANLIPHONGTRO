using System.Windows;
using LiveChartsCore.SkiaSharpView;
using QLPT.WPF.Views;

namespace QLPT.WPF;

public partial class App : Application
{
    private Window? _activeMainWindow;
    private bool _returningToLogin;
    private static readonly string LogPath = System.IO.Path.Combine(
        System.IO.Path.GetTempPath(), "qlpt_wpf_crash.txt");

    private void App_Startup(object sender, StartupEventArgs e)
    {
        // Chi tiết kỹ thuật (stack trace) chỉ ghi vào file; người dùng thấy câu dễ hiểu.
        const string LoiChung = "Đã xảy ra lỗi ngoài dự kiến. Vui lòng thử lại thao tác vừa rồi.";

        AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
        {
            System.IO.File.WriteAllText(LogPath, ex.ExceptionObject?.ToString() ?? "Unknown");
            MessageBox.Show($"{LoiChung}\n\nChi tiết đã được ghi vào:\n{LogPath}", "Lỗi ứng dụng",
                MessageBoxButton.OK, MessageBoxImage.Error);
        };

        DispatcherUnhandledException += (s, ex) =>
        {
            System.IO.File.WriteAllText(LogPath, ex.Exception.ToString());
            Views.Dialogs.ConfirmDialog.Info("Lỗi ứng dụng", LoiChung, $"Chi tiết: {LogPath}", isError: true);
            ex.Handled = true;
        };

        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (s, ex) =>
        {
            var msg = ex.Exception.ToString();
            System.IO.File.AppendAllText(LogPath, "\n\nTaskScheduler:\n" + msg);
            ex.SetObserved();
        };

        // Nạp thư viện native từ đầu ra build theo đúng kiến trúc Windows. Trên một số
        // máy .NET không tự dò được thư mục runtimes khi mở WPF trực tiếp từ .exe.
        var architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture switch
        {
            System.Runtime.InteropServices.Architecture.X86 => "win-x86",
            System.Runtime.InteropServices.Architecture.Arm64 => "win-arm64",
            _ => "win-x64"
        };
        var skiaPath = System.IO.Path.Combine(AppContext.BaseDirectory,
            "runtimes", architecture, "native", "libSkiaSharp.dll");
        if (System.IO.File.Exists(skiaPath))
            System.Runtime.InteropServices.NativeLibrary.Load(skiaPath);

        LiveChartsCore.LiveCharts.Configure(cfg => cfg.UseDefaults());

        ShowLogin();
    }

    public void ReturnToLogin()
    {
        if (_returningToLogin) return;
        _returningToLogin = true;
        try
        {
            (_activeMainWindow ?? MainWindow)?.Close();
            _activeMainWindow = null;
        }
        finally { _returningToLogin = false; }
        ShowLogin();
    }

    private void ShowLogin()
    {
        var login = new LoginWindow();
        MainWindow = login;
        if (login.ShowDialog() == true)
        {
            try
            {
                System.IO.File.WriteAllText(LogPath, "Step 1: Creating MainWindow\n");
                var main = new MainWindow();
                _activeMainWindow = MainWindow = main;
                System.IO.File.AppendAllText(LogPath, "Step 2: MainWindow created OK\n");
                main.Closed += (_, __) => { if (!_returningToLogin) Shutdown(); };
                main.Show();
                System.IO.File.AppendAllText(LogPath, "Step 3: MainWindow shown OK\n");
            }
            catch (Exception ex)
            {
                System.IO.File.WriteAllText(LogPath, "CATCH: " + ex);
                MessageBox.Show($"Không mở được màn hình chính.\n\nChi tiết đã được ghi vào:\n{LogPath}", "Lỗi ứng dụng",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }
        else
        {
            Shutdown();
        }
    }
}
