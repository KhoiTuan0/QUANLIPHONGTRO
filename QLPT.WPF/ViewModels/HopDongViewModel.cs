using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QLPT.WPF.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using Microsoft.Win32;

namespace QLPT.WPF.ViewModels;

public partial class HopDongViewModel : BaseViewModel
{
    public bool IsAdmin => Session.IsAdmin;
    public bool CanManageContracts => Session.IsAdmin || Session.IsManager;
    [ObservableProperty] private ObservableCollection<HopDongItem> _items = new();
    [ObservableProperty] private HopDongItem? _selectedItem;
    [ObservableProperty] private ObservableCollection<HopDongItem> _sapHetHanItems = new();
    [ObservableProperty] private bool _showSapHetHan;
    public List<string> HetHanOptions { get; } = new() { "7 ngày", "30 ngày", "60 ngày", "90 ngày", "Tùy chọn" };
    [ObservableProperty] private string _mocHetHan = "30 ngày";
    [ObservableProperty] private string _soNgayTuyChon = "30";
    [ObservableProperty] private int _soNgaySapHetHan = 30;
    public bool HienSoNgayTuyChon => MocHetHan == "Tùy chọn";
    public string TieuDeHetHan => $"Hợp đồng quá hạn hoặc hết hạn trong {SoNgaySapHetHan} ngày";

    public HopDongViewModel() => _ = LoadAsync();

    partial void OnMocHetHanChanged(string value)
    {
        OnPropertyChanged(nameof(HienSoNgayTuyChon));
        if (value == "Tùy chọn") ApDungSoNgayTuyChon();
        else if (int.TryParse(value.Split(' ')[0], out int days)) SoNgaySapHetHan = days;
    }

    partial void OnSoNgayTuyChonChanged(string value)
    {
        if (HienSoNgayTuyChon) ApDungSoNgayTuyChon();
    }

    private void ApDungSoNgayTuyChon()
    {
        if (!int.TryParse(SoNgayTuyChon, out int days) || days is < 1 or > 365)
        { ErrorMessage = "Số ngày cần xem phải từ 1 đến 365."; return; }
        ErrorMessage = "";
        SoNgaySapHetHan = days;
    }

    partial void OnSoNgaySapHetHanChanged(int value)
    {
        OnPropertyChanged(nameof(TieuDeHetHan));
        if (ShowSapHetHan) _ = LoadSapHetHanAsync();
    }

    [RelayCommand]
    private async Task LoadAsync() => await RunSafeAsync(async () =>
    {
        var arr = await ApiClient.GetRawAsync("/api/HopDong");
        var list = new ObservableCollection<HopDongItem>();
        foreach (var el in arr.EnumerateArray())
            list.Add(MapItem(el));
        Items = list;
    });

    private static HopDongItem MapItem(System.Text.Json.JsonElement el)
    {
        decimal.TryParse(ApiClient.ElStr(el, "giaThueThang"), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var gia);
        decimal.TryParse(ApiClient.ElStr(el, "tienCoc"), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var coc);
        return new HopDongItem
        {
            MaHopDong = ApiClient.ElStr(el, "maHopDong"),
            MaPhong = ApiClient.ElStr(el, "maPhong"),
            TenPhong = LookupData.Display(ApiClient.ElStr(el, "tenPhong"), ApiClient.ElStr(el, "maPhong")),
            MaKhachDaiDien = ApiClient.ElStr(el, "maKhachDaiDien"),
            HoTenKhach = LookupData.Display(ApiClient.ElStr(el, "hoTenKhach"), ApiClient.ElStr(el, "maKhachDaiDien")),
            NgayBatDau = ApiClient.ElStr(el, "ngayBatDau"),
            NgayKetThuc = ApiClient.ElStr(el, "ngayKetThuc"),
            GiaThueThang = gia,
            GiaThueThangDisplay = VnMoney.FormatD(gia),
            TienCoc = coc,
            TienCocDisplay = VnMoney.FormatD(coc),
            TrangThai = ApiClient.ElStr(el, "trangThai")
        };
    }

    [RelayCommand]
    private async Task TaoHopDongAsync()
    {
        var dlg = new Views.Dialogs.HopDongDialog();
        if (dlg.ShowDialog() == true) await LoadAsync();
    }

    [RelayCommand]
    private async Task GiaHanAsync()
    {
        if (SelectedItem == null) { ErrorMessage = "Vui lòng chọn một hợp đồng."; return; }
        if (SelectedItem.TrangThai != "Đang hiệu lực") { ErrorMessage = "Chỉ gia hạn hợp đồng đang hiệu lực."; return; }
        var dlg = new Views.Dialogs.GiaHanDialog(SelectedItem.MaHopDong,
            DateTime.TryParse(SelectedItem.NgayKetThuc, out var end) ? end : null,
            $"{SelectedItem.TenPhong} · {SelectedItem.HoTenKhach}");
        if (dlg.ShowDialog() == true) await LoadAsync();
    }

    [RelayCommand]
    private async Task ThanhLyAsync()
    {
        if (IsBusy) return;
        if (SelectedItem == null) { ErrorMessage = "Vui lòng chọn một hợp đồng."; return; }
        if (SelectedItem.TrangThai != "Đang hiệu lực") { ErrorMessage = "Chỉ thanh lý hợp đồng đang hiệu lực."; return; }
        var selected = SelectedItem;
        // Fetch outstanding debt
        decimal tongNo = 0;
        await RunSafeAsync(async () =>
        {
            var arr = await ApiClient.GetRawAsync($"/api/HoaDon/hop-dong/{selected.MaHopDong}");
            foreach (var el in arr.EnumerateArray())
            {
                decimal.TryParse(ApiClient.ElStr(el, "conLai"), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var con);
                tongNo += con;
            }
        });
        if (!string.IsNullOrEmpty(ErrorMessage)) return;
        var dlg = new Views.Dialogs.ThanhLyDialog(selected.MaHopDong, selected.TienCoc, tongNo,
            $"{selected.TenPhong} · {selected.HoTenKhach}");
        if (dlg.ShowDialog() == true) await LoadAsync();
    }

    [RelayCommand]
    private async Task XuatHopDongAsync()
    {
        if (SelectedItem == null || !KiemTra.Ma(SelectedItem.MaHopDong, out _))
        { ErrorMessage = "Vui lòng chọn một hợp đồng đã lưu."; return; }
        string id = SelectedItem.MaHopDong;
        await RunSafeAsync(async () =>
        {
            var contract = await ApiClient.GetRawAsync($"/api/HopDong/{id}");
            string roomId = ApiClient.ElStr(contract, "maPhong");
            string customerId = ApiClient.ElStr(contract, "maKhachDaiDien");
            if (!KiemTra.Ma(roomId, out _) || !KiemTra.Ma(customerId, out _))
                throw new InvalidDataException("Hợp đồng thiếu phòng hoặc khách đại diện.");
            var room = await ApiClient.GetRawAsync($"/api/Phong/{roomId}");
            var customer = await ApiClient.GetRawAsync($"/api/KhachThue/{customerId}");
            SaveHtml($"HopDong_{id}.html", BieuMauHtml.HopDong(contract, room, customer));
        });
    }

    [RelayCommand]
    private async Task XuatBanGiaoAsync()
    {
        if (SelectedItem == null || !KiemTra.Ma(SelectedItem.MaHopDong, out _))
        { ErrorMessage = "Vui lòng chọn một hợp đồng đã lưu."; return; }
        string id = SelectedItem.MaHopDong;
        await RunSafeAsync(async () =>
        {
            var records = await ApiClient.GetRawAsync($"/api/BanGiao/hop-dong/{id}");
            var list = records.EnumerateArray().ToList();
            if (list.Count == 0)
                throw new InvalidDataException("Hợp đồng này chưa có biên bản bàn giao đã lưu.");
            var picker = new Views.Dialogs.ChonBanGiaoDialog(list)
                { Owner = Application.Current.MainWindow };
            if (picker.ShowDialog() != true) return;
            string recordId = ApiClient.ElStr(picker.SelectedRecord, "maBanGiao");
            SaveHtml($"BanGiao_{recordId}.html", BieuMauHtml.BanGiao(picker.SelectedRecord));
        });
    }

    private static void SaveHtml(string fileName, string html)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Lưu biểu mẫu",
            Filter = "Trang HTML (*.html)|*.html",
            FileName = fileName,
            DefaultExt = ".html",
            AddExtension = true,
            OverwritePrompt = true
        };
        if (dialog.ShowDialog(Application.Current.MainWindow) != true) return;
        File.WriteAllText(dialog.FileName, html, new UTF8Encoding(false));
        try { Process.Start(new ProcessStartInfo(dialog.FileName) { UseShellExecute = true }); }
        catch { MessageBox.Show($"Đã lưu biểu mẫu tại:\n{dialog.FileName}", "Xuất biểu mẫu"); }
    }

    [RelayCommand]
    private async Task XemSapHetHanAsync()
    {
        ShowSapHetHan = !ShowSapHetHan;
        if (ShowSapHetHan) await LoadSapHetHanAsync();
    }

    private async Task LoadSapHetHanAsync() => await RunSafeAsync(async () =>
    {
        while (ShowSapHetHan)
        {
            var days = SoNgaySapHetHan;
            var arr = await ApiClient.GetRawAsync($"/api/HopDong/sap-het-han?days={days}");
            if (!ShowSapHetHan) return;
            if (days != SoNgaySapHetHan) continue;
            var list = new ObservableCollection<HopDongItem>();
            foreach (var el in arr.EnumerateArray()) list.Add(MapItem(el));
            SapHetHanItems = list;
            return;
        }
    });
}

public class HopDongItem
{
    public string MaHopDong { get; set; } = "";
    public string MaPhong { get; set; } = "";
    public string TenPhong { get; set; } = "";
    public string MaKhachDaiDien { get; set; } = "";
    public string HoTenKhach { get; set; } = "";
    public string NgayBatDau { get; set; } = "";
    public string NgayKetThuc { get; set; } = "";
    public decimal GiaThueThang { get; set; }
    public string GiaThueThangDisplay { get; set; } = "";
    public decimal TienCoc { get; set; }
    public string TienCocDisplay { get; set; } = "";
    public string TrangThai { get; set; } = "";
}
