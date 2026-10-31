using QLPT.WPF.Services;
using QLPT.WPF.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace QLPT.WPF.Views.Dialogs;

public partial class SuaChuaCapNhatDialog : Window
{
    private readonly string _maSuaChua;
    private readonly string _maPhong;
    private readonly DateTime? _ngayYeuCau;
    private readonly bool _alreadyCompleted;

    public SuaChuaCapNhatDialog(SuaChuaItem item)
    {
        InitializeComponent();
        _maSuaChua = item.MaSuaChua;
        _maPhong = item.MaPhong;
        _alreadyCompleted = item.TrangThai == "Đã hoàn thành";
        _ngayYeuCau = DateTime.TryParse(item.NgayYeuCau, out var requestDate) ? requestDate.Date : null;
        DpNgayHoanThanh.SelectedDate = DateTime.TryParse(item.NgayHoanThanh, out var completedDate) ? completedDate : DateTime.Today;

        // Pre-select current status
        foreach (System.Windows.Controls.ComboBoxItem ci in CbTrangThai.Items)
            if (ci.Content?.ToString() == item.TrangThai)
            {
                CbTrangThai.SelectedItem = ci;
                break;
            }
        if (CbTrangThai.SelectedItem == null && CbTrangThai.Items.Count > 0)
            CbTrangThai.SelectedIndex = 0;
        if (_alreadyCompleted) CbTrangThai.IsEnabled = false;
        CbHienVat.Items.Add(new ComboBoxItem { Content = "Không gắn hiện vật cụ thể" });
        CbHienVat.SelectedIndex = 0;
        CbTrangThai.SelectionChanged += (_, _) => UpdateAssetPicker();
        UpdateAssetPicker();
        Loaded += async (_, _) => await LoadAssetsAsync();

        // Pre-fill chi phi (strip formatting)
        TxtChiPhi.Text = VnMoney.Format(VnMoney.Parse(item.ChiPhi));
    }

    private void UpdateAssetPicker()
    {
        bool canPick = !_alreadyCompleted &&
            (CbTrangThai.SelectedItem as ComboBoxItem)?.Content?.ToString() == "Đã hoàn thành";
        CbHienVat.IsEnabled = canPick;
        if (!canPick) CbHienVat.SelectedIndex = 0;
    }

    private async Task LoadAssetsAsync()
    {
        if (_alreadyCompleted || !KiemTra.Ma(_maPhong, out int roomId)) return;
        try
        {
            var arr = await ApiClient.GetRawAsync($"/api/TaiSan/ca-the/phong/{roomId}");
            foreach (var unit in arr.EnumerateArray())
            {
                if (!KiemTra.Ma(ApiClient.ElStr(unit, "maTaiSanCaThe"), out int id)) continue;
                CbHienVat.Items.Add(new ComboBoxItem
                {
                    Content = $"{ApiClient.ElStr(unit, "maDinhDanh")} · {ApiClient.ElStr(unit, "tenTaiSan")}",
                    Tag = id
                });
            }
        }
        catch (Exception ex) { TxtError.Text = $"Không tải được hiện vật của phòng: {ex.Message}"; }
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
            (!VnMoney.TryParse(TxtChiPhi.Text, out chiPhi) || chiPhi < 0))
        {
            TxtError.Text = "Chi phí phải là số không âm."; return;
        }

        var trangThai = ((System.Windows.Controls.ComboBoxItem)CbTrangThai.SelectedItem)
            .Content?.ToString() ?? "";

        bool hoanThanh = trangThai == "Đã hoàn thành";
        int? unitId = (CbHienVat.SelectedItem as ComboBoxItem)?.Tag is int selectedUnit ? selectedUnit : null;
        if (hoanThanh && DpNgayHoanThanh.SelectedDate == null)
        {
            TxtError.Text = "Chọn ngày hoàn thành."; return;
        }
        if (DpNgayHoanThanh.SelectedDate != null && !KiemTra.KhongTuongLai(DpNgayHoanThanh.SelectedDate))
        {
            TxtError.Text = "Ngày hoàn thành không được sau hôm nay."; return;
        }
        if (hoanThanh && _ngayYeuCau.HasValue && DpNgayHoanThanh.SelectedDate < _ngayYeuCau)
        { TxtError.Text = "Ngày hoàn thành không được trước ngày yêu cầu."; return; }

        BtnSave.IsEnabled = false;
        try
        {
            using var saveGuard = new DialogSaveGuard(this);
            await ApiClient.PutAsync($"/api/SuaChua/{_maSuaChua}", new
            {
                trangThai,
                chiPhi,
                maTaiSanCaThe = hoanThanh ? unitId : null,
                // Chưa hoàn thành thì không gửi ngày hoàn thành.
                ngayHoanThanh = hoanThanh && DpNgayHoanThanh.SelectedDate.HasValue
                    ? DpNgayHoanThanh.SelectedDate.Value.ToString("yyyy-MM-dd")
                    : (string?)null,
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
