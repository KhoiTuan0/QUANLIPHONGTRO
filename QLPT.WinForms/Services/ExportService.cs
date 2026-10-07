using System.Diagnostics;
using System.Text;
using System.Data;
using System.Text.Json;

namespace QLPT.WinForms.Services;

/// <summary>Xuất biểu mẫu HTML (mở bằng trình duyệt để in/lưu PDF).</summary>
public static class ExportService
{
    static string H(string? value) => System.Net.WebUtility.HtmlEncode(value ?? "");

    public static string BaoCao(JsonElement report, DataTable table)
    {
        var header = "<tr>" + string.Join("", table.Columns.Cast<DataColumn>().Select(c => $"<th>{H(c.ColumnName)}</th>")) + "</tr>";
        var rows = string.Join("", table.Rows.Cast<DataRow>().Select(r => "<tr>" + string.Join("", r.ItemArray.Select(c => $"<td>{H(c?.ToString())}</td>")) + "</tr>"));
        string summary = string.Join("", report.GetProperty("tomTat").EnumerateArray().Select(s =>
            $"<p>{H(ApiClient.ElStr(s, "chiTieu"))}: <b>{H(ApiClient.ElStr(s, "kieu") == "tien" ? VnMoney.Format(ApiClient.ElStr(s, "giaTri")) + " đ" : ApiClient.ElStr(s, "giaTri"))}</b></p>"));
        string body = $"<h1>BÁO CÁO {H(ApiClient.ElStr(report, "loai")).ToUpperInvariant()}</h1>" +
            $"<p>Cơ sở: {H(ApiClient.ElStr(report, "coSo"))}. Từ {H(ApiClient.ElStr(report, "tuNgay"))} đến {H(ApiClient.ElStr(report, "denNgay"))}. Thời điểm: {H(ApiClient.ElStr(report, "thoiDiem"))}.</p>" +
            summary + $"<p>{H(ApiClient.ElStr(report, "ghiChu"))}</p><table>{header}{rows}</table>";
        return SaveAndOpen($"BaoCao_{DateTime.Now:yyyyMMdd_HHmmss}.html", Layout("Báo cáo", body));
    }

    public static string BanGiao(JsonElement handover, string room, string customer)
    {
        string rows = "";
        if (handover.TryGetProperty("chiTietBanGiaos", out var items))
            rows = string.Join("", items.EnumerateArray().Select(a =>
            {
                string name = a.TryGetProperty("maTaiSanNavigation", out var asset) ? ApiClient.ElStr(asset, "tenTaiSan") : ApiClient.ElStr(a, "maTaiSan");
                return $"<tr><td>{H(name)}</td><td>{H(ApiClient.ElStr(a, "soLuong"))}</td><td>{H(ApiClient.ElStr(a, "tinhTrang"))}</td><td>{H(VnMoney.Format(ApiClient.ElStr(a, "chiPhiPhat")))}</td><td>{H(ApiClient.ElStr(a, "ghiChu"))}</td></tr>";
            }));
        string body = $"<h1>BIÊN BẢN {H(ApiClient.ElStr(handover, "loaiBanGiao")).ToUpperInvariant()}</h1>" +
            $"<p>Mã biên bản: {H(ApiClient.ElStr(handover, "maBanGiao"))}. HĐ #{H(ApiClient.ElStr(handover, "maHopDong"))}. Ngày: {H(ApiClient.ElStr(handover, "ngayBanGiao"))}.</p>" +
            $"<p>Phòng: {H(room)}. Khách: {H(customer)}.</p><p>Chỉ số điện: {H(ApiClient.ElStr(handover, "chiSoDien"))}; nước: {H(ApiClient.ElStr(handover, "chiSoNuoc"))}.</p>" +
            $"<table><tr><th>Tài sản</th><th>Số lượng</th><th>Tình trạng</th><th>Chi phí phạt</th><th>Ghi chú</th></tr>{rows}</table>" +
            $"<p>{H(ApiClient.ElStr(handover, "ghiChu"))}</p><div class='sign'><div>CHỦ TRỌ<br/>(Ký, ghi rõ họ tên)</div><div>KHÁCH THUÊ<br/>(Ký, ghi rõ họ tên)</div></div>";
        return SaveAndOpen($"BanGiao_{ApiClient.ElStr(handover, "maBanGiao")}_{DateTime.Now:yyyyMMdd_HHmmss}.html", Layout("Biên bản bàn giao", body));
    }
    public static string ExportDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "QLPT_BieuMau");

    private static string SaveAndOpen(string fileName, string html)
    {
        Directory.CreateDirectory(ExportDir);
        fileName = string.Concat(fileName.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        string path = Path.Combine(ExportDir, fileName);
        File.WriteAllText(path, html, Encoding.UTF8);
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); } catch { }
        return path;
    }

    private static string Layout(string title, string body) =>
        $@"<!DOCTYPE html><html><head><meta charset='utf-8'><title>{title}</title>
        <style>body{{font-family:'Times New Roman',serif;margin:40px;color:#111}}h1,h2{{text-align:center}}table{{width:100%;border-collapse:collapse;margin:12px 0}}td,th{{border:1px solid #333;padding:6px}} .sign{{display:flex;justify-content:space-between;margin-top:40px}} @media print{{button{{display:none}}}}</style></head>
        <body><button onclick='window.print()'>In / Lưu PDF</button>{body}</body></html>";

    public static string HopDong(string tenPhong, string tenKhach, string cccd, decimal giaThue, decimal coc, DateTime tu, DateTime den,
        string ghiChu = "", DateTime? ngayKy = null, string maHopDong = "", string coSo = "", string diaChi = "", string dienThoai = "")
    {
        tenPhong = System.Net.WebUtility.HtmlEncode(tenPhong); tenKhach = System.Net.WebUtility.HtmlEncode(tenKhach);
        cccd = System.Net.WebUtility.HtmlEncode(cccd); ghiChu = System.Net.WebUtility.HtmlEncode(ghiChu);
        string body = $@"<h1>CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM</h1><h2>HỢP ĐỒNG THUÊ PHÒNG TRỌ</h2>
        <p>Mã hợp đồng: {H(maHopDong)} — Ngày ký: {(ngayKy.HasValue ? ngayKy.Value.ToString("dd/MM/yyyy") : "Chưa ghi nhận")}.</p>
        <p><b>Bên A (Chủ trọ):</b> Họ tên: ____________________ — CCCD: ____________________.<br/>Cơ sở: {H(coSo)}. Điền thông tin chủ trọ thật trước khi ký.</p>
        <p><b>Bên B (Khách thuê):</b> {tenKhach} — CCCD: {cccd}<br/>Điện thoại: {H(dienThoai)} — Địa chỉ: {H(diaChi)}</p>
        <table><tr><th>Nội dung</th><th>Giá trị</th></tr>
        <tr><td>Phòng thuê</td><td>{tenPhong}</td></tr>
        <tr><td>Thời hạn</td><td>{tu:dd/MM/yyyy} → {den:dd/MM/yyyy}</td></tr>
        <tr><td>Giá thuê hiện tại/tháng</td><td>{VnMoney.Format(giaThue)} đ</td></tr>
        <tr><td>Tiền cọc</td><td>{VnMoney.Format(coc)} đ</td></tr>
        <tr><td>Ghi chú</td><td>{ghiChu}</td></tr></table>
        <p><b>Điều khoản:</b> Theo thỏa thuận và nội quy được hai bên xác nhận, đính kèm hợp đồng cùng các phụ lục gia hạn nếu có.</p>
        <div class='sign'><div>BÊN A<br/><br/>(Ký, ghi rõ họ tên)</div><div>BÊN B<br/><br/>(Ký, ghi rõ họ tên)</div></div>";
        return SaveAndOpen($"HopDong_{maHopDong}_{DateTime.Now:yyyyMMddHHmmss}.html", Layout("Hợp đồng thuê", body));
    }

    public static string HoaDon(string tenPhong, int thang, int nam, List<(string nd, decimal dg, decimal sl, decimal tt)> ct, decimal tong, DateTime? ngayLap = null, DateTime? hanThanhToan = null)
    {
        var rows = string.Join("", ct.Select((x, i) => $"<tr><td>{i + 1}</td><td>{System.Net.WebUtility.HtmlEncode(x.nd)}</td><td>{VnMoney.Format(x.dg)}</td><td>{x.sl}</td><td>{VnMoney.Format(x.tt)}</td></tr>"));
        string body = $@"<h1>HÓA ĐƠN TIỀN PHÒNG {thang}/{nam}</h1><p>Phòng: <b>{System.Net.WebUtility.HtmlEncode(tenPhong)}</b> — Ngày lập: {ngayLap:dd/MM/yyyy} — Hạn thanh toán: {hanThanhToan:dd/MM/yyyy}</p>
        <table><tr><th>STT</th><th>Nội dung</th><th>Đơn giá</th><th>SL</th><th>Thành tiền</th></tr>{rows}
        <tr><td colspan='4'><b>TỔNG CỘNG</b></td><td><b>{VnMoney.Format(tong)} đ</b></td></tr></table>
        <div class='sign'><div>NGƯỜI LẬP</div><div>KHÁCH THUÊ</div></div>";
        return SaveAndOpen($"HoaDon_{thang}_{nam}_{DateTime.Now:HHmmss}.html", Layout("Hóa đơn", body));
    }

    public static string BienBanBanGiao(string loai, string tenPhong, string chiSoDien, string chiSoNuoc, string ghiChu = "")
    {
        tenPhong = System.Net.WebUtility.HtmlEncode(tenPhong); loai = System.Net.WebUtility.HtmlEncode(loai);
        chiSoDien = System.Net.WebUtility.HtmlEncode(chiSoDien); chiSoNuoc = System.Net.WebUtility.HtmlEncode(chiSoNuoc);
        ghiChu = System.Net.WebUtility.HtmlEncode(ghiChu);
        string body = $@"<h1>BIÊN BẢN {(loai == "Trả phòng" ? "TRẢ" : "BÀN GIAO NHẬN")} PHÒNG</h1>
        <p>Phòng: <b>{tenPhong}</b> — Loại: {loai} — Ngày: {DateTime.Now:dd/MM/yyyy}</p>
        <table><tr><th>Hạng mục</th><th>Giá trị</th></tr>
        <tr><td>Chỉ số điện</td><td>{chiSoDien}</td></tr><tr><td>Chỉ số nước</td><td>{chiSoNuoc}</td></tr>
        <tr><td>Ghi chú tài sản/hư hao</td><td>{ghiChu}</td></tr></table>
        <div class='sign'><div>CHỦ TRỌ</div><div>KHÁCH THUÊ</div></div>";
        return SaveAndOpen($"BienBan_{DateTime.Now:yyyyMMddHHmmss}.html", Layout("Biên bản", body));
    }

    public static string BienBanThanhLy(string tenPhong, decimal tienCoc, decimal truPhat, decimal hoanLai, string ghiChu = "")
    {
        tenPhong = System.Net.WebUtility.HtmlEncode(tenPhong); ghiChu = System.Net.WebUtility.HtmlEncode(ghiChu);
        string body = $@"<h1>BIÊN BẢN THANH LÝ HỢP ĐỒNG</h1>
        <p>Phòng: <b>{tenPhong}</b> — Ngày thanh lý: {DateTime.Now:dd/MM/yyyy}</p>
        <table><tr><th>Khoản</th><th>Số tiền</th></tr>
        <tr><td>Tiền cọc ban đầu</td><td>{VnMoney.Format(tienCoc)} đ</td></tr>
        <tr><td>Nợ hóa đơn / phạt ghi nhận</td><td>{VnMoney.Format(truPhat)} đ</td></tr>
        <tr><td><b>Hoàn lại khách</b></td><td><b>{VnMoney.Format(hoanLai)} đ</b></td></tr></table>
        <p>Ghi chú: {ghiChu}</p><p>Tiền hoàn là khoản thực tế hai bên xác nhận. Nợ hóa đơn còn lại không tự cấn trừ/xóa bởi biên bản; ghi nhận thu tiền riêng nếu thanh toán bằng cọc.</p><div class='sign'><div>CHỦ TRỌ</div><div>KHÁCH THUÊ</div></div>";
        return SaveAndOpen($"ThanhLy_{DateTime.Now:yyyyMMddHHmmss}.html", Layout("Thanh lý", body));
    }
}
