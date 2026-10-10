using System.Windows;
using QLPT.WPF.Services;
using QLPT.WPF.ViewModels;

namespace QLPT.WPF.Views.Dialogs;

/// <summary>Bước "Xử lý" của quy trình vi phạm; tùy chọn báo kết quả cho khách.</summary>
public partial class XuLyViPhamDialog : Window
{
    private readonly ViPhamItem _item;

    public XuLyViPhamDialog(ViPhamItem item)
    {
        InitializeComponent();
        _item = item;
        TxtInfo.Text = $"#{item.MaViPham} · {item.TenPhong} · {item.TenKhach}\n{item.NoiDung}";
        TxtTienPhat.Text = item.TienPhatSo > 0 ? VnMoney.Format(item.TienPhatSo) : "";
        CbHinhThuc.Text = string.IsNullOrWhiteSpace(item.HinhThucXuLy)
            ? (item.TienPhatSo > 0 ? "Phạt tiền" : "Nhắc nhở")
            : item.HinhThucXuLy;
    }

    private async void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        TxtError.Text = "";
        string hinhThuc = CbHinhThuc.Text.Trim();
        if (hinhThuc.Length == 0) { TxtError.Text = "Chọn hoặc nhập hình thức xử lý."; return; }

        decimal? tienPhat = null;
        if (!string.IsNullOrWhiteSpace(TxtTienPhat.Text))
        {
            if (!VnMoney.TryParse(TxtTienPhat.Text, out decimal t) || t < 0)
            { TxtError.Text = "Tiền phạt không hợp lệ."; return; }
            tienPhat = t;
        }

        BtnSave.IsEnabled = false;
        try
        {
            await ApiClient.PutAsync($"/api/ViPham/{_item.MaViPham}/xu-ly", new
            {
                hinhThucXuLy = hinhThuc,
                tienPhat,
                guiThongBao = ChkThongBao.IsChecked == true
            });
            DialogResult = true;
        }
        catch (Exception ex)
        {
            TxtError.Text = ex.Message;
            BtnSave.IsEnabled = true;
        }
    }
}
