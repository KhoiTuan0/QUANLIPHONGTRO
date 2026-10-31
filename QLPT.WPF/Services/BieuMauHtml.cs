using System.Globalization;
using System.IO;
using System.Net;
using System.Text.Json;

namespace QLPT.WPF.Services;

// Biểu mẫu lấy lại dữ liệu đã lưu qua API trước khi xuất, không dùng giá trị trên dòng bảng có thể đã cũ.
public static class BieuMauHtml
{
    private static string H(string? value) => WebUtility.HtmlEncode(value ?? "");
    private static string Field(JsonElement value, string name) => ApiClient.ElStr(value, name).Trim();
    private static string Date(string? value) => DateTime.TryParse(value, CultureInfo.InvariantCulture,
        DateTimeStyles.None, out var date) ? date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) : H(value);
    private static string Money(JsonElement value, string name)
    {
        if (!decimal.TryParse(Field(value, name), NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
            throw new InvalidDataException($"Không đọc được {name} của biểu mẫu.");
        return H(VnMoney.FormatD(amount));
    }

    private static string Layout(string title, string body) => $$"""
        <!doctype html><html lang="vi"><head><meta charset="utf-8"><title>{{H(title)}}</title>
        <style>
        @page { size: A4; margin: 18mm; }
        body { font-family: 'Times New Roman', serif; font-size: 12pt; color: #111; background: white; max-width: 800px; margin: 28px auto; padding: 0 16px; line-height: 1.45; overflow-wrap: anywhere; }
        h1,h2 { text-align: center; margin: 6px 0 12px; } h1 { font-size: 19px; } h2 { font-size: 17px; }
        h3 { font-size: 12pt; margin: 16px 0 6px; break-after: avoid; }
        p { margin: 8px 0; } .center { text-align: center; } .preserve { white-space: pre-wrap; }
        table { width: 100%; border-collapse: collapse; margin: 16px 0; } td,th { border: 1px solid #555; padding: 7px; text-align: left; vertical-align: top; }
        thead { display: table-header-group; } tr { break-inside: avoid; } .sign { break-inside: avoid; }
        .note { color: #555; font-size: 13px; } .sign { display: flex; justify-content: space-between; text-align: center; margin-top: 48px; }
        .sign div { width: 42%; } button { padding: 8px 14px; margin-bottom: 16px; }
        @media print { body { margin: 0; padding: 0; max-width: none; } button,.screen-only { display: none; } }
        </style></head><body><button onclick="window.print()">In / lưu PDF</button>{{body}}</body></html>
        """;

    public static string HopDong(JsonElement contract, JsonElement room, JsonElement customer)
    {
        string id = Field(contract, "maHopDong");
        string roomName = Field(room, "tenPhong");
        string tenantName = Field(customer, "hoTen");
        if (id.Length == 0 || roomName.Length == 0 || tenantName.Length == 0)
            throw new InvalidDataException("Hợp đồng thiếu mã, phòng hoặc khách đại diện; hãy kiểm tra dữ liệu trước khi xuất.");
        string body = $"""
            <h1>CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM</h1>
            <p class="center"><b>Độc lập – Tự do – Hạnh phúc</b></p>
            <h2>HỢP ĐỒNG THUÊ PHÒNG TRỌ</h2>
            <p style="text-align:center">Mã hợp đồng: <b>{H(id)}</b> · Ngày ký: {Date(Field(contract, "ngayKy"))}</p>
            <p class="note screen-only">Điền các chỗ trống trên bản in và thống nhất nội dung trước khi ký. Xuất biểu mẫu không thay đổi hợp đồng trong hệ thống.</p>
            <h3>1. Thông tin các bên</h3>
            <p><b>Bên cho thuê (Bên A):</b> ............................................................<br>
            CCCD / giấy tờ đại diện: ............................................................<br>
            Địa chỉ liên hệ: ................................................................................<br>
            Điện thoại: ............................................................<br>
            Cơ sở: {H(Field(room, "tenCoSo"))}<br>
            Địa chỉ nhà cho thuê: ....................................................................</p>
            <p><b>Bên thuê:</b> {H(tenantName)} · CCCD: {H(Field(customer, "cccd"))}<br>
            Số điện thoại: {H(Field(customer, "soDienThoai"))}<br>
            Địa chỉ liên hệ: {H(Field(customer, "diaChi"))}</p>
            <h3>2. Phòng thuê, thời hạn và tiền thuê</h3>
            <table><thead><tr><th>Nội dung</th><th>Thông tin đã lưu</th></tr></thead><tbody>
            <tr><td>Phòng thuê</td><td>{H(roomName)}</td></tr>
            <tr><td>Thời hạn</td><td>{Date(Field(contract, "ngayBatDau"))} – {Date(Field(contract, "ngayKetThuc"))}</td></tr>
            <tr><td>Giá thuê hiện tại mỗi tháng</td><td>{Money(contract, "giaThueThang")}</td></tr>
            <tr><td>Tiền cọc</td><td>{Money(contract, "tienCoc")}</td></tr>
            <tr><td>Ghi chú</td><td class="preserve">{H(Field(contract, "ghiChu"))}</td></tr></tbody></table>
            <h3>3. Thỏa thuận thanh toán và dịch vụ</h3>
            <p>Ngày đến hạn thanh toán: ....................; phương thức: ........................<br>
            Điện: ........................ đ/kWh; nước: ........................ (đơn vị: ............)<br>
            Các dịch vụ khác và cách tính: ................................................................<br>
            ....................................................................................................</p>
            <p>Điều kiện hoàn / khấu trừ tiền cọc và thời điểm quyết toán:<br>
            ....................................................................................................<br>
            ....................................................................................................</p>
            <h3>4. Bàn giao, sử dụng và sửa chữa</h3>
            <p>Biên bản bàn giao kèm theo số: ........................; ngày: ........................<br>
            Số người ở theo thỏa thuận: ....................<br>
            Trách nhiệm sử dụng, bảo quản và chi phí sửa chữa của mỗi bên:<br>
            ....................................................................................................<br>
            ....................................................................................................</p>
            <h3>5. Gia hạn, kết thúc và thỏa thuận khác</h3>
            <p>Thời hạn báo trước và điều kiện kết thúc trước hạn:<br>
            ....................................................................................................<br>
            Thỏa thuận gia hạn, xử lý công nợ và nội dung bổ sung:<br>
            ....................................................................................................<br>
            ....................................................................................................</p>
            <p>Hai bên đã đối chiếu thông tin, thống nhất các nội dung điền bổ sung và các phụ lục kèm theo. Hợp đồng lập thành ........ bản; mỗi bên giữ ........ bản.</p>
            <p class="note">Bản in phản ánh dữ liệu hiện tại trên hệ thống. Hai bên cần kiểm tra thông tin, điều khoản và phụ lục gia hạn (nếu có) trước khi ký.</p>
            <div class="sign"><div>BÊN CHO THUÊ<br><br><br><br>(Ký, ghi rõ họ tên)</div><div>BÊN THUÊ<br><br><br><br>(Ký, ghi rõ họ tên)</div></div>
            """;
        return Layout($"Hợp đồng #{id}", body);
    }

    public static string BanGiao(JsonElement record)
    {
        string id = Field(record, "maBanGiao");
        string kind = Field(record, "loaiBanGiao");
        if (id.Length == 0 || kind.Length == 0)
            throw new InvalidDataException("Biên bản bàn giao thiếu mã hoặc loại biên bản.");
        string rows = "";
        if (record.TryGetProperty("chiTietBanGiaos", out var details) && details.ValueKind == JsonValueKind.Array)
            rows = string.Join("", details.EnumerateArray().Select((item, index) => $"""
                <tr><td>{index + 1}</td><td>{H(Field(item, "tenTaiSan"))}</td><td>{H(Field(item, "soLuong"))}</td>
                <td>{H(Field(item, "tinhTrang"))}</td><td>{(Field(item, "chiPhiPhat").Length == 0 ? H(VnMoney.FormatD(0m)) : Money(item, "chiPhiPhat"))}</td><td>{H(Field(item, "ghiChu"))}</td></tr>
                """));
        string body = $"""
            <h1>BIÊN BẢN {H(kind.ToUpperInvariant())}</h1>
            <p>Mã biên bản: <b>{H(id)}</b> · Hợp đồng #{H(Field(record, "maHopDong"))} · Ngày: {Date(Field(record, "ngayBanGiao"))}</p>
            <p>Phòng: <b>{H(Field(record, "tenPhong"))}</b> · Khách đại diện: <b>{H(Field(record, "hoTenKhach"))}</b></p>
            <p>Chỉ số điện: {H(Field(record, "chiSoDien"))} · Chỉ số nước: {H(Field(record, "chiSoNuoc"))}</p>
            <p>Bên giao: ........................................; bên nhận: ........................................</p>
            <table><thead><tr><th>STT</th><th>Tài sản</th><th>Số lượng</th><th>Tình trạng</th><th>Chi phí phạt</th><th>Ghi chú</th></tr></thead><tbody>{(rows.Length == 0 ? "<tr><td colspan=\"6\">Chưa ghi nhận tài sản trong biên bản này.</td></tr>" : rows)}</tbody></table>
            <p class="preserve"><b>Ghi chú chung:</b> {H(Field(record, "ghiChu"))}</p>
            <p>Nội dung đối chiếu / bổ sung tại thời điểm ký:<br>....................................................................................................<br>....................................................................................................</p>
            <p class="note">Đối chiếu tài sản và chỉ số thực tế trước khi hai bên xác nhận. Chi phí phạt trên biên bản không tự ghi nhận là khoản đã thu.</p>
            <div class="sign"><div>BÊN GIAO<br><br><br><br>(Ký, ghi rõ họ tên)</div><div>BÊN NHẬN<br><br><br><br>(Ký, ghi rõ họ tên)</div></div>
            """;
        return Layout($"Biên bản {kind} #{id}", body);
    }
}
