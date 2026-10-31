using System.Windows;
using QLPT.WPF.Services;

namespace QLPT.WPF.Views.Dialogs;

public partial class GiaHanDialog : Window
{
    private readonly string _maHopDong;
    private readonly DateTime? _ngayKetThucCu;

    public GiaHanDialog(string maHopDong, DateTime? ngayKetThucCu = null, string? thongTin = null)
    {
        InitializeComponent();
        _maHopDong = maHopDong;
        _ngayKetThucCu = ngayKetThucCu;
        TxtMaHD.Text = $"HĐ #{maHopDong} · {thongTin}";
        DpNgayKetThucMoi.SelectedDate = (ngayKetThucCu > DateTime.Today ? ngayKetThucCu.Value : DateTime.Today).AddYears(1);
    }

    private async void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        TxtError.Visibility = Visibility.Collapsed;

        if (DpNgayKetThucMoi.SelectedDate == null)
        { ShowError("Vui lòng chọn ngày kết thúc mới."); return; }

        if (DpNgayKetThucMoi.SelectedDate.Value <= DateTime.Today)
        { ShowError("Ngày kết thúc mới phải sau ngày hôm nay."); return; }
        if (_ngayKetThucCu.HasValue && DpNgayKetThucMoi.SelectedDate <= _ngayKetThucCu)
        { ShowError($"Ngày kết thúc mới phải sau ngày kết thúc hiện tại ({_ngayKetThucCu:dd/MM/yyyy})."); return; }

        decimal? giaThueMoi = null;
        if (!string.IsNullOrWhiteSpace(TxtGiaThueMoi.Text))
        {
            if (!VnMoney.TryParse(TxtGiaThueMoi.Text, out decimal gia) || gia <= 0)
            { ShowError("Giá thuê mới không hợp lệ."); return; }
            giaThueMoi = gia;
        }

        try
        {
            using var saveGuard = new DialogSaveGuard(this);
            object body = giaThueMoi.HasValue
                ? new { ngayKetThucMoi = DpNgayKetThucMoi.SelectedDate.Value.ToString("yyyy-MM-dd"), giaThueMoi = giaThueMoi.Value }
                : (object)new { ngayKetThucMoi = DpNgayKetThucMoi.SelectedDate.Value.ToString("yyyy-MM-dd") };
            await ApiClient.PostAsync($"/api/HopDong/{_maHopDong}/gia-han", body);
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
