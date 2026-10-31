using QLPT.WPF.Services;
using QLPT.WPF.ViewModels;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace QLPT.WPF.Views.Dialogs;

public partial class KhachThueDialog : Window
{
    private readonly KhachThueItem? _item;

    public KhachThueDialog(KhachThueItem? item)
    {
        InitializeComponent();
        _item = item;
        if (Session.IsManager && item == null)
        {
            PickCoSo.Visibility = Visibility.Visible;
            BtnTiepNhan.Visibility = Visibility.Visible;
        }

        if (item != null)
        {
            TxtTitle.Text = "Sửa khách thuê";
            TxtHoTen.Text = item.HoTen;
            TxtCccd.Text = item.Cccd;
            TxtSdt.Text = item.SoDienThoai;
            TxtEmail.Text = item.Email;
            TxtDiaChi.Text = item.DiaChi;

            foreach (ComboBoxItem ci in CboGioiTinh.Items)
            {
                if (ci.Content?.ToString() == item.GioiTinh)
                {
                    CboGioiTinh.SelectedItem = ci;
                    break;
                }
            }
        }
    }

    private async void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        TxtError.Text = "";

        var hoTen = TxtHoTen.Text.Trim();
        if (string.IsNullOrEmpty(hoTen))
        {
            TxtError.Text = "Họ tên không được để trống.";
            return;
        }
        if (!KiemTra.HoTen(hoTen))
        {
            TxtError.Text = "Họ tên chỉ gồm chữ cái và khoảng trắng (2–100 ký tự).";
            return;
        }

        var cccd = TxtCccd.Text.Trim();
        if (!Regex.IsMatch(cccd, @"^\d{12}$"))
        {
            TxtError.Text = "CCCD phải gồm đúng 12 chữ số.";
            return;
        }

        var sdt = TxtSdt.Text.Trim();
        if (!Regex.IsMatch(sdt, @"^0\d{9}$"))
        {
            TxtError.Text = "SĐT phải gồm 10 chữ số và bắt đầu bằng 0.";
            return;
        }
        var email = TxtEmail.Text.Trim();
        if (string.IsNullOrWhiteSpace(TxtDiaChi.Text))
        {
            TxtError.Text = "Địa chỉ không được để trống.";
            return;
        }
        if (!string.IsNullOrEmpty(email) && !KiemTra.Email(email))
        {
            TxtError.Text = "Email không đúng định dạng (VD: ten@gmail.com).";
            return;
        }

        var gioiTinh = (CboGioiTinh.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Nam";
        int? siteId = null;
        if (_item == null && Session.IsManager)
        {
            if (!PickCoSo.TryGetSelectedId(out int selectedSite))
            { TxtError.Text = "Chọn cơ sở tiếp nhận khách."; return; }
            siteId = selectedSite;
        }

        BtnSave.IsEnabled = false;
        try
        {
            using var saveGuard = new DialogSaveGuard(this);
            var body = new
            {
                hoTen = hoTen,
                cccd = cccd,
                soDienThoai = sdt,
                gioiTinh = gioiTinh,
                email = TxtEmail.Text.Trim(),
                diaChi = TxtDiaChi.Text.Trim(),
                maCoSoTiepNhan = siteId
            };

            if (_item == null)
                await ApiClient.PostAsync("/api/KhachThue", body);
            else
                await ApiClient.PutAsync($"/api/KhachThue/{_item.MaKhach}", body);

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

    private async void TiepNhan_Click(object sender, RoutedEventArgs e)
    {
        TxtError.Text = "";
        if (!PickCoSo.TryGetSelectedId(out int siteId))
        { TxtError.Text = "Chọn cơ sở tiếp nhận trước."; return; }
        var cccd = TxtCccd.Text.Trim();
        if (!Regex.IsMatch(cccd, @"^\d{12}$"))
        { TxtError.Text = "Nhập đúng CCCD 12 chữ số của khách đã có hồ sơ."; return; }
        BtnTiepNhan.IsEnabled = false;
        try
        {
            using var guard = new DialogSaveGuard(this);
            await ApiClient.PostAsync("/api/KhachThue/tiep-nhan", new { cccd, maCoSo = siteId });
            guard.Complete();
            DialogResult = true;
        }
        catch (Exception ex) { TxtError.Text = ex.Message; }
        finally { BtnTiepNhan.IsEnabled = true; }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
