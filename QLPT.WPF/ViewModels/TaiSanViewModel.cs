using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QLPT.WPF.Services;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows;

namespace QLPT.WPF.ViewModels;

public partial class TaiSanViewModel : BaseViewModel
{
    public bool CanManageStock => Session.IsAdmin;
    [ObservableProperty] private ObservableCollection<TaiSanItem> _allItems = new();
    [ObservableProperty] private ObservableCollection<TaiSanPhongItem> _phongItems = new();
    [ObservableProperty] private ObservableCollection<TaiSanCaTheItem> _caTheItems = new();
    [ObservableProperty] private TaiSanItem? _selectedItem;
    [ObservableProperty] private TaiSanPhongItem? _selectedPhongItem;
    [ObservableProperty] private TaiSanCaTheItem? _selectedCaTheItem;
    [ObservableProperty] private string _maPhongInput = "";
    [ObservableProperty] private string _caTheTitle = "Từng hiện vật trong phòng";
    private bool _showingAllUnits;
    private string? _filteredTemplateId;
    private bool _suppressTemplateSelectionLoad;

    public TaiSanViewModel() => _ = LoadAllAsync();

    partial void OnSelectedItemChanged(TaiSanItem? value)
    {
        if (value != null && !_suppressTemplateSelectionLoad) _ = LoadCaTheTheoMauAsync(value);
    }

    partial void OnMaPhongInputChanged(string value)
    {
        PhongItems = new();
        CaTheItems = new();
        SelectedPhongItem = null;
        SelectedCaTheItem = null;
        ErrorMessage = "";
        _showingAllUnits = false;
        _filteredTemplateId = null;
        CaTheTitle = "Từng hiện vật trong phòng";
    }

    [RelayCommand]
    private async Task LoadAllAsync() => await RunSafeAsync(async () =>
    {
        var oldSelectedId = SelectedItem?.MaTaiSan;
        var arr = await ApiClient.GetRawAsync("/api/TaiSan");
        var stock = CanManageStock ? await ApiClient.GetRawAsync("/api/TaiSan/ton-kho") : default;
        var assignedSiteUnits = Session.IsManager ? await ApiClient.GetRawAsync("/api/TaiSan/ca-the") : default;
        var list = new ObservableCollection<TaiSanItem>();
        foreach (var el in arr.EnumerateArray())
        {
            decimal.TryParse(ApiClient.ElStr(el, "giaTriThamKhao"), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var gia);
            list.Add(new TaiSanItem
            {
                MaTaiSan = ApiClient.ElStr(el, "maTaiSan"),
                TenTaiSan = ApiClient.ElStr(el, "tenTaiSan"),
                GiaTriThamKhao = gia,
                GiaTriDisplay = VnMoney.FormatD(gia),
                TonKhoDisplay = CanManageStock ? "0" : "—",
                TrongPhongDisplay = "0"
            });
        }
        if (CanManageStock)
        {
            foreach (var row in stock.EnumerateArray())
            {
                var item = list.FirstOrDefault(x => x.MaTaiSan == ApiClient.ElStr(row, "maTaiSan"));
                if (item == null) continue;
                int.TryParse(ApiClient.ElStr(row, "soLuong"), out int count);
                switch (ApiClient.ElStr(row, "trangThai"))
                {
                    case "Trong kho": item.SoLuongTonKho = count; break;
                    case "Đang sử dụng": item.SoLuongTrongPhong = count; break;
                    case "Đã thanh lý": item.SoLuongDaThanhLy = count; break;
                }
            }
            foreach (var item in list)
            {
                item.TonKhoDisplay = item.SoLuongTonKho.ToString();
                item.TrongPhongDisplay = item.SoLuongTrongPhong.ToString();
            }
        }
        else if (Session.IsManager)
        {
            foreach (var unit in assignedSiteUnits.EnumerateArray())
            {
                var item = list.FirstOrDefault(x => x.MaTaiSan == ApiClient.ElStr(unit, "maTaiSan"));
                if (item != null) item.SoLuongTrongPhong++;
            }
            foreach (var item in list) item.TrongPhongDisplay = item.SoLuongTrongPhong.ToString();
        }
        AllItems = list;
        if (oldSelectedId != null)
        {
            _suppressTemplateSelectionLoad = true;
            try { SelectedItem = list.FirstOrDefault(x => x.MaTaiSan == oldSelectedId); }
            finally { _suppressTemplateSelectionLoad = false; }
        }
    });

    [RelayCommand]
    private async Task LoadPhongAsync() => await RunSafeAsync(async () =>
    {
        if (!KiemTra.Ma(MaPhongInput, out _))
        {
            ErrorMessage = "Vui lòng chọn phòng từ danh sách.";
            return;
        }
        var roomId = MaPhongInput;
        var arr = await ApiClient.GetRawAsync($"/api/TaiSan/phong/{roomId}");
        if (roomId != MaPhongInput) return;
        var list = new ObservableCollection<TaiSanPhongItem>();
        foreach (var el in arr.EnumerateArray())
        {
            int.TryParse(ApiClient.ElStr(el, "soLuong"), out var sl);
            list.Add(new TaiSanPhongItem
            {
                MaTaiSanPhong = ApiClient.ElStr(el, "maTaiSanPhong"),
                TenTaiSan = ApiClient.ElStr(el, "tenTaiSan"),
                SoLuong = sl,
                TinhTrang = ApiClient.ElStr(el, "tinhTrang")
            });
        }
        PhongItems = list;
        var units = await ApiClient.GetRawAsync($"/api/TaiSan/ca-the/phong/{roomId}");
        if (roomId != MaPhongInput) return;
        CaTheItems = ReadUnits(units);
        CaTheTitle = "Từng hiện vật trong phòng";
        _showingAllUnits = false;
        _filteredTemplateId = null;
    });

    [RelayCommand]
    private async Task LoadCaTheAllAsync() => await RunSafeAsync(async () =>
    {
        var units = await ApiClient.GetRawAsync("/api/TaiSan/ca-the");
        CaTheItems = ReadUnits(units);
        PhongItems = new();
        SelectedPhongItem = null;
        SelectedCaTheItem = null;
        CaTheTitle = Session.IsManager ? "Hiện vật tại cơ sở được giao" : "Toàn bộ hiện vật, kể cả kho và thanh lý";
        _showingAllUnits = true;
        _filteredTemplateId = null;
    });

    private async Task LoadCaTheTheoMauAsync(TaiSanItem item) => await RunSafeAsync(async () =>
    {
        var units = await ApiClient.GetRawAsync("/api/TaiSan/ca-the");
        if (SelectedItem?.MaTaiSan != item.MaTaiSan) return;
        CaTheItems = new ObservableCollection<TaiSanCaTheItem>(ReadUnits(units).Where(x => x.MaTaiSan == item.MaTaiSan));
        PhongItems = new();
        SelectedPhongItem = null;
        SelectedCaTheItem = null;
        CaTheTitle = $"{item.TenTaiSan}: {CaTheItems.Count} hiện vật có mã riêng";
        _showingAllUnits = true;
        _filteredTemplateId = item.MaTaiSan;
    });

    private static ObservableCollection<TaiSanCaTheItem> ReadUnits(JsonElement units)
    {
        var individuals = new ObservableCollection<TaiSanCaTheItem>();
        foreach (var el in units.EnumerateArray())
        {
            decimal.TryParse(ApiClient.ElStr(el, "tongChiPhiSua"), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var total);
            individuals.Add(new TaiSanCaTheItem
            {
                MaTaiSanCaThe = ApiClient.ElStr(el, "maTaiSanCaThe"),
                MaTaiSan = ApiClient.ElStr(el, "maTaiSan"),
                MaDinhDanh = ApiClient.ElStr(el, "maDinhDanh"),
                TenTaiSan = ApiClient.ElStr(el, "tenTaiSan"),
                TenPhong = ApiClient.ElStr(el, "tenPhong"),
                TenCoSo = ApiClient.ElStr(el, "tenCoSo"),
                TinhTrang = ApiClient.ElStr(el, "tinhTrang"),
                TrangThai = ApiClient.ElStr(el, "trangThai"),
                SoLanSua = ApiClient.ElStr(el, "soLanSua"),
                TongChiPhiSua = VnMoney.FormatD(total),
                NgayMua = DateTime.TryParse(ApiClient.ElStr(el, "ngayMua"), out var purchase) ? purchase : null,
                GiaMua = decimal.TryParse(ApiClient.ElStr(el, "giaMua"), System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var price) ? price : null,
                GhiChu = ApiClient.ElStr(el, "ghiChu")
            });
        }
        return individuals;
    }

    [RelayCommand]
    private async Task AddTaiSanAsync()
    {
        if (!CanManageStock) { ErrorMessage = "Chỉ chủ trọ được thêm mẫu tài sản dùng chung."; return; }
        var dlg = new Views.Dialogs.TaiSanDialog();
        if (dlg.ShowDialog() == true) await LoadAllAsync();
    }

    [RelayCommand]
    private async Task NhapKhoAsync()
    {
        if (!CanManageStock) { ErrorMessage = "Chỉ chủ trọ được nhập kho."; return; }
        if (SelectedItem is not { } item) { ErrorMessage = "Hãy chọn mẫu tài sản trước khi nhập kho."; return; }
        var dlg = new Views.Dialogs.KhoTaiSanDialog(item);
        if (dlg.ShowDialog() == true)
        {
            await LoadAllAsync();
            if (SelectedItem is { } selected) await LoadCaTheTheoMauAsync(selected);
        }
    }

    [RelayCommand]
    private async Task XuatKhoNhieuAsync()
    {
        if (!CanManageStock) { ErrorMessage = "Chỉ chủ trọ được chuyển hiện vật từ kho."; return; }
        if (SelectedItem is not { } item) { ErrorMessage = "Hãy chọn mẫu tài sản."; return; }
        if (!KiemTra.Ma(MaPhongInput, out int roomId)) { ErrorMessage = "Hãy chọn phòng nhận."; return; }
        if (item.SoLuongTonKho == 0) { ErrorMessage = "Mẫu này đã hết tồn kho."; return; }
        var dlg = new Views.Dialogs.KhoTaiSanDialog(item, roomId);
        if (dlg.ShowDialog() == true)
        {
            await LoadAllAsync();
            await LoadPhongAsync();
        }
    }

    [RelayCommand]
    private async Task GanVaoPhongAsync()
    {
        if (!CanManageStock) { ErrorMessage = "Chỉ chủ trọ được ghi nhận hiện vật mới vào phòng."; return; }
        if (SelectedItem == null) { ErrorMessage = "Vui lòng chọn tài sản mẫu."; return; }
        if (!KiemTra.Ma(MaPhongInput, out _)) { ErrorMessage = "Vui lòng chọn phòng từ danh sách."; return; }
        var dlg = new Views.Dialogs.GanTaiSanDialog(SelectedItem, MaPhongInput.Trim());
        if (dlg.ShowDialog() == true) await LoadPhongAsync();
    }

    [RelayCommand]
    private async Task XemLichSuAsync()
    {
        if (SelectedCaTheItem == null) { ErrorMessage = "Hãy chọn một hiện vật có mã riêng."; return; }
        var dlg = new Views.Dialogs.TaiSanLichSuDialog(SelectedCaTheItem);
        dlg.ShowDialog();
        if (_filteredTemplateId != null && SelectedItem is { } selected) await LoadCaTheTheoMauAsync(selected);
        else if (_showingAllUnits) await LoadCaTheAllAsync();
        else await LoadPhongAsync();
    }

    [RelayCommand]
    private async Task DuaTuKhoVaoPhongAsync()
    {
        if (!CanManageStock) { ErrorMessage = "Chỉ chủ trọ được điều chuyển hiện vật từ kho."; return; }
        if (SelectedCaTheItem is not { TrangThai: "Trong kho" } unit)
        { ErrorMessage = "Hãy chọn một hiện vật có trạng thái Trong kho từ danh sách tất cả hiện vật."; return; }
        if (!KiemTra.Ma(MaPhongInput, out var roomId))
        { ErrorMessage = "Vui lòng chọn phòng nhận từ danh sách."; return; }
        if (MessageBox.Show($"Đưa {unit.MaDinhDanh} · {unit.TenTaiSan} từ kho vào phòng đã chọn? Mã và lịch sử hiện vật sẽ được giữ nguyên.",
                "Điều chuyển hiện vật", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        await RunSafeAsync(async () =>
        {
            await ApiClient.PostAsync($"/api/TaiSan/ca-the/{unit.MaTaiSanCaThe}/gan-phong", new { maPhong = roomId });
            await LoadPhongAsync();
        });
    }

    [RelayCommand]
    private async Task GoCaTheVeKhoAsync()
    {
        if (!CanManageStock) { ErrorMessage = "Chỉ chủ trọ được điều chuyển hiện vật về kho."; return; }
        if (SelectedCaTheItem is not { TrangThai: "Đang sử dụng" } unit)
        { ErrorMessage = "Hãy chọn một hiện vật đang sử dụng trong phòng."; return; }
        if (MessageBox.Show($"Gỡ {unit.MaDinhDanh} · {unit.TenTaiSan} từ phòng {unit.TenPhong} về kho? Mã và lịch sử hiện vật sẽ được giữ nguyên.",
                "Điều chuyển hiện vật", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        await RunSafeAsync(async () =>
        {
            await ApiClient.PostAsync($"/api/TaiSan/ca-the/{unit.MaTaiSanCaThe}/go-kho", new { });
            if (_filteredTemplateId != null && SelectedItem is { } selected) await LoadCaTheTheoMauAsync(selected);
            else if (_showingAllUnits) await LoadCaTheAllAsync();
            else await LoadPhongAsync();
        });
    }
}

public sealed class TaiSanCaTheItem
{
    public string MaTaiSanCaThe { get; set; } = "";
    public string MaTaiSan { get; set; } = "";
    public string MaDinhDanh { get; set; } = "";
    public string TenTaiSan { get; set; } = "";
    public string TenPhong { get; set; } = "";
    public string TenCoSo { get; set; } = "";
    public string TinhTrang { get; set; } = "";
    public string TrangThai { get; set; } = "";
    public string SoLanSua { get; set; } = "";
    public string TongChiPhiSua { get; set; } = "";
    public DateTime? NgayMua { get; set; }
    public decimal? GiaMua { get; set; }
    public string GhiChu { get; set; } = "";
}

public class TaiSanItem
{
    public string MaTaiSan { get; set; } = "";
    public string TenTaiSan { get; set; } = "";
    public decimal GiaTriThamKhao { get; set; }
    public string GiaTriDisplay { get; set; } = "";
    public int SoLuongTonKho { get; set; }
    public int SoLuongTrongPhong { get; set; }
    public int SoLuongDaThanhLy { get; set; }
    public string TonKhoDisplay { get; set; } = "";
    public string TrongPhongDisplay { get; set; } = "";
}

// TaiSanPhongItem is defined in BanGiaoViewModel.cs
