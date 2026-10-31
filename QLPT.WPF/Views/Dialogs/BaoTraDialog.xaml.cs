using System.Windows;
using QLPT.WPF.Services;

namespace QLPT.WPF.Views.Dialogs;

public partial class BaoTraDialog : Window
{
    public BaoTraDialog()
    {
        InitializeComponent();
    }

    private async void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        TxtError.Visibility = Visibility.Collapsed;

        if (!PickMaHopDong.TryGetSelectedId(out int maHopDong))
        { ShowError("Vui lòng chọn hợp đồng từ danh sách đã tải."); return; }

        if (!PickMaKhach.TryGetSelectedId(out int maKhach))
        { ShowError("Vui lòng chọn khách thuê từ danh sách đã tải."); return; }

        if (DpNgayDuKienTra.SelectedDate < DateTime.Today)
        { ShowError("Ngày dự kiến trả không được trước hôm nay."); return; }

        if (string.IsNullOrWhiteSpace(TxtLyDo.Text))
        { ShowError("Vui lòng nhập lý do."); return; }

        try
        {
            using var saveGuard = new DialogSaveGuard(this);
            await ApiClient.PostAsync("/api/BaoTraPhong", new
            {
                maHopDong,
                maKhach,
                ngayDuKienTra = DpNgayDuKienTra.SelectedDate?.ToString("yyyy-MM-dd"),
                lyDo = TxtLyDo.Text.Trim()
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
