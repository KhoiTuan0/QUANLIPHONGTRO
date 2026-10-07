using System.Text.Json;
using QLPT.WinForms.Services;

namespace QLPT.WinForms.Forms;

public sealed class TenantOverviewForm : Form
{
    readonly TabControl tabs = new() { Dock = DockStyle.Fill };
    readonly Label profile = new() { Dock = DockStyle.Fill, Padding = new Padding(20), Font = new Font("Segoe UI", 11) };
    readonly DataGridView contracts = Grid(), invoices = Grid(), notices = Grid(), payments = Grid(), meters = Grid(), services = Grid();
    readonly Button refresh = new() { Text = "Tải lại", Dock = DockStyle.Top, Height = 38 };

    public TenantOverviewForm()
    {
        Text = "Thông tin và lịch sử của khách thuê";
        tabs.TabPages.Add(Page("Cá nhân", profile)); tabs.TabPages.Add(Page("Hợp đồng", contracts));
        tabs.TabPages.Add(Page("Hóa đơn", invoices)); tabs.TabPages.Add(Page("Lịch sử thanh toán", payments));
        tabs.TabPages.Add(Page("Điện nước", meters)); tabs.TabPages.Add(Page("Dịch vụ", services));
        var noticePanel = new Panel { Dock = DockStyle.Fill }; noticePanel.Controls.Add(notices);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44 };
        var read = new Button { Text = "Xem / Đánh dấu đã đọc", AutoSize = true };
        var reply = new Button { Text = "Phản hồi chủ trọ", AutoSize = true };
        actions.Controls.AddRange([read, reply]); noticePanel.Controls.Add(actions);
        read.Click += async (_, _) => await NoticeAction(false); reply.Click += async (_, _) => await NoticeAction(true);
        tabs.TabPages.Add(Page("Thông báo", noticePanel));
        refresh.Click += async (_, _) => await LoadData();
        Controls.Add(tabs); Controls.Add(refresh); Load += async (_, _) => await LoadData();
    }
    static DataGridView Grid() => new() { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false,
        MultiSelect = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells };
    static TabPage Page(string title, Control content) { var page = new TabPage(title); page.Controls.Add(content); return page; }
    static void Fill(DataGridView grid, JsonElement rows, params (string key, string title, bool money)[] columns)
    {
        var table = new System.Data.DataTable(); foreach (var c in columns) table.Columns.Add(c.title);
        foreach (var row in rows.EnumerateArray()) table.Rows.Add(columns.Select(c => (object)(c.money ? VnMoney.Format(ApiClient.ElStr(row, c.key)) : ApiClient.ElStr(row, c.key))).ToArray());
        grid.DataSource = table;
    }
    async Task LoadData()
    {
        if (!Session.MaKhach.HasValue || !Session.MaTaiKhoan.HasValue) { profile.Text = "Tài khoản chưa liên kết khách thuê."; return; }
        refresh.Enabled = false;
        try
        {
            int customerId = Session.MaKhach.Value, accountId = Session.MaTaiKhoan.Value;
            var customer = await ApiClient.GetRawAsync($"/api/KhachThue/{customerId}");
            profile.Text = $"Họ tên: {ApiClient.ElStr(customer, "hoTen")}\nCCCD: {ApiClient.ElStr(customer, "cccd")}\n" +
                $"Điện thoại: {ApiClient.ElStr(customer, "soDienThoai")}\nEmail: {ApiClient.ElStr(customer, "email")}";
            var contractData = await ApiClient.GetRawAsync($"/api/HopDong/customer/{customerId}");
            Session.OwnContractIds.Clear();
            foreach (var c in contractData.EnumerateArray()) Session.OwnContractIds.Add(int.Parse(ApiClient.ElStr(c, "maHopDong")));
            Fill(contracts, contractData, ("maHopDong", "HĐ", false), ("tenPhong", "Phòng", false), ("ngayBatDau", "Từ", false),
                ("ngayKetThuc", "Đến", false), ("giaThue", "Giá/tháng", true), ("tienCoc", "Cọc", true), ("trangThai", "Trạng thái", false));
            Fill(invoices, await ApiClient.GetRawAsync($"/api/HoaDon/customer/{customerId}"),
                ("maHoaDon", "Hóa đơn", false), ("maHopDong", "HĐ", false), ("thang", "Tháng", false), ("nam", "Năm", false),
                ("tongTien", "Tổng", true), ("daThanhToan", "Đã thu", true), ("conLai", "Còn lại", true),
                ("hanThanhToan", "Hạn thu", false), ("trangThai", "Trạng thái", false));
            Fill(payments, await ApiClient.GetRawAsync($"/api/KhachThue/{customerId}/lich-su/thanh-toan"),
                ("maThanhToan", "Mã thu", false), ("maHoaDon", "Hóa đơn", false), ("tenPhong", "Phòng", false),
                ("ngayThanhToan", "Ngày thu", false), ("soTien", "Số tiền", true), ("phuongThuc", "Phương thức", false), ("ghiChu", "Ghi chú", false));
            Fill(meters, await ApiClient.GetRawAsync($"/api/KhachThue/{customerId}/lich-su/dien-nuoc"),
                ("tenPhong", "Phòng", false), ("loaiChiSo", "Loại", false), ("thang", "Tháng", false), ("nam", "Năm", false),
                ("chiSoCu", "Cũ", false), ("chiSoMoi", "Mới", false), ("tieuThu", "Tiêu thụ", false), ("ngayGhi", "Ngày ghi", false));
            Fill(services, await ApiClient.GetRawAsync($"/api/KhachThue/{customerId}/lich-su/dich-vu"),
                ("tenPhong", "Phòng", false), ("tenDichVu", "Dịch vụ", false), ("donGia", "Đơn giá", true), ("donViTinh", "Đơn vị", false),
                ("ngayBatDau", "Từ", false), ("ngayKetThuc", "Đến", false), ("trangThai", "Trạng thái", false));
            Fill(notices, await ApiClient.GetRawAsync($"/api/ThongBao/customer/{accountId}"),
                ("maThongBao", "Mã", false), ("ngayTao", "Ngày", false), ("tieuDe", "Tiêu đề", false),
                ("noiDung", "Nội dung", false), ("loaiThongBao", "Loại", false), ("daDoc", "Đã đọc", false));
        }
        catch (Exception ex) { MessageBox.Show("Không tải được thông tin: " + ex.Message); }
        finally { if (!IsDisposed) refresh.Enabled = true; }
    }
    async Task NoticeAction(bool reply)
    {
        if (notices.CurrentRow == null || !Session.MaTaiKhoan.HasValue) return;
        try
        {
            int id = int.Parse(notices.CurrentRow.Cells[0].Value?.ToString() ?? "0");
            if (reply)
            {
                var content = new TextBox { Width = 250, Height = 100, Multiline = true, MaxLength = 800 };
                using var form = ContractResourceForm.Editor("Phản hồi chủ trọ", ("Nội dung", content)); form.Height = 250;
                if (form.ShowDialog(this) != DialogResult.OK) return;
                await ApiClient.PostAsync($"/api/ThongBao/{id}/phan-hoi", new { noiDung = content.Text });
            }
            else
            {
                MessageBox.Show(notices.CurrentRow.Cells[3].Value?.ToString(), notices.CurrentRow.Cells[2].Value?.ToString());
                await ApiClient.PostAsync($"/api/ThongBao/{id}/doc/{Session.MaTaiKhoan}", new { });
            }
            await LoadData();
        }
        catch (Exception ex) { MessageBox.Show(ex.Message); }
    }
}
