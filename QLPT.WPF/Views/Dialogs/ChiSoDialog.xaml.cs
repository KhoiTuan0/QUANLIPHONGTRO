using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using QLPT.WPF.Services;
using QLPT.WPF.ViewModels;

namespace QLPT.WPF.Views.Dialogs;

public partial class ChiSoDialog : Window
{
    private string? _imagePath;
    private Int32Rect? _numberRegion;
    private int _digitCount = 5;
    private decimal? _ocrSuggestion;
    private readonly ChiSoItem? _item;

    public ChiSoDialog(ChiSoItem? item = null)
    {
        InitializeComponent();
        _item = item;
        TxtThangNam.Text = DateTime.Today.ToString("yyyy-MM");
        if (item == null) return;
        TxtTitle.Text = "Sửa chỉ số điện nước";
        BtnSave.Content = "Lưu";
        PickMaPhong.Visibility = Visibility.Collapsed;
        CbLoai.SelectedIndex = item.Loai == "Nước" ? 1 : 0;
        CbLoai.IsEnabled = false;
        TxtThangNam.Text = $"{item.Nam:0000}-{item.Thang:00}";
        TxtThangNam.IsReadOnly = true;
        TxtChiSoCu.Text = item.ChiSoCu.ToString(CultureInfo.InvariantCulture);
        TxtChiSoMoi.Text = item.ChiSoMoi.ToString(CultureInfo.InvariantCulture);
        OcrPanel.Visibility = Visibility.Collapsed;
    }

    private void CbLoai_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (OcrPanel == null) return; // sự kiện có thể bắn trong InitializeComponent
        bool isElectric = (CbLoai.SelectedItem as ComboBoxItem)?.Content?.ToString() == "Điện";
        OcrPanel.Visibility = isElectric ? Visibility.Visible : Visibility.Collapsed;
        if (!isElectric) ClearImage();
    }

    private void ChiSoMoi_Changed(object sender, TextChangedEventArgs e)
    {
        if (ChkConfirmed != null) ChkConfirmed.IsChecked = false;
    }

    private void PickImage_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Chọn ảnh đồng hồ điện",
            Filter = "Ảnh đồng hồ|*.jpg;*.jpeg;*.png;*.bmp"
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var file = new FileInfo(dialog.FileName);
            if (file.Length == 0 || file.Length > 10 * 1024 * 1024)
            { ShowError("Ảnh phải có dữ liệu và không quá 10 MB."); return; }
            var bitmap = new BitmapImage();
            using (var stream = file.OpenRead())
            {
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.DecodePixelWidth = 500;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
            }
            bitmap.Freeze();
            ClearImage();
            _imagePath = file.FullName;
            ImgMeter.Source = bitmap;
            ImgMeter.Visibility = Visibility.Visible;
            BtnCrop.IsEnabled = BtnClearImage.IsEnabled = ChkConfirmed.IsEnabled = true;
            TxtOcrStatus.Text = $"Đã chọn {file.Name}. Khoanh vùng chữ số màu đen để đọc OCR, hoặc nhập tay rồi đối chiếu ảnh.";
            TxtError.Visibility = Visibility.Collapsed;
        }
        catch (Exception)
        {
            ShowError("Không mở được ảnh. Hãy chọn ảnh JPG, PNG hoặc BMP khác; vẫn có thể nhập tay.");
        }
    }

    private void Crop_Click(object sender, RoutedEventArgs e)
    {
        if (_imagePath == null) return;
        try
        {
            var crop = new MeterNumberCropDialog(_imagePath) { Owner = this };
            if (crop.ShowDialog() != true || crop.NumberRegion is not { } region) return;
            _numberRegion = region;
            _digitCount = crop.DigitCount;
            using var stream = File.OpenRead(_imagePath);
            var bitmap = BitmapFrame.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            ImgNumber.Source = new CroppedBitmap(bitmap, region);
            ImgNumber.Visibility = Visibility.Visible;
            _ocrSuggestion = null;
            BtnUseSuggestion.IsEnabled = false;
            BtnReadOcr.IsEnabled = true;
            ChkConfirmed.IsChecked = false;
            TxtOcrStatus.Text = "Đã chọn vùng số. Kiểm tra ảnh cắt chỉ có các chữ số màu đen rồi bấm ‘Đọc số từ ảnh’.";
        }
        catch (Exception)
        {
            ShowError("Không mở được vùng số. Hãy chọn lại ảnh hoặc nhập chỉ số bằng tay.");
        }
    }

    private async void ReadOcr_Click(object sender, RoutedEventArgs e)
    {
        if (_imagePath == null || _numberRegion == null) return;
        string imagePath = _imagePath;
        Int32Rect numberRegion = _numberRegion.Value;
        int digitCount = _digitCount;
        BtnReadOcr.IsEnabled = false;
        BtnUseSuggestion.IsEnabled = false;
        ChkConfirmed.IsChecked = false;
        TxtOcrStatus.Text = "Đang đọc ảnh…";
        var result = await Task.Run(() => MeterOcr.Read(imagePath, numberRegion, digitCount));
        if (_imagePath != imagePath || _numberRegion != numberRegion || _digitCount != digitCount) return;
        string source = "OCR trên máy";
        if (!result.Suggestion.HasValue)
        {
            TxtOcrStatus.Text = "OCR trên máy chưa đọc chắc chắn. Đang gửi ảnh tới API để đọc bằng AI…";
            try
            {
                var images = MeterAiImages.Prepare(imagePath, numberRegion);
                string? digits = await ApiClient.ReadMeterWithAiAsync(images.Crop, images.Overview, digitCount);
                if (digits != null && decimal.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var value))
                { result = new MeterOcr.Result(value, null); source = "AI"; }
                else result = new MeterOcr.Result(null, "AI cũng chưa đọc chắc chắn đủ chữ số. Hãy khoanh sát hơn hoặc nhập tay rồi đối chiếu ảnh.");
            }
            catch (Exception ex) { result = new MeterOcr.Result(null, $"{result.Error} {ex.Message}"); }
        }
        if (_imagePath != imagePath || _numberRegion != numberRegion || _digitCount != digitCount) return;
        BtnReadOcr.IsEnabled = true;
        _ocrSuggestion = result.Suggestion;
        BtnUseSuggestion.IsEnabled = result.Suggestion.HasValue;
        TxtOcrStatus.Text = result.Error ??
            $"{source} gợi ý: {result.Suggestion?.ToString(new string('0', digitCount), CultureInfo.InvariantCulture)}. Hãy so với ảnh; bấm ‘Dùng số gợi ý’ hoặc nhập số đúng vào ô Chỉ số mới.";
    }

    private void UseSuggestion_Click(object sender, RoutedEventArgs e)
    {
        if (_ocrSuggestion is decimal value)
            TxtChiSoMoi.Text = value.ToString(new string('0', _digitCount), CultureInfo.InvariantCulture);
    }

    private void ClearImage_Click(object sender, RoutedEventArgs e) => ClearImage();

    private void ClearImage()
    {
        _imagePath = null;
        _numberRegion = null;
        _ocrSuggestion = null;
        ImgMeter.Source = null;
        ImgMeter.Visibility = Visibility.Collapsed;
        ImgNumber.Source = null;
        ImgNumber.Visibility = Visibility.Collapsed;
        BtnCrop.IsEnabled = BtnReadOcr.IsEnabled = BtnUseSuggestion.IsEnabled = BtnClearImage.IsEnabled = false;
        ChkConfirmed.IsChecked = false;
        ChkConfirmed.IsEnabled = false;
        TxtOcrStatus.Text = "";
    }

    private async void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        TxtError.Visibility = Visibility.Collapsed;

        int maPhong;
        if (_item == null ? !PickMaPhong.TryGetSelectedId(out maPhong) : !int.TryParse(_item.MaPhong, out maPhong))
        { ShowError("Vui lòng chọn phòng đang thuê có hợp đồng hiệu lực từ danh sách."); return; }

        if (CbLoai.SelectedItem is not ComboBoxItem loaiItem)
        { ShowError("Vui lòng chọn loại chỉ số."); return; }

        if (!KiemTra.Ky(TxtThangNam.Text, out int nam, out int thang))
        { ShowError("Tháng năm không đúng định dạng yyyy-MM (VD: 2026-10, tháng 01–12)."); return; }
        if (new DateTime(nam, thang, 1) > DateTime.Today)
        { ShowError("Không ghi chỉ số cho kỳ chưa tới."); return; }

        if (!KiemTra.SoKhongAm(TxtChiSoCu.Text, out decimal cu))
        { ShowError("Chỉ số cũ phải là số không âm."); return; }

        if (!KiemTra.SoKhongAm(TxtChiSoMoi.Text, out decimal moi))
        { ShowError("Chỉ số mới phải là số không âm."); return; }

        if (moi < cu)
        { ShowError("Chỉ số mới phải lớn hơn hoặc bằng chỉ số cũ."); return; }

        if (moi > 9_999_999)
        { ShowError("Chỉ số quá lớn (tối đa 9.999.999)."); return; }

        bool hasElectricImage = loaiItem.Content?.ToString() == "Điện" && _imagePath != null;
        if (hasElectricImage && ChkConfirmed.IsChecked != true)
        { ShowError("Hãy đối chiếu ảnh và xác nhận chỉ số mới, hoặc bỏ ảnh để lưu bằng cách nhập tay."); return; }

        try
        {
            using var saveGuard = new DialogSaveGuard(this);
            if (_item != null)
            {
                await ApiClient.PutAsync($"/api/ChiSoDienNuoc/{_item.MaChiSo}", new { chiSoCu = cu, chiSoMoi = moi });
            }
            else if (hasElectricImage)
            {
                string imageUrl;
                try { imageUrl = await ApiClient.UploadImageAsync(_imagePath!, "dong-ho"); }
                catch (Exception ex)
                { ShowError($"{ex.Message} Bấm ‘Bỏ ảnh’ để lưu chỉ số nhập tay."); return; }
                await ApiClient.PostAsync("/api/ChiSoDienNuoc", new
                {
                    maPhong, loaiChiSo = "Điện", nam, thang,
                    chiSoCu = cu, chiSoMoi = moi,
                    chiSoOcr = _ocrSuggestion, chiSoXacNhan = moi, duongDanAnh = imageUrl,
                    ngayGhi = DateTime.Today.ToString("yyyy-MM-dd")
                });
            }
            else
            {
                await ApiClient.PostAsync("/api/ChiSoDienNuoc", new
                {
                    maPhong,
                    loaiChiSo = loaiItem.Content?.ToString() ?? "Điện",
                    nam, thang, chiSoCu = cu, chiSoMoi = moi,
                    ngayGhi = DateTime.Today.ToString("yyyy-MM-dd")
                });
            }
            saveGuard.Complete();
            DialogResult = true;
        }
        catch (Exception ex) { ShowError(ex.Message); }
    }

    private void ShowError(string msg)
    {
        TxtError.Text = msg;
        TxtError.Visibility = Visibility.Visible;
    }
}
