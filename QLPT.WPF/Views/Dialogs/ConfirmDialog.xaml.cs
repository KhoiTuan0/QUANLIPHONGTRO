using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;

namespace QLPT.WPF.Views.Dialogs;

/// <summary>
/// Hộp thoại xác nhận dùng chung, thay cho MessageBox mặc định của Windows:
/// bám theo theme sáng/tối của app và nêu rõ đối tượng sẽ bị tác động.
/// </summary>
public partial class ConfirmDialog : Window
{
    private ConfirmDialog() => InitializeComponent();

    /// <summary>Xác nhận xóa — nút hành động màu đỏ, kèm cảnh báo không hoàn tác.</summary>
    public static bool Delete(string message, string? itemName = null, string confirmText = "Xóa")
        => Show("Xác nhận xóa", message, itemName, confirmText,
                PackIconKind.TrashCanOutline, "#EF4444", warn: true);

    /// <summary>Xác nhận một thao tác quan trọng nhưng không phải xóa.</summary>
    public static bool Ask(string title, string message, string? itemName = null,
                           string confirmText = "Đồng ý", bool warn = false)
        => Show(title, message, itemName, confirmText,
                PackIconKind.HelpCircleOutline, "#0066CC", warn);

    /// <summary>Thông báo kết quả một nút "Đóng", cùng giao diện với hộp xác nhận.</summary>
    public static void Info(string title, string message, string? detail = null, bool isError = false)
        => Show(title, message, detail, "Đóng",
                isError ? PackIconKind.AlertCircleOutline : PackIconKind.CheckCircleOutline,
                isError ? "#EF4444" : "#16A34A", warn: false, singleButton: true);

    public static bool Show(string title, string message, string? itemName,
                            string confirmText, PackIconKind icon, string accentHex, bool warn,
                            bool singleButton = false)
    {
        var dlg = new ConfirmDialog();
        if (singleButton)
        {
            dlg.BtnCancel.Visibility = Visibility.Collapsed;
            dlg.BtnOk.IsCancel = true; // Esc vẫn đóng được khi chỉ còn một nút
        }
        dlg.TxtTitle.Text   = title;
        dlg.TxtMessage.Text = message;
        dlg.BtnOk.Content   = confirmText;

        var accent = (Color)ColorConverter.ConvertFromString(accentHex);
        dlg.Icon.Kind        = icon;
        dlg.Icon.Foreground  = new SolidColorBrush(accent);
        // Nền huy hiệu dùng chính màu nhấn ở độ mờ thấp để hợp cả dark lẫn light mode.
        dlg.IconBadge.Background = new SolidColorBrush(Color.FromArgb(26, accent.R, accent.G, accent.B));
        dlg.BtnOk.Background     = new SolidColorBrush(accent);
        dlg.BtnOk.BorderBrush    = new SolidColorBrush(accent);

        if (!string.IsNullOrWhiteSpace(itemName))
        {
            dlg.TxtItem.Text    = itemName;
            dlg.ItemBox.Visibility = Visibility.Visible;
        }
        if (warn) dlg.WarnBox.Visibility = Visibility.Visible;

        var owner = Application.Current?.Windows.OfType<Window>()
            .FirstOrDefault(w => w.IsActive && w != dlg) ?? Application.Current?.MainWindow;
        if (owner != null && owner != dlg && owner.IsLoaded) dlg.Owner = owner;
        else dlg.WindowStartupLocation = WindowStartupLocation.CenterScreen;

        return dlg.ShowDialog() == true;
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)     { DialogResult = true;  Close(); }
    private void BtnCancel_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }

    // Khung hệ thống đã tắt nên tự cho phép kéo cửa sổ.
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }
}
