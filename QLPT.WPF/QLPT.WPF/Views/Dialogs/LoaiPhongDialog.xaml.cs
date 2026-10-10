using QLPT.WPF.Services;
using QLPT.WPF.ViewModels;
using System.Windows;

namespace QLPT.WPF.Views.Dialogs;

public partial class LoaiPhongDialog : Window
{
    private readonly LoaiPhongItem? _item;

    public LoaiPhongDialog(LoaiPhongItem? item)
    {
        InitializeComponent();
        _item = item;
        if (item != null)
        {
            TxtTitle.Text = "Sửa loại phòng";
            TxtTen.Text = item.TenLoaiPhong;
            TxtDienTich.Text = item.DienTich > 0 ? item.DienTich.ToString() : "";
            TxtSoNguoi.Text = item.SoNguoiToiDa > 0 ? item.SoNguoiToiDa.ToString() : "";
            TxtGia.Text = item.GiaMacDinh > 0 ? item.GiaMacDinh.ToString() : "";
            TxtMoTa.Text = item.MoTa;
        }
    }

    private async void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        TxtError.Text = "";
        var ten = TxtTen.Text.Trim();
        if (string.IsNullOrEmpty(ten))
        {
            TxtError.Text = "Tên loại phòng không được để trống.";
            return;
        }

        // Trước đây nhập sai bị lặng lẽ đổi thành 0; riêng số người = 0 làm API hiểu là
        // "phòng tối đa 0 người" và chặn mọi lượt thêm cư trú.
        decimal dienTich = 0;
        if (!string.IsNullOrWhiteSpace(TxtDienTich.Text) &&
            (!KiemTra.SoKhongAm(TxtDienTich.Text, out dienTich) || dienTich <= 0 || dienTich > 1000))
        {
            TxtError.Text = "Diện tích phải là số lớn hơn 0 và không quá 1000 m².";
            return;
        }
        if (!KiemTra.SoNguyen(TxtSoNguoi.Text, 1, 20, out var soNguoi))
        {
            TxtError.Text = "Số người tối đa phải là số nguyên từ 1 đến 20.";
            return;
        }

        decimal giaMacDinh = 0;
        if (!string.IsNullOrWhiteSpace(TxtGia.Text) && !VnMoney.TryParse(TxtGia.Text, out giaMacDinh))
        {
            TxtError.Text = "Giá mặc định không hợp lệ.";
            return;
        }

        BtnSave.IsEnabled = false;
        try
        {
            var body = new
            {
                tenLoaiPhong = ten,
                dienTich = dienTich,
                soNguoiToiDa = soNguoi,
                giaMacDinh = giaMacDinh,
                moTa = TxtMoTa.Text.Trim()
            };

            if (_item == null)
                await ApiClient.PostAsync("/api/LoaiPhong", body);
            else
                await ApiClient.PutAsync($"/api/LoaiPhong/{_item.MaLoaiPhong}", body);

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
