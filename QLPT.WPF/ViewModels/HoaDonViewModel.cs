using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QLPT.WPF.Services;
using System.Collections.ObjectModel;

namespace QLPT.WPF.ViewModels;

public partial class HoaDonViewModel : BaseViewModel
{
    [ObservableProperty] private ObservableCollection<HoaDonItem> _items = new();
    [ObservableProperty] private HoaDonItem? _selectedItem;

    public HoaDonViewModel() => _ = LoadAsync();

    [RelayCommand]
    private async Task LoadAsync() => await RunSafeAsync(async () =>
    {
        var arr = await ApiClient.GetRawAsync("/api/HoaDon");
        var list = new ObservableCollection<HoaDonItem>();
        foreach (var el in arr.EnumerateArray())
        {
            decimal.TryParse(ApiClient.ElStr(el, "tongTien"), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var tong);
            // API trả entity HoaDon: "daThanhToan" (không phải "daThu"), "thang" + "nam"
            // tách riêng (không có "thangNam") — đọc sai tên nên hai cột này từng trống / 0đ.
            decimal.TryParse(ApiClient.ElStr(el, "daThanhToan"), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var da);
            decimal.TryParse(ApiClient.ElStr(el, "conLai"), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var con);
            string thang = ApiClient.ElStr(el, "thang"), nam = ApiClient.ElStr(el, "nam");
            string ngayLap = ApiClient.ElStr(el, "ngayLap");
            list.Add(new HoaDonItem
            {
                MaHoaDon = ApiClient.ElStr(el, "maHoaDon"),
                MaHopDong = ApiClient.ElStr(el, "maHopDong"),
                ThangNam = string.IsNullOrEmpty(thang) ? "" : $"{thang}/{nam}",
                TongTien = tong,
                TongTienDisplay = VnMoney.FormatD(tong),
                DaThu = da,
                DaThuDisplay = VnMoney.FormatD(da),
                ConLai = con,
                ConLaiDisplay = VnMoney.FormatD(con),
                TrangThai = ApiClient.ElStr(el, "trangThai"),
                NgayLap = DateTime.TryParse(ngayLap, out var nl) ? nl.ToString("dd/MM/yyyy") : ngayLap
            });
        }
        Items = list;
    });

    [RelayCommand]
    private async Task TaoHoaDonAsync()
    {
        var dlg = new Views.Dialogs.HoaDonDialog();
        if (dlg.ShowDialog() == true) await LoadAsync();
    }

    [RelayCommand]
    private async Task ThuTienAsync()
    {
        if (SelectedItem == null) { ErrorMessage = "Vui lòng chọn một hóa đơn."; return; }
        if (SelectedItem.ConLai <= 0) { ErrorMessage = "Hóa đơn này đã được thanh toán đầy đủ."; return; }
        var dlg = new Views.Dialogs.ThuTienDialog(SelectedItem.MaHoaDon, SelectedItem.ConLai);
        if (dlg.ShowDialog() == true) await LoadAsync();
    }
}

public class HoaDonItem
{
    public string MaHoaDon { get; set; } = "";
    public string MaHopDong { get; set; } = "";
    public string ThangNam { get; set; } = "";
    public decimal TongTien { get; set; }
    public string TongTienDisplay { get; set; } = "";
    public decimal DaThu { get; set; }
    public string DaThuDisplay { get; set; } = "";
    public decimal ConLai { get; set; }
    public string ConLaiDisplay { get; set; } = "";
    public string TrangThai { get; set; } = "";
    public string NgayLap { get; set; } = "";
}
