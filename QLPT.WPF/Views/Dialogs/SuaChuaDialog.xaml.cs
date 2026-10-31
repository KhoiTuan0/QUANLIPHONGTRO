using QLPT.WPF.Services;
using System.Windows;

namespace QLPT.WPF.Views.Dialogs;

public partial class SuaChuaDialog : Window
{
    public SuaChuaDialog()
    {
        InitializeComponent();
        DpNgayYeuCau.SelectedDate = DateTime.Today;
        DpNgayYeuCau.DisplayDateStart = DateTime.Today;
        DpNgayYeuCau.DisplayDateEnd = DateTime.Today;
    }

    private async void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        TxtError.Text = "";

        if (!PickMaHopDong.TryGetSelectedId(out int maHopDong))
        {
            TxtError.Text = "Vui lòng chọn hợp đồng từ danh sách đã tải."; return;
        }
        if (string.IsNullOrWhiteSpace(TxtMoTa.Text))
        {
            TxtError.Text = "Mô tả yêu cầu không được để trống."; return;
        }
        if (DpNgayYeuCau.SelectedDate == null)
        {
            TxtError.Text = "Chọn ngày yêu cầu."; return;
        }
        if (DpNgayYeuCau.SelectedDate.Value.Date != DateTime.Today)
        {
            TxtError.Text = "Yêu cầu sửa chữa mới phải ghi ngày hôm nay."; return;
        }

        BtnSave.IsEnabled = false;
        try
        {
            using var saveGuard = new DialogSaveGuard(this);
            await ApiClient.PostAsync("/api/SuaChua", new
            {
                maHopDong,
                moTa       = TxtMoTa.Text.Trim(),
                ngayYeuCau = DpNgayYeuCau.SelectedDate.Value.ToString("yyyy-MM-dd"),
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
