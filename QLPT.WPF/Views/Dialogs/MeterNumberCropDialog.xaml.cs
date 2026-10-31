using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace QLPT.WPF.Views.Dialogs;

public partial class MeterNumberCropDialog : Window
{
    private readonly BitmapSource _source;
    private Point _start;
    private Rect _selection;

    public Int32Rect? NumberRegion { get; private set; }
    public int DigitCount { get; private set; } = 5;

    public MeterNumberCropDialog(string imagePath)
    {
        InitializeComponent();
        using var stream = File.OpenRead(imagePath);
        _source = BitmapFrame.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        FullImage.Source = _source;
        double width = Math.Min(720, _source.PixelWidth);
        double height = width * _source.PixelHeight / _source.PixelWidth;
        CropCanvas.Width = FullImage.Width = width;
        CropCanvas.Height = FullImage.Height = height;
    }

    private void CropCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _start = Clamp(e.GetPosition(CropCanvas));
        _selection = Rect.Empty;
        Selection.Visibility = Visibility.Visible;
        CropCanvas.CaptureMouse();
        DrawSelection(_start);
    }

    private void CropCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (CropCanvas.IsMouseCaptured)
            DrawSelection(Clamp(e.GetPosition(CropCanvas)));
    }

    private void CropCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!CropCanvas.IsMouseCaptured) return;
        DrawSelection(Clamp(e.GetPosition(CropCanvas)));
        CropCanvas.ReleaseMouseCapture();
    }

    private Point Clamp(Point point) => new(
        Math.Clamp(point.X, 0, CropCanvas.Width),
        Math.Clamp(point.Y, 0, CropCanvas.Height));

    private void DrawSelection(Point end)
    {
        _selection = new Rect(_start, end);
        Canvas.SetLeft(Selection, _selection.X);
        Canvas.SetTop(Selection, _selection.Y);
        Selection.Width = _selection.Width;
        Selection.Height = _selection.Height;
        Hint.Text = "";
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (_selection.IsEmpty)
        {
            Hint.Text = "Hãy khoanh vùng số trước.";
            return;
        }

        double scaleX = _source.PixelWidth / CropCanvas.Width;
        double scaleY = _source.PixelHeight / CropCanvas.Height;
        int left = Math.Clamp((int)Math.Floor(_selection.Left * scaleX), 0, _source.PixelWidth - 1);
        int top = Math.Clamp((int)Math.Floor(_selection.Top * scaleY), 0, _source.PixelHeight - 1);
        int right = Math.Clamp((int)Math.Ceiling(_selection.Right * scaleX), left + 1, _source.PixelWidth);
        int bottom = Math.Clamp((int)Math.Ceiling(_selection.Bottom * scaleY), top + 1, _source.PixelHeight);
        if (right - left < 30 || bottom - top < 12)
        {
            Hint.Text = "Vùng chọn quá nhỏ.";
            return;
        }

        NumberRegion = new Int32Rect(left, top, right - left, bottom - top);
        DigitCount = DigitCountBox.SelectedIndex + 4;
        DialogResult = true;
    }
}
