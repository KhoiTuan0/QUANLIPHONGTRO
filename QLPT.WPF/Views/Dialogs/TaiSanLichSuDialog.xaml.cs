using QLPT.WPF.Services;
using QLPT.WPF.ViewModels;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace QLPT.WPF.Views.Dialogs;

public partial class TaiSanLichSuDialog : Window
{
    private readonly TaiSanCaTheItem _unit;

    public TaiSanLichSuDialog(TaiSanCaTheItem unit)
    {
        InitializeComponent();
        _unit = unit;
        TxtTitle.Text = $"{unit.MaDinhDanh} · {unit.TenTaiSan} · {unit.TinhTrang}";
        TxtTinhTrang.Text = unit.TinhTrang;
        DpNgayMua.SelectedDate = unit.NgayMua;
        TxtGiaMua.Text = unit.GiaMua?.ToString(CultureInfo.InvariantCulture) ?? "";
        DpNgayMua.IsEnabled = Session.IsAdmin;
        TxtGiaMua.IsReadOnly = !Session.IsAdmin;
        if (!Session.IsAdmin)
        {
            TxtEventHeading.Text = "Ghi nhận sửa chữa, bảo dưỡng hoặc kiểm kê";
            TxtPurchaseHelp.Text = "Ngày mua và giá mua do chủ trọ cập nhật; quản lý chỉ xem. Chi phí sửa chữa, bảo dưỡng được ghi riêng bên dưới.";
            foreach (var choice in CbLoai.Items.OfType<ComboBoxItem>())
                if (choice.Content?.ToString() == "Thanh lý") choice.Visibility = Visibility.Collapsed;
            DpNgayMua.ToolTip = "Chỉ chủ trọ được sửa ngày mua.";
            TxtGiaMua.ToolTip = "Chỉ chủ trọ được sửa giá mua.";
        }
        DpNgay.SelectedDate = DateTime.Today;
        DpNgayMua.DisplayDateEnd = DateTime.Today;
        DpNgay.DisplayDateEnd = DateTime.Today;
        Loaded += async (_, _) =>
        {
            await RefreshUnitAsync();
            await LoadHistoryAsync();
        };
    }

    private async void Inventory_Click(object sender, RoutedEventArgs e)
    {
        TxtError.Text = "";
        if (string.IsNullOrWhiteSpace(TxtTinhTrang.Text))
        { TxtError.Text = "Vui lòng nhập tình trạng hiện vật."; return; }
        if (DpNgayMua.SelectedDate?.Date > DateTime.Today)
        { TxtError.Text = "Ngày mua không được sau hôm nay."; return; }
        if (!string.IsNullOrWhiteSpace(TxtGiaMua.Text) && !VnMoney.TryParse(TxtGiaMua.Text, out _))
        { TxtError.Text = "Giá mua phải là số tiền không âm."; return; }
        decimal? price = string.IsNullOrWhiteSpace(TxtGiaMua.Text) ? null : VnMoney.Parse(TxtGiaMua.Text);
        BtnInventory.IsEnabled = false;
        try
        {
            await ApiClient.PutAsync($"/api/TaiSan/ca-the/{_unit.MaTaiSanCaThe}/tinh-trang",
                new { tinhTrang = TxtTinhTrang.Text.Trim(), ngayMua = Session.IsAdmin ? DpNgayMua.SelectedDate?.ToString("yyyy-MM-dd") : null,
                    giaMua = Session.IsAdmin ? price : null });
            await RefreshUnitAsync();
            await LoadHistoryAsync();
        }
        catch (Exception ex) { TxtError.Text = ex.Message; }
        finally { BtnInventory.IsEnabled = true; }
    }

    private async Task RefreshUnitAsync()
    {
        try
        {
            var units = await ApiClient.GetRawAsync("/api/TaiSan/ca-the");
            var current = units.EnumerateArray().FirstOrDefault(x =>
                ApiClient.ElStr(x, "maTaiSanCaThe") == _unit.MaTaiSanCaThe);
            if (current.ValueKind == System.Text.Json.JsonValueKind.Undefined) return;
            TxtTinhTrang.Text = ApiClient.ElStr(current, "tinhTrang");
            TxtTitle.Text = $"{_unit.MaDinhDanh} · {_unit.TenTaiSan} · {TxtTinhTrang.Text}";
            DpNgayMua.SelectedDate = DateTime.TryParse(ApiClient.ElStr(current, "ngayMua"), out var date)
                ? date : null;
            TxtGiaMua.Text = VnMoney.Format(ApiClient.ElStr(current, "giaMua"));
        }
        catch (Exception ex) { TxtError.Text = ex.Message; }
    }

    private async Task LoadHistoryAsync()
    {
        try
        {
            var arr = await ApiClient.GetRawAsync($"/api/TaiSan/ca-the/{_unit.MaTaiSanCaThe}/lich-su");
            var history = arr.EnumerateArray().Select(x => new
            {
                Ngay = DateTime.TryParse(ApiClient.ElStr(x, "ngaySuKien"), out var day) ? day.ToString("dd/MM/yyyy") : "",
                Loai = ApiClient.ElStr(x, "loaiSuKien"),
                ChiPhi = decimal.TryParse(ApiClient.ElStr(x, "chiPhi"), NumberStyles.Any, CultureInfo.InvariantCulture, out var cost)
                    ? VnMoney.FormatD(cost) : "0 đ",
                NoiDung = ApiClient.ElStr(x, "noiDung")
            }).ToList();
            GridHistory.ItemsSource = history;
            TxtHistoryEmpty.Visibility = history.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Exception ex) { TxtHistoryEmpty.Visibility = Visibility.Collapsed; TxtError.Text = ex.Message; }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        TxtError.Text = "";
        var type = (CbLoai.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
        if (type == "Thanh lý" && !Session.IsAdmin)
        { TxtError.Text = "Chỉ chủ trọ được thanh lý hiện vật."; return; }
        if (DpNgay.SelectedDate is not { } date)
        { TxtError.Text = "Vui lòng chọn ngày xảy ra sự kiện."; return; }
        if (date.Date > DateTime.Today)
        { TxtError.Text = "Ngày sự kiện không được sau hôm nay."; return; }
        if (string.IsNullOrWhiteSpace(TxtNoiDung.Text))
        { TxtError.Text = "Vui lòng nhập nội dung sự kiện."; return; }
        if (!VnMoney.TryParse(TxtChiPhi.Text, out var cost) || cost < 0)
        { TxtError.Text = "Chi phí phải là số tiền không âm; nhập 0 nếu không phát sinh."; return; }
        BtnSave.IsEnabled = false;
        try
        {
            await ApiClient.PostAsync($"/api/TaiSan/ca-the/{_unit.MaTaiSanCaThe}/lich-su",
                new { loaiSuKien = type, ngaySuKien = date.ToString("yyyy-MM-dd"), chiPhi = cost,
                    noiDung = TxtNoiDung.Text.Trim() });
            TxtNoiDung.Text = "";
            TxtChiPhi.Text = "0";
            await LoadHistoryAsync();
        }
        catch (Exception ex) { TxtError.Text = ex.Message; }
        finally { BtnSave.IsEnabled = true; }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
