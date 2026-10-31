using System.Windows;
using System.Windows.Controls;
using QLPT.WPF.Services;
using QLPT.WPF.ViewModels;

namespace QLPT.WPF.Views.Dialogs;

public partial class YeuCauHuyDatPhongDialog : Window
{
    private readonly DatPhongItem? _booking;
    public YeuCauHuyDatPhongDialog(DatPhongItem? booking)
    {
        InitializeComponent(); _booking = booking;
        TxtBooking.Text = booking == null ? "Muốn tiếp nhận yêu cầu trực tiếp từ khách: đóng cửa sổ và chọn một phiếu đặt trước."
            : $"Tiếp nhận cho phiếu #{booking.MaDatPhong} · {booking.TenPhong} · {booking.TenKhach}";
        BtnCreate.IsEnabled = booking?.TrangThai == "Đã đặt cọc";
        Loaded += async (_, _) => await LoadAsync();
    }
    private async Task LoadAsync()
    {
        try { GridRequests.ItemsSource = await ApiClient.GetAsync<List<YeuCauHuyItem>>("/api/DatPhong/yeu-cau-huy"); }
        catch (Exception ex) { TxtError.Text = ex.Message; }
    }
    private async void Reload_Click(object sender, RoutedEventArgs e) { TxtError.Text = ""; await LoadAsync(); }
    private void SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TxtDetail == null) return;
        TxtRefund.Text = ""; TxtDecision.Text = "";
        if (GridRequests.SelectedItem is not YeuCauHuyItem item) { TxtDetail.Text = "Chưa chọn yêu cầu."; return; }
        TxtDetail.Text = $"Lý do: {item.LyDo}\nTiền cọc: {VnMoney.FormatD(item.TienCoc ?? 0)} · Báo trước: {item.SoNgayBaoTruoc?.ToString() ?? "chưa có ngày nhận"} ngày\nKết quả: {item.GhiChuXuLy}";
    }
    private async void Create_Click(object sender, RoutedEventArgs e)
    {
        if (_booking == null || !int.TryParse(_booking.MaDatPhong, out int id)) return;
        if (string.IsNullOrWhiteSpace(TxtReason.Text)) { TxtError.Text = "Nhập lý do khách yêu cầu hủy."; return; }
        using var guard = new DialogSaveGuard(this);
        try {
            await ApiClient.PostAsync("/api/DatPhong/yeu-cau-huy", new { maDatPhong = id, lyDo = TxtReason.Text.Trim() });
            TxtReason.Clear(); TxtError.Text = ""; await LoadAsync();
        } catch (Exception ex) { TxtError.Text = ex.Message; }
    }
    private async void Approve_Click(object sender, RoutedEventArgs e) => await DecideAsync(true);
    private async void Reject_Click(object sender, RoutedEventArgs e) => await DecideAsync(false);
    private async Task DecideAsync(bool approve)
    {
        if (GridRequests.SelectedItem is not YeuCauHuyItem item || item.TrangThai != "Chờ duyệt")
        { TxtError.Text = "Chọn một yêu cầu đang chờ duyệt."; return; }
        decimal refund = 0;
        if (approve && (!VnMoney.TryParse(TxtRefund.Text, out refund) || refund < 0 || refund > (item.TienCoc ?? 0)))
        { TxtError.Text = "Nhập số tiền hoàn từ 0 đến tiền cọc."; return; }
        if (string.IsNullOrWhiteSpace(TxtDecision.Text)) { TxtError.Text = "Nhập lý do quyết định / thỏa thuận."; return; }
        if (!ConfirmDialog.Ask(approve ? "Duyệt hủy đặt phòng" : "Từ chối yêu cầu",
            approve ? $"Hủy phiếu #{item.MaDatPhong} và ghi hoàn cọc {VnMoney.FormatD(refund)}? Chỉ xác nhận khi thực hiện hoàn tiền."
                    : "Phiếu đặt vẫn giữ chỗ. Xác nhận từ chối yêu cầu?", $"{item.TenPhong} · {item.TenKhach}")) return;
        using var guard = new DialogSaveGuard(this);
        try {
            await ApiClient.PutAsync($"/api/DatPhong/yeu-cau-huy/{item.MaYeuCau}/xu-ly", new { duyet = approve, soTienHoan = refund, ghiChuXuLy = TxtDecision.Text.Trim() });
            TxtError.Text = ""; await LoadAsync();
        } catch (Exception ex) { TxtError.Text = ex.Message; }
    }
}

public class YeuCauHuyItem
{
    public int MaYeuCau { get; set; }
    public int MaDatPhong { get; set; }
    public string TenPhong { get; set; } = "";
    public string TenKhach { get; set; } = "";
    public DateTime NgayYeuCau { get; set; }
    public string? LyDo { get; set; }
    public string TrangThai { get; set; } = "";
    public decimal? TienCoc { get; set; }
    public int? SoNgayBaoTruoc { get; set; }
    public decimal? SoTienHoan { get; set; }
    public string? GhiChuXuLy { get; set; }
    public string HoanDisplay => SoTienHoan.HasValue ? VnMoney.FormatD(SoTienHoan.Value) : "Chưa xử lý";
}
