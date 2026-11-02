using System.Windows;
using LiveChartsCore.SkiaSharpView; // UseDefaults / HasGlobalSKTypeface
using QLPT.WPF.Views;

namespace QLPT.WPF;

public partial class App : Application
{
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

        // LiveCharts vẽ bằng SkiaSharp, không dùng font của WPF; chỉ định Segoe UI để
        // chữ có dấu tiếng Việt (tên phòng, "Đã thu", "Còn nợ"…) hiển thị đúng.
        LiveChartsCore.LiveCharts.Configure(cfg => cfg
            .UseDefaults()
            .HasGlobalSKTypeface(SkiaSharp.SKTypeface.FromFamilyName("Segoe UI")));

        var login = new LoginWindow();
        if (login.ShowDialog() == true)
        {
            try
            {
                System.IO.File.WriteAllText(LogPath, "Step 1: Creating MainWindow\n");
                var main = new MainWindow();
                System.IO.File.AppendAllText(LogPath, "Step 2: MainWindow created OK\n");
                main.Closed += (_, __) => Shutdown();
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
