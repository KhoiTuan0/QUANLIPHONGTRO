using System.Globalization;
using System.Text.Json;

namespace QLPT.WPF.Services;

public enum LookupKind { KhachThue, KhachSanSang, KhachDatPhong, Phong, PhongTrong, PhongDaDat, PhongDangThue, HopDong, HopDongHieuLuc, CoSo, LoaiPhong, DichVu }

public sealed record LookupOption(string Id, string Label, string SearchText, decimal? Price = null,
    string CustomerId = "", string Status = "")
{
    public string SearchKey { get; } = TextUtil.Normalize(SearchText);
}

/// <summary>Danh sách tham chiếu luôn được tải từ hồ sơ đã tồn tại; mã chỉ dùng khi gửi API.</summary>
public static class LookupData
{
    public static async Task<List<LookupOption>> LoadAsync(LookupKind kind)
    {
        if (kind is LookupKind.Phong or LookupKind.PhongTrong or LookupKind.PhongDaDat or LookupKind.PhongDangThue)
        {
            var roomsTask = ApiClient.GetRawAsync("/api/Phong");
            var contractsTask = kind == LookupKind.Phong ? null : ApiClient.GetRawAsync("/api/HopDong");
            var bookingsTask = kind is LookupKind.PhongTrong or LookupKind.PhongDaDat ? ApiClient.GetRawAsync("/api/DatPhong") : null;
            var tasks = new List<Task<JsonElement>> { roomsTask };
            if (contractsTask != null) tasks.Add(contractsTask);
            if (bookingsTask != null) tasks.Add(bookingsTask);
            await Task.WhenAll(tasks);
            var rooms = await roomsTask;
            var activeRooms = contractsTask == null ? new HashSet<string>() :
                (await contractsTask).EnumerateArray().Where(x => ApiClient.ElStr(x, "trangThai") == "Đang hiệu lực")
                .Select(x => ApiClient.ElStr(x, "maPhong")).ToHashSet();
            var bookedRooms = bookingsTask == null ? new HashSet<string>() :
                (await bookingsTask).EnumerateArray().Where(x => ApiClient.ElStr(x, "trangThai") == "Đã đặt cọc")
                .Select(x => ApiClient.ElStr(x, "maPhong")).ToHashSet();
            return rooms.EnumerateArray().Where(x =>
            {
                var id = ApiClient.ElStr(x, "maPhong");
                var status = ApiClient.ElStr(x, "trangThai");
                return kind == LookupKind.Phong ||
                    (kind == LookupKind.PhongDangThue ? status == "Đang thuê" && activeRooms.Contains(id) :
                    !activeRooms.Contains(id) &&
                    (kind == LookupKind.PhongTrong ? status == "Trống" && !bookedRooms.Contains(id) :
                     status == "Đã đặt cọc" && bookedRooms.Contains(id)));
            }).Select(x =>
            {
                var id = ApiClient.ElStr(x, "maPhong");
                var name = ApiClient.ElStr(x, "tenPhong");
                var site = ApiClient.ElStr(x, "tenCoSo");
                var status = ApiClient.ElStr(x, "trangThai");
                decimal? price = decimal.TryParse(ApiClient.ElStr(x, "giaThue"), NumberStyles.Any, CultureInfo.InvariantCulture, out var p) ? p : null;
                var label = string.Join(" · ", new[] { name, site, status }.Where(s => s.Length > 0)) + $" (#{id})";
                return new LookupOption(id, label, $"{id} {name} {site} {status}", price, Status: status);
            }).OrderBy(x => x.Label, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        var (url, idField, nameField) = kind switch
        {
            LookupKind.KhachThue or LookupKind.KhachSanSang or LookupKind.KhachDatPhong => ("/api/KhachThue", "maKhach", "hoTen"),
            LookupKind.HopDong or LookupKind.HopDongHieuLuc => ("/api/HopDong", "maHopDong", "tenPhong"),
            LookupKind.CoSo => ("/api/CoSo", "maCoSo", "tenCoSo"),
            LookupKind.LoaiPhong => ("/api/LoaiPhong", "maLoaiPhong", "tenLoai"),
            LookupKind.DichVu => ("/api/DichVu", "maDichVu", "tenDichVu"),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        var data = await ApiClient.GetRawAsync(url);
        if (data.ValueKind != JsonValueKind.Array) throw new Exception("Không tải được danh sách lựa chọn. Vui lòng thử lại.");
        var unavailable = new HashSet<string>();
        if (kind == LookupKind.KhachSanSang)
        {
            var contracts = await ApiClient.GetRawAsync("/api/HopDong");
            var bookings = await ApiClient.GetRawAsync("/api/DatPhong");
            var residents = await ApiClient.GetRawAsync("/api/DanhMucMoRong/cu-tru");
            var activeContracts = contracts.EnumerateArray()
                .Where(h => ApiClient.ElStr(h, "trangThai") == "Đang hiệu lực").ToList();
            foreach (var h in activeContracts) unavailable.Add(ApiClient.ElStr(h, "maKhachDaiDien"));
            foreach (var b in bookings.EnumerateArray().Where(b => ApiClient.ElStr(b, "trangThai") == "Đã đặt cọc"))
                unavailable.Add(ApiClient.ElStr(b, "maKhach"));
            var activeIds = activeContracts.Select(h => ApiClient.ElStr(h, "maHopDong")).ToHashSet();
            foreach (var c in residents.EnumerateArray().Where(c =>
                activeIds.Contains(ApiClient.ElStr(c, "maHopDong")) &&
                ApiClient.ElStr(c, "trangThaiCuTru") is "Đang ở" or "Tạm vắng"))
                unavailable.Add(ApiClient.ElStr(c, "maKhach"));
        }
        // Người đứng tên được phép đặt thêm phòng cho người thân; phòng vẫn phải trống.
        var result = new List<LookupOption>();
        foreach (var x in data.EnumerateArray())
        {
            var id = ApiClient.ElStr(x, idField);
            if (!int.TryParse(id, out var number) || number <= 0) continue;
            if (kind is LookupKind.KhachSanSang or LookupKind.KhachDatPhong && unavailable.Contains(id)) continue;
            var name = ApiClient.ElStr(x, nameField, "tenLoaiPhong");
            var status = ApiClient.ElStr(x, "trangThai");
            if (kind == LookupKind.HopDongHieuLuc && status != "Đang hiệu lực") continue;
            if (kind == LookupKind.DichVu && status is "Ngừng" or "Ngừng áp dụng") continue;
            var label = $"{name} (#{id})";
            var search = $"{id} {name}";
            var customer = "";
            if (kind is LookupKind.KhachThue or LookupKind.KhachSanSang or LookupKind.KhachDatPhong)
            {
                var identity = ApiClient.ElStr(x, "cccd");
                var phone = ApiClient.ElStr(x, "soDienThoai");
                label = string.Join(" · ", new[] { name, identity.Length > 0 ? $"CCCD {identity}" : "", phone.Length > 0 ? $"SĐT {phone}" : "" }.Where(s => s.Length > 0)) + $" (#{id})";
                search += $" {identity} {phone}";
            }
            else if (kind is LookupKind.HopDong or LookupKind.HopDongHieuLuc)
            {
                var tenantName = ApiClient.ElStr(x, "hoTenKhach");
                customer = ApiClient.ElStr(x, "maKhachDaiDien");
                label = $"HĐ #{id} · {name} · {tenantName} · {status}";
                search += $" {tenantName} {customer} {status}";
            }
            result.Add(new LookupOption(id, label, search, CustomerId: customer, Status: status));
        }
        return result.OrderBy(x => x.Label, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public static string EmptyMessage(LookupKind kind) => kind switch
    {
        LookupKind.KhachThue => "Chưa có hồ sơ khách thuê. Thêm khách ở mục Khách thuê rồi làm mới danh sách.",
        LookupKind.KhachSanSang => "Không còn khách chưa thuê hoặc chưa giữ chỗ. Có thể thêm khách mới ngay tại đây.",
        LookupKind.KhachDatPhong => "Chưa có hồ sơ khách để đứng tên đặt phòng.",
        LookupKind.PhongTrong => "Không có phòng trống để chọn. Phòng đã đặt cọc cần nhận phòng từ mục Đặt phòng.",
        LookupKind.PhongDaDat => "Không còn phòng đang giữ chỗ hợp lệ. Làm mới mục Đặt phòng để kiểm tra.",
        LookupKind.PhongDangThue => "Không có phòng đang thuê với hợp đồng hiệu lực để ghi chỉ số.",
        LookupKind.HopDongHieuLuc => "Chưa có hợp đồng đang hiệu lực.",
        _ => "Chưa có dữ liệu. Thêm hồ sơ tương ứng rồi làm mới danh sách."
    };

    public static string Display(string? name, string? id) => !string.IsNullOrWhiteSpace(name) ? name :
        string.IsNullOrEmpty(id) || id == "0" ? "Chưa gắn hồ sơ" : $"Hồ sơ #{id}";

    public static string NestedName(JsonElement item, params string[] path)
    {
        foreach (var part in path)
        {
            if (item.ValueKind != JsonValueKind.Object) return "";
            if (item.TryGetProperty(part, out var child)) item = child;
            else if (item.TryGetProperty(char.ToUpperInvariant(part[0]) + part[1..], out child)) item = child;
            else return "";
        }
        return item.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ? "" : item.ToString();
    }
}
