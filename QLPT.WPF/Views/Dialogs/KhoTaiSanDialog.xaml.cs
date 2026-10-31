using QLPT.WPF.Services;
using QLPT.WPF.ViewModels;
using System.Windows;

namespace QLPT.WPF.Views.Dialogs;

public partial class KhoTaiSanDialog : Window
{
    private readonly int _maTaiSan;
    private readonly int? _maPhong;

    public KhoTaiSanDialog(TaiSanItem item, int? maPhong = null)
    {
        InitializeComponent();
        _maTaiSan = int.Parse(item.MaTaiSan);
        _maPhong = maPhong;
        _stockCount = item.SoLuongTonKho;
        bool intake = maPhong == null;
        Title = intake ? "Nhập kho tài sản" : "Chuyển hiện vật từ kho";
        TxtTitle.Text = Title;
        TxtInfo.Text = intake
            ? $"{item.TenTaiSan}: nhập số lượng một lần, hệ thống cấp mã riêng cho từng chiếc. Giá mua là giá mỗi chiếc."
            : $"{item.TenTaiSan}: kho còn {item.SoLuongTonKho} chiếc. Chuyển các hiện vật tồn kho lâu nhất vào phòng mã {maPhong}; mã và lịch sử được giữ nguyên.";
        PurchaseFields.Visibility = intake ? Visibility.Visible : Visibility.Collapsed;
        BtnSave.Content = intake ? "Nhập kho" : "Chuyển";
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        TxtError.Text = "";
        if (!int.TryParse(TxtSoLuong.Text.Trim(), out int quantity) || quantity is < 1 or > 500)
        { TxtError.Text = "Số lượng phải từ 1 đến 500."; return; }
        if (_maPhong.HasValue && quantity > 0 && quantity > _stockCount)
        { TxtError.Text = "Số lượng chuyển vượt quá tồn kho."; return; }

        decimal? price = null;
        if (!_maPhong.HasValue && !string.IsNullOrWhiteSpace(TxtGiaMua.Text))
        {
            if (!VnMoney.TryParse(TxtGiaMua.Text, out decimal parsed) || parsed < 0 || parsed > 9999999999999999.99m)
            { TxtError.Text = "Giá mua mỗi chiếc không hợp lệ."; return; }
            price = parsed;
        }
        if (!_maPhong.HasValue && string.IsNullOrWhiteSpace(TxtTinhTrang.Text))
        { TxtError.Text = "Hãy nhập tình trạng hiện vật."; return; }
        if (PickNgayMua.SelectedDate?.Date > DateTime.Today)
        { TxtError.Text = "Ngày mua không được sau hôm nay."; return; }

        BtnSave.IsEnabled = false;
        try
        {
            using var guard = new DialogSaveGuard(this);
            if (_maPhong.HasValue)
                await ApiClient.PostAsync("/api/TaiSan/xuat-kho", new { maTaiSan = _maTaiSan, maPhong = _maPhong.Value, soLuong = quantity });
            else
                await ApiClient.PostAsync("/api/TaiSan/nhap-kho", new { maTaiSan = _maTaiSan, soLuong = quantity,
                    ngayMua = PickNgayMua.SelectedDate, giaMua = price, tinhTrang = TxtTinhTrang.Text.Trim(), ghiChu = TxtGhiChu.Text.Trim() });
            guard.Complete();
            DialogResult = true;
        }
        catch (Exception ex) { TxtError.Text = ex.Message; }
        finally { BtnSave.IsEnabled = true; }
    }

    private int _stockCount;
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
