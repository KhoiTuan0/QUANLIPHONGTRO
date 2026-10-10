using System.Windows;
using QLPT.WPF.Services;

namespace QLPT.WPF.Views.Dialogs;

public partial class ThanhLyDialog : Window
{
    private readonly string _maHopDong;
    private readonly decimal _tienNo;
    private readonly decimal _tienCoc;

    public ThanhLyDialog(string maHopDong, decimal tienCoc, decimal tongNo)
    {
        InitializeComponent();
        _maHopDong = maHopDong;
        _tienNo = tongNo;
        _tienCoc = tienCoc;
        TxtInfo.Text = $"Mã HĐ: {maHopDong}  |  Tiền cọc: {VnMoney.FormatD(tienCoc)}  |  Tổng nợ hóa đơn: {VnMoney.FormatD(tongNo)}";
        TxtTienHoanCoc.Text = VnMoney.Format(tienCoc);
        TxtTienNo.Text = VnMoney.Format(tongNo);
        TxtTienPhat.Text = "0";
    }

    private async void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        TxtError.Visibility = Visibility.Collapsed;

        if (!VnMoney.TryParse(TxtTienHoanCoc.Text, out decimal hoanCoc) || hoanCoc < 0)
        { ShowError("Tiền cọc quyết toán không hợp lệ."); return; }
        if (hoanCoc > _tienCoc)
        { ShowError($"Tiền cọc quyết toán không được vượt quá tiền cọc ({VnMoney.FormatD(_tienCoc)})."); return; }

        decimal phat = 0;
        if (!string.IsNullOrWhiteSpace(TxtTienPhat.Text) && TxtTienPhat.Text != "0")
        {
            if (!VnMoney.TryParse(TxtTienPhat.Text, out phat) || phat < 0)
            { ShowError("Tiền phạt không hợp lệ."); return; }
        }

        if (!ConfirmDialog.Show(
                "Xác nhận thanh lý",
                $"Hợp đồng sẽ được quyết toán và đóng lại. Tiền cọc {VnMoney.FormatD(hoanCoc)} − nợ hóa đơn {VnMoney.FormatD(_tienNo)} − phạt {VnMoney.FormatD(phat)} → " +
                (hoanCoc - _tienNo - phat >= 0
                    ? $"hoàn lại khách {VnMoney.FormatD(hoanCoc - _tienNo - phat)}."
                    : $"khách còn nợ {VnMoney.FormatD(_tienNo + phat - hoanCoc)} (giữ trong công nợ)."),
                $"Hợp đồng #{_maHopDong}",
                "Thanh lý",
                MaterialDesignThemes.Wpf.PackIconKind.FileSign,
                "#EA580C", warn: true)) return;

        try
        {
            IsEnabled = false;
            await ApiClient.PostAsync($"/api/HopDong/{_maHopDong}/thanh-ly", new
            {
                tienHoanCoc = hoanCoc,
                tienNo = _tienNo,
                tienPhat = phat,
                ghiChu = TxtGhiChu.Text.Trim()
            });
            DialogResult = true;
        }
        catch (Exception ex) { ShowError(ex.Message); }
        finally { IsEnabled = true; }
    }

    private void ShowError(string msg)
    {
        TxtError.Text = msg;
        TxtError.Visibility = Visibility.Visible;
    }
}
