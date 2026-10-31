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
        TxtTang.Text = "1";
        CboTrangThai.SelectedIndex = 0; // Trống

        if (item != null)
        {
            TxtTitle.Text = "Sửa phòng";
            TxtTenPhong.Text = item.TenPhong;
            PickMaLoaiPhong.SelectedId = item.MaLoaiPhong.ToString();
            TxtGiaThue.Text = item.GiaThue > 0 ? item.GiaThue.ToString() : "";
            PickMaCoSo.SelectedId = item.MaCoSo.ToString();
            TxtTang.Text = item.Tang.ToString();
            TxtMoTa.Text = item.MoTa;
            if (item.CoLichSu)
            {
                TxtHistoryNotice.Visibility = Visibility.Visible;
                TxtTenPhong.IsReadOnly = true;
                TxtTang.IsReadOnly = true;
                PickMaCoSo.ReadOnly = true;
                PickMaLoaiPhong.ReadOnly = true;
                TxtTenPhong.ToolTip = TxtTang.ToolTip =
                    "Phòng đã có hợp đồng hoặc phiếu đặt; không thể đổi tên, tầng, loại phòng hay cơ sở vì sẽ làm sai lịch sử.";
                PickMaLoaiPhong.ToolTip = PickMaCoSo.ToolTip = TxtTenPhong.ToolTip;
            }

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
        if (!PickMaLoaiPhong.TryGetSelectedId(out var maLoai))
        {
            TxtError.Text = "Vui lòng chọn loại phòng từ danh sách đã tải.";
            return;
        }
        if (!PickMaCoSo.TryGetSelectedId(out var maCoSo))
        {
            TxtError.Text = "Vui lòng chọn cơ sở từ danh sách đã tải.";
            return;
        }
        if (!KiemTra.SoNguyen(TxtTang.Text, 0, 100, out var tang))
        {
            TxtError.Text = "Tầng phải là số nguyên từ 0 đến 100.";
            return;
        }

        if (!VnMoney.TryParse(TxtGiaThue.Text, out var giaThue) || giaThue < 100_000)
        {
            TxtError.Text = "Giá thuê phải từ 100.000 đ trở lên.";
            return;
        }

        var trangThai = (CboTrangThai.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Trống";

        BtnSave.IsEnabled = false;
        try
        {
            using var saveGuard = new DialogSaveGuard(this);
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
