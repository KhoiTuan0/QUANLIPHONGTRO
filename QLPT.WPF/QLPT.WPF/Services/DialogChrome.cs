using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace QLPT.WPF.Services;

/// <summary>
/// Phần code cho style <c>Win10Dialog</c> (App.xaml): nối nút ✕ tự vẽ với lệnh đóng cửa sổ
/// và tắt bo góc của Windows 11 để dialog có góc vuông như Windows 10.
/// </summary>
public static class DialogChrome
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached("IsEnabled", typeof(bool), typeof(DialogChrome),
            new PropertyMetadata(false, KhiDoi));

    public static bool GetIsEnabled(DependencyObject o) => (bool)o.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject o, bool v) => o.SetValue(IsEnabledProperty, v);

    private static void KhiDoi(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Window w || !(bool)e.NewValue) return;

        w.CommandBindings.Add(new CommandBinding(SystemCommands.CloseWindowCommand,
            (_, _) => SystemCommands.CloseWindow(w)));

        if (new WindowInteropHelper(w).Handle != IntPtr.Zero) GocVuong(w);
        else w.SourceInitialized += (_, _) => GocVuong(w);
    }

    // DWMWA_WINDOW_CORNER_PREFERENCE = 33, DWMWCP_DONOTROUND = 1. Trên Windows 10 lệnh này
    // trả lỗi và bị bỏ qua — vốn dĩ đã là góc vuông.
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    private static void GocVuong(Window w)
    {
        try
        {
            int khongBo = 1;
            DwmSetWindowAttribute(new WindowInteropHelper(w).Handle, 33, ref khongBo, sizeof(int));
        }
        catch (DllNotFoundException) { }
        catch (EntryPointNotFoundException) { }
    }
}
