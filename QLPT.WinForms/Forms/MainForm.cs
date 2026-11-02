using System.Text.Json;
using QLPT.WinForms.Services;

namespace QLPT.WinForms.Forms;

/// <summary>Form cha full-screen MDI + menu + dashboard.</summary>
public class MainForm : Form
{
    MenuStrip menu = new();
    StatusStrip status = new();
    ToolStripStatusLabel lblUser = new();
    ToolStripStatusLabel lblApi = new();

    public MainForm()
    {
        Text = "HỆ THỐNG QUẢN LÝ CHO THUÊ PHÒNG TRỌ — KLCN052 (WinForms Full màn hình)";
        IsMdiContainer = true;
        WindowState = FormWindowState.Maximized;
        StartPosition = FormStartPosition.CenterScreen;

        BuildMenu();
        Controls.Add(menu);
        status.Items.Add(lblUser); status.Items.Add(new ToolStripStatusLabel(" | "));
        status.Items.Add(lblApi);
        Controls.Add(status);
        lblUser.Text = $"User: {Session.TenDangNhap} ({Session.VaiTro})";
        lblApi.Text = $"API: {AppConfig.ApiBaseUrl}";

        Load += (s, e) => OpenDashboard();
    }

    void AddMenu(string text, Action onClick)
    {
        var item = new ToolStripMenuItem(text);
        item.Click += (s, e) => { try { onClick(); } catch (Exception ex) { MessageBox.Show(ex.Message); } };
        menu.Items.Add(item);
    }

    ToolStripMenuItem AddParent(string text)
    {
        var m = new ToolStripMenuItem(text);
        menu.Items.Add(m);
        return m;
    }

    void AddChild(ToolStripMenuItem parent, string text, Func<Form> factory)
    {
        var item = new ToolStripMenuItem(text);
        item.Click += (s, e) =>
        {
            try { OpenChild(factory()); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Không mở được màn hình", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        };
        parent.DropDownItems.Add(item);
    }

    void BuildMenu()
    {
        if (!Session.IsAdmin)
        {
            AddMenu("Thông tin của tôi", () => OpenChild(new TenantOverviewForm()));
            AddMenu("Đăng xuất", () => _ = LogoutAsync());
            return;
        }
        var dm = AddParent("Danh mục");
        AddChild(dm, "Cơ sở", () => new CoSoForm());
        AddChild(dm, "Loại phòng", () => new LoaiPhongForm());
        AddChild(dm, "Phòng trọ", () => new PhongForm());
        AddChild(dm, "Dịch vụ", () => new DichVuForm());
        AddChild(dm, "Tài sản / Tài sản phòng", () => new TaiSanForm());
        AddChild(dm, "Khách thuê", () => new KhachThueForm());
        AddChild(dm, "Tài khoản / Cấp quyền khách thuê", () => new TaiKhoanForm());

        var nv = AddParent("Đặt / Nhận / Trả phòng");
        AddChild(nv, "Đặt phòng (giữ chỗ, cọc)", () => new DatPhongForm());
        AddChild(nv, "Hợp đồng + Gia hạn + Thanh lý", () => new HopDongForm());
        AddChild(nv, "Báo trả phòng", () => new BaoTraForm());
        AddChild(nv, "Cư trú / Tạm trú", () => new CuTruForm());
        AddChild(nv, "Bàn giao tài sản / In biên bản", () => new BanGiaoForm());

        var dn = AddParent("Điện nước & Hóa đơn");
        AddChild(dn, "Chỉ số điện nước + OCR đồng hồ", () => new ChiSoForm());
        AddChild(dn, "Hóa đơn + Tính tiền + Thu tiền", () => new HoaDonForm());
        AddChild(dn, "Sổ thu / chi", () => new ThuChiForm());
        AddChild(dn, "Đăng ký / Ngừng dịch vụ theo hợp đồng", () => new DangKyDichVuForm());

        var kh = AddParent("Vi phạm / Sửa chữa / Thông báo");
        AddChild(kh, "Vi phạm", () => new ViPhamForm());
        AddChild(kh, "Yêu cầu sửa chữa", () => new SuaChuaForm());
        AddChild(kh, "Thông báo", () => new ThongBaoForm());

        AddMenu("Dashboard", OpenDashboard);
        AddMenu("Báo cáo", () => OpenChild(new BaoCaoForm()));
        AddMenu("Sao lưu / Khôi phục", () => OpenChild(new SaoLuuForm()));
        AddMenu("Đăng xuất", () => _ = LogoutAsync());
    }

    async Task LogoutAsync()
    {
        try { await ApiClient.PostAsync("/api/Auth/logout", new { }); }
        catch { /* Đăng xuất tại ứng dụng dù API tạm thời không phản hồi. */ }
        Session.Logout();
        Application.Restart();
    }

    void OpenChild(Form f)
    {
        var existing = MdiChildren.FirstOrDefault(child => child.GetType() == f.GetType());
        if (existing != null) { f.Dispose(); existing.Activate(); return; }
        f.MdiParent = this;
        f.WindowState = FormWindowState.Maximized;
        f.Show();
    }

    void OpenDashboard()
    {
        OpenChild(Session.IsAdmin ? new DashboardForm() : new TenantOverviewForm());
    }
}

public class DashboardForm : Form
{
    DataGridView grid = new() { Dock = DockStyle.Fill, ReadOnly = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
    Label lbl = new() { Dock = DockStyle.Top, Height = 90, Font = new Font("Segoe UI", 12, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter };

    public DashboardForm()
    {
        Text = "Dashboard tổng quan";
        var btn = new Button { Text = "Tải lại", Dock = DockStyle.Bottom, Height = 34 };
        btn.Click += async (s, e) => await LoadData();
        Controls.Add(grid); Controls.Add(lbl); Controls.Add(btn);
        Load += async (s, e) => await LoadData();
    }

    async Task LoadData()
    {
        try
        {
            var el = await ApiClient.GetRawAsync("/api/DanhMucMoRong/dashboard");
            string G(string n) => ApiClient.ElStr(el, n);
            string noStr = G("noChuaThu");
            string dtStr = G("doanhThuThang");
            string noFmt = VnMoney.Format(noStr);
            string dtFmt = VnMoney.Format(dtStr);
            lbl.Text = $"Tổng phòng: {G("tongPhong")} | Trống: {G("phongTrong")} | Đang thuê: {G("dangThue")} | " +
                       $"Khách: {G("tongKhach")} | HĐ hiệu lực: {G("hdHieuLuc")} | Sắp hết hạn (30 ngày): {G("sapHetHan")}\n" +
                       $"Công nợ chưa thu: {noFmt} đ | Doanh thu tháng: {dtFmt} đ";
            var sap = await ApiClient.GetRawAsync("/api/HopDong/sap-het-han?days=30");
            var dt = new System.Data.DataTable();
            dt.Columns.Add("Mã HĐ"); dt.Columns.Add("Phòng"); dt.Columns.Add("Khách"); dt.Columns.Add("Hết hạn"); dt.Columns.Add("Trạng thái");
            foreach (var h in sap.EnumerateArray())
                dt.Rows.Add(ApiClient.ElStr(h, "maHopDong"), h.TryGetProperty("maPhongNavigation", out var p) ? ApiClient.ElStr(p, "tenPhong") : "",
                    h.TryGetProperty("maKhachDaiDienNavigation", out var k) ? ApiClient.ElStr(k, "hoTen") : "",
                    ApiClient.ElStr(h, "ngayKetThuc"), ApiClient.ElStr(h, "trangThai"));
            grid.DataSource = dt;
        }
        catch (Exception ex) { MessageBox.Show("Lỗi tải dashboard: " + ex.Message); }
    }
}

/// <summary>Base CRUD: lưới + thanh nút, form con kế thừa để khỏi lặp code.</summary>
public abstract class CrudForm : Form
{
    protected DataGridView grid = new() { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, MultiSelect = false };
    protected FlowLayoutPanel bar = new() { Dock = DockStyle.Top, Height = 44, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(6) };
    protected Button bLoad = new() { Text = "Tải lại", Width = 90 };
    protected Button bAdd = new() { Text = "Thêm", Width = 90 };
    protected Button bEdit = new() { Text = "Sửa", Width = 90 };
    protected Button bDel = new() { Text = "Xóa", Width = 90 };
    bool busy;

    protected CrudForm(string title)
    {
        Text = title;
        bLoad.Click += async (s, e) => await Safe(LoadData);
        bAdd.Click += async (s, e) => await Safe(OnAdd);
        bEdit.Click += async (s, e) => await Safe(OnEdit);
        bDel.Click += async (s, e) => await Safe(OnDelete);
        bar.Controls.AddRange(new Control[] { bLoad, bAdd, bEdit, bDel });
        Controls.Add(grid); Controls.Add(bar);
        Load += async (s, e) => await Safe(LoadData);
    }

    protected async Task Safe(Func<Task> fn)
    {
        if (busy) return;
        busy = true; bar.Enabled = false;
        try { await fn(); }
        catch (Exception ex) { if (!IsDisposed) ShowError(ex.Message); }
        finally { busy = false; if (!IsDisposed) bar.Enabled = true; }
    }

    protected virtual void ShowError(string message) => MessageBox.Show(message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Warning);

    protected static string Input(string title, string label, string val = "")
    {
        using var f = new Form { Text = title, Size = new Size(400, 160), StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog };
        var l = new Label { Text = label, Dock = DockStyle.Top, Height = 30 };
        var t = new TextBox { Text = val, Dock = DockStyle.Top, Height = 30 };
        var b = new Button { Text = "OK", DialogResult = DialogResult.OK, Dock = DockStyle.Bottom, Height = 36 };
        f.Controls.Add(b); f.Controls.Add(t); f.Controls.Add(l);
        f.AcceptButton = b;
        return f.ShowDialog() == DialogResult.OK ? t.Text : null!;
    }

    protected abstract Task LoadData();
    protected virtual Task OnAdd() => Task.CompletedTask;
    protected virtual Task OnEdit() => Task.CompletedTask;
    protected virtual Task OnDelete() => Task.CompletedTask;

    protected static System.Data.DataTable ToTable(JsonElement arr, params (string col, string[] props)[] cols)
    {
        var dt = new System.Data.DataTable();
        foreach (var c in cols) dt.Columns.Add(c.col);
        if (arr.ValueKind == JsonValueKind.Array)
            foreach (var el in arr.EnumerateArray())
            {
                var row = dt.NewRow();
                for (int i = 0; i < cols.Length; i++)
                    row[i] = ApiClient.ElStr(el, cols[i].props);
                dt.Rows.Add(row);
            }
        return dt;
    }

    /// <summary>Bảng có cột tiền định dạng 1.000.000: truyền tên cột cần format.</summary>
    protected static System.Data.DataTable ToTableVn(JsonElement arr, HashSet<string> moneyCols, params (string col, string[] props)[] cols)
    {
        var dt = new System.Data.DataTable();
        foreach (var c in cols) dt.Columns.Add(c.col);
        if (arr.ValueKind == JsonValueKind.Array)
            foreach (var el in arr.EnumerateArray())
            {
                var row = dt.NewRow();
                for (int i = 0; i < cols.Length; i++)
                {
                    string raw = ApiClient.ElStr(el, cols[i].props);
                    row[i] = moneyCols.Contains(cols[i].col) ? Services.VnMoney.Format(raw) : raw;
                }
                dt.Rows.Add(row);
            }
        return dt;
    }
}
