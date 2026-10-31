using System.Windows;
using QLPT.WPF.Services;

namespace QLPT.WPF.Views.Dialogs;

public partial class LichSuKhachThueDialog : Window
{
    private readonly string _customerId;

    public LichSuKhachThueDialog(string customerId, string customerName)
    {
        InitializeComponent();
        _customerId = customerId;
        TxtTitle.Text = $"Lịch sử chỉnh sửa · {customerName} (#{customerId})";
        Loaded += async (_, _) =>
        {
            try
            {
                var history = await ApiClient.GetAsync<List<LichSuKhachItem>>($"/api/KhachThue/{_customerId}/lich-su-chinh-sua");
                // API/SQL lưu thời điểm audit theo UTC để đồng nhất giữa máy chủ và các máy trạm.
                // Khi hiển thị trên WPF, đổi sang giờ Việt Nam (UTC+7), không đổi dữ liệu gốc.
                foreach (var item in history)
                    item.NgaySua = ToVietnamTime(item.NgaySua);
                GridHistory.ItemsSource = history;
            }
            catch (Exception ex) { TxtError.Text = ex.Message; }
        };
    }

    private static DateTime ToVietnamTime(DateTime value)
    {
        var utc = value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        try
        {
            return TimeZoneInfo.ConvertTimeBySystemTimeZoneId(utc, "SE Asia Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            return utc.AddHours(7);
        }
        catch (InvalidTimeZoneException)
        {
            return utc.AddHours(7);
        }
    }
}

public class LichSuKhachItem
{
    public DateTime NgaySua { get; set; }
    public string Truong { get; set; } = "";
    public string? GiaTriCu { get; set; }
    public string? GiaTriMoi { get; set; }
    public int? MaTaiKhoanSua { get; set; }
}
