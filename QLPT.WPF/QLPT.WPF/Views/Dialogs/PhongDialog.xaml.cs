using QLPT.WPF.Services;
using QLPT.WPF.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace QLPT.WPF.Views.Dialogs;

public partial class PhongDialog : Window
{
    private readonly PhongItem? _item;

    public PhongDialog(PhongItem? item)
    {
        InitializeComponent();
        _item = item;

        // Default values
        TxtMaLoaiPhong.Text = "1";
        TxtMaCoSo.Text = "1";
        TxtTang.Text = "1";
        CboTrangThai.SelectedIndex = 0; // Trống

        if (item != null)
        {
            TxtTitle.Text = "Sửa phòng";
            TxtTenPhong.Text = item.TenPhong;
            TxtMaLoaiPhong.Text = item.MaLoaiPhong.ToString();
            TxtGiaThue.Text = item.GiaThue > 0 ? item.GiaThue.ToString() : "";
            TxtMaCoSo.Text = item.MaCoSo.ToString();
            TxtTang.Text = item.Tang.ToString();
            TxtMoTa.Text = item.MoTa;

            // Set combobox
            foreach (ComboBoxItem ci in CboTrangThai.Items)
            {
                if (ci.Content?.ToString() == item.TrangThai)
                {
                    CboTrangThai.SelectedItem = ci;
                    break;
                }
            }
        }
    }

    private async void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        TxtError.Text = "";
        var ten = TxtTenPhong.Text.Trim();
        if (string.IsNullOrEmpty(ten))
        {
            TxtError.Text = "Tên phòng không được để trống.";
            return;
        }

        // Trước đây nhập sai bị lặng lẽ thay bằng 1 → phòng gắn nhầm loại / cơ sở.
        if (!KiemTra.Ma(TxtMaLoaiPhong.Text, out var maLoai))
        {
            TxtError.Text = "Mã loại phòng phải là số nguyên dương (xem ở trang Loại phòng).";
            return;
        }
        if (!KiemTra.Ma(TxtMaCoSo.Text, out var maCoSo))
        {
            TxtError.Text = "Mã cơ sở phải là số nguyên dương (xem ở trang Cơ sở).";
            return;
        }
        if (!KiemTra.SoNguyen(TxtTang.Text, 0, 100, out var tang))
        {
            TxtError.Text = "Tầng phải là số nguyên từ 0 đến 100.";
            return;
        }

        decimal giaThue = 0;
        if (!string.IsNullOrWhiteSpace(TxtGiaThue.Text) && !VnMoney.TryParse(TxtGiaThue.Text, out giaThue))
        {
            TxtError.Text = "Giá thuê không hợp lệ.";
            return;
        }

        var trangThai = (CboTrangThai.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Trống";

        BtnSave.IsEnabled = false;
        try
        {
            var body = new
            {
                tenPhong = ten,
                maLoaiPhong = maLoai,
                giaThue = giaThue,
                trangThai = trangThai,
                maCoSo = maCoSo,
                tang = tang,
                moTa = TxtMoTa.Text.Trim()
            };

            if (_item == null)
                await ApiClient.PostAsync("/api/Phong", body);
            else
                await ApiClient.PutAsync($"/api/Phong/{_item.MaPhong}", body);

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
