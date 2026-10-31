using QLPT.WPF.Services;
using System.Windows;

namespace QLPT.WPF.Views.Dialogs;

public partial class DangKyDichVuDialog : Window
{
    public DangKyDichVuDialog(string maHopDong)
    {
        InitializeComponent();
        PickMaHopDong.SelectedId = maHopDong;
        DpNgayBatDau.SelectedDate = DateTime.Today;
        DpNgayBatDau.DisplayDateStart = DateTime.Today;
    }

    private async void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        TxtError.Text = "";

        if (string.IsNullOrWhiteSpace(PickMaHopDong.SelectedId) ||
            !PickMaHopDong.TryGetSelectedId(out int maHopDong))
        {
            TxtError.Text = "Vui lòng chọn hợp đồng từ danh sách đã tải."; return;
        }
        if (string.IsNullOrWhiteSpace(PickMaDichVu.SelectedId) ||
            !PickMaDichVu.TryGetSelectedId(out int maDichVu))
        {
            TxtError.Text = "Vui lòng chọn dịch vụ từ danh sách đã tải."; return;
        }
        if (DpNgayBatDau.SelectedDate == null)
        {
            TxtError.Text = "Chọn ngày bắt đầu."; return;
        }
        if (DpNgayBatDau.SelectedDate.Value.Date < DateTime.Today)
        {
            TxtError.Text = "Dịch vụ mới không được bắt đầu trước hôm nay."; return;
        }

        BtnSave.IsEnabled = false;
        try
        {
            using var saveGuard = new DialogSaveGuard(this);
            await ApiClient.PostAsync("/api/DangKyDichVu", new
            {
                maHopDong,
                maDichVu,
                ngayBatDau = DpNgayBatDau.SelectedDate.Value.ToString("yyyy-MM-dd"),
            });
            saveGuard.Complete();
            DialogResult = true;
        }
        catch (Exception ex)
        {
            TxtError.Text = ex.Message;
            BtnSave.IsEnabled = true;
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
        => DialogResult = false;
}
