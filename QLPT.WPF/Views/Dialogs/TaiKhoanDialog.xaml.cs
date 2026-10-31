using QLPT.WPF.Services;
using QLPT.WPF.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace QLPT.WPF.Views.Dialogs;

public partial class TaiKhoanDialog : Window
{
    private readonly TaiKhoanItem? _item;
    private readonly bool _isEdit;

    public TaiKhoanDialog(TaiKhoanItem? item)
    {
        InitializeComponent();
        _item = item;
        _isEdit = item != null;
        Loaded += async (_, _) => await LoadCoSoAsync();

        if (_isEdit)
        {
            TxtTitle.Text = "Sửa tài khoản";
            TxtTenDangNhap.Text = item!.TenDangNhap;
            TxtTenDangNhap.IsReadOnly = true;
            // Cho admin đặt lại mật khẩu khi khách quên / gõ nhầm lúc đăng ký.
            MaterialDesignThemes.Wpf.HintAssist.SetHint(TxtMatKhau, "Mật khẩu mới (để trống = giữ nguyên)");
            PickMaKhach.SelectedId = item.MaKhach;
            PickMaKhach.ReadOnly = true; // API sửa tài khoản không cho đổi hồ sơ khách liên kết.
            PickMaKhach.Hint = "Khách liên kết (cố định)";

            foreach (ComboBoxItem ci in CboVaiTro.Items)
            {
                if (ci.Content?.ToString() == item.VaiTro)
                {
                    CboVaiTro.SelectedItem = ci;
                    break;
                }
            }
        }
    }

    private async Task LoadCoSoAsync()
    {
        try
        {
            var arr = await ApiClient.GetRawAsync("/api/CoSo");
            var list = arr.EnumerateArray().Select(x => new CoSoOption(
                int.Parse(ApiClient.ElStr(x, "maCoSo")), ApiClient.ElStr(x, "tenCoSo"))).ToList();
            ListCoSo.ItemsSource = list;
            foreach (var site in list.Where(x => _item?.MaCoSos.Contains(x.MaCoSo) == true))
                ListCoSo.SelectedItems.Add(site);
        }
        catch (Exception ex) { TxtError.Text = ex.Message; }
    }

    private void CboVaiTro_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PanelCoSo == null || PickMaKhach == null || TxtRoleHint == null) return;
        string role = (CboVaiTro.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Customer";
        PanelCoSo.Visibility = role == "Manager" ? Visibility.Visible : Visibility.Collapsed;
        PickMaKhach.Visibility = role == "Customer" ? Visibility.Visible : Visibility.Collapsed;
        TxtRoleHint.Text = role switch
        {
            "Admin" => "Admin: chủ trọ có quyền quản trị toàn bộ cơ sở và tài khoản trên desktop.",
            "Manager" => "Manager: đăng nhập desktop và chỉ thao tác tại cơ sở được phân công.",
            "BackupOperator" => "BackupOperator: chỉ xem và tạo bản sao lưu; không xem dữ liệu vận hành hay tài khoản.",
            _ => "Customer: chủ trọ cấp cho khách dùng ứng dụng mobile; không đăng nhập WPF."
        };
    }

    private async void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        TxtError.Text = "";

        var tenDangNhap = TxtTenDangNhap.Text.Trim();
        var vaiTro = (CboVaiTro.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Customer";

        if (!_isEdit)
        {
            if (string.IsNullOrEmpty(tenDangNhap))
            {
                TxtError.Text = "Tên đăng nhập không được để trống.";
                return;
            }
            if (!KiemTra.TenDangNhap(tenDangNhap))
            {
                TxtError.Text = "Tên đăng nhập 3–50 ký tự, chỉ gồm chữ không dấu, số, dấu chấm hoặc gạch dưới.";
                return;
            }
            if (string.IsNullOrEmpty(TxtMatKhau.Password))
            {
                TxtError.Text = "Mật khẩu không được để trống.";
                return;
            }
            if (TxtMatKhau.Password.Length < 6)
            {
                TxtError.Text = "Mật khẩu tối thiểu 6 ký tự.";
                return;
            }
        }

        // Trước đây gõ sai mã khách thì bị bỏ qua, rồi API báo "phải gắn mã khách" khó hiểu.
        int? maKhach = null;
        if (!_isEdit && !string.IsNullOrWhiteSpace(PickMaKhach.SelectedId))
        {
            if (!PickMaKhach.TryGetSelectedId(out var mk))
            {
                TxtError.Text = "Vui lòng chọn khách thuê từ danh sách đã tải.";
                return;
            }
            maKhach = mk;
        }
        if (!_isEdit && vaiTro == "Customer" && maKhach == null)
        {
            TxtError.Text = "Tài khoản khách thuê phải chọn một hồ sơ khách thuê.";
            return;
        }
        var maCoSos = vaiTro == "Manager" ? ListCoSo.SelectedItems.Cast<CoSoOption>().Select(x => x.MaCoSo).ToList() : new List<int>();
        if (vaiTro == "Manager" && maCoSos.Count == 0)
        { TxtError.Text = "Quản lý phải được giao ít nhất một cơ sở."; return; }

        BtnSave.IsEnabled = false;
        try
        {
            using var saveGuard = new DialogSaveGuard(this);
            if (_isEdit)
            {
                string? matKhauMoi = string.IsNullOrEmpty(TxtMatKhau.Password) ? null : TxtMatKhau.Password;
                if (matKhauMoi != null && matKhauMoi.Length < 6)
                {
                    TxtError.Text = "Mật khẩu mới tối thiểu 6 ký tự.";
                    return;
                }
                var body = new { vaiTro = vaiTro, matKhau = matKhauMoi, maCoSos };
                await ApiClient.PutAsync($"/api/TaiKhoan/{_item!.MaTaiKhoan}", body);
                if (matKhauMoi != null)
                    ConfirmDialog.Info("Đã đặt lại mật khẩu",
                        "Báo cho chủ tài khoản đăng nhập bằng mật khẩu mới.", _item.TenDangNhap, isError: false);
            }
            else
            {
                var body = new
                {
                    tenDangNhap = tenDangNhap,
                    matKhau = TxtMatKhau.Password,
                    vaiTro = vaiTro,
                    maKhach = maKhach,
                    maCoSos
                };
                await ApiClient.PostAsync("/api/TaiKhoan", body);
            }
            saveGuard.Complete();
            DialogResult = true;
        }
        catch (Exception ex)
        {
            TxtError.Text = ex.Message;
        }
        finally
        {
            BtnSave.IsEnabled = true;
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}

internal sealed record CoSoOption(int MaCoSo, string TenCoSo);
