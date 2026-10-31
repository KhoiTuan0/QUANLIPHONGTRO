using System.Windows;
using QLPT.WPF.Services;
using QLPT.WPF.ViewModels;

namespace QLPT.WPF.Views.Dialogs;

public partial class KhaiBaoCuTruDialog : Window
{
    private readonly CuTruItem _item;

    public KhaiBaoCuTruDialog(CuTruItem item)
    {
        InitializeComponent();
        _item = item;
        TxtTenant.Text = $"{item.HoTen} · {item.TenPhong} · Hợp đồng #{item.MaHopDong}";
        TxtDates.Text = $"Ngày dọn vào: {item.NgayVao} · Ngày khách gửi: {FormatDate(item.NgayGui)}";
        TxtAddress.Text = item.DiaChiThuongTru;
        TxtDetails.Text = string.IsNullOrWhiteSpace(item.ThongTinBoSung) ? "Không có" : item.ThongTinBoSung;
    }

    private static string FormatDate(string value) => DateTime.TryParse(value, out var date)
        ? date.ToString("dd/MM/yyyy HH:mm") : value;

    private async void BtnApprove_Click(object sender, RoutedEventArgs e) => await SubmitAsync(true);
    private async void BtnReject_Click(object sender, RoutedEventArgs e) => await SubmitAsync(false);

    private async Task SubmitAsync(bool approve)
    {
        TxtError.Text = "";
        string reason = TxtReason.Text.Trim();
        if (!approve && reason.Length == 0)
        { TxtError.Text = "Nhập lý do để khách biết cần bổ sung gì."; return; }
        try
        {
            using var guard = new DialogSaveGuard(this);
            await ApiClient.PostAsync($"/api/DanhMucMoRong/cu-tru/{_item.MaCuTru}/xu-ly-khai-bao",
                new { chapNhan = approve, lyDoTraLai = approve ? null : reason });
            guard.Complete();
            DialogResult = true;
        }
        catch (Exception ex) { TxtError.Text = ex.Message; }
    }
}
