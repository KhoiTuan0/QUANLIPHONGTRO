using System.Globalization;
using System.Text.Json;
using System.Windows;
using QLPT.WPF.Services;

namespace QLPT.WPF.Views.Dialogs;

public partial class HoaDonChiTietDialog : Window
{
    public HoaDonChiTietDialog(JsonElement hoaDon)
    {
        InitializeComponent();
        string ma = ApiClient.ElStr(hoaDon, "maHoaDon");
        string phong = LookupData.NestedName(hoaDon, "maHopDongNavigation", "maPhongNavigation", "tenPhong");
        string khach = LookupData.NestedName(hoaDon, "maHopDongNavigation", "maKhachDaiDienNavigation", "hoTen");
        TxtThongTin.Text = $"Hóa đơn #{ma} · Phòng {phong} · {khach} · Kỳ {ApiClient.ElStr(hoaDon, "thang")}/{ApiClient.ElStr(hoaDon, "nam")}";
        TxtNgay.Text = $"Ngày lập: {Ngay(ApiClient.ElStr(hoaDon, "ngayLap"))} · Hạn thanh toán: {Ngay(ApiClient.ElStr(hoaDon, "hanThanhToan"))} · {ApiClient.ElStr(hoaDon, "trangThai")}";
        var dong = new List<DongChiTiet>();
        if (hoaDon.TryGetProperty("chiTietHoaDons", out var chiTiet) && chiTiet.ValueKind == JsonValueKind.Array)
            foreach (var c in chiTiet.EnumerateArray())
                dong.Add(new DongChiTiet(ApiClient.ElStr(c, "noiDung"), ApiClient.ElStr(c, "soLuong"),
                    Tien(ApiClient.ElStr(c, "donGia")), Tien(ApiClient.ElStr(c, "thanhTien"))));
        GridChiTiet.ItemsSource = dong;
        TxtTong.Text = $"Tổng tiền: {Tien(ApiClient.ElStr(hoaDon, "tongTien"))}";
        TxtThanhToan.Text = $"Đã thu: {Tien(ApiClient.ElStr(hoaDon, "daThanhToan"))} · Còn lại: {Tien(ApiClient.ElStr(hoaDon, "conLai"))}";
    }

    private static string Tien(string value) => decimal.TryParse(value, NumberStyles.Any,
        CultureInfo.InvariantCulture, out var amount) ? VnMoney.FormatD(amount) : value;
    private static string Ngay(string value) => DateTime.TryParse(value, out var day) ? day.ToString("dd/MM/yyyy") : "—";
    private sealed record DongChiTiet(string NoiDung, string SoLuong, string DonGia, string ThanhTien);
}
