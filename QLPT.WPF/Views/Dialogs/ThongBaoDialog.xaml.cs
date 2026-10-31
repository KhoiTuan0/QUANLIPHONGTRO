using QLPT.WPF.Services;
using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;

namespace QLPT.WPF.Views.Dialogs;

public partial class ThongBaoDialog : Window
{
    private List<RecipientOption> _recipients = new();
    private bool _recipientsReady;
    private bool _loadingRecipients;

    public ThongBaoDialog()
    {
        InitializeComponent();
        Loaded += async (_, _) => await LoadRecipientsAsync();
    }

    private async Task LoadRecipientsAsync()
    {
        if (_loadingRecipients) return;
        _loadingRecipients = true;
        _recipientsReady = false;
        RecipientPanel.IsEnabled = false;
        TxtRecipientStatus.Text = "Đang tải người nhận…";
        var selected = _recipients.Where(x => x.IsSelected).Select(x => x.Id).ToHashSet();
        try
        {
            var accountsTask = ApiClient.GetRawAsync("/api/TaiKhoan");
            var tenantsTask = ApiClient.GetRawAsync("/api/KhachThue");
            await Task.WhenAll(accountsTask, tenantsTask);
            var tenants = tenantsTask.Result.EnumerateArray().ToDictionary(x => ApiClient.ElStr(x, "maKhach"));
            var result = new List<RecipientOption>();
            foreach (var x in accountsTask.Result.EnumerateArray())
            {
                if (!string.Equals(ApiClient.ElStr(x, "vaiTro"), "Customer", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(ApiClient.ElStr(x, "trangThai"), "Active", StringComparison.OrdinalIgnoreCase)) continue;
                if (!int.TryParse(ApiClient.ElStr(x, "maTaiKhoan"), out var id) || id <= 0) continue;
                var username = ApiClient.ElStr(x, "tenDangNhap");
                var tenantId = ApiClient.ElStr(x, "maKhach");
                tenants.TryGetValue(tenantId, out var tenant);
                var name = LookupData.Display(ApiClient.ElStr(tenant, "hoTen"), tenantId);
                var option = new RecipientOption(id, $"{name} · {username}",
                    $"{name} {username} {ApiClient.ElStr(tenant, "cccd")} {ApiClient.ElStr(tenant, "soDienThoai")}") { IsSelected = selected.Contains(id) };
                option.PropertyChanged += (_, _) => UpdateRecipientStatus();
                result.Add(option);
            }
            _recipients = result.OrderBy(x => x.Label, StringComparer.CurrentCultureIgnoreCase).ToList();
            _recipientsReady = true;
            FilterRecipients();
        }
        catch (Exception ex) { TxtRecipientStatus.Text = $"Không tải được người nhận: {ex.Message}"; }
        finally
        {
            _loadingRecipients = false;
            RecipientPanel.IsEnabled = _recipientsReady && CbAll.IsChecked != true;
        }
    }

    private void FilterRecipients()
    {
        if (!_recipientsReady || ListRecipients == null) return;
        var query = TextUtil.Normalize(TxtSearchRecipient.Text.Trim());
        ListRecipients.ItemsSource = _recipients.Where(x => TextUtil.Normalize(x.SearchText).Contains(query)).ToList();
        UpdateRecipientStatus();
    }

    private void UpdateRecipientStatus()
    {
        if (TxtRecipientStatus == null || !_recipientsReady) return;
        TxtRecipientStatus.Text = _recipients.Count == 0 ? "Chưa có tài khoản khách thuê đang hoạt động." :
            $"{ListRecipients.Items.Count}/{_recipients.Count} người nhận · Đã chọn {_recipients.Count(x => x.IsSelected)}";
    }

    private void RecipientSearch_Changed(object sender, TextChangedEventArgs e) => FilterRecipients();
    private void All_Changed(object sender, RoutedEventArgs e)
    {
        if (RecipientPanel != null) RecipientPanel.IsEnabled = _recipientsReady && CbAll.IsChecked != true;
    }

    private async void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        TxtError.Text = "";

        if (string.IsNullOrWhiteSpace(TxtTieuDe.Text))
        {
            TxtError.Text = "Nhập tiêu đề."; return;
        }
        if (string.IsNullOrWhiteSpace(TxtNoiDung.Text))
        {
            TxtError.Text = "Nhập nội dung."; return;
        }

        var loai = (CbLoai.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Hệ thống";

        if (!_recipientsReady || _recipients.Count == 0)
        { TxtError.Text = "Vui lòng tải danh sách người nhận đang hoạt động trước khi gửi."; return; }
        var nguoiNhan = _recipients.Where(x => x.IsSelected).Select(x => x.Id).ToList();
        bool guiTatCa = CbAll.IsChecked == true;
        if (!guiTatCa && nguoiNhan.Count == 0)
        { TxtError.Text = "Vui lòng chọn ít nhất một người nhận hoặc chọn gửi cho tất cả."; return; }
        if (guiTatCa && !ConfirmDialog.Ask("Gửi cho tất cả?",
                $"Thông báo sẽ được gửi tới {_recipients.Count} tài khoản khách thuê đang hoạt động.",
                TxtTieuDe.Text.Trim(), "Gửi tất cả", warn: true))
            return;

        BtnSave.IsEnabled = false;
        try
        {
            using var saveGuard = new DialogSaveGuard(this);
            // Khớp GuiThongBaoDto: trước đây gửi "loai"/"maTaiKhoanNhan" nên thông báo
            // được lưu nhưng không tạo dòng người nhận nào — không ai thấy.
            await ApiClient.PostAsync("/api/ThongBao", new
            {
                tieuDe = TxtTieuDe.Text.Trim(),
                noiDung = TxtNoiDung.Text.Trim(),
                loaiThongBao = loai,
                guiChoTatCa = guiTatCa,
                danhSachMaTaiKhoan = guiTatCa ? null : nguoiNhan
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

public sealed class RecipientOption : INotifyPropertyChanged
{
    public RecipientOption(int id, string label, string searchText) { Id = id; Label = label; SearchText = searchText; }
    public int Id { get; }
    public string Label { get; }
    public string SearchText { get; }
    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set { if (_isSelected == value) return; _isSelected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected))); }
    }
    public event PropertyChangedEventHandler? PropertyChanged;
}
