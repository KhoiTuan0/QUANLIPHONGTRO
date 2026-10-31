using System.Windows;
using QLPT.WPF.Services;

namespace QLPT.WPF.Views.Dialogs;

public partial class DatPhongDialog : Window
{
    public DatPhongDialog()
    {
        InitializeComponent();
        TxtTienCoc.Text = "0";
    }

    private async void AddRoom_Click(object sender, RoutedEventArgs e)
    {
        if (new PhongDialog(null) { Owner = this }.ShowDialog() == true)
            await PickMaPhong.ReloadAsync();
    }

    private async void AddCustomer_Click(object sender, RoutedEventArgs e)
    {
        if (new KhachThueDialog(null) { Owner = this }.ShowDialog() == true)
            await PickMaKhach.ReloadAsync();
    }

    private async void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        TxtError.Visibility = Visibility.Collapsed;

        if (!PickMaPhong.TryGetSelectedId(out int maPhong))
        { ShowError("Vui lòng chọn phòng từ danh sách đã tải."); return; }

        if (!PickMaKhach.TryGetSelectedId(out int maKhach))
        { ShowError("Vui lòng chọn khách thuê từ danh sách đã tải."); return; }

        if (!VnMoney.TryParse(TxtTienCoc.Text, out decimal tienCoc) || tienCoc < 0)
        { ShowError("Tiền cọc không hợp lệ."); return; }

        try
        {
            using var saveGuard = new DialogSaveGuard(this);
            await ApiClient.PostAsync("/api/DatPhong", new
            {
                maPhong,
                maKhach,
                ngayDat = DateTime.Today.ToString("yyyy-MM-dd"),
                tienCoc
            });
            saveGuard.Complete();
            DialogResult = true;
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private void ShowError(string msg)
    {
        TxtError.Text = msg;
        TxtError.Visibility = Visibility.Visible;
    }
}
