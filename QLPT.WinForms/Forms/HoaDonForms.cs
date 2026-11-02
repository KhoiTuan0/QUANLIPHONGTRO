using System.Text.Json;
using QLPT.WinForms.Services;

namespace QLPT.WinForms.Forms;

public class ChiSoForm : Form
{
    DataGridView grid = new() { Dock = DockStyle.Bottom, Height = 300, ReadOnly = true, SelectionMode = DataGridViewSelectionMode.FullRowSelect, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
    TextBox txtPhong = new() { Width = 60, Text = "1" };
    ComboBox cbLoai = new() { Width = 80 }; TextBox txtThang = new() { Width = 50, Text = DateTime.Now.Month.ToString() };
    TextBox txtNam = new() { Width = 70, Text = DateTime.Now.Year.ToString() };
    TextBox txtCu = new() { Width = 90 }; TextBox txtMoi = new() { Width = 90 };
    PictureBox pic = new() { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BorderStyle = BorderStyle.FixedSingle };
    Label lblOcr = new() { Dock = DockStyle.Bottom, Height = 60, ForeColor = Color.DarkBlue };
    string? imgPath;
    decimal? rawOcr;

    public ChiSoForm()
    {
        Text = "Chỉ số Điện/Nước + OCR đồng hồ điện";
        cbLoai.DropDownStyle = ComboBoxStyle.DropDownList;
        cbLoai.Items.AddRange(new object[] { "Điện", "Nước" }); cbLoai.SelectedIndex = 0;
        cbLoai.SelectedIndexChanged += async (_, _) =>
        { imgPath = null; rawOcr = null; pic.Image?.Dispose(); pic.Image = null; txtMoi.Clear(); lblOcr.Text = ""; await LoadData(); };
        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 90, Padding = new Padding(6), WrapContents = true };
        var bLoad = new Button { Text = "Tải" }; bLoad.Click += async (s, e) => await LoadData();
        var bChon = new Button { Text = "Chọn ảnh đồng hồ..." }; bChon.Click += (s, e) => ChonAnh();
        var bOcr = new Button { Text = "OCR đọc số", BackColor = Color.DarkOrange, ForeColor = Color.White }; bOcr.Click += (s, e) => ChayOcr();
        var bLuu = new Button { Text = "Lưu chỉ số", BackColor = Color.SteelBlue, ForeColor = Color.White }; bLuu.Click += async (s, e) => await Luu();
        var bSua = new Button { Text = "Sửa chỉ số đang chọn" }; bSua.Click += async (_, _) =>
        {
            try
            {
                if (grid.CurrentRow == null || grid.CurrentRow.IsNewRow) return;
                var row = grid.CurrentRow;
                string? oldText = Microsoft.VisualBasic.Interaction.InputBox("Chỉ số cũ (không có dấu phân tách nghìn):", "Sửa chỉ số", row.Cells[4].Value?.ToString());
                if (string.IsNullOrWhiteSpace(oldText)) return;
                string? newText = Microsoft.VisualBasic.Interaction.InputBox("Chỉ số mới đã xác nhận:", "Sửa chỉ số", row.Cells[5].Value?.ToString());
                if (string.IsNullOrWhiteSpace(newText)) return;
                decimal oldNumber = InputRules.NonNegativeNumber(oldText, "Chỉ số cũ"), newNumber = InputRules.NonNegativeNumber(newText, "Chỉ số mới");
                string ocrText = row.Cells[6].Value?.ToString() ?? "";
                decimal? original = string.IsNullOrWhiteSpace(ocrText) ? null : InputRules.NonNegativeNumber(ocrText, "OCR gốc");
                await ApiClient.PutAsync($"/api/ChiSoDienNuoc/{row.Cells[0].Value}", new { chiSoCu = oldNumber,
                    chiSoMoi = newNumber, chiSoXacNhan = newNumber, chiSoOcr = original });
                await LoadData();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Sửa chỉ số"); }
        };
        var bAnh = new Button { Text = "Xem ảnh đã lưu" }; bAnh.Click += async (_, _) =>
        {
            try
            {
                if (grid.CurrentRow == null) return;
                string path = grid.CurrentRow.Cells[8].Value?.ToString() ?? "";
                if (!path.StartsWith("/api/Anh/")) { MessageBox.Show("Kỳ này chưa có ảnh lưu trên API."); return; }
                var bytes = await ApiClient.GetImageAsync(path);
                using var memory = new MemoryStream(bytes); using var loaded = Image.FromStream(memory);
                pic.Image?.Dispose(); pic.Image = new Bitmap(loaded);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        };
        var bHd = new Label { Text = OcrService.InstallGuide, AutoSize = false, Width = 600, Height = 60, ForeColor = Color.Gray };
        top.Controls.AddRange(new Control[] {
            new Label{Text="Phòng:",AutoSize=true}, txtPhong,
            new Label{Text="Loại:",AutoSize=true}, cbLoai,
            new Label{Text="Tháng:",AutoSize=true}, txtThang,
            new Label{Text="Năm:",AutoSize=true}, txtNam,
            new Label{Text="Cũ:",AutoSize=true}, txtCu,
            new Label{Text="Mới/OCR:",AutoSize=true}, txtMoi,
            bLoad, bChon, bOcr, bLuu, bSua, bAnh });
        Controls.Add(pic); Controls.Add(lblOcr); Controls.Add(grid); Controls.Add(top);
        Load += async (s, e) => await LoadData();
    }
    void ChonAnh()
    {
        using var d = new OpenFileDialog { Filter = "Ảnh|*.jpg;*.jpeg;*.png;*.bmp" };
        if (d.ShowDialog() == DialogResult.OK)
        {
            imgPath = d.FileName;
            rawOcr = null;
            pic.Image?.Dispose();
            pic.Image = Image.FromFile(imgPath);
            lblOcr.Text = "Đã chọn: " + imgPath;
        }
    }
    void ChayOcr()
    {
        if (imgPath == null) { MessageBox.Show("Hãy chọn ảnh đồng hồ trước."); return; }
        var r = OcrService.ReadMeterNumber(imgPath);
        rawOcr = r.Ok && decimal.TryParse(r.Number.Replace(',', '.'), System.Globalization.NumberStyles.AllowDecimalPoint,
            System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : null;
        lblOcr.Text = $"Engine: {r.Engine}\nRaw: {r.RawText}\nSố nhận dạng: {r.Number}";
        if (r.Ok) { txtMoi.Text = r.Number; MessageBox.Show("OCR nhận: " + r.Number + " — hãy đối chiếu ảnh rồi bấm Lưu."); }
        else MessageBox.Show("OCR chưa đọc được (xem hướng dẫn tessdata). Hãy nhập tay vào ô Mới.");
    }
    async Task LoadData()
    {
        try
        {
            var arr = await ApiClient.GetRawAsync($"/api/ChiSoDienNuoc?maPhong={txtPhong.Text}");
            var dt = new System.Data.DataTable();
            foreach (var c in new[] { "Mã", "Loại", "Tháng", "Năm", "Cũ", "Mới", "OCR", "Xác nhận", "Ảnh" }) dt.Columns.Add(c);
            foreach (var x in arr.EnumerateArray())
                dt.Rows.Add(ApiClient.ElStr(x, "maChiSo", "MaChiSo"), ApiClient.ElStr(x, "loaiChiSo", "LoaiChiSo"), ApiClient.ElStr(x, "thang", "Thang"), ApiClient.ElStr(x, "nam", "Nam"), ApiClient.ElStr(x, "chiSoCu", "ChiSoCu"), ApiClient.ElStr(x, "chiSoMoi", "ChiSoMoi"), ApiClient.ElStr(x, "chiSoOcr", "ChiSoOcr", "ChiSoOCR"), ApiClient.ElStr(x, "chiSoXacNhan", "ChiSoXacNhan"), ApiClient.ElStr(x, "duongDanAnh"));
            grid.DataSource = dt;
            // gợi ý chỉ số cũ
            try
            {
                var cuoi = await ApiClient.GetRawAsync($"/api/ChiSoDienNuoc/cuoi/{txtPhong.Text}/{Uri.EscapeDataString(cbLoai.SelectedItem!.ToString()!)}?thang={txtThang.Text}&nam={txtNam.Text}");
                txtCu.Text = ApiClient.ElStr(cuoi, "chiSoMoi", "ChiSoMoi");
            }
            catch { }
        }
        catch (Exception ex) { MessageBox.Show(ex.Message); }
    }
    async Task Luu()
    {
        try
        {
            int maPhong = InputRules.PositiveId(txtPhong.Text, "Mã phòng");
            var (thang, nam) = InputRules.Period(txtThang.Text, txtNam.Text);
            decimal chiSoCu = InputRules.NonNegativeNumber(txtCu.Text, "Chỉ số cũ");
            decimal chiSoMoi = InputRules.NonNegativeNumber(txtMoi.Text, "Chỉ số mới");
            if (chiSoMoi < chiSoCu) throw new ArgumentException("Chỉ số mới không được nhỏ hơn chỉ số cũ.");
            string? rel = imgPath == null ? null : await ApiClient.UploadImageAsync(imgPath, "dong-ho");
            await ApiClient.PostAsync("/api/ChiSoDienNuoc", new
            {
                maPhong,
                loaiChiSo = cbLoai.SelectedItem!.ToString(),
                thang, nam,
                chiSoCu, chiSoMoi,
                chiSoOCR = cbLoai.SelectedItem!.ToString() == "Điện" ? rawOcr : null,
                chiSoXacNhan = chiSoMoi,
                duongDanAnh = rel
            });
            MessageBox.Show("Lưu chỉ số thành công");
            await LoadData();
        }
        catch (Exception ex) { MessageBox.Show("Lỗi lưu: " + ex.Message); }
    }
}

public class HoaDonForm : Form
{
    DataGridView grid = new() { Dock = DockStyle.Fill, ReadOnly = true, SelectionMode = DataGridViewSelectionMode.FullRowSelect, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
    TextBox txtHd = new() { Width = 70, Text = "" };
    public HoaDonForm()
    {
        Text = "Hóa đơn — Tính tiền (OCR) — Thu tiền — Xuất";
        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 80, Padding = new Padding(6), WrapContents = true };
        var bLoad = new Button { Text = "Tải HĐ" }; bLoad.Click += async (s, e) => await LoadData();
        var bTinh = new Button { Text = "Lập HĐ từ chỉ số (tinh-tien-ocr)", Width = 220 }; bTinh.Click += async (s, e) => await TinhTien();
        var bThu = new Button { Text = "Thu tiền" }; bThu.Click += async (s, e) => await ThuTien();
        var bXuat = new Button { Text = "Xuất HĐ" }; bXuat.Click += async (s, e) => await Xuat();
        bar.Controls.AddRange(new Control[] { new Label { Text = "Mã HĐ:", AutoSize = true }, txtHd, bLoad, bTinh, bThu, bXuat,
            new Label{Text="Lập HĐ: nhập Mã phòng + tháng/năm + 4 chỉ số trong hộp thoại sau.", AutoSize=true, ForeColor=Color.Gray} });
        Controls.Add(grid); Controls.Add(bar);
        Load += async (s, e) => await LoadData();
    }
    async Task LoadData()
    {
        try
        {
            var arr = await ApiClient.GetRawAsync("/api/HoaDon");
            var dt = new System.Data.DataTable();
            foreach (var c in new[] { "Mã", "HĐồng", "Tháng", "Năm", "Tổng", "Đã thu", "Còn lại", "Trạng thái" }) dt.Columns.Add(c);
            foreach (var x in arr.EnumerateArray())
                dt.Rows.Add(ApiClient.ElStr(x, "maHoaDon", "MaHoaDon"), ApiClient.ElStr(x, "maHopDong", "MaHopDong"), ApiClient.ElStr(x, "thang", "Thang"), ApiClient.ElStr(x, "nam", "Nam"), Services.VnMoney.Format(ApiClient.ElStr(x, "tongTien", "TongTien")), Services.VnMoney.Format(ApiClient.ElStr(x, "daThanhToan", "DaThanhToan")), Services.VnMoney.Format(ApiClient.ElStr(x, "conLai", "ConLai")), ApiClient.ElStr(x, "trangThai", "TrangThai"));
            grid.DataSource = dt;
        }
        catch (Exception ex) { MessageBox.Show(ex.Message); }
    }
    async Task TinhTien()
    {
        try
        {
            string mp = Microsoft.VisualBasic.Interaction.InputBox("Mã phòng (phải có HĐ hiệu lực):", "Lập HĐ", "1"); if (mp == "") return;
            string th = Microsoft.VisualBasic.Interaction.InputBox("Tháng:", "Lập HĐ", DateTime.Now.Month.ToString()); if (th == "") return;
            string na = Microsoft.VisualBasic.Interaction.InputBox("Năm:", "Lập HĐ", DateTime.Now.Year.ToString()); if (na == "") return;
            int maPhong = InputRules.PositiveId(mp, "Mã phòng");
            var (thang, nam) = InputRules.Period(th, na);
            var saved = await ApiClient.GetRawAsync($"/api/ChiSoDienNuoc?maPhong={maPhong}&thang={thang}&nam={nam}");
            string Meter(string type, string field) => saved.EnumerateArray().Where(x => ApiClient.ElStr(x, "loaiChiSo") == type)
                .Select(x => ApiClient.ElStr(x, field)).FirstOrDefault() ?? "0";
            string dCu = Microsoft.VisualBasic.Interaction.InputBox("Điện cũ:", "Lập HĐ", Meter("Điện", "chiSoCu")); if (dCu == "") return;
            string dMoi = Microsoft.VisualBasic.Interaction.InputBox("Điện mới (đã xác nhận):", "Lập HĐ", Meter("Điện", "chiSoMoi")); if (dMoi == "") return;
            string nCu = Microsoft.VisualBasic.Interaction.InputBox("Nước cũ:", "Lập HĐ", Meter("Nước", "chiSoCu")); if (nCu == "") return;
            string nMoi = Microsoft.VisualBasic.Interaction.InputBox("Nước mới:", "Lập HĐ", Meter("Nước", "chiSoMoi")); if (nMoi == "") return;
            decimal dienCu = InputRules.NonNegativeNumber(dCu, "Điện cũ");
            decimal dienMoi = InputRules.NonNegativeNumber(dMoi, "Điện mới");
            decimal nuocCu = InputRules.NonNegativeNumber(nCu, "Nước cũ");
            decimal nuocMoi = InputRules.NonNegativeNumber(nMoi, "Nước mới");
            if (dienMoi < dienCu || nuocMoi < nuocCu) throw new ArgumentException("Chỉ số điện/nước mới không được nhỏ hơn chỉ số cũ.");
            var el = await ApiClient.PostAsync("/api/HoaDon/tinh-tien-ocr", new
            {
                maPhong, thang, nam,
                chiSoDienCu = dienCu, chiSoDienMoi = dienMoi,
                chiSoNuocCu = nuocCu, chiSoNuocMoi = nuocMoi,
                donGiaDien = 3500, donGiaNuoc = 20000
            });
            MessageBox.Show("Lập HĐ thành công. Tổng: " + Services.VnMoney.FormatD(ApiClient.ElStr(el, "tongTien", "TongTien")));
            await LoadData();
        }
        catch (Exception ex) { MessageBox.Show("Lỗi: " + ex.Message); }
    }
    async Task ThuTien()
    {
        if (grid.CurrentRow == null) return;
        try
        {
            int id = int.Parse(grid.CurrentRow.Cells[0].Value.ToString()!);
            string st = Microsoft.VisualBasic.Interaction.InputBox("Số tiền thu (VD 3.000.000):", "Thu tiền", grid.CurrentRow.Cells[6].Value.ToString()!); if (st == "") return;
            decimal soTien = InputRules.NonNegativeMoney(st, "Số tiền thu");
            decimal conLai = InputRules.NonNegativeMoney(grid.CurrentRow.Cells[6].Value?.ToString() ?? "0", "Số tiền còn lại");
            if (soTien <= 0 || soTien > conLai) throw new ArgumentException("Số tiền thu phải lớn hơn 0 và không vượt số tiền còn lại.");
            var el = await ApiClient.PostAsync("/api/ThanhToan", new { maHoaDon = id, soTien, phuongThuc = "Tiền mặt" });
            MessageBox.Show("Thu xong. Trạng thái: " + ApiClient.ElStr(el, "trangThai") + ", còn lại: " + Services.VnMoney.FormatD(ApiClient.ElStr(el, "conLai")));
            await LoadData();
        }
        catch (Exception ex) { MessageBox.Show(ex.Message); }
    }
    async Task Xuat()
    {
        if (grid.CurrentRow == null) return;
        try
        {
            var h = await ApiClient.GetRawAsync($"/api/HoaDon/{grid.CurrentRow.Cells[0].Value}");
            var details = new List<(string, decimal, decimal, decimal)>();
            decimal Money(JsonElement el, string field) => decimal.TryParse(ApiClient.ElStr(el, field), System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;
            foreach (var line in h.GetProperty("chiTietHoaDons").EnumerateArray())
                details.Add((ApiClient.ElStr(line, "noiDung"), Money(line, "donGia"), Money(line, "soLuong"), Money(line, "thanhTien")));
            var room = h.GetProperty("maHopDongNavigation").GetProperty("maPhongNavigation");
            DateTime? Date(string field) => DateTime.TryParse(ApiClient.ElStr(h, field), out var d) ? d : null;
            string path = ExportService.HoaDon(ApiClient.ElStr(room, "tenPhong"), h.GetProperty("thang").GetInt32(),
                h.GetProperty("nam").GetInt32(), details, Money(h, "tongTien"), Date("ngayLap"), Date("hanThanhToan"));
            MessageBox.Show("Đã xuất: " + path);
        }
        catch (Exception ex) { MessageBox.Show("Không xuất được hóa đơn: " + ex.Message); }
    }
}

public class ViPhamForm : CrudForm
{
    public ViPhamForm() : base("Quản lý Vi phạm") { bEdit.Text = "Xử lý vi phạm"; bDel.Visible = false; }
    protected override async Task LoadData()
    {
        var arr = await ApiClient.GetRawAsync("/api/ViPham");
        var dt = new System.Data.DataTable();
        foreach (var c in new[] { "Mã", "Phòng", "Khách", "Ngày", "Nội dung", "Phạt", "Trạng thái" }) dt.Columns.Add(c);
        foreach (var x in arr.EnumerateArray())
            dt.Rows.Add(ApiClient.ElStr(x, "maViPham", "MaViPham"), ApiClient.ElStr(x, "maPhong", "MaPhong"), ApiClient.ElStr(x, "maKhach", "MaKhach"), ApiClient.ElStr(x, "ngayViPham", "NgayViPham"), ApiClient.ElStr(x, "noiDung", "NoiDung"), Services.VnMoney.Format(ApiClient.ElStr(x, "tienPhat", "TienPhat")), ApiClient.ElStr(x, "trangThai", "TrangThai"));
        grid.DataSource = dt;
    }
    protected override async Task OnAdd()
    {
        string mp = Microsoft.VisualBasic.Interaction.InputBox("Mã phòng:", "Vi phạm", ""); if (mp == "") return;
        string mk = Microsoft.VisualBasic.Interaction.InputBox("Mã khách:", "Vi phạm", ""); if (mk == "") return;
        string nd = Microsoft.VisualBasic.Interaction.InputBox("Nội dung vi phạm:", "Vi phạm", "Ồn ào sau 22h"); if (nd == "") return;
        string phat = Microsoft.VisualBasic.Interaction.InputBox("Tiền phạt (VD 100.000, 0 nếu nhắc nhở):", "Vi phạm", "0"); if (phat == "") return;
        await ApiClient.PostAsync("/api/ViPham", new { maPhong = int.Parse(mp), maKhach = int.Parse(mk), noiDung = nd, tienPhat = Services.VnMoney.Parse(phat) });
        await LoadData();
    }
    protected override async Task OnEdit()
    {
        if (grid.CurrentRow == null) return;
        string resolution = Microsoft.VisualBasic.Interaction.InputBox("Hình thức xử lý:", "Vi phạm", "Nhắc nhở");
        if (string.IsNullOrWhiteSpace(resolution)) return;
        string fine = Microsoft.VisualBasic.Interaction.InputBox("Tiền phạt:", "Vi phạm", "0");
        if (string.IsNullOrWhiteSpace(fine)) return;
        await ApiClient.PutAsync($"/api/ViPham/{grid.CurrentRow.Cells[0].Value}/xu-ly", new { hinhThucXuLy = resolution,
            tienPhat = InputRules.NonNegativeMoney(fine, "Tiền phạt") });
        await LoadData();
    }
}

public class SuaChuaForm : CrudForm
{
    public SuaChuaForm() : base("Yêu cầu Sửa chữa") { bEdit.Text = "Cập nhật trạng thái"; bDel.Visible = false; }
    protected override async Task LoadData()
    {
        var arr = await ApiClient.GetRawAsync("/api/YeuCauSuaChua");
        var dt = new System.Data.DataTable();
        foreach (var c in new[] { "Mã", "Phòng", "Khách", "Ngày báo", "Nội dung", "Trạng thái", "Chi phí" }) dt.Columns.Add(c);
        foreach (var x in arr.EnumerateArray())
            dt.Rows.Add(ApiClient.ElStr(x, "maYeuCau", "MaYeuCau"), ApiClient.ElStr(x, "maPhong", "MaPhong"), ApiClient.ElStr(x, "maKhach", "MaKhach"), ApiClient.ElStr(x, "ngayBao", "NgayBao"), ApiClient.ElStr(x, "noiDung", "NoiDung"), ApiClient.ElStr(x, "trangThai", "TrangThai"), Services.VnMoney.Format(ApiClient.ElStr(x, "chiPhi", "ChiPhi")));
        grid.DataSource = dt;
    }
    protected override async Task OnAdd()
    {
        string mp = Microsoft.VisualBasic.Interaction.InputBox("Mã phòng:", "Sửa chữa", ""); if (mp == "") return;
        string mk = Microsoft.VisualBasic.Interaction.InputBox("Mã khách:", "Sửa chữa", ""); if (mk == "") return;
        string nd = Microsoft.VisualBasic.Interaction.InputBox("Nội dung:", "Sửa chữa", ""); if (nd == "") return;
        await ApiClient.PostAsync("/api/YeuCauSuaChua", new { maPhong = int.Parse(mp), maKhach = int.Parse(mk), noiDung = nd });
        await LoadData();
    }
    protected override async Task OnEdit()
    {
        if (grid.CurrentRow == null) return;
        string status = Microsoft.VisualBasic.Interaction.InputBox("Trạng thái: Đang sửa hoặc Đã hoàn thành", "Sửa chữa", "Đang sửa");
        if (string.IsNullOrWhiteSpace(status)) return;
        string cost = Microsoft.VisualBasic.Interaction.InputBox("Chi phí sửa chữa", "Sửa chữa", "0");
        if (string.IsNullOrWhiteSpace(cost)) return;
        await ApiClient.PutAsync($"/api/YeuCauSuaChua/{grid.CurrentRow.Cells[0].Value}",
            new { trangThai = status.Trim(), chiPhi = InputRules.NonNegativeMoney(cost, "Chi phí") });
        await LoadData();
    }
}

public sealed class ThongBaoForm : Form
{
    readonly Dictionary<int, int[]> recipientMap = [];
    readonly DataGridView grid = new() { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
    public ThongBaoForm()
    {
        Text = "Thông báo / Phản hồi khách thuê";
        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(6) };
        var reload = new Button { Text = "Tải lại", AutoSize = true };
        var send = new Button { Text = "Gửi / Trả lời khách", AutoSize = true };
        var remind = new Button { Text = "Nhắc hợp đồng hết hạn", AutoSize = true };
        bar.Controls.AddRange([reload, send, remind]);
        reload.Click += async (_, _) => await LoadData();
        send.Click += async (_, _) => { send.Enabled = false; try { await Gui(); } catch (Exception ex) { MessageBox.Show(ex.Message); } finally { if (!IsDisposed) send.Enabled = true; } };
        remind.Click += async (_, _) =>
        {
            try { var data = await ApiClient.PostAsync("/api/ThongBao/nhac-het-han?days=30", new { }); MessageBox.Show("Đã gửi: " + ApiClient.ElStr(data, "soThongBao") + " thông báo."); await LoadData(); }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        };
        Controls.Add(grid); Controls.Add(bar);
        Load += async (_, _) => await LoadData();
    }
    async Task LoadData()
    {
        try
        {
            var rows = await ApiClient.GetRawAsync("/api/ThongBao");
            var table = new System.Data.DataTable();
            recipientMap.Clear();
            foreach (var name in new[] { "Mã", "Ngày", "Tiêu đề", "Nội dung", "Loại", "Số người nhận", "Số đã đọc" }) table.Columns.Add(name);
            foreach (var t in rows.EnumerateArray())
            {
                recipientMap[int.Parse(ApiClient.ElStr(t, "maThongBao"))] = t.GetProperty("nguoiNhan").EnumerateArray().Select(n => n.GetInt32()).ToArray();
                table.Rows.Add(ApiClient.ElStr(t, "maThongBao"), ApiClient.ElStr(t, "ngayTao"),
                ApiClient.ElStr(t, "tieuDe"), ApiClient.ElStr(t, "noiDung"), ApiClient.ElStr(t, "loaiThongBao"),
                ApiClient.ElStr(t, "soNguoiNhan"), ApiClient.ElStr(t, "soDaDoc"));
            }
            grid.DataSource = table;
        }
        catch (Exception ex) { MessageBox.Show(ex.Message); }
    }
    async Task Gui()
    {
        var rows = await ApiClient.GetRawAsync("/api/ThongBao/nguoi-nhan");
        var title = new TextBox { Width = 300, MaxLength = 200 };
        var content = new TextBox { Width = 300, Height = 130, Multiline = true, MaxLength = 1000, ScrollBars = ScrollBars.Vertical };
        var all = new CheckBox { Text = "Gửi tất cả khách có tài khoản", Checked = false, AutoSize = true };
        var recipients = new CheckedListBox { Width = 300, Height = 150, CheckOnClick = true };
        foreach (var t in rows.EnumerateArray())
            recipients.Items.Add(new Recipient(int.Parse(ApiClient.ElStr(t, "maTaiKhoan")), ApiClient.ElStr(t, "hoTen") + " — " + ApiClient.ElStr(t, "tenDangNhap")));
        all.CheckedChanged += (_, _) => recipients.Enabled = !all.Checked;
        if (grid.CurrentRow != null) title.Text = "Trả lời thông báo #" + grid.CurrentRow.Cells[0].Value;
        if (grid.CurrentRow != null && int.TryParse(grid.CurrentRow.Cells[0].Value?.ToString(), out int noticeId) && recipientMap.TryGetValue(noticeId, out var selected))
            for (int i = 0; i < recipients.Items.Count; i++) recipients.SetItemChecked(i, selected.Contains(((Recipient)recipients.Items[i]).Id));
        using var form = new Form { Text = "Gửi thông báo", Size = new Size(550, 580), StartPosition = FormStartPosition.CenterParent };
        var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(10) };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170)); table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.Controls.Add(new Label { Text = "Tiêu đề", AutoSize = true }, 0, 0); table.Controls.Add(title, 1, 0);
        table.Controls.Add(new Label { Text = "Nội dung", AutoSize = true }, 0, 1); table.Controls.Add(content, 1, 1);
        table.Controls.Add(all, 1, 2); table.Controls.Add(new Label { Text = "Người nhận", AutoSize = true }, 0, 3); table.Controls.Add(recipients, 1, 3);
        var save = new Button { Text = "Gửi", DialogResult = DialogResult.OK }; table.Controls.Add(save, 1, 4); form.Controls.Add(table);
        if (form.ShowDialog(this) != DialogResult.OK) return;
        var chosen = recipients.CheckedItems.Cast<Recipient>().Select(r => r.Id).ToArray();
        await ApiClient.PostAsync("/api/ThongBao/gui-thong-bao", new { tieuDe = title.Text.Trim(), noiDung = content.Text.Trim(),
            loaiThongBao = "Chung", guiChoTatCa = all.Checked, danhSachMaTaiKhoan = chosen });
        await LoadData();
    }
    sealed record Recipient(int Id, string Name) { public override string ToString() => Name; }
}

public class ThuChiForm : CrudForm
{
    public ThuChiForm() : base("Sổ Thu / Chi") { bEdit.Visible = false; bDel.Visible = false; }
    protected override async Task LoadData()
    {
        var arr = await ApiClient.GetRawAsync("/api/DanhMucMoRong/thu-chi");
        var dt = new System.Data.DataTable();
        foreach (var c in new[] { "Mã", "Loại", "Ngày", "Nội dung", "Số tiền", "Trạng thái" }) dt.Columns.Add(c);
        foreach (var x in arr.EnumerateArray())
            dt.Rows.Add(ApiClient.ElStr(x, "maPhieu", "MaPhieu"), ApiClient.ElStr(x, "loaiPhieu", "LoaiPhieu"), ApiClient.ElStr(x, "ngayLap", "NgayLap"), ApiClient.ElStr(x, "noiDung", "NoiDung"), Services.VnMoney.Format(ApiClient.ElStr(x, "soTien", "SoTien")), ApiClient.ElStr(x, "trangThai", "TrangThai"));
        grid.DataSource = dt;
    }
    protected override async Task OnAdd()
    {
        string loai = Microsoft.VisualBasic.Interaction.InputBox("Loại (Thu/Chi):", "Phiếu", "Chi"); if (loai == "") return;
        string nd = Microsoft.VisualBasic.Interaction.InputBox("Nội dung:", "Phiếu", ""); if (nd == "") return;
        string st = Microsoft.VisualBasic.Interaction.InputBox("Số tiền (VD 1.000.000):", "Phiếu", "0"); if (st == "") return;
        await ApiClient.PostAsync("/api/DanhMucMoRong/thu-chi", new { loaiPhieu = loai, noiDung = nd, soTien = Services.VnMoney.Parse(st), trangThai = "Hoàn tất" });
        await LoadData();
    }
}
