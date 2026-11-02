using System.Data;
using System.Globalization;
using System.Text;
using System.Text.Json;
using QLPT.WinForms.Services;

namespace QLPT.WinForms.Forms;

public sealed class BaoCaoForm : Form
{
    readonly ComboBox type = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 170 };
    readonly ComboBox site = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
    readonly DateTimePicker from = new() { Format = DateTimePickerFormat.Short, Width = 110, Value = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1) };
    readonly DateTimePicker to = new() { Format = DateTimePickerFormat.Short, Width = 110, Value = DateTime.Today };
    readonly DataGridView grid = new() { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells };
    readonly Label summary = new() { Dock = DockStyle.Top, Height = 90, Padding = new Padding(10), AutoEllipsis = true };
    readonly Button load = new() { Text = "Xem báo cáo", AutoSize = true };
    JsonElement snapshot;
    readonly List<int?> siteIds = [null];
    static readonly string[] Types = ["doanh-thu", "phong", "khach", "hop-dong"];

    public BaoCaoForm()
    {
        Text = "Báo cáo phòng / doanh thu / khách / hợp đồng";
        type.Items.AddRange(["Doanh thu thực thu", "Phòng trọ", "Khách thuê", "Hợp đồng"]); type.SelectedIndex = 0;
        site.Items.Add("Tất cả cơ sở"); site.SelectedIndex = 0;
        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 84, AutoScroll = true, Padding = new Padding(8) };
        var csv = new Button { Text = "Xuất CSV", AutoSize = true };
        var print = new Button { Text = "In / Lưu PDF", AutoSize = true };
        bar.Controls.AddRange([type, site, new Label { Text = "Từ", AutoSize = true }, from,
            new Label { Text = "Đến", AutoSize = true }, to, load, csv, print]);
        load.Click += async (_, _) => await RefreshReport();
        csv.Click += (_, _) => ExportCsv();
        print.Click += (_, _) => { if (snapshot.ValueKind == JsonValueKind.Object) ExportService.BaoCao(snapshot, (DataTable)grid.DataSource); };
        Controls.Add(grid); Controls.Add(summary); Controls.Add(bar);
        Load += async (_, _) =>
        {
            try
            {
                var sites = await ApiClient.GetRawAsync("/api/CoSo");
                foreach (var s in sites.EnumerateArray())
                { siteIds.Add(int.Parse(ApiClient.ElStr(s, "maCoSo"))); site.Items.Add(ApiClient.ElStr(s, "tenCoSo")); }
                await RefreshReport();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        };
    }

    async Task RefreshReport()
    {
        if (!load.Enabled) return;
        load.Enabled = false;
        try
        {
            if (from.Value.Date > to.Value.Date) throw new ArgumentException("Ngày bắt đầu phải trước hoặc bằng ngày kết thúc.");
            string filter = siteIds[site.SelectedIndex] is int id ? $"&maCoSo={id}" : "";
            var data = await ApiClient.GetRawAsync($"/api/BaoCao?loai={Types[type.SelectedIndex]}&tuNgay={from.Value:yyyy-MM-dd}&denNgay={to.Value:yyyy-MM-dd}{filter}");
            var table = new DataTable();
            var columns = data.GetProperty("cot").EnumerateArray().ToArray();
            foreach (var c in columns) table.Columns.Add(ApiClient.ElStr(c, "ten"));
            foreach (var row in data.GetProperty("dong").EnumerateArray())
                table.Rows.Add(columns.Select(c => (object)Format(ApiClient.ElStr(row, ApiClient.ElStr(c, "truong")), ApiClient.ElStr(c, "kieu"))).ToArray());
            grid.DataSource = table;
            summary.Text = string.Join("   |   ", data.GetProperty("tomTat").EnumerateArray().Select(s =>
                ApiClient.ElStr(s, "chiTieu") + ": " + Format(ApiClient.ElStr(s, "giaTri"), ApiClient.ElStr(s, "kieu")))) +
                "\n" + ApiClient.ElStr(data, "ghiChu") + $"\nBộ lọc: {site.Text} — {from.Value:dd/MM/yyyy} đến {to.Value:dd/MM/yyyy}";
            snapshot = data;
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Báo cáo"); }
        finally { if (!IsDisposed) load.Enabled = true; }
    }

    static string Format(string text, string kind)
    {
        if (kind == "tien") return VnMoney.Format(text) + " đ";
        if (kind == "ngay" && DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) return date.ToString("dd/MM/yyyy");
        return text;
    }

    void ExportCsv()
    {
        if (snapshot.ValueKind != JsonValueKind.Object) return;
        using var dialog = new SaveFileDialog { Filter = "CSV UTF-8 (*.csv)|*.csv", FileName = $"BaoCao_{ApiClient.ElStr(snapshot, "loai")}_{DateTime.Now:yyyyMMdd_HHmmss}.csv" };
        if (dialog.ShowDialog() != DialogResult.OK) return;
        try
        {
            var columns = snapshot.GetProperty("cot").EnumerateArray().ToArray();
            using var writer = new StreamWriter(dialog.FileName, false, new UTF8Encoding(true));
            writer.WriteLine(string.Join(";", columns.Select(c => Csv(ApiClient.ElStr(c, "ten")))));
            foreach (var row in snapshot.GetProperty("dong").EnumerateArray())
                writer.WriteLine(string.Join(";", columns.Select(c => Csv(ApiClient.ElStr(row, ApiClient.ElStr(c, "truong"))))));
            MessageBox.Show("Đã lưu báo cáo CSV UTF-8.");
        }
        catch (Exception ex) { MessageBox.Show(ex.Message); }
    }

    public static string Csv(string text)
    {
        if (text.TrimStart().StartsWith("=") || text.TrimStart().StartsWith("+") || text.TrimStart().StartsWith("-") || text.TrimStart().StartsWith("@")) text = "'" + text;
        return "\"" + text.Replace("\"", "\"\"") + "\"";
    }
}

public sealed class SaoLuuForm : Form
{
    readonly DataGridView grid = new() { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
    readonly Button create = new() { Text = "Tạo bản sao lưu", AutoSize = true };

    public SaoLuuForm()
    {
        Text = "Sao lưu / Khôi phục database";
        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(8) };
        var refresh = new Button { Text = "Tải danh sách", AutoSize = true };
        bar.Controls.AddRange([refresh, create]);
        refresh.Click += async (_, _) => await LoadData();
        create.Click += async (_, _) =>
        {
            create.Enabled = false;
            try { var b = await ApiClient.PostLongAsync("/api/SaoLuu", new { }); MessageBox.Show("Đã sao lưu và kiểm tra checksum:\n" + ApiClient.ElStr(b, "duongDan")); await LoadData(); }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
            finally { if (!IsDisposed) create.Enabled = true; }
        };
        var guide = new Label { Dock = DockStyle.Bottom, Height = 110, Padding = new Padding(10), Text =
            "File .bak được lưu trên máy SQL Server.\nKhôi phục: dừng API, đóng ứng dụng và các cửa sổ query; mở RESTORE_DB.cmd trong thư mục đã giải nén.\nChọn mã sao lưu, nhập đúng tên database. Chương trình kiểm tra file và tạo bản dự phòng trước khi khôi phục.\nSau khi khôi phục, mở START_API.cmd rồi START_WINFORMS.cmd và đăng nhập lại." };
        Controls.Add(grid); Controls.Add(guide); Controls.Add(bar);
        Load += async (_, _) => await LoadData();
    }

    async Task LoadData()
    {
        try
        {
            var rows = await ApiClient.GetRawAsync("/api/SaoLuu");
            var table = new DataTable(); foreach (string c in new[] { "Mã sao lưu", "Ngày", "Đường dẫn trên SQL Server", "Dung lượng (byte)" }) table.Columns.Add(c);
            foreach (var b in rows.EnumerateArray()) table.Rows.Add(ApiClient.ElStr(b, "maSaoLuu"), ApiClient.ElStr(b, "ngayTao"), ApiClient.ElStr(b, "duongDan"), ApiClient.ElStr(b, "soByte"));
            grid.DataSource = table;
        }
        catch (Exception ex) { MessageBox.Show(ex.Message); }
    }
}
