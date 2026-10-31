using System.ComponentModel;
using System.Windows;

namespace QLPT.WPF.Services;

/// <summary>Giữ hộp thoại mở trong khi yêu cầu lưu đang chạy để kết quả không bị bỏ mất.</summary>
public sealed class DialogSaveGuard : IDisposable
{
    private readonly Window _window;
    private readonly bool _wasEnabled;
    private bool _disposed;

    public DialogSaveGuard(Window window)
    {
        _window = window;
        _wasEnabled = window.IsEnabled;
        window.Closing += PreventClose;
        window.IsEnabled = false;
    }

    private void PreventClose(object? sender, CancelEventArgs e) => e.Cancel = true;

    /// <summary>Gọi sau khi lưu thành công và trước khi gán DialogResult để cho phép đóng.</summary>
    public void Complete() => Dispose();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _window.Closing -= PreventClose;
        _window.IsEnabled = _wasEnabled;
    }
}
