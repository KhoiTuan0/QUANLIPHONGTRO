using QLPT.WPF.Services;
using QLPT.WPF.ViewModels;
using System.Windows;

namespace QLPT.WPF.Views.Dialogs;

public partial class SuaChuaCapNhatDialog : Window
{
    private readonly string _maSuaChua;

    public SuaChuaCapNhatDialog(SuaChuaItem item)
    {
        InitializeComponent();
        _maSuaChua = item.MaSuaChua;

        // Pre-select current status
        foreach (System.Windows.Controls.ComboBoxItem ci in CbTrangThai.Items)
            if (ci.Content?.ToString() == item.TrangThai)
            {
                CbTrangThai.SelectedItem = ci;
                break;
            }
        if (CbTrangThai.SelectedItem == null && CbTrangThai.Items.Count > 0)
            CbTrangThai.SelectedIndex = 0;

        // Pre-fill chi phi (strip formatting)
        TxtChiPhi.Text = item.ChiPhi.Replace("đ", "").Replace(" ", "").Replace(".", "").Trim();
    }

    private async void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        TxtError.Text = "";

        if (CbTrangThai.SelectedItem == null)
        {
            TxtError.Text = "Chọn trạng thái."; return;
        }

        decimal chiPhi = 0;
        if (!string.IsNullOrWhiteSpace(TxtChiPhi.Text) &&
            !VnMoney.TryParse(TxtChiPhi.Text, out chiPhi))
        {
            TxtError.Text = "Chi phí phải là số hợp lệ."; return;
        }

        var trangThai = ((System.Windows.Controls.ComboBoxItem)CbTrangThai.SelectedItem)
            .Content?.ToString() ?? "";

        bool hoanThanh = trangThai == "Đã hoàn thành";
        if (hoanThanh && DpNgayHoanThanh.SelectedDate == null)
        {
            TxtError.Text = "Chọn ngày hoàn thành."; return;
        }
        if (DpNgayHoanThanh.SelectedDate != null && !KiemTra.KhongTuongLai(DpNgayHoanThanh.SelectedDate))
        {
            TxtError.Text = "Ngày hoàn thành không được sau hôm nay."; return;
        }

        BtnSave.IsEnabled = false;
        try
        {
            await ApiClient.PutAsync($"/api/SuaChua/{_maSuaChua}", new
            {
                trangThai,
                chiPhi,
                // Chưa hoàn thành thì không gửi ngày hoàn thành.
                ngayHoanThanh = hoanThanh && DpNgayHoanThanh.SelectedDate.HasValue
                    ? DpNgayHoanThanh.SelectedDate.Value.ToString("yyyy-MM-dd")
                    : (string?)null,
            });
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
