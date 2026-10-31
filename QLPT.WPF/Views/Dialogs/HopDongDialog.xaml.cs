using System.Windows;
using QLPT.WPF.Services;

namespace QLPT.WPF.Views.Dialogs;

public partial class HopDongDialog : Window
{
    private readonly decimal _cocDaThu;
    private string _lastRoomId = "";

    public HopDongDialog(string? maPhong = null, string? maKhach = null, decimal cocDaThu = 0)
    {
        InitializeComponent();
        _cocDaThu = cocDaThu;
        DpNgayBatDau.SelectedDate = DateTime.Today;
        DpNgayKetThuc.SelectedDate = DateTime.Today.AddYears(1);
        TxtTienCoc.Text = VnMoney.Format(cocDaThu);
        if (!string.IsNullOrEmpty(maPhong) && !string.IsNullOrEmpty(maKhach))
        {
            Title = "Nhận phòng / Lập hợp đồng";
            TxtHeading.Text = "Nhận phòng / Lập hợp đồng";
            PickMaPhong.Source = LookupKind.PhongDaDat;
            PickMaKhachDaiDien.Source = LookupKind.KhachThue;
            PickMaPhong.AllowedId = PickMaPhong.SelectedId = maPhong;
            PickMaKhachDaiDien.AllowedId = PickMaKhachDaiDien.SelectedId = maKhach;
            PickMaPhong.ReadOnly = PickMaKhachDaiDien.ReadOnly = true;
            BtnAddRoom.Visibility = BtnAddCustomer.Visibility = Visibility.Collapsed;
        }
        PickMaPhong.SelectionChanged += (_, _) =>
        {
            var room = PickMaPhong.SelectedOption;
            if (room == null || room.Id == _lastRoomId) return;
            _lastRoomId = room.Id;
            TxtGiaThueThang.Text = VnMoney.Format(room.Price);
        };
    }

    private async void AddRoom_Click(object sender, RoutedEventArgs e)
    {
        if (new PhongDialog(null) { Owner = this }.ShowDialog() == true)
            await PickMaPhong.ReloadAsync();
    }

    private async void AddCustomer_Click(object sender, RoutedEventArgs e)
    {
        if (new KhachThueDialog(null) { Owner = this }.ShowDialog() == true)
        {
            await PickMaKhachDaiDien.ReloadAsync();
            await PickMaKhachCuTru.ReloadAsync();
        }
    }

    private async void AddOccupant_Click(object sender, RoutedEventArgs e)
    {
        if (new KhachThueDialog(null) { Owner = this }.ShowDialog() == true)
            await PickMaKhachCuTru.ReloadAsync();
    }

    private async void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        TxtError.Visibility = Visibility.Collapsed;

        if (!PickMaPhong.TryGetSelectedId(out int maPhong))
        { ShowError("Vui lòng chọn phòng từ danh sách đã tải."); return; }

        if (!PickMaKhachDaiDien.TryGetSelectedId(out int maKhach))
        { ShowError("Vui lòng chọn khách đại diện từ danh sách đã tải."); return; }
        if (!PickMaKhachCuTru.TryGetSelectedId(out int maKhachCuTru))
        { ShowError("Vui lòng chọn người thực tế ở phòng (có thể là người đứng tên)."); return; }

        if (DpNgayBatDau.SelectedDate == null)
        { ShowError("Vui lòng chọn ngày bắt đầu."); return; }

        if (DpNgayKetThuc.SelectedDate == null)
        { ShowError("Vui lòng chọn ngày kết thúc."); return; }

        if (DpNgayKetThuc.SelectedDate <= DpNgayBatDau.SelectedDate)
        { ShowError("Ngày kết thúc phải sau ngày bắt đầu."); return; }

        if (!VnMoney.TryParse(TxtGiaThueThang.Text, out decimal giaThue) || giaThue <= 0)
        { ShowError("Giá thuê/tháng không hợp lệ."); return; }

        if (!VnMoney.TryParse(TxtTienCoc.Text, out decimal tienCoc) || tienCoc < 0)
        { ShowError("Tiền cọc không hợp lệ."); return; }

        if (tienCoc < _cocDaThu)
        { ShowError($"Tiền cọc hợp đồng không được thấp hơn khoản đã thu khi đặt phòng ({VnMoney.FormatD(_cocDaThu)})."); return; }

        try
        {
            using var saveGuard = new DialogSaveGuard(this);
            await ApiClient.PostAsync("/api/HopDong", new
            {
                maPhong,
                maKhachDaiDien = maKhach,
                maKhachCuTru,
                ngayBatDau = DpNgayBatDau.SelectedDate.Value.ToString("yyyy-MM-dd"),
                ngayKetThuc = DpNgayKetThuc.SelectedDate.Value.ToString("yyyy-MM-dd"),
                giaThueThang = giaThue,
                tienCoc,
                trangThai = "Đang hiệu lực"
            });
            saveGuard.Complete();
            DialogResult = true;
        }
        catch (Exception ex) { ShowError(ex.Message); }
    }

    private void ShowError(string msg)
    {
        TxtError.Text = msg;
        TxtError.Visibility = Visibility.Visible;
    }
}
