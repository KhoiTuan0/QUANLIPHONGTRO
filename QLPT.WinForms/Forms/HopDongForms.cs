using System.Text.Json;
using QLPT.WinForms.Services;

namespace QLPT.WinForms.Forms;

public class DatPhongForm : CrudForm
{
    public DatPhongForm() : base("Đặt phòng / Giữ chỗ (cọc)")
    {
        bAdd.Text = "Đặt mới"; bEdit.Visible = false; bDel.Visible = false;
        var bHuy = new Button { Text = "Hủy giữ chỗ", Width = 110 }; bHuy.Click += async (s, e) => await Safe(OnHuy);
        var bNhan = new Button { Text = "Lập HĐ / nhận phòng", Width = 150 }; bNhan.Click += async (s, e) => await Safe(OnNhan);
        bar.Controls.Add(bHuy); bar.Controls.Add(bNhan);
    }
    protected override async Task LoadData()
    {
        var arr = await ApiClient.GetRawAsync("/api/DatPhong");
        grid.DataSource = ToTableVn(arr, new HashSet<string> { "Cọc" }, ("Mã", new[] { "maDatPhong" }), ("Phòng", new[] { "tenPhong", "maPhong" }), ("Khách", new[] { "tenKhach", "maKhach" }), ("Ngày đặt", new[] { "ngayDat" }), ("Dự kiến nhận", new[] { "ngayDuKienNhan" }), ("Cọc", new[] { "tienCoc" }), ("Trạng thái", new[] { "trangThai" }));
    }
    protected override async Task OnAdd()
    {
        string mp = Input("Đặt phòng", "Mã phòng (phải Trống):"); if (mp == null) return;
        string mk = Input("Đặt phòng", "Mã khách:"); if (mk == null) return;
        string coc = Input("Đặt phòng", "Tiền cọc (VD 1.000.000):", "1.000.000"); if (coc == null) return;
        await ApiClient.PostAsync("/api/DatPhong", new { maPhong = InputRules.PositiveId(mp, "Mã phòng"), maKhach = InputRules.PositiveId(mk, "Mã khách"), ngayDat = DateTime.Now.ToString("yyyy-MM-dd"), tienCoc = InputRules.NonNegativeMoney(coc, "Tiền cọc") });
        await LoadData();
    }
    async Task OnHuy()
    {
        if (grid.CurrentRow == null) return;
        if (MessageBox.Show("Hủy phiếu giữ chỗ đang chọn? Nếu thực hoàn tiền cọc, ghi phiếu chi riêng để giữ lịch sử thu/chi.", Text,
            MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        await ApiClient.PutAsync($"/api/DatPhong/{grid.CurrentRow.Cells[0].Value}/huy", new { });
        await LoadData();
    }
    async Task OnNhan()
    {
        if (grid.CurrentRow == null) return;
        int id = InputRules.PositiveId(grid.CurrentRow.Cells[0].Value?.ToString() ?? "", "Mã phiếu");
        var bookings = await ApiClient.GetRawAsync("/api/DatPhong");
        var booking = bookings.EnumerateArray().FirstOrDefault(d => ApiClient.ElStr(d, "maDatPhong") == id.ToString());
        if (booking.ValueKind != JsonValueKind.Object) throw new InvalidOperationException("Phiếu đã thay đổi, hãy tải lại danh sách.");
        string status = ApiClient.ElStr(booking, "trangThai");
        if (status == "Đã đặt cọc")
        {
            decimal deposit = booking.TryGetProperty("tienCoc", out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var money) ? money : 0;
            if (!await HopDongForm.CreateContractAsync(InputRules.PositiveId(ApiClient.ElStr(booking, "maPhong"), "Mã phòng"),
                InputRules.PositiveId(ApiClient.ElStr(booking, "maKhach"), "Mã khách"), deposit)) return;
        }
        else if (status == "Đã nhận phòng")
            await ApiClient.PutAsync($"/api/DatPhong/{id}/nhan-phong", new { });
        else throw new InvalidOperationException("Chỉ lập hợp đồng từ phiếu đang giữ chỗ.");
        await LoadData();
    }
}

public class HopDongForm : Form
{
    DataGridView grid = new() { Dock = DockStyle.Fill, ReadOnly = true, SelectionMode = DataGridViewSelectionMode.FullRowSelect, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
    FlowLayoutPanel bar = new() { Dock = DockStyle.Top, Height = 44, Padding = new Padding(6) };
    public HopDongForm()
    {
        Text = "Hợp đồng — Hết hạn / Gia hạn / Thanh lý + Xuất biểu mẫu";
        var bLoad = new Button { Text = "Tải lại" }; bLoad.Click += async (s, e) => await LoadData();
        var bAdd = new Button { Text = "Tạo HĐ" }; bAdd.Click += async (s, e) => await TaoHd();
        var bGiaHan = new Button { Text = "Gia hạn", Width = 90 }; bGiaHan.Click += async (s, e) => await GiaHan();
        var bThanhLy = new Button { Text = "Thanh lý", Width = 90 }; bThanhLy.Click += async (s, e) => await ThanhLy();
        var bXuat = new Button { Text = "Xuất HĐ (HTML)", Width = 120 }; bXuat.Click += async (s, e) => await XuatHd();
        var bSap = new Button { Text = "Sắp hết hạn", Width = 110 }; bSap.Click += async (s, e) => await SapHetHan();
        bar.Controls.AddRange(new Control[] { bLoad, bAdd, bGiaHan, bThanhLy, bXuat, bSap });
        Controls.Add(grid); Controls.Add(bar);
        Load += async (s, e) => await LoadData();
    }
    async Task LoadData()
    {
        try
        {
            var arr = await ApiClient.GetRawAsync("/api/HopDong");
            var dt = new System.Data.DataTable();
            foreach (var c in new[] { "Mã", "Phòng", "Khách", "Từ", "Đến", "Giá", "Cọc", "Trạng thái" }) dt.Columns.Add(c);
            foreach (var h in arr.EnumerateArray())
                dt.Rows.Add(ApiClient.ElStr(h, "maHopDong", "MaHopDong"), ApiClient.ElStr(h, "maPhong", "MaPhong"),
                    ApiClient.ElStr(h, "maKhachDaiDien", "MaKhachDaiDien"), ApiClient.ElStr(h, "ngayBatDau", "NgayBatDau"),
                    ApiClient.ElStr(h, "ngayKetThuc", "NgayKetThuc"), Services.VnMoney.Format(ApiClient.ElStr(h, "giaThueThang", "GiaThueThang")),
                    Services.VnMoney.Format(ApiClient.ElStr(h, "tienCoc", "TienCoc")), ApiClient.ElStr(h, "trangThai", "TrangThai"));
            grid.DataSource = dt;
        }
        catch (Exception ex) { MessageBox.Show(ex.Message); }
    }
    async Task SapHetHan()
    {
        try
        {
            var arr = await ApiClient.GetRawAsync("/api/HopDong/sap-het-han?days=30");
            int n = arr.ValueKind == JsonValueKind.Array ? arr.GetArrayLength() : 0;
            MessageBox.Show($"Có {n} hợp đồng hết hạn trong 30 ngày.");
            var dt = new System.Data.DataTable();
            foreach (var c in new[] { "Mã", "Phòng", "Khách", "Từ", "Đến", "Giá", "Cọc", "Trạng thái" }) dt.Columns.Add(c);
            foreach (var h in arr.EnumerateArray())
                dt.Rows.Add(ApiClient.ElStr(h, "maHopDong"), ApiClient.ElStr(h, "maPhong"), ApiClient.ElStr(h, "maKhachDaiDien"),
                    ApiClient.ElStr(h, "ngayBatDau"), ApiClient.ElStr(h, "ngayKetThuc"), VnMoney.Format(ApiClient.ElStr(h, "giaThueThang")),
                    VnMoney.Format(ApiClient.ElStr(h, "tienCoc")), ApiClient.ElStr(h, "trangThai"));
            grid.DataSource = dt;
        }
        catch (Exception ex) { MessageBox.Show(ex.Message); }
    }
    async Task TaoHd()
    {
        try
        {
            if (await CreateContractAsync()) await LoadData();
        }
        catch (Exception ex) { MessageBox.Show(ex.Message); }
    }
    internal static async Task<bool> CreateContractAsync(int? roomId = null, int? customerId = null, decimal? deposit = null)
    {
        string Prompt(string label, string value) => Microsoft.VisualBasic.Interaction.InputBox(label, "Tạo hợp đồng", value).Trim();
        string mp = Prompt("Mã phòng:", roomId?.ToString() ?? "1"); if (mp == "") return false;
        string mk = Prompt("Mã khách đại diện / khách giữ chỗ:", customerId?.ToString() ?? "1"); if (mk == "") return false;
        int selectedRoom = InputRules.PositiveId(mp, "Mã phòng");
        var room = await ApiClient.GetRawAsync($"/api/Phong/{selectedRoom}");
        string gia = Prompt("Giá thuê/tháng (VD 2.500.000):", VnMoney.Format(ApiClient.ElStr(room, "giaThue"))); if (gia == "") return false;
        string coc = Prompt("Tổng tiền cọc hợp đồng (đối chiếu tiền đã đặt):", deposit.HasValue ? VnMoney.Format(deposit.Value) : "2.500.000"); if (coc == "") return false;
        string tu = Prompt("Ngày bắt đầu (yyyy-MM-dd):", DateTime.Today.ToString("yyyy-MM-dd")); if (tu == "") return false;
        if (!DateTime.TryParseExact(tu, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var start) || start.Year is < 1900 or > 2100)
            throw new ArgumentException("Ngày bắt đầu đúng dạng yyyy-MM-dd, năm 1900–2100.");
        string den = Prompt("Ngày kết thúc (yyyy-MM-dd):", start.AddYears(1).ToString("yyyy-MM-dd")); if (den == "") return false;
        DateTime end = InputRules.EndAfterStart(den, start);
        await ApiClient.PostAsync("/api/HopDong", new
        {
            maPhong = selectedRoom, maKhachDaiDien = InputRules.PositiveId(mk, "Mã khách"),
            ngayBatDau = start.ToString("yyyy-MM-dd"), ngayKetThuc = end.ToString("yyyy-MM-dd"),
            giaThueThang = InputRules.NonNegativeMoney(gia, "Giá thuê"), tienCoc = InputRules.NonNegativeMoney(coc, "Tiền cọc"), trangThai = "Đang hiệu lực"
        });
        return true;
    }
    async Task GiaHan()
    {
        if (grid.CurrentRow == null) return;
        try
        {
            int id = int.Parse(grid.CurrentRow.Cells[0].Value.ToString()!);
            string den = Microsoft.VisualBasic.Interaction.InputBox("Ngày kết thúc mới (yyyy-MM-dd):", "Gia hạn", DateTime.Now.AddYears(1).ToString("yyyy-MM-dd"));
            if (den == "") return;
            DateTime currentEnd = DateTime.TryParse(grid.CurrentRow.Cells[4].Value?.ToString(), out var parsed) ? parsed : DateTime.Today;
            DateTime newEnd = InputRules.EndAfterStart(den, currentEnd);
            string gia = Microsoft.VisualBasic.Interaction.InputBox("Giá thuê gia hạn (áp dụng từ tháng sau tháng hết hạn cũ):", "Gia hạn", grid.CurrentRow.Cells[5].Value?.ToString() ?? "0");
            if (gia == "") return;
            await ApiClient.PostAsync($"/api/HopDong/{id}/gia-han", new { ngayKetThucMoi = newEnd.ToString("yyyy-MM-dd"), giaThueMoi = InputRules.NonNegativeMoney(gia, "Giá gia hạn") });
            MessageBox.Show("Gia hạn thành công"); await LoadData();
        }
        catch (Exception ex) { MessageBox.Show(ex.Message); }
    }
    async Task ThanhLy()
    {
        if (grid.CurrentRow == null) return;
        try
        {
            int id = int.Parse(grid.CurrentRow.Cells[0].Value.ToString()!);
            string hoan = Microsoft.VisualBasic.Interaction.InputBox("Tiền hoàn cọc (VD 2.100.000):", "Thanh lý", "0");
            if (hoan == "") return;
            decimal tienHoan = InputRules.NonNegativeMoney(hoan, "Tiền hoàn cọc");
            var contract = await ApiClient.GetRawAsync($"/api/HopDong/{id}");
            var invoices = await ApiClient.GetRawAsync($"/api/HoaDon/hop-dong/{id}");
            decimal Money(JsonElement x, string field) => decimal.TryParse(ApiClient.ElStr(x, field), System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0;
            decimal debt = invoices.EnumerateArray().Sum(x => Money(x, "conLai"));
            string fine = Microsoft.VisualBasic.Interaction.InputBox("Tiền phạt/hư hao (không bao gồm nợ hóa đơn):", "Thanh lý", "0");
            if (fine == "") return;
            decimal penalty = InputRules.NonNegativeMoney(fine, "Tiền phạt");
            if (MessageBox.Show($"Nợ: {debt:N0}; phạt: {penalty:N0}; hoàn cọc: {tienHoan:N0}. Xác nhận thanh lý?",
                "Thanh lý", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            await ApiClient.PostAsync($"/api/HopDong/{id}/thanh-ly", new { tienHoanCoc = tienHoan, tienNo = debt, tienPhat = penalty, ghiChu = "Thanh lý từ WinForms" });
            string roomName = contract.TryGetProperty("maPhongNavigation", out var room) ? ApiClient.ElStr(room, "tenPhong") : "HĐ " + id;
            string path = ExportService.BienBanThanhLy(roomName, Money(contract, "tienCoc"), debt + penalty, tienHoan, "Thanh lý HĐ " + id);
            MessageBox.Show("Thanh lý xong. Đã xuất: " + path);
            await LoadData();
        }
        catch (Exception ex) { MessageBox.Show(ex.Message); }
    }
    async Task XuatHd()
    {
        if (grid.CurrentRow == null) return;
        try
        {
            var h = await ApiClient.GetRawAsync($"/api/HopDong/{grid.CurrentRow.Cells[0].Value}");
            var room = h.GetProperty("maPhongNavigation");
            var customer = h.GetProperty("maKhachDaiDienNavigation");
            decimal Money(string field) => decimal.TryParse(ApiClient.ElStr(h, field), System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;
            DateTime? signed = DateTime.TryParse(ApiClient.ElStr(h, "ngayKy"), System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var signedDate) ? signedDate : null;
            string site = room.TryGetProperty("maCoSoNavigation", out var siteData) && siteData.ValueKind == JsonValueKind.Object
                ? ApiClient.ElStr(siteData, "tenCoSo") : "";
            string path = ExportService.HopDong(ApiClient.ElStr(room, "tenPhong"), ApiClient.ElStr(customer, "hoTen"),
                ApiClient.ElStr(customer, "cccd"), Money("giaThueThang"), Money("tienCoc"),
                DateTime.Parse(ApiClient.ElStr(h, "ngayBatDau"), System.Globalization.CultureInfo.InvariantCulture),
                DateTime.Parse(ApiClient.ElStr(h, "ngayKetThuc"), System.Globalization.CultureInfo.InvariantCulture), ApiClient.ElStr(h, "ghiChu"),
                signed, ApiClient.ElStr(h, "maHopDong"), site, ApiClient.ElStr(customer, "diaChi"), ApiClient.ElStr(customer, "soDienThoai"));
            MessageBox.Show("Đã xuất: " + path);
        }
        catch (Exception ex) { MessageBox.Show("Không xuất được hợp đồng: " + ex.Message); }
    }
}

public class BaoTraForm : CrudForm
{
    public BaoTraForm() : base("Báo trả phòng") { bEdit.Visible = false; bDel.Visible = false; }
    protected override async Task LoadData()
    {
        var arr = await ApiClient.GetRawAsync("/api/BaoTraPhong");
        var dt = new System.Data.DataTable();
        foreach (var c in new[] { "Mã", "HĐ", "Khách", "Ngày báo", "Dự kiến trả", "Lý do", "Trạng thái" }) dt.Columns.Add(c);
        foreach (var x in arr.EnumerateArray())
            dt.Rows.Add(ApiClient.ElStr(x, "maBaoTra", "MaBaoTra"), ApiClient.ElStr(x, "maHopDong", "MaHopDong"), ApiClient.ElStr(x, "maKhach", "MaKhach"), ApiClient.ElStr(x, "ngayBao", "NgayBao"), ApiClient.ElStr(x, "ngayDuKienTra", "NgayDuKienTra"), ApiClient.ElStr(x, "lyDo", "LyDo"), ApiClient.ElStr(x, "trangThai", "TrangThai"));
        grid.DataSource = dt;
    }
    protected override async Task OnAdd()
    {
        string hd = Microsoft.VisualBasic.Interaction.InputBox("Mã hợp đồng:", "Báo trả", ""); if (hd == "") return;
        string mk = Microsoft.VisualBasic.Interaction.InputBox("Mã khách:", "Báo trả", ""); if (mk == "") return;
        string ly = Microsoft.VisualBasic.Interaction.InputBox("Lý do:", "Báo trả", "Hết hạn"); if (ly == "") return;
        await ApiClient.PostAsync("/api/BaoTraPhong", new { maHopDong = int.Parse(hd), maKhach = int.Parse(mk), lyDo = ly });
        await LoadData();
    }
}

public sealed class CuTruForm : ContractResourceForm
{
    public CuTruForm() : base("Cư trú / Tạm trú / Tạm vắng")
    {
        AddAction("Thêm người ở cùng", Add);
        AddAction("Đã khai báo", () => Update(new { trangThaiKhaiBao = "Đã khai báo" }));
        AddAction("Tạm vắng", () => Update(new { trangThaiCuTru = "Tạm vắng" }));
        AddAction("Trở lại", () => Update(new { trangThaiCuTru = "Đang ở" }));
        AddAction("Đã rời đi", () => Update(new { trangThaiCuTru = "Đã rời đi", ngayRoi = DateTime.Today.ToString("yyyy-MM-dd") }));
        bar.Height = 86;
    }
    protected override async Task LoadData()
    {
        var data = await ApiClient.GetRawAsync($"/api/DanhMucMoRong/cu-tru?maHopDong={ContractId}");
        var table = Table("Mã", "Mã khách", "Họ tên", "Vào", "Rời", "Cư trú", "Khai báo", "Ngày khai báo");
        foreach (var c in data.EnumerateArray()) table.Rows.Add(ApiClient.ElStr(c, "maCuTru"), ApiClient.ElStr(c, "maKhach"),
            c.TryGetProperty("maKhachNavigation", out var k) ? ApiClient.ElStr(k, "hoTen") : "",
            ApiClient.ElStr(c, "ngayVao"), ApiClient.ElStr(c, "ngayRoi"), ApiClient.ElStr(c, "trangThaiCuTru"),
            ApiClient.ElStr(c, "trangThaiKhaiBao"), ApiClient.ElStr(c, "ngayKhaiBao"));
        grid.DataSource = table;
    }
    async Task Add()
    {
        var customers = (await ApiClient.GetRawAsync("/api/KhachThue")).EnumerateArray().ToArray();
        if (customers.Length == 0) throw new InvalidOperationException("Chưa có khách thuê.");
        var choose = new ComboBox { Width = 260, DropDownStyle = ComboBoxStyle.DropDownList };
        foreach (var k in customers) choose.Items.Add($"#{ApiClient.ElStr(k, "maKhach")} — {ApiClient.ElStr(k, "hoTen")}");
        choose.SelectedIndex = 0;
        var arrival = new DateTimePicker { Format = DateTimePickerFormat.Short };
        var notes = new TextBox { MaxLength = 300, Width = 250 };
        using var form = Editor("Thêm người cư trú", ("Khách thuê", choose), ("Ngày vào", arrival), ("Ghi chú", notes));
        if (form.ShowDialog(this) != DialogResult.OK) return;
        await ApiClient.PostAsync("/api/DanhMucMoRong/cu-tru", new { maHopDong = ContractId,
            maKhach = int.Parse(ApiClient.ElStr(customers[choose.SelectedIndex], "maKhach")),
            ngayVao = arrival.Value.ToString("yyyy-MM-dd"), trangThaiCuTru = "Đang ở", trangThaiKhaiBao = "Chưa khai báo", ghiChu = notes.Text });
        await LoadData();
    }
    async Task Update(object body)
    {
        if (grid.CurrentRow == null) return;
        if (MessageBox.Show("Cập nhật trạng thái cho người cư trú đang chọn?", Text, MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        await ApiClient.PutAsync($"/api/DanhMucMoRong/cu-tru/{grid.CurrentRow.Cells[0].Value}", body);
        await LoadData();
    }
}
