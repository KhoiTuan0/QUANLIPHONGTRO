using System.Text.Json;
using QLPT.WinForms.Services;

namespace QLPT.WinForms.Forms;

public class CoSoForm : CrudForm
{
    public CoSoForm() : base("Quản lý Cơ sở") { }
    protected override async Task LoadData()
    {
        var arr = await ApiClient.GetRawAsync("/api/CoSo");
        grid.DataSource = ToTable(arr, ("Mã", new[] { "maCoSo", "MaCoSo" }), ("Tên cơ sở", new[] { "tenCoSo", "TenCoSo" }), ("Địa chỉ", new[] { "diaChi", "DiaChi" }), ("SĐT", new[] { "soDienThoai", "SoDienThoai" }), ("Mô tả", new[] { "moTa", "MoTa" }));
    }
    protected override async Task OnAdd()
    {
        string ten = Input("Thêm", "Tên cơ sở:"); if (ten == null) return;
        await ApiClient.PostAsync("/api/CoSo", new { tenCoSo = ten }); await LoadData();
    }
    protected override async Task OnEdit()
    {
        if (grid.CurrentRow == null) return;
        int id = int.Parse(grid.CurrentRow.Cells[0].Value.ToString()!);
        string ten = Input("Sửa", "Tên cơ sở:", grid.CurrentRow.Cells[1].Value.ToString()!); if (ten == null) return;
        await ApiClient.PutAsync($"/api/CoSo/{id}", new { tenCoSo = ten }); await LoadData();
    }
    protected override async Task OnDelete()
    {
        if (grid.CurrentRow == null) return;
        int id = int.Parse(grid.CurrentRow.Cells[0].Value.ToString()!);
        if (MessageBox.Show("Xóa cơ sở?", "Xác nhận", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        await ApiClient.DeleteAsync($"/api/CoSo/{id}"); await LoadData();
    }
}

public class LoaiPhongForm : CrudForm
{
    public LoaiPhongForm() : base("Quản lý Loại phòng") { }
    protected override async Task LoadData()
    {
        var arr = await ApiClient.GetRawAsync("/api/LoaiPhong");
        grid.DataSource = ToTableVn(arr, new HashSet<string> { "Giá mặc định" }, ("Mã", new[] { "maLoaiPhong", "MaLoaiPhong" }), ("Tên loại", new[] { "tenLoaiPhong", "TenLoaiPhong" }), ("Diện tích", new[] { "dienTich", "DienTich" }), ("Tối đa", new[] { "soNguoiToiDa", "SoNguoiToiDa" }), ("Giá mặc định", new[] { "giaMacDinh", "GiaMacDinh" }));
    }
    protected override async Task OnAdd()
    {
        using var f = new LoaiPhongEditForm(null);
        if (f.ShowDialog() == DialogResult.OK) await LoadData();
    }
    protected override async Task OnEdit()
    {
        if (grid.CurrentRow == null) return;
        int id = int.Parse(grid.CurrentRow.Cells[0].Value.ToString()!);
        var el = await ApiClient.GetRawAsync("/api/LoaiPhong");
        JsonElement found = default; bool ok = false;
        foreach (var x in el.EnumerateArray()) if (ApiClient.ElStr(x, "maLoaiPhong", "MaLoaiPhong") == id.ToString()) { found = x; ok = true; }
        if (!ok) return;
        using var f = new LoaiPhongEditForm(found);
        if (f.ShowDialog() == DialogResult.OK) await LoadData();
    }
    protected override async Task OnDelete()
    {
        if (grid.CurrentRow == null) return;
        int id = int.Parse(grid.CurrentRow.Cells[0].Value.ToString()!);
        if (MessageBox.Show("Xóa loại phòng? (bị chặn nếu còn phòng)", "Xác nhận", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        await ApiClient.DeleteAsync($"/api/LoaiPhong/{id}"); await LoadData();
    }
}

public class LoaiPhongEditForm : Form
{
    TextBox tTen = new(); TextBox tDt = new(); TextBox tMax = new(); TextBox tGia = new(); TextBox tMoTa = new();
    JsonElement? src; int? id;
    public LoaiPhongEditForm(JsonElement? src)
    {
        this.src = src;
        if (src.HasValue) int.TryParse(ApiClient.ElStr(src.Value, "maLoaiPhong", "MaLoaiPhong"), out int _i);
        Text = "Loại phòng"; Size = new Size(420, 360); StartPosition = FormStartPosition.CenterParent;
        var p = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 6, ColumnCount = 2, Padding = new Padding(12) };
        p.Controls.Add(new Label { Text = "Tên loại *" }); p.Controls.Add(tTen);
        p.Controls.Add(new Label { Text = "Diện tích" }); p.Controls.Add(tDt);
        p.Controls.Add(new Label { Text = "Số người tối đa" }); p.Controls.Add(tMax);
        p.Controls.Add(new Label { Text = "Giá mặc định" }); p.Controls.Add(tGia);
        p.Controls.Add(new Label { Text = "Mô tả" }); p.Controls.Add(tMoTa);
        var btn = new Button { Text = "Lưu", Dock = DockStyle.Bottom, Height = 38, BackColor = Color.SteelBlue, ForeColor = Color.White };
        btn.Click += async (s, e) => await Save();
        if (src.HasValue)
        {
            var v = src.Value;
            tTen.Text = ApiClient.ElStr(v, "tenLoaiPhong", "TenLoaiPhong");
            tDt.Text = ApiClient.ElStr(v, "dienTich", "DienTich");
            tMax.Text = ApiClient.ElStr(v, "soNguoiToiDa", "SoNguoiToiDa");
            tGia.Text = ApiClient.ElStr(v, "giaMacDinh", "GiaMacDinh");
            tMoTa.Text = ApiClient.ElStr(v, "moTa", "MoTa");
            id = int.Parse(ApiClient.ElStr(v, "maLoaiPhong", "MaLoaiPhong"));
        }
        Controls.Add(p); Controls.Add(btn);
    }
    async Task Save()
    {
        try
        {
            var body = new
            {
                tenLoaiPhong = tTen.Text.Trim(),
                dienTich = decimal.TryParse(tDt.Text, out var d) ? d : (decimal?)null,
                soNguoiToiDa = int.TryParse(tMax.Text, out var m) ? m : (int?)null,
                giaMacDinh = Services.VnMoney.TryParse(tGia.Text, out var g) ? g : (decimal?)null,
                moTa = tMoTa.Text
            };
            if (string.IsNullOrWhiteSpace(tTen.Text)) { MessageBox.Show("Vui lòng nhập tên loại phòng"); return; }
            if (!string.IsNullOrWhiteSpace(tDt.Text) && (!decimal.TryParse(tDt.Text, out var area) || area <= 0) ||
                !string.IsNullOrWhiteSpace(tMax.Text) && (!int.TryParse(tMax.Text, out var count) || count <= 0) ||
                !string.IsNullOrWhiteSpace(tGia.Text) && (!Services.VnMoney.TryParse(tGia.Text, out var price) || price < 0))
            { MessageBox.Show("Diện tích, số người phải dương; giá phải là số không âm"); return; }
            if (id.HasValue) await ApiClient.PutAsync($"/api/LoaiPhong/{id.Value}", body);
            else await ApiClient.PostAsync("/api/LoaiPhong", body);
            DialogResult = DialogResult.OK; Close();
        }
        catch (Exception ex) { MessageBox.Show(ex.Message); }
    }
}

public class PhongForm : CrudForm
{
    ComboBox cbTT = new() { Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
    public PhongForm() : base("Quản lý Phòng trọ")
    {
        cbTT.Items.AddRange(new object[] { "", "Trống", "Đã đặt cọc", "Đang thuê", "Bảo trì" });
        cbTT.SelectedIndex = 0;
        cbTT.SelectedIndexChanged += async (s, e) => await Safe(LoadData);
        bar.Controls.Add(new Label { Text = "Trạng thái:", AutoSize = true });
        bar.Controls.Add(cbTT);
        bAdd.Text = "Thêm phòng"; bEdit.Text = "Sửa / Đổi TT";
    }
    protected override async Task LoadData()
    {
        string tt = cbTT.SelectedItem?.ToString() ?? "";
        string q = string.IsNullOrEmpty(tt) ? "" : $"?trangThai={Uri.EscapeDataString(tt)}";
        var arr = await ApiClient.GetRawAsync("/api/Phong" + q);
        grid.DataSource = ToTableVn(arr, new HashSet<string> { "Giá" }, ("Mã", new[] { "maPhong", "MaPhong" }), ("Tên", new[] { "tenPhong", "TenPhong" }), ("Loại", new[] { "maLoaiPhong", "MaLoaiPhong" }), ("Giá", new[] { "giaThue", "GiaThue" }), ("Trạng thái", new[] { "trangThai", "TrangThai" }), ("Cơ sở", new[] { "maCoSo", "MaCoSo" }), ("Tầng", new[] { "tang", "Tang" }));
    }
    protected override async Task OnAdd()
    {
        using var f = new PhongEditForm(null);
        if (f.ShowDialog() == DialogResult.OK) await LoadData();
    }
    protected override async Task OnEdit()
    {
        if (grid.CurrentRow == null) return;
        int id = int.Parse(grid.CurrentRow.Cells[0].Value.ToString()!);
        var el = await ApiClient.GetRawAsync($"/api/Phong/{id}");
        using var f = new PhongEditForm(el);
        if (f.ShowDialog() == DialogResult.OK) await LoadData();
    }
    protected override async Task OnDelete()
    {
        if (grid.CurrentRow == null) return;
        int id = int.Parse(grid.CurrentRow.Cells[0].Value.ToString()!);
        if (MessageBox.Show("Xóa phòng? (bị chặn nếu đã có hợp đồng)", "Xác nhận", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        await ApiClient.DeleteAsync($"/api/Phong/{id}"); await LoadData();
    }
}

public class PhongEditForm : Form
{
    TextBox tTen = new(); TextBox tLoai = new() { Text = "1" }; TextBox tGia = new();
    ComboBox cbTT = new(); TextBox tCoSo = new() { Text = "1" }; TextBox tTang = new(); TextBox tMoTa = new();
    JsonElement? src; int? id;
    public PhongEditForm(JsonElement? src)
    {
        this.src = src; Text = "Phòng"; Size = new Size(440, 420); StartPosition = FormStartPosition.CenterParent;
        cbTT.Items.AddRange(new object[] { "Trống", "Đã đặt cọc", "Đang thuê", "Bảo trì" });
        var p = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 7, ColumnCount = 2, Padding = new Padding(12) };
        p.Controls.Add(new Label { Text = "Tên phòng *" }); p.Controls.Add(tTen);
        p.Controls.Add(new Label { Text = "Mã loại phòng" }); p.Controls.Add(tLoai);
        p.Controls.Add(new Label { Text = "Giá thuê" }); p.Controls.Add(tGia);
        p.Controls.Add(new Label { Text = "Trạng thái" }); p.Controls.Add(cbTT);
        p.Controls.Add(new Label { Text = "Mã cơ sở" }); p.Controls.Add(tCoSo);
        p.Controls.Add(new Label { Text = "Tầng" }); p.Controls.Add(tTang);
        p.Controls.Add(new Label { Text = "Mô tả" }); p.Controls.Add(tMoTa);
        var btn = new Button { Text = "Lưu", Dock = DockStyle.Bottom, Height = 38, BackColor = Color.SteelBlue, ForeColor = Color.White };
        btn.Click += async (s, e) => await Save();
        if (src.HasValue)
        {
            var v = src.Value;
            id = int.Parse(ApiClient.ElStr(v, "maPhong", "MaPhong"));
            tTen.Text = ApiClient.ElStr(v, "tenPhong", "TenPhong");
            tLoai.Text = ApiClient.ElStr(v, "maLoaiPhong", "MaLoaiPhong");
            tGia.Text = ApiClient.ElStr(v, "giaThue", "GiaThue");
            cbTT.SelectedItem = ApiClient.ElStr(v, "trangThai", "TrangThai");
            tCoSo.Text = ApiClient.ElStr(v, "maCoSo", "MaCoSo");
            tTang.Text = ApiClient.ElStr(v, "tang", "Tang");
            tMoTa.Text = ApiClient.ElStr(v, "moTa", "MoTa");
        }
        else cbTT.SelectedIndex = 0;
        Controls.Add(p); Controls.Add(btn);
    }
    async Task Save()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(tTen.Text)) { MessageBox.Show("Vui lòng nhập tên phòng."); tTen.Focus(); return; }
            if (!int.TryParse(tLoai.Text.Trim(), out int l) || l <= 0) { MessageBox.Show("Mã loại phòng phải là số nguyên dương."); tLoai.Focus(); return; }
            int? t = null;
            if (!string.IsNullOrWhiteSpace(tTang.Text))
            {
                if (!int.TryParse(tTang.Text.Trim(), out int tang) || tang < 0) { MessageBox.Show("Tầng phải là số nguyên không âm."); tTang.Focus(); return; }
                t = tang;
            }
            decimal? g = null;
            if (!string.IsNullOrWhiteSpace(tGia.Text))
            {
                if (!Services.VnMoney.TryParse(tGia.Text, out decimal gia) || gia < 0) { MessageBox.Show("Giá thuê phải là số không âm."); tGia.Focus(); return; }
                g = gia;
            }
            int? c = null;
            if (!string.IsNullOrWhiteSpace(tCoSo.Text))
            {
                if (!int.TryParse(tCoSo.Text.Trim(), out int coSo) || coSo <= 0) { MessageBox.Show("Mã cơ sở phải là số nguyên dương."); tCoSo.Focus(); return; }
                c = coSo;
            }
            if (cbTT.SelectedItem == null) { MessageBox.Show("Vui lòng chọn trạng thái phòng."); cbTT.Focus(); return; }
            var body = new
            {
                maLoaiPhong = l,
                tenPhong = tTen.Text.Trim(),
                tang = t,
                giaThue = g,
                trangThai = cbTT.SelectedItem.ToString(),
                moTa = tMoTa.Text,
                maCoSo = c
            };
            if (id.HasValue) await ApiClient.PutAsync($"/api/Phong/{id}", body);
            else await ApiClient.PostAsync("/api/Phong", body);
            DialogResult = DialogResult.OK; Close();
        }
        catch (Exception ex) { MessageBox.Show(ex.Message); }
    }
}

public class DichVuForm : CrudForm
{
    public DichVuForm() : base("Quản lý Dịch vụ (điện/nước/net/rác/giữ xe)") { }
    protected override async Task LoadData()
    {
        var arr = await ApiClient.GetRawAsync("/api/DichVu");
        grid.DataSource = ToTableVn(arr, new HashSet<string> { "Đơn giá" }, ("Mã", new[] { "maDichVu", "MaDichVu" }), ("Tên", new[] { "tenDichVu", "TenDichVu" }), ("ĐVT", new[] { "donViTinh", "DonViTinh" }), ("Đơn giá", new[] { "donGia", "DonGia" }), ("Trạng thái", new[] { "trangThai", "TrangThai" }));
    }
    protected override async Task OnAdd()
    {
        string ten = Input("Thêm", "Tên dịch vụ:"); if (ten == null) return;
        string gia = Input("Thêm", "Đơn giá (VD 100.000):", "100.000"); if (gia == null) return;
        await ApiClient.PostAsync("/api/DichVu", new { tenDichVu = ten, donGia = Services.VnMoney.Parse(gia), trangThai = "Đang áp dụng" });
        await LoadData();
    }
    protected override async Task OnDelete()
    {
        if (grid.CurrentRow == null) return;
        await ApiClient.DeleteAsync($"/api/DichVu/{grid.CurrentRow.Cells[0].Value}");
        await LoadData();
    }
}

public class TaiSanForm : Form
{
    DataGridView g1 = new() { Dock = DockStyle.Left, Width = 450, ReadOnly = true, SelectionMode = DataGridViewSelectionMode.FullRowSelect, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
    DataGridView g2 = new() { Dock = DockStyle.Fill, ReadOnly = true, SelectionMode = DataGridViewSelectionMode.FullRowSelect, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
    TextBox txtPhong = new() { Width = 80, Text = "1" };
    public TaiSanForm()
    {
        Text = "Tài sản mẫu (trái) — Tài sản trong phòng (phải)";
        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(6) };
        var b1 = new Button { Text = "Tải" }; b1.Click += async (s, e) => await LoadAll();
        var bAdd = new Button { Text = "+ Tài sản mẫu" }; bAdd.Click += async (s, e) => { string t = Microsoft.VisualBasic.Interaction.InputBox("Tên tài sản:", "Thêm", ""); if (t != "") { await ApiClient.PostAsync("/api/TaiSan", new { tenTaiSan = t }); await LoadAll(); } };
        var bG = new Button { Text = "Gán vào phòng" };
        bG.Click += async (s, e) =>
        {
            if (g1.CurrentRow == null) return;
            int maTS = int.Parse(g1.CurrentRow.Cells[0].Value.ToString()!);
            await ApiClient.PostAsync("/api/TaiSan/phong", new { maPhong = int.Parse(txtPhong.Text), maTaiSan = maTS, soLuong = 1, tinhTrang = "Tốt" });
            await LoadAll();
        };
        top.Controls.AddRange(new Control[] { new Label { Text = "Mã phòng:", AutoSize = true }, txtPhong, b1, bAdd, bG });
        Controls.Add(g2); Controls.Add(g1); Controls.Add(top);
        Load += async (s, e) => await LoadAll();
    }
    async Task LoadAll()
    {
        try
        {
            var a1 = await ApiClient.GetRawAsync("/api/TaiSan");
            var dt1 = new System.Data.DataTable(); dt1.Columns.Add("Mã"); dt1.Columns.Add("Tên"); dt1.Columns.Add("Giá TK");
            foreach (var x in a1.EnumerateArray()) dt1.Rows.Add(ApiClient.ElStr(x, "maTaiSan", "MaTaiSan"), ApiClient.ElStr(x, "tenTaiSan", "TenTaiSan"), Services.VnMoney.Format(ApiClient.ElStr(x, "giaTriThamKhao", "GiaTriThamKhao")));
            g1.DataSource = dt1;
            var a2 = await ApiClient.GetRawAsync($"/api/TaiSan/phong/{txtPhong.Text}");
            var dt2 = new System.Data.DataTable(); dt2.Columns.Add("ID"); dt2.Columns.Add("Tài sản"); dt2.Columns.Add("SL"); dt2.Columns.Add("Tình trạng");
            foreach (var x in a2.EnumerateArray()) dt2.Rows.Add(ApiClient.ElStr(x, "maTaiSanPhong", "MaTaiSanPhong"), ApiClient.ElStr(x, "tenTaiSan"), ApiClient.ElStr(x, "soLuong", "SoLuong"), ApiClient.ElStr(x, "tinhTrang", "TinhTrang"));
            g2.DataSource = dt2;
        }
        catch (Exception ex) { MessageBox.Show(ex.Message); }
    }
}

public class KhachThueForm : CrudForm
{
    TextBox txtSearch = new() { Width = 220, PlaceholderText = "Tên, CCCD, SĐT, email, địa chỉ" };
    Label lblStatus = new() { Dock = DockStyle.Bottom, Height = 48, AutoEllipsis = true };
    JsonElement customers;
    public KhachThueForm() : base("Quản lý Khách thuê")
    {
        bLoad.Text = "Làm mới"; bDel.Text = "Xóa mềm";
        bDel.Width = 100;
        bar.Height = 78; bar.WrapContents = true;
        bar.Controls.Add(new Label { Text = "Tìm kiếm:", AutoSize = true });
        bar.Controls.Add(txtSearch);
        txtSearch.TextChanged += (_, _) => ApplySearch();
        Controls.Add(lblStatus);
    }
    protected override async Task LoadData()
    {
        var arr = await ApiClient.GetRawAsync("/api/KhachThue");
        if (arr.ValueKind != JsonValueKind.Array) throw new Exception("API Khách thuê phải trả một mảng JSON. Danh sách chưa được cập nhật.");
        customers = arr;
        ApplySearch();
    }
    void ApplySearch()
    {
        if (customers.ValueKind != JsonValueKind.Array) return;
        var dt = ToTable(customers, ("Mã", new[] { "maKhach" }), ("Họ tên", new[] { "hoTen" }), ("CCCD", new[] { "cccd", "Cccd", "CCCD" }), ("SĐT", new[] { "soDienThoai" }), ("Email", new[] { "email" }), ("Địa chỉ", new[] { "diaChi" }));
        // Lọc trên dữ liệu vừa tải, không phụ thuộc query tìm kiếm mà API legacy bỏ qua.
        string term = txtSearch.Text.Trim();
        for (int i = dt.Rows.Count - 1; i >= 0; i--)
        {
            var item = customers[i];
            bool deleted = bool.TryParse(ApiClient.ElStr(item, "isDeleted"), out bool value) && value;
            if (deleted || term.Length > 0 && !dt.Rows[i].ItemArray.Any(v => (v?.ToString() ?? "").Contains(term, StringComparison.CurrentCultureIgnoreCase)))
                dt.Rows.RemoveAt(i);
        }
        grid.DataSource = dt;
        lblStatus.ForeColor = SystemColors.ControlText;
        lblStatus.Text = $"Hiển thị {dt.Rows.Count} khách thuê. Tìm kiếm trên danh sách đã tải; Làm mới lấy dữ liệu từ API.";
    }
    int? SelectedId()
    {
        if (grid.CurrentRow == null || grid.CurrentRow.IsNewRow) return null;
        return int.TryParse(grid.CurrentRow.Cells[0].Value?.ToString(), out int id) && id > 0 ? id : null;
    }
    protected override void ShowError(string message) { lblStatus.ForeColor = Color.Firebrick; lblStatus.Text = message; }
    protected override async Task OnAdd()
    {
        using var f = new KhachEditForm(null);
        if (f.ShowDialog() == DialogResult.OK) await LoadData();
    }
    protected override async Task OnEdit()
    {
        int? id = SelectedId();
        if (!id.HasValue) { ShowError("Vui lòng chọn một khách thuê."); return; }
        var el = await ApiClient.GetRawAsync($"/api/KhachThue/{id}");
        using var f = new KhachEditForm(el);
        if (f.ShowDialog() == DialogResult.OK) await LoadData();
    }
    protected override async Task OnDelete()
    {
        int? id = SelectedId();
        if (!id.HasValue) { ShowError("Vui lòng chọn một khách thuê."); return; }
        if (MessageBox.Show("Xóa mềm khách thuê đã chọn? Hồ sơ phải được giữ trong database, chỉ ẩn khỏi danh sách hoạt động.", "Xác nhận xóa mềm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        await DeleteSelectedAsync(id.Value);
    }
    async Task DeleteSelectedAsync(int id)
    {
        try
        {
            // Hợp đồng đề xuất cho API_TOAN. Tuyệt đối không fallback sang DELETE xóa vật lý.
            var result = await ApiClient.PutAsync($"/api/KhachThue/{id}/xoa-mem", new { });
            if (ApiClient.ElStr(result, "maKhach") != id.ToString() || !bool.TryParse(ApiClient.ElStr(result, "isDeleted"), out bool deleted) || !deleted)
                throw new Exception("API chưa xác nhận xóa mềm (cần maKhach và isDeleted=true). Không báo thao tác thành công.");
        }
        catch (ApiResponseException ex) when (ex.StatusCode is System.Net.HttpStatusCode.NotFound or System.Net.HttpStatusCode.MethodNotAllowed)
        { throw new Exception("API chưa hỗ trợ PUT /api/KhachThue/{id}/xoa-mem. Cần Toàn bổ sung xóa mềm; WinForms không gọi DELETE xóa vật lý.", ex); }
        await LoadData();
    }
}

public class KhachEditForm : Form
{
    TextBox tTen = new(), tCccd = new(), tSdt = new(), tEmail = new(), tDc = new();
    ComboBox cbGt = new();
    JsonElement? src;
    Button btnSave = new() { Text = "Lưu", Dock = DockStyle.Bottom, Height = 38, BackColor = Color.SteelBlue, ForeColor = Color.White };
    Label lblError = new() { Dock = DockStyle.Bottom, Height = 58, ForeColor = Color.Firebrick };
    bool saving;
    public KhachEditForm(JsonElement? src)
    {
        this.src = src; Text = "Khách thuê"; Size = new Size(440, 400); StartPosition = FormStartPosition.CenterParent;
        cbGt.DropDownStyle = ComboBoxStyle.DropDownList;
        cbGt.Items.AddRange(new object[] { "", "Nam", "Nữ", "Khác" }); cbGt.SelectedIndex = 0;
        var p = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 6, ColumnCount = 2, Padding = new Padding(12) };
        p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 125));
        p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (var box in new[] { tTen, tCccd, tSdt, tEmail, tDc }) box.Dock = DockStyle.Fill;
        cbGt.Dock = DockStyle.Fill;
        p.Controls.Add(new Label { Text = "Họ tên *" }); p.Controls.Add(tTen);
        p.Controls.Add(new Label { Text = "CCCD (unique)" }); p.Controls.Add(tCccd);
        p.Controls.Add(new Label { Text = "SĐT" }); p.Controls.Add(tSdt);
        p.Controls.Add(new Label { Text = "Giới tính" }); p.Controls.Add(cbGt);
        p.Controls.Add(new Label { Text = "Email" }); p.Controls.Add(tEmail);
        p.Controls.Add(new Label { Text = "Địa chỉ" }); p.Controls.Add(tDc);
        btnSave.Click += async (s, e) => await Save();
        if (src.HasValue)
        {
            var v = src.Value;
            tTen.Text = ApiClient.ElStr(v, "hoTen", "HoTen"); tCccd.Text = ApiClient.ElStr(v, "cccd", "Cccd", "CCCD");
            tSdt.Text = ApiClient.ElStr(v, "soDienThoai", "SoDienThoai"); tEmail.Text = ApiClient.ElStr(v, "email", "Email"); tDc.Text = ApiClient.ElStr(v, "diaChi", "DiaChi");
            var gender = ApiClient.ElStr(v, "gioiTinh", "GioiTinh");
            if (!cbGt.Items.Contains(gender)) cbGt.Items.Add(gender);
            cbGt.SelectedItem = gender;
        }
        Controls.Add(p); Controls.Add(lblError); Controls.Add(btnSave);
        AcceptButton = btnSave;
    }
    async Task Save()
    {
        if (saving) return;
        saving = true; btnSave.Enabled = false; lblError.Text = "";
        try
        {
            string ten = tTen.Text.Trim(), cccd = tCccd.Text.Trim(), phone = tSdt.Text.Trim(), email = tEmail.Text.Trim();
            if (string.IsNullOrWhiteSpace(ten)) throw new Exception("Vui lòng nhập họ tên khách thuê.");
            if (ten.Length > 100 || email.Length > 100 || tDc.Text.Trim().Length > 300)
                throw new Exception("Họ tên và email tối đa 100 ký tự; địa chỉ tối đa 300 ký tự.");
            if (cccd.Length > 0 && (cccd.Length != 12 || cccd.Any(ch => ch < '0' || ch > '9')))
                throw new Exception("CCCD phải gồm đúng 12 chữ số.");
            if (phone.Length > 0 && (phone.Length != 10 || phone[0] != '0' || phone.Any(ch => ch < '0' || ch > '9')))
                throw new Exception("Số điện thoại phải gồm 10 chữ số và bắt đầu bằng 0.");
            if (email.Length > 0 && !System.Text.RegularExpressions.Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                throw new Exception("Email không đúng định dạng.");
            string? ngaySinh = src.HasValue ? ApiClient.ElStr(src.Value, "ngaySinh") : null;
            var body = new { hoTen = ten, cccd = cccd.Length == 0 ? null : cccd, soDienThoai = phone.Length == 0 ? null : phone,
                gioiTinh = string.IsNullOrWhiteSpace(cbGt.SelectedItem?.ToString()) ? null : cbGt.SelectedItem.ToString(),
                email = email.Length == 0 ? null : email, diaChi = string.IsNullOrWhiteSpace(tDc.Text) ? null : tDc.Text.Trim(),
                ngaySinh = string.IsNullOrWhiteSpace(ngaySinh) ? null : ngaySinh };
            if (src.HasValue)
            {
                if (!int.TryParse(ApiClient.ElStr(src.Value, "maKhach"), out int id) || id <= 0)
                    throw new Exception("API trả mã khách thuê không hợp lệ. Không gửi yêu cầu sửa.");
                await ApiClient.PutAsync($"/api/KhachThue/{id}", body);
            }
            else await ApiClient.PostAsync("/api/KhachThue", body);
            DialogResult = DialogResult.OK; Close();
        }
        catch (Exception ex) { lblError.Text = ex.Message; }
        finally { saving = false; if (!IsDisposed) btnSave.Enabled = true; }
    }
}
