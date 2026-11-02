using System.Data;
using System.Text.Json;
using QLPT.WinForms.Services;

namespace QLPT.WinForms.Forms;

public abstract class ContractResourceForm : Form
{
    protected readonly ComboBox contracts = new() { Width = 420, DropDownStyle = ComboBoxStyle.DropDownList };
    protected readonly FlowLayoutPanel bar = new() { Dock = DockStyle.Top, Height = 48, Padding = new Padding(6), AutoScroll = true };
    protected readonly DataGridView grid = new() { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
    protected readonly List<JsonElement> contractData = [];
    protected JsonElement Contract => contractData[contracts.SelectedIndex];
    protected int ContractId => int.Parse(ApiClient.ElStr(Contract, "maHopDong"));

    protected ContractResourceForm(string title)
    {
        Text = title;
        bar.Controls.AddRange([new Label { Text = "Hợp đồng", AutoSize = true }, contracts]);
        var reload = new Button { Text = "Tải lại", AutoSize = true };
        bar.Controls.Add(reload);
        reload.Click += async (_, _) => await Safe(LoadContracts);
        contracts.SelectedIndexChanged += async (_, _) => { if (contracts.SelectedIndex >= 0) await Safe(LoadData); };
        Controls.Add(grid); Controls.Add(bar);
        Load += async (_, _) => await Safe(LoadContracts);
    }

    async Task LoadContracts()
    {
        var all = await ApiClient.GetRawAsync("/api/HopDong");
        contracts.Items.Clear(); contractData.Clear();
        foreach (var h in all.EnumerateArray())
        {
            contractData.Add(h);
            string room = h.TryGetProperty("maPhongNavigation", out var p) ? ApiClient.ElStr(p, "tenPhong") : "";
            string customer = h.TryGetProperty("maKhachDaiDienNavigation", out var k) ? ApiClient.ElStr(k, "hoTen") : "";
            contracts.Items.Add($"#{ApiClient.ElStr(h, "maHopDong")} — {room} — {customer} — {ApiClient.ElStr(h, "trangThai")}");
        }
        if (contracts.Items.Count > 0) contracts.SelectedIndex = 0;
    }

    protected void AddAction(string text, Func<Task> action)
    {
        var button = new Button { Text = text, AutoSize = true };
        button.Click += async (_, _) =>
        {
            if (contracts.SelectedIndex < 0) return;
            button.Enabled = false;
            try { await Safe(action); } finally { if (!IsDisposed) button.Enabled = true; }
        };
        bar.Controls.Add(button);
    }
    protected async Task Safe(Func<Task> action) { try { await action(); } catch (Exception ex) { MessageBox.Show(ex.Message, Text); } }
    protected static DataTable Table(params string[] names) { var t = new DataTable(); foreach (var n in names) t.Columns.Add(n); return t; }
    protected static NumericUpDown Number(decimal value = 0) => new() { Minimum = 0, Maximum = 9999999999999999.99m, DecimalPlaces = 2, Value = value, Width = 230, ThousandsSeparator = true };
    protected internal static Form Editor(string title, params (string name, Control control)[] fields)
    {
        var form = new Form { Text = title, Size = new Size(520, 100 + fields.Sum(f => Math.Max(42, f.control.Height + 10))), StartPosition = FormStartPosition.CenterParent, AutoScroll = true };
        var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = fields.Length + 1, Padding = new Padding(10) };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190)); table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < fields.Length; i++)
        { table.RowStyles.Add(new RowStyle(SizeType.Absolute, Math.Max(42, fields[i].control.Height + 10))); table.Controls.Add(new Label { Text = fields[i].name, AutoSize = true }, 0, i); table.Controls.Add(fields[i].control, 1, i); }
        var save = new Button { Text = "Lưu", DialogResult = DialogResult.OK }; var cancel = new Button { Text = "Hủy", DialogResult = DialogResult.Cancel };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill }; buttons.Controls.AddRange([save, cancel]);
        table.Controls.Add(buttons, 1, fields.Length); form.Controls.Add(table); form.AcceptButton = save; form.CancelButton = cancel;
        return form;
    }
    protected abstract Task LoadData();
}

public sealed class DangKyDichVuForm : ContractResourceForm
{
    public DangKyDichVuForm() : base("Dịch vụ theo hợp đồng")
    { AddAction("Đăng ký dịch vụ", Add); AddAction("Ngừng dịch vụ", Stop); }

    protected override async Task LoadData()
    {
        var rows = await ApiClient.GetRawAsync($"/api/DanhMucMoRong/dang-ky-dich-vu/{ContractId}");
        var table = Table("Mã", "Dịch vụ", "Từ", "Đến", "Đơn giá", "Trạng thái");
        foreach (var d in rows.EnumerateArray()) table.Rows.Add(ApiClient.ElStr(d, "maDangKy"),
            d.TryGetProperty("maDichVuNavigation", out var s) ? ApiClient.ElStr(s, "tenDichVu") : ApiClient.ElStr(d, "maDichVu"),
            ApiClient.ElStr(d, "ngayBatDau"), ApiClient.ElStr(d, "ngayKetThuc"), VnMoney.Format(ApiClient.ElStr(d, "donGiaApDung")), ApiClient.ElStr(d, "trangThai"));
        grid.DataSource = table;
    }
    async Task Add()
    {
        var data = (await ApiClient.GetRawAsync("/api/DichVu")).EnumerateArray().ToArray();
        var service = new ComboBox { Width = 250, DropDownStyle = ComboBoxStyle.DropDownList };
        foreach (var d in data) service.Items.Add($"{ApiClient.ElStr(d, "tenDichVu")} — {ApiClient.ElStr(d, "donViTinh")}");
        if (data.Length == 0) throw new InvalidOperationException("Chưa có danh mục dịch vụ.");
        var price = Number(); var start = new DateTimePicker { Format = DateTimePickerFormat.Short };
        var end = new DateTimePicker { Format = DateTimePickerFormat.Short, ShowCheckBox = true, Checked = false };
        service.SelectedIndexChanged += (_, _) => { if (decimal.TryParse(ApiClient.ElStr(data[service.SelectedIndex], "donGia"), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var value)) price.Value = Math.Clamp(value, 0, price.Maximum); };
        service.SelectedIndex = 0;
        using var form = Editor("Đăng ký dịch vụ", ("Dịch vụ", service), ("Đơn giá áp dụng", price), ("Bắt đầu", start), ("Kết thúc (tùy chọn)", end));
        if (form.ShowDialog(this) != DialogResult.OK) return;
        await ApiClient.PostAsync("/api/DanhMucMoRong/dang-ky-dich-vu", new { maHopDong = ContractId,
            maDichVu = int.Parse(ApiClient.ElStr(data[service.SelectedIndex], "maDichVu")), donGiaApDung = price.Value,
            ngayBatDau = start.Value.ToString("yyyy-MM-dd"), ngayKetThuc = end.Checked ? end.Value.ToString("yyyy-MM-dd") : null });
        await LoadData();
    }
    async Task Stop()
    {
        if (grid.CurrentRow == null || MessageBox.Show("Ngừng từ hôm nay và giữ lịch sử đăng ký?", Text, MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        await ApiClient.DeleteAsync($"/api/DanhMucMoRong/dang-ky-dich-vu/{grid.CurrentRow.Cells[0].Value}"); await LoadData();
    }
}

public sealed class BanGiaoForm : ContractResourceForm
{
    JsonElement[] handovers = [];
    public BanGiaoForm() : base("Bàn giao tài sản / Nhận và trả phòng")
    { AddAction("Lập biên bản", Add); AddAction("In biên bản đã lưu", Print); }
    protected override async Task LoadData()
    {
        handovers = (await ApiClient.GetRawAsync($"/api/DanhMucMoRong/ban-giao/{ContractId}")).EnumerateArray().ToArray();
        var table = Table("Mã", "Loại", "Ngày", "Điện", "Nước", "Ghi chú");
        foreach (var b in handovers) table.Rows.Add(ApiClient.ElStr(b, "maBanGiao"), ApiClient.ElStr(b, "loaiBanGiao"), ApiClient.ElStr(b, "ngayBanGiao"), ApiClient.ElStr(b, "chiSoDien"), ApiClient.ElStr(b, "chiSoNuoc"), ApiClient.ElStr(b, "ghiChu"));
        grid.DataSource = table;
    }
    async Task Add()
    {
        int roomId = int.Parse(ApiClient.ElStr(Contract, "maPhong"));
        var assets = (await ApiClient.GetRawAsync($"/api/TaiSan/phong/{roomId}")).EnumerateArray().ToArray();
        var kind = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList }; kind.Items.AddRange(["Nhận phòng", "Trả phòng"]); kind.SelectedIndex = 0;
        var date = new DateTimePicker { Format = DateTimePickerFormat.Short }; var electric = Number(); var water = Number();
        var notes = new TextBox { Width = 240, MaxLength = 300 };
        using var form = Editor("Thông tin biên bản", ("Loại", kind), ("Ngày bàn giao", date), ("Chỉ số điện", electric), ("Chỉ số nước", water), ("Ghi chú", notes));
        if (form.ShowDialog(this) != DialogResult.OK) return;
        var details = new List<object>();
        foreach (var asset in assets)
        {
            string name = asset.TryGetProperty("maTaiSanNavigation", out var a) ? ApiClient.ElStr(a, "tenTaiSan") : ApiClient.ElStr(asset, "maTaiSan");
            var condition = new TextBox { Width = 240, MaxLength = 100, Text = ApiClient.ElStr(asset, "tinhTrang") };
            var quantity = Number(InputRules.NonNegativeNumber(ApiClient.ElStr(asset, "soLuong"), "Số lượng")); quantity.DecimalPlaces = 0;
            var fine = Number();
            using var item = Editor("Tài sản: " + name, ("Số lượng bàn giao", quantity), ("Tình trạng kiểm tra", condition), ("Phạt/hư hao thỏa thuận", fine));
            if (item.ShowDialog(this) != DialogResult.OK) return;
            details.Add(new { maTaiSan = int.Parse(ApiClient.ElStr(asset, "maTaiSan")), soLuong = (int)quantity.Value, tinhTrang = condition.Text, chiPhiPhat = fine.Value });
        }
        await ApiClient.PostAsync("/api/DanhMucMoRong/ban-giao", new { maHopDong = ContractId, maPhong = roomId,
            loaiBanGiao = kind.Text, ngayBanGiao = date.Value.ToString("yyyy-MM-dd"), chiSoDien = electric.Value,
            chiSoNuoc = water.Value, ghiChu = notes.Text, chiTietBanGiaos = details });
        await LoadData();
    }
    Task Print()
    {
        if (grid.CurrentRow == null) return Task.CompletedTask;
        var handover = handovers.First(b => ApiClient.ElStr(b, "maBanGiao") == grid.CurrentRow.Cells[0].Value?.ToString());
        string room = Contract.TryGetProperty("maPhongNavigation", out var p) ? ApiClient.ElStr(p, "tenPhong") : "";
        string customer = Contract.TryGetProperty("maKhachDaiDienNavigation", out var k) ? ApiClient.ElStr(k, "hoTen") : "";
        ExportService.BanGiao(handover, room, customer); return Task.CompletedTask;
    }
}
