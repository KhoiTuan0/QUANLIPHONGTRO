using QLPT.WinForms.Services;

namespace QLPT.WinForms.Forms;

public class LoginForm : Form
{
    TextBox txtUser = new() { PlaceholderText = "Nhập tên đăng nhập" };
    TextBox txtPass = new() { PlaceholderText = "Nhập mật khẩu", UseSystemPasswordChar = true };
    TextBox txtApi = new() { Text = AppConfig.ApiBaseUrl };
    Label lblMsg = new() { ForeColor = Color.Firebrick, AutoSize = false, Height = 60, TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Fill };
    Panel card = new() { Size = new Size(620, 560) };
    bool loggingIn;

    public LoginForm()
    {
        Text = "Đăng nhập — QL Phòng trọ KLCN052";
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        BackColor = Color.FromArgb(237, 242, 247);
        AutoScaleMode = AutoScaleMode.Font;

        var header = new Label
        {
            Text = "HỆ THỐNG QUẢN LÝ CHO THUÊ PHÒNG TRỌ KLCN052",
            Dock = DockStyle.Top, Height = 84,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 20, FontStyle.Bold), ForeColor = Color.FromArgb(43, 108, 176)
        };
        var sub = new Label
        {
            Text = "Đăng nhập để tiếp tục  •  Admin / Khách thuê",
            Dock = DockStyle.Top, Height = 34,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 11), ForeColor = Color.Gray
        };

        card.BackColor = Color.White;
        card.BorderStyle = BorderStyle.FixedSingle;
        card.Anchor = AnchorStyles.None;
        card.Padding = new Padding(0);

        var title = new Label { Text = "ĐĂNG NHẬP", Dock = DockStyle.Top, Height = 52, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 16, FontStyle.Bold), ForeColor = Color.FromArgb(45, 55, 72) };

        var p = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 11, ColumnCount = 1, Padding = new Padding(44, 8, 44, 16) };
        p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        p.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // lbl api
        p.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // txt api
        p.RowStyles.Add(new RowStyle(SizeType.Absolute, 10F));
        p.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // lbl user
        p.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // txt user
        p.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // lbl pass
        p.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // txt pass + show
        p.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // chkShow
        p.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // msg
        p.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // btn login
        p.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // btn reg

        var lblApi = new Label { Text = "Địa chỉ API", AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = Color.FromArgb(74, 85, 104), Dock = DockStyle.Fill };
        var lblUser = new Label { Text = "Tên đăng nhập", AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = Color.FromArgb(74, 85, 104), Dock = DockStyle.Fill };
        var lblPass = new Label { Text = "Mật khẩu", AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = Color.FromArgb(74, 85, 104), Dock = DockStyle.Fill };

        foreach (var t in new[] { txtApi, txtUser, txtPass })
        {
            t.Dock = DockStyle.Fill;
            t.Font = new Font("Segoe UI", 11);
            t.Margin = new Padding(0, 4, 0, 4);
            t.Height = 38;
        }

        var chkShow = new CheckBox { Text = "Hiện mật khẩu", AutoSize = true, Dock = DockStyle.Left, Font = new Font("Segoe UI", 9), ForeColor = Color.Gray };
        chkShow.CheckedChanged += (s, e) => txtPass.UseSystemPasswordChar = !chkShow.Checked;

        var btnLogin = new Button
        {
            Text = "Đăng nhập",
            Dock = DockStyle.Fill, Height = 48, AutoSize = false,
            BackColor = Color.FromArgb(43, 108, 176), ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 12, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter, Margin = new Padding(0, 10, 0, 6)
        };
        btnLogin.FlatAppearance.BorderSize = 0;

        var btnReg = new Button
        {
            Text = "Chưa có tài khoản? Đăng ký ngay",
            Dock = DockStyle.Fill, Height = 38, AutoSize = false,
            FlatStyle = FlatStyle.Flat, ForeColor = Color.FromArgb(43, 108, 176),
            BackColor = Color.White, Font = new Font("Segoe UI", 10),
            TextAlign = ContentAlignment.MiddleCenter, Margin = new Padding(0, 2, 0, 0)
        };
        btnReg.FlatAppearance.BorderSize = 0;
        var hint = new Label { Text = "Dùng tài khoản trong cơ sở dữ liệu của nhóm.", Dock = DockStyle.Fill, AutoSize = true, Font = new Font("Segoe UI", 9), ForeColor = Color.Gray, TextAlign = ContentAlignment.MiddleCenter };

        btnLogin.Click += async (s, e) => await DoLogin();
        btnReg.Click += (s, e) =>
        {
            try { ApiClient.SetBaseUrl(txtApi.Text.Trim()); using var f = new RegisterForm(); f.ShowDialog(); }
            catch (Exception ex) { lblMsg.Text = ex.Message; }
        };
        txtPass.KeyDown += async (s, e) => { if (e.KeyCode == Keys.Enter) await DoLogin(); };

        p.Controls.Add(lblApi, 0, 0); p.Controls.Add(txtApi, 0, 1);
        p.Controls.Add(new Panel { Height = 6, Dock = DockStyle.Fill }, 0, 2);
        p.Controls.Add(lblUser, 0, 3); p.Controls.Add(txtUser, 0, 4);
        p.Controls.Add(lblPass, 0, 5);
        var passRow = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 1, ColumnCount = 1, Margin = new Padding(0), Padding = new Padding(0) };
        passRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        passRow.Controls.Add(txtPass, 0, 0);
        p.Controls.Add(passRow, 0, 6);
        p.Controls.Add(chkShow, 0, 7);
        p.Controls.Add(lblMsg, 0, 8);
        p.Controls.Add(btnLogin, 0, 9);
        p.Controls.Add(btnReg, 0, 10);
        // hint thêm vào cuối bằng panel riêng để không vỡ layout
        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 30 };
        hint.Dock = DockStyle.Fill;
        bottom.Controls.Add(hint);

        card.Controls.Add(p); card.Controls.Add(bottom); card.Controls.Add(title);
        // Keep the Fill panel first in z-order so docking reserves space for the title.

        Controls.Add(card); Controls.Add(sub); Controls.Add(header);
        Load += (s, e) => CenterCard();
        Resize += (s, e) => CenterCard();
    }

    void CenterCard()
    {
        card.Location = new Point(Math.Max(0, (ClientSize.Width - card.Width) / 2), Math.Max(120, (ClientSize.Height - card.Height) / 2 + 30));
    }

    async Task DoLogin()
    {
        if (loggingIn) return;
        loggingIn = true;
        card.Enabled = false;
        ApiClient.ResetAuthentication();
        try
        {
            if (string.IsNullOrWhiteSpace(txtUser.Text) || string.IsNullOrWhiteSpace(txtPass.Text))
            {
                lblMsg.Text = "Vui lòng nhập đầy đủ tên đăng nhập và mật khẩu.";
                return;
            }
            lblMsg.ForeColor = Color.Gray;
            lblMsg.Text = "Đang đăng nhập...";
            ApiClient.SetBaseUrl(txtApi.Text.Trim());
            var el = await ApiClient.PostAsync("/api/Auth/login", new { username = txtUser.Text.Trim(), password = txtPass.Text });
            if (!int.TryParse(ApiClient.ElStr(el, "maTaiKhoan"), out int accountId) || accountId <= 0)
                throw new Exception("API đăng nhập không trả mã tài khoản hợp lệ. Cần sửa hợp đồng đăng nhập phía API.");
            string vt = ApiClient.ElStr(el, "vaiTro", "VaiTro");
            if (!string.Equals(vt, "Admin", StringComparison.OrdinalIgnoreCase) && !string.Equals(vt, "Customer", StringComparison.OrdinalIgnoreCase))
                throw new Exception("API đăng nhập không trả vai trò Admin/Customer hợp lệ.");
            string mk = ApiClient.ElStr(el, "maKhach", "MaKhach");
            int? customerId = int.TryParse(mk, out int mkv) && mkv > 0 ? mkv : null;
            if (vt.Equals("Customer", StringComparison.OrdinalIgnoreCase) && !customerId.HasValue)
                throw new Exception("Tài khoản Customer chưa được API liên kết với khách thuê.");
            if (!ApiClient.HasSessionCookie)
                throw new Exception("API đăng nhập chưa cấp cookie QLPT.Session. Cần cấu hình xác thực phía API.");
            Session.MaTaiKhoan = accountId;
            Session.VaiTro = vt.Trim();
            Session.MaKhach = customerId;
            Session.TenDangNhap = txtUser.Text.Trim();
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (System.Net.Http.HttpRequestException) { lblMsg.ForeColor = Color.Firebrick; lblMsg.Text = $"Không kết nối được API tại {AppConfig.ApiBaseUrl}. Kiểm tra API đã chạy và đúng địa chỉ/cổng."; }
        catch (TaskCanceledException) { lblMsg.ForeColor = Color.Firebrick; lblMsg.Text = "API phản hồi quá chậm. Kiểm tra kết nối API và SQL Server."; }
        catch (Exception ex) { lblMsg.ForeColor = Color.Firebrick; lblMsg.Text = ex.Message; }
        finally
        {
            loggingIn = false;
            if (!Session.IsLoggedIn) ApiClient.ResetAuthentication();
            if (!IsDisposed) card.Enabled = true;
        }
    }
}

public class RegisterForm : Form
{
    TextBox t1 = new() { PlaceholderText = "VD: nguyen_van_a" };
    TextBox t2 = new() { PlaceholderText = "Tối thiểu 6 ký tự", UseSystemPasswordChar = true };
    TextBox t3 = new() { PlaceholderText = "VD: Nguyễn Văn A" };
    TextBox t4 = new() { PlaceholderText = "VD: 0901234567" };
    TextBox t5 = new() { PlaceholderText = "VD: 079204001234" };
    TextBox t6 = new() { PlaceholderText = "VD: ban@gmail.com" };
    Label msg = new() { ForeColor = Color.Firebrick, AutoSize = false, Height = 30, TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Fill };
    Panel card = new() { Size = new Size(640, 640), AutoScroll = true };

    public RegisterForm()
    {
        Text = "Đăng ký tài khoản khách thuê";
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        BackColor = Color.FromArgb(237, 242, 247);

        var header = new Label
        {
            Text = "TẠO TÀI KHOẢN KHÁCH THUÊ MỚI",
            Dock = DockStyle.Top, Height = 70,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 18, FontStyle.Bold), ForeColor = Color.FromArgb(39, 103, 73)
        };
        var sub = new Label
        {
            Text = "Điền đầy đủ thông tin bên dưới, tài khoản mặc định là vai trò Khách thuê",
            Dock = DockStyle.Top, Height = 30, TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 10), ForeColor = Color.Gray
        };
        card.BackColor = Color.White;
        card.BorderStyle = BorderStyle.FixedSingle;
        card.Anchor = AnchorStyles.None;

        var p = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, RowCount = 8, ColumnCount = 1, Padding = new Padding(44, 12, 44, 8) };
        p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        p.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        p.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        p.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        p.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        p.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        p.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        p.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
        p.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));

        p.Controls.Add(Field("Tên đăng nhập *", t1), 0, 0);
        p.Controls.Add(Field("Mật khẩu *", t2), 0, 1);
        p.Controls.Add(Field("Họ và tên *", t3), 0, 2);
        p.Controls.Add(Field("Số điện thoại", t4), 0, 3);
        p.Controls.Add(Field("CCCD (không trùng người khác)", t5), 0, 4);
        p.Controls.Add(Field("Email", t6), 0, 5);
        p.Controls.Add(msg, 0, 6);

        var btn = new Button
        {
            Text = "Đăng ký tài khoản",
            Dock = DockStyle.Fill, Height = 44, MaximumSize = new Size(0, 44), AutoSize = false,
            BackColor = Color.FromArgb(39, 103, 73), ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 12, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter, Margin = new Padding(0, 4, 0, 2)
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.Click += async (s, e) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(t1.Text) || string.IsNullOrWhiteSpace(t2.Text) || string.IsNullOrWhiteSpace(t3.Text))
                {
                    msg.Text = "Vui lòng nhập đầy đủ Tên đăng nhập, Mật khẩu và Họ tên.";
                    return;
                }
                if (t2.Text.Length < 6) { msg.Text = "Mật khẩu tối thiểu 6 ký tự"; return; }
                string cccd = t5.Text.Trim(), phone = t4.Text.Trim(), email = t6.Text.Trim();
                if (cccd.Length > 0 && !System.Text.RegularExpressions.Regex.IsMatch(cccd, @"^[0-9]{12}$"))
                { msg.Text = "CCCD phải gồm 12 chữ số"; return; }
                if (phone.Length > 0 && !System.Text.RegularExpressions.Regex.IsMatch(phone, @"^0[0-9]{9}$"))
                { msg.Text = "Số điện thoại phải gồm 10 số, bắt đầu 0"; return; }
                if (email.Length > 0 && !System.Text.RegularExpressions.Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                { msg.Text = "Email không đúng định dạng"; return; }
                await ApiClient.PostAsync("/api/Auth/register", new
                {
                    tenDangNhap = t1.Text.Trim(), matKhau = t2.Text, hoTen = t3.Text.Trim(),
                    soDienThoai = t4.Text.Trim(), cccd = t5.Text.Trim(), email = t6.Text.Trim(), diaChi = ""
                });
                MessageBox.Show("Đăng ký thành công! Hãy đăng nhập.", "Thành công");
                Close();
            }
            catch (Exception ex) { msg.Text = ex.Message; }
        };
        var bBack = new Button
        {
            Text = "← Quay lại màn hình đăng nhập",
            Dock = DockStyle.Fill, Height = 34, AutoSize = false,
            FlatStyle = FlatStyle.Flat, ForeColor = Color.FromArgb(43, 108, 176),
            TextAlign = ContentAlignment.MiddleCenter
        };
        bBack.FlatAppearance.BorderSize = 0;
        bBack.Click += (s, e) => Close();
        p.Controls.Add(btn, 0, 7);
        // bBack đặt ở bottom card
        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 40 };
        bBack.Dock = DockStyle.Fill;
        bottom.Controls.Add(bBack);

        card.Controls.Add(p); card.Controls.Add(bottom);
        Controls.Add(card); Controls.Add(sub); Controls.Add(header);
        Load += (s, e) => CenterCard();
        Resize += (s, e) => CenterCard();
    }

    static Panel Field(string label, TextBox box)
    {
        var wrap = new Panel { Dock = DockStyle.Fill, AutoSize = true, Margin = new Padding(0, 2, 0, 6) };
        var l = new Label { Text = label, Dock = DockStyle.Top, Height = 20, Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = Color.FromArgb(74, 85, 104) };
        box.Dock = DockStyle.Top; box.Height = 38; box.Font = new Font("Segoe UI", 11); box.Margin = new Padding(0, 2, 0, 0);
        wrap.Controls.Add(box); wrap.Controls.Add(l);
        return wrap;
    }

    void CenterCard()
    {
        card.Location = new Point(Math.Max(0, (ClientSize.Width - card.Width) / 2), Math.Max(105, (ClientSize.Height - card.Height) / 2 + 20));
    }
}
