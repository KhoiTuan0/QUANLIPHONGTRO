using System.Windows;
using QLPT.WPF.Services;

namespace QLPT.WPF.Views.Dialogs;

public partial class CuTruDialog : Window
{
    /// <param name="maHopDong">Điền sẵn từ dòng đang chọn (có thể rỗng).</param>
    public CuTruDialog(string? maHopDong)
    {
        InitializeComponent();
        PickMaHopDong.SelectedId = maHopDong ?? "";
        DpNgayVao.SelectedDate = DateTime.Today;
        Loaded += (_, _) => (string.IsNullOrEmpty(PickMaHopDong.SelectedId) ? PickMaHopDong : PickMaKhach).Focus();
    }

    private async void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        TxtError.Visibility = Visibility.Collapsed;

        if (!PickMaHopDong.TryGetSelectedId(out int maHopDong))
        { ShowError("Vui lòng chọn hợp đồng từ danh sách đã tải."); return; }

        if (!PickMaKhach.TryGetSelectedId(out int maKhach))
        { ShowError("Vui lòng chọn khách thuê từ danh sách đã tải."); return; }

        if (DpNgayVao.SelectedDate == null)
        { ShowError("Vui lòng chọn ngày vào."); return; }

        try
        {
            using var saveGuard = new DialogSaveGuard(this);
            await ApiClient.PostAsync("/api/DanhMucMoRong/cu-tru", new
            {
                maHopDong,
                maKhach,
                ngayVao = DpNgayVao.SelectedDate.Value.ToString("yyyy-MM-dd"),
                trangThaiCuTru = "Đang ở",
                trangThaiKhaiBao = "Chưa khai báo"
            });
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
