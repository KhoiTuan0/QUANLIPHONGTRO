using System.Data;
using QLPT.WinForms.Services;

namespace QLPT.WinForms.Forms;

public sealed class TaiKhoanForm : CrudForm
{
    public TaiKhoanForm() : base("Tài khoản / Cấp quyền khách thuê")
    {
        bEdit.Text = "Mở khóa"; bDel.Text = "Khóa";
        var reset = new Button { Text = "Đổi mật khẩu", AutoSize = true }; bar.Controls.Add(reset);
        reset.Click += async (_, _) => await Safe(Reset);
    }
    protected override async Task LoadData()
    {
        var rows = await ApiClient.GetRawAsync("/api/TaiKhoan");
        var table = new DataTable(); foreach (string c in new[] { "Mã", "Tên đăng nhập", "Vai trò", "Mã khách", "Họ tên", "Trạng thái" }) table.Columns.Add(c);
        foreach (var t in rows.EnumerateArray()) table.Rows.Add(ApiClient.ElStr(t, "maTaiKhoan"), ApiClient.ElStr(t, "tenDangNhap"),
            ApiClient.ElStr(t, "vaiTro"), ApiClient.ElStr(t, "maKhach"), ApiClient.ElStr(t, "hoTen"), ApiClient.ElStr(t, "trangThai"));
        grid.DataSource = table;
    }
    protected override async Task OnAdd()
    {
        var customers = (await ApiClient.GetRawAsync("/api/KhachThue")).EnumerateArray().ToArray();
        var username = new TextBox { Width = 240, MaxLength = 100 };
        var password = new TextBox { Width = 240, UseSystemPasswordChar = true, MaxLength = 72 };
        var role = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList }; role.Items.AddRange(["Customer", "Admin"]); role.SelectedIndex = 0;
        var customer = new ComboBox { Width = 270, DropDownStyle = ComboBoxStyle.DropDownList };
        foreach (var k in customers) customer.Items.Add($"#{ApiClient.ElStr(k, "maKhach")} — {ApiClient.ElStr(k, "hoTen")}");
        if (customers.Length > 0) customer.SelectedIndex = 0;
        role.SelectedIndexChanged += (_, _) => customer.Enabled = role.Text == "Customer";
        using var form = ContractResourceForm.Editor("Cấp tài khoản", ("Tên đăng nhập", username), ("Mật khẩu mới", password), ("Vai trò", role), ("Khách thuê hiện có", customer));
        if (form.ShowDialog(this) != DialogResult.OK) return;
        int? customerId = role.Text == "Customer" && customer.SelectedIndex >= 0 ? int.Parse(ApiClient.ElStr(customers[customer.SelectedIndex], "maKhach")) : null;
        await ApiClient.PostAsync("/api/TaiKhoan", new { tenDangNhap = username.Text.Trim(), matKhau = password.Text, vaiTro = role.Text, maKhach = customerId });
        await LoadData();
    }
    protected override Task OnEdit() => SetStatus("Active");
    protected override Task OnDelete() => SetStatus("Locked");
    async Task SetStatus(string value)
    {
        if (grid.CurrentRow == null || MessageBox.Show($"Đổi trạng thái tài khoản đang chọn thành {value}?", Text, MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        await ApiClient.PutAsync($"/api/TaiKhoan/{grid.CurrentRow.Cells[0].Value}/trang-thai", new { trangThai = value }); await LoadData();
    }
    async Task Reset()
    {
        if (grid.CurrentRow == null) return;
        var password = new TextBox { Width = 240, UseSystemPasswordChar = true, MaxLength = 72 };
        using var form = ContractResourceForm.Editor("Đổi mật khẩu tài khoản đang chọn", ("Mật khẩu mới", password));
        if (form.ShowDialog(this) != DialogResult.OK) return;
        await ApiClient.PostAsync($"/api/TaiKhoan/{grid.CurrentRow.Cells[0].Value}/mat-khau", new { matKhauMoi = password.Text });
        MessageBox.Show("Đã đổi mật khẩu. Tài khoản đó cần đăng nhập lại.");
    }
}
