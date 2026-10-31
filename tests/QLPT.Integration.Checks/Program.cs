using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using QLPT.API.Models;

internal static class Program
{
    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    static readonly string ApiBaseUrl = string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("QLPT_TEST_API_URL"))
        ? "http://localhost:5180"
        : Environment.GetEnvironmentVariable("QLPT_TEST_API_URL")!.Trim();
    static int checks;
    static void Check(bool condition, string label)
    { if (!condition) throw new InvalidOperationException(label); checks++; Console.WriteLine("PASS " + label); }

    static string Connection(string database) => new SqlConnectionStringBuilder
    { DataSource = @".\SQLEXPRESS", InitialCatalog = database, IntegratedSecurity = true, TrustServerCertificate = true }.ConnectionString;
    static Klcn052QuanLyPhongTroContext Db(string database) => new(new DbContextOptionsBuilder<Klcn052QuanLyPhongTroContext>().UseSqlServer(Connection(database)).Options);

    static async Task Main(string[] args)
    {
        if (args.Length < 2 || !System.Text.RegularExpressions.Regex.IsMatch(args[1], @"^QLPT_Test_\d{8}_[a-f0-9]{8}$"))
            throw new ArgumentException("Expected seed|run and the exact isolated test database name.");
        string database = args[1], fixtureFile = Path.Combine(AppContext.BaseDirectory, "fixture.json");
        if (args[0] == "seed")
        {
            await using var db = Db(database);
            string suffix = Guid.NewGuid().ToString("N")[..8];
            string digits = (Convert.ToUInt32(suffix, 16) % 10000000).ToString("D7");
            var customer = new KhachThue { HoTen = "Khách kiểm thử " + suffix, Cccd = "90001" + digits, SoDienThoai = "090" + digits };
            var outsider = new KhachThue { HoTen = "Khách ngoài hợp đồng " + suffix, SoDienThoai = "091" + digits };
            const string password = "IntegrationTest@2026";
            var admin = new TaiKhoan { TenDangNhap = "test_admin_" + suffix, MatKhauHash = BCrypt.Net.BCrypt.HashPassword(password), VaiTro = "Admin", TrangThai = "Active", NgayTao = DateTime.Now };
            var tenant = new TaiKhoan { TenDangNhap = "test_customer_" + suffix, MatKhauHash = BCrypt.Net.BCrypt.HashPassword(password), VaiTro = "Customer", TrangThai = "Active", NgayTao = DateTime.Now, MaKhachNavigation = customer };
            var other = new TaiKhoan { TenDangNhap = "test_outside_" + suffix, MatKhauHash = BCrypt.Net.BCrypt.HashPassword(password), VaiTro = "Customer", TrangThai = "Active", NgayTao = DateTime.Now, MaKhachNavigation = outsider };
            db.TaiKhoans.AddRange(admin, tenant, other);
            await db.SaveChangesAsync();
            await File.WriteAllTextAsync(fixtureFile, JsonSerializer.Serialize(new Fixture(database, suffix, admin.TenDangNhap, tenant.TenDangNhap, other.TenDangNhap, password, admin.MaTaiKhoan, tenant.MaTaiKhoan, customer.MaKhach, outsider.MaKhach), Json));
            Console.WriteLine("Seeded isolated test accounts. Fixture credentials are kept only in ignored build output.");
            return;
        }
        var fixture = JsonSerializer.Deserialize<Fixture>(await File.ReadAllTextAsync(fixtureFile), Json)!;
        Check(fixture.Database == database, "fixture points to the isolated SQL copy");
        if (args[0] == "extra")
        {
            await RunExtra(fixture, database);
            Console.WriteLine($"PASS {checks} additional live HTTP/SQL checks on {database}.");
            return;
        }
        using var adminClient = Client(); using var tenantClient = Client(); using var otherClient = Client(); using var anonymous = Client();
        await Send(anonymous, HttpMethod.Get, "health", null, 200);
        await Send(anonymous, HttpMethod.Get, "KhachThue", null, 401);
        await Send(adminClient, HttpMethod.Post, "Auth/login", new { username = fixture.Admin, password = "wrong" }, 401);
        await Login(adminClient, fixture.Admin, fixture.Password);
        await Login(tenantClient, fixture.Tenant, fixture.Password);
        await Login(otherClient, fixture.Other, fixture.Password);
        await Send(tenantClient, HttpMethod.Get, "KhachThue", null, 403);
        await Send(tenantClient, HttpMethod.Get, $"KhachThue/{fixture.CustomerId}", null, 200);
        await Send(tenantClient, HttpMethod.Get, $"KhachThue/{fixture.OtherCustomerId}", null, 403);

        var site = await Send(adminClient, HttpMethod.Post, "CoSo", new { tenCoSo = "Cơ sở test " + fixture.Tag, diaChi = "12 Đường số 1, TP. Hồ Chí Minh", soDienThoai = "0901234567" }, 200);
        int siteId = site.GetProperty("data").GetProperty("maCoSo").GetInt32();
        var type = await Send(adminClient, HttpMethod.Post, "LoaiPhong", new { tenLoaiPhong = "Loại test " + fixture.Tag, dienTich = 20, soNguoiToiDa = 3, giaMacDinh = 2000000 }, 200);
        int typeId = type.GetProperty("data").GetProperty("maLoaiPhong").GetInt32();
        var room = await Send(adminClient, HttpMethod.Post, "Phong", new { tenPhong = "TEST-" + fixture.Tag, maLoaiPhong = typeId, maCoSo = siteId, giaThue = 2000000, trangThai = "Trống" }, 200);
        int roomId = room.GetProperty("data").GetProperty("maPhong").GetInt32();
        await Send(adminClient, HttpMethod.Post, "ChiSoDienNuoc", new { maPhong = roomId, loaiChiSo = "Điện", thang = DateTime.Today.Month, nam = DateTime.Today.Year, chiSoCu = 0, chiSoMoi = 1 }, 400);
        await Send(adminClient, HttpMethod.Post, "DichVu", new { tenDichVu = "Điện", donGia = 3500, donViTinh = "kWh", trangThai = "Đang áp dụng" }, 200);
        await Send(adminClient, HttpMethod.Post, "DichVu", new { tenDichVu = "Nước", donGia = 20000, donViTinh = "m3", trangThai = "Đang áp dụng" }, 200);
        var today = DateOnly.FromDateTime(DateTime.Today); var start = new DateOnly(today.Year, today.Month, 1);
        var booking = await Send(adminClient, HttpMethod.Post, "DatPhong", new { maPhong = roomId, maKhach = fixture.CustomerId, ngayDat = start, ngayDuKienNhan = today, tienCoc = 1000000 }, 200);
        int bookingId = booking.GetProperty("data").GetProperty("maDatPhong").GetInt32();
        var rooms = await Send(adminClient, HttpMethod.Get, "Phong", null, 200);
        Check(rooms.EnumerateArray().Single(x => Int(x,"maPhong") == roomId).GetProperty("trangThai").GetString() == "Đã đặt cọc", "booking reserves the correct room");
        await Send(adminClient, HttpMethod.Post, "DatPhong", new { maPhong = roomId, maKhach = fixture.OtherCustomerId, tienCoc = 1000000 }, 400);
        await Send(adminClient, HttpMethod.Post, "HopDong", new { maPhong = roomId, maKhachDaiDien = fixture.OtherCustomerId, ngayBatDau = start, ngayKetThuc = start.AddMonths(6), giaThueThang = 2000000, tienCoc = 2000000 }, 400);
        await Send(adminClient, HttpMethod.Post, "HopDong", new { maPhong = roomId, maKhachDaiDien = fixture.CustomerId, ngayBatDau = start, ngayKetThuc = start.AddMonths(6), giaThueThang = 2000000, tienCoc = 500000 }, 400);
        var contract = await Send(adminClient, HttpMethod.Post, "HopDong", new { maPhong = roomId, maKhachDaiDien = fixture.CustomerId, ngayBatDau = start, ngayKetThuc = start.AddMonths(6), giaThueThang = 2000000, tienCoc = 2000000 }, 200);
        int contractId = contract.GetProperty("data").GetProperty("maHopDong").GetInt32();
        var bookings = await Send(adminClient, HttpMethod.Get, "DatPhong", null, 200);
        Check(bookings.EnumerateArray().Single(x => Int(x,"maDatPhong") == bookingId).GetProperty("trangThai").GetString() == "Đã nhận phòng", "contract completes its booking");
        await using (var db = Db(database))
        {
            Check(await db.CuTrus.AnyAsync(c => c.MaHopDong == contractId && c.MaKhach == fixture.CustomerId), "contract creates a resident record");
            Check(await db.DangKyDichVus.CountAsync(d => d.MaHopDong == contractId) == 2, "contract creates electricity and water subscriptions");
            Check(await db.PhieuThuChis.Where(p => p.LoaiPhieu == "Thu" && p.NguoiNopNhan == "Khách kiểm thử " + fixture.Tag).SumAsync(p => p.SoTien ?? 0) == 2000000, "deposit is collected once across booking and check-in");
        }
        await Send(adminClient, HttpMethod.Put, $"Phong/{roomId}", new { trangThai = "Trống" }, 400);
        await Send(otherClient, HttpMethod.Get, $"HopDong/{contractId}", null, 403);
        await Send(tenantClient, HttpMethod.Get, $"HopDong/{contractId}", null, 200);
        await Send(tenantClient, HttpMethod.Get, $"HoaDon/hop-dong/{contractId}", null, 200);
        await Send(otherClient, HttpMethod.Get, $"HoaDon/hop-dong/{contractId}", null, 403);
        await Send(adminClient, HttpMethod.Post, "HoaDon", new { maHopDong = contractId, thang = start.AddMonths(-1).Month, nam = start.AddMonths(-1).Year, tongTien = 100000 }, 400);
        await Send(adminClient, HttpMethod.Post, "HoaDon", new { maHopDong = contractId, thang = 1, nam = 10000, tongTien = 100000 }, 400);
        await Send(adminClient, HttpMethod.Post, "HoaDon/generate", new { maHopDong = contractId, thang = today.Month, nam = today.Year }, 400);
        foreach (var (kind, oldValue, newValue) in new[] { ("Điện",100,110), ("Nước",50,52) })
            await Send(adminClient, HttpMethod.Post, "ChiSoDienNuoc", new { maPhong = roomId, loaiChiSo = kind, thang = today.Month, nam = today.Year, chiSoCu = oldValue, chiSoMoi = newValue }, 200);
        var invoice = await Send(adminClient, HttpMethod.Post, "HoaDon/generate", new { maHopDong = contractId, thang = today.Month, nam = today.Year }, 200);
        int invoiceId = invoice.GetProperty("data").GetProperty("maHoaDon").GetInt32();
        Check(invoice.GetProperty("data").GetProperty("tongTien").GetDecimal() == 2075000, "invoice uses room price and actual metered consumption");
        await Send(adminClient, HttpMethod.Post, "HoaDon/generate", new { maHopDong = contractId, thang = today.Month, nam = today.Year }, 400);
        await Send(otherClient, HttpMethod.Get, $"HoaDon/{invoiceId}", null, 403);
        await Send(tenantClient, HttpMethod.Get, $"HoaDon/{invoiceId}", null, 200);
        var readings = await Send(adminClient, HttpMethod.Get, $"ChiSoDienNuoc?maPhong={roomId}", null, 200);
        int readingId = Int(readings[0], "maChiSo");
        await Send(adminClient, HttpMethod.Put, $"ChiSoDienNuoc/{readingId}", new { chiSoMoi = 999 }, 400);
        await Send(adminClient, HttpMethod.Put, $"HoaDon/{invoiceId}/thu-tien", new { soTienThu = 2075001 }, 400);
        var payment = await Send(adminClient, HttpMethod.Put, $"HoaDon/{invoiceId}/thu-tien", new { soTienThu = 500000, phuongThuc = "Tiền mặt" }, 200);
        Check(payment.GetProperty("data").GetProperty("conLai").GetDecimal() == 1575000, "partial payment leaves correct balance");

        var concurrentInvoice = await Send(adminClient, HttpMethod.Post, "HoaDon", new { maHopDong = contractId, thang = start.AddMonths(1).Month, nam = start.AddMonths(1).Year, tongTien = 100000 }, 200);
        int concurrentId = concurrentInvoice.GetProperty("data").GetProperty("maHoaDon").GetInt32();
        var responses = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
        {
            using var response = await adminClient.PutAsJsonAsync($"HoaDon/{concurrentId}/thu-tien", new { soTienThu = 80000 }, Json);
            return (int)response.StatusCode;
        }));
        Check(responses.Count(s => s == 200) == 1 && responses.All(s => s is 200 or 400 or 409), "simultaneous collections cannot collect above the remaining balance");
        await using (var db = Db(database))
        {
            var persisted = await db.HoaDons.AsNoTracking().SingleAsync(h => h.MaHoaDon == concurrentId);
            Check(persisted.DaThanhToan == 80000 && persisted.ConLai == 20000 && await db.ThanhToans.Where(t => t.MaHoaDon == concurrentId).SumAsync(t => t.SoTien) == 80000,
                "concurrent payment ledger and stored balance remain consistent");
        }
        await Send(adminClient, HttpMethod.Put, $"HoaDon/{concurrentId}/thu-tien", new { soTienThu = 20000 }, 200);

        var repair = await Send(tenantClient, HttpMethod.Post, "YeuCauSuaChua", new { maPhong = roomId, maKhach = fixture.CustomerId, noiDung = "Sửa đèn kiểm thử" }, 200);
        int repairId = repair.GetProperty("data").GetProperty("maYeuCau").GetInt32();
        await Send(otherClient, HttpMethod.Post, "YeuCauSuaChua", new { maPhong = roomId, maKhach = fixture.CustomerId, noiDung = "Giả mạo" }, 403);
        await Send(tenantClient, HttpMethod.Post, $"YeuCauSuaChua/{repairId}/xac-nhan", null, 400);
        for (int i=0; i<2; i++) await Send(adminClient, HttpMethod.Put, $"YeuCauSuaChua/{repairId}", new { trangThai = "Đã hoàn thành", chiPhi = 100000 }, 200);
        await using (var db = Db(database)) Check(await db.PhieuThuChis.CountAsync(p => p.LoaiPhieu == "Chi" && p.NoiDung!.Contains($"(yêu cầu sửa chữa số {repairId})")) == 1, "repair expense is recorded once");
        await Send(otherClient, HttpMethod.Post, $"YeuCauSuaChua/{repairId}/xac-nhan", null, 403);
        await Send(tenantClient, HttpMethod.Post, $"YeuCauSuaChua/{repairId}/xac-nhan", null, 200);
        await Send(tenantClient, HttpMethod.Post, "BaoTraPhong", new { maHopDong = contractId, maKhach = fixture.CustomerId, ngayDuKienTra = today, lyDo = "Test" }, 200);
        await Send(tenantClient, HttpMethod.Post, "BaoTraPhong", new { maHopDong = contractId, maKhach = fixture.CustomerId, ngayDuKienTra = today, lyDo = "Duplicate" }, 400);
        await Send(adminClient, HttpMethod.Post, $"HopDong/{contractId}/gia-han", new { ngayKetThucMoi = start.AddMonths(7), giaThueMoi = 2100000 }, 200);
        await Send(adminClient, HttpMethod.Post, "BanGiao", new { maHopDong = contractId, maPhong = roomId, loaiBanGiao = "Trả phòng", ngayBanGiao = today, chiSoDien = 110, chiSoNuoc = 52, chiTietBanGiaos = Array.Empty<object>() }, 200);
        var settled = await Send(adminClient, HttpMethod.Post, $"HopDong/{contractId}/thanh-ly", new { tienHoanCoc = 2000000, tienPhat = 50000, tienNo = 0, ghiChu = "Integration test" }, 200);
        Check(settled.GetProperty("data").GetProperty("tienNo").GetDecimal() == 1575000, "settlement calculates debt at server");
        Check(settled.GetProperty("data").GetProperty("tienHoanCoc").GetDecimal() == 375000, "settlement refunds deposit after debt and fine");
        await Send(adminClient, HttpMethod.Post, $"HopDong/{contractId}/thanh-ly", new { tienHoanCoc = 2000000, tienPhat = 0 }, 400);
        var clearedInvoice = await Send(adminClient, HttpMethod.Get, $"HoaDon/{invoiceId}", null, 200);
        Check(clearedInvoice.GetProperty("conLai").GetDecimal() == 0, "deposit offset clears invoice balance");
        var clearedRoom = await Send(adminClient, HttpMethod.Get, $"Phong/{roomId}", null, 200);
        Check(clearedRoom.GetProperty("trangThai").GetString() == "Trống", "settlement frees the room");
        await Send(adminClient, HttpMethod.Post, "ChiSoDienNuoc", new { maPhong = roomId, loaiChiSo = "Điện", thang = today.Month, nam = today.Year, chiSoCu = 110, chiSoMoi = 111 }, 400);
        foreach (var kind in new[] { "thanh-toan", "dien-nuoc", "dich-vu" }) await Send(tenantClient, HttpMethod.Get, $"KhachThue/{fixture.CustomerId}/lich-su/{kind}", null, 200);
        foreach (var kind in new[] { "doanh-thu", "phong", "khach", "hop-dong" }) await Send(adminClient, HttpMethod.Get, $"BaoCao?loai={kind}&tuNgay={start:yyyy-MM-dd}&denNgay={today:yyyy-MM-dd}", null, 200);
        await Send(adminClient, HttpMethod.Get, "DanhMucMoRong/dashboard", null, 200);
        await Send(adminClient, HttpMethod.Post, "SaoLuu/tao", null, 200);
        await Send(adminClient, HttpMethod.Get, "SaoLuu", null, 200);
        await Send(tenantClient, HttpMethod.Post, "Auth/logout", null, 200);
        await Send(tenantClient, HttpMethod.Get, $"KhachThue/{fixture.CustomerId}", null, 401);
        Console.WriteLine($"PASS {checks} live HTTP/SQL checks on {database}.");
    }

    static async Task RunExtra(Fixture fixture, string database)
    {
        using var adminClient = Client(); using var tenantClient = Client();
        await Send(adminClient, HttpMethod.Get, "health", null, 200);
        await Login(adminClient, fixture.Admin, fixture.Password);
        await Login(tenantClient, fixture.Tenant, fixture.Password);
        var rooms = await Send(adminClient, HttpMethod.Get, "Phong", null, 200);
        Check(rooms.EnumerateArray().Any(x => x.GetProperty("tenPhong").GetString() == "TEST-" + fixture.Tag),
            "API points at the isolated copy with the completed integration fixture");

        int contractId, roomId, receiptsBefore;
        DateOnly contractStart, contractEnd;
        await using (var db = Db(database))
        {
            var active = await db.HopDongs.AsNoTracking().Where(h => h.TrangThai == "Đang hiệu lực")
                .Select(h => new { h.MaHopDong, h.MaPhong, h.NgayBatDau, h.NgayKetThuc }).FirstAsync();
            contractId = active.MaHopDong; roomId = active.MaPhong;
            contractStart = active.NgayBatDau; contractEnd = active.NgayKetThuc;
            receiptsBefore = await db.PhieuThuChis.CountAsync();
        }
        string suffix = Guid.NewGuid().ToString("N")[..8];
        var violation = await Send(adminClient, HttpMethod.Post, "ViPham",
            new { maHopDong = contractId, loaiViPham = "Kiểm thử", moTa = "Bản sao " + suffix, mucPhat = 50000 }, 200);
        int violationId = Int(violation.GetProperty("data"), "maViPham");
        var violations = await Send(adminClient, HttpMethod.Get, "ViPham", null, 200);
        Check(violations.EnumerateArray().Any(v => Int(v, "maViPham") == violationId &&
            v.GetProperty("tenPhong").GetString() is { Length: > 0 }), "violation lists the room name");
        await Send(adminClient, HttpMethod.Put, $"ViPham/{violationId}/xu-ly",
            new { hinhThucXuLy = "Nhắc nhở", tienPhat = 50000, guiThongBao = true }, 200);
        await Send(adminClient, HttpMethod.Put, $"ViPham/{violationId}/xu-ly",
            new { hinhThucXuLy = "Nhắc nhở", tienPhat = 50000, guiThongBao = true }, 400);
        await using (var db = Db(database))
            Check(await db.PhieuThuChis.CountAsync() == receiptsBefore,
                "processing a violation does not collect money automatically");

        var asset = await Send(adminClient, HttpMethod.Post, "TaiSan",
            new { tenTaiSan = "Tài sản kiểm thử " + suffix, donViTinh = "cái", giaTriThamKhao = 100000 }, 200);
        int assetId = Int(asset.GetProperty("data"), "maTaiSan");
        int assetRoomId = rooms.EnumerateArray()
            .Where(x => x.GetProperty("tenPhong").GetString() == "TEST-" + fixture.Tag)
            .Select(x => Int(x, "maPhong")).Single();
        await Send(adminClient, HttpMethod.Post, "TaiSan/gan-phong",
            new { maPhong = roomId, maTaiSan = assetId, soLuong = 1, tinhTrang = "Tốt" }, 400);
        await Send(adminClient, HttpMethod.Post, "TaiSan/gan-phong",
            new { maPhong = assetRoomId, maTaiSan = assetId, soLuong = 1, tinhTrang = "Tốt" }, 200);
        await Send(adminClient, HttpMethod.Post, "TaiSan/gan-phong",
            new { maPhong = assetRoomId, maTaiSan = assetId, soLuong = 1 }, 400);
        var assets = await Send(adminClient, HttpMethod.Get, $"TaiSan/phong/{assetRoomId}", null, 200);
        var assigned = assets.EnumerateArray().Single(a => Int(a, "maTaiSan") == assetId);
        Check(assigned.GetProperty("tenTaiSan").GetString() == "Tài sản kiểm thử " + suffix,
            "room asset list displays the asset name");

        var service = await Send(adminClient, HttpMethod.Post, "DichVu",
            new { tenDichVu = "Dịch vụ kiểm thử " + suffix, donGia = 45000, donViTinh = "tháng", trangThai = "Đang áp dụng" }, 200);
        int serviceId = Int(service.GetProperty("data"), "maDichVu");
        var today = DateOnly.FromDateTime(DateTime.Today);
        DateOnly serviceStart = today < contractStart ? contractStart : today > contractEnd ? contractEnd : today;
        var subscription = await Send(adminClient, HttpMethod.Post, "DangKyDichVu",
            new { maHopDong = contractId, maDichVu = serviceId, ngayBatDau = serviceStart }, 200);
        int subscriptionId = Int(subscription.GetProperty("data"), "maDangKy");
        await Send(adminClient, HttpMethod.Post, "DangKyDichVu",
            new { maHopDong = contractId, maDichVu = serviceId, ngayBatDau = serviceStart }, 400);
        var subscriptions = await Send(adminClient, HttpMethod.Get, $"DangKyDichVu?maHopDong={contractId}", null, 200);
        var subscribed = subscriptions.EnumerateArray().Single(s => Int(s, "maDangKy") == subscriptionId);
        Check(subscribed.GetProperty("donGiaApDung").GetDecimal() == 45000 &&
              subscribed.GetProperty("tenDichVu").GetString() == "Dịch vụ kiểm thử " + suffix,
            "subscription locks the price and displays the service name");
        if (serviceStart <= today)
        {
            await Send(adminClient, HttpMethod.Put, $"DangKyDichVu/{subscriptionId}/ngung", null, 200);
            var stopped = await Send(adminClient, HttpMethod.Get, $"DangKyDichVu?maHopDong={contractId}", null, 200);
            var stopDate = stopped.EnumerateArray().Single(s => Int(s, "maDangKy") == subscriptionId)
                .GetProperty("ngayKetThuc").GetString();
            await Send(adminClient, HttpMethod.Put, $"DangKyDichVu/{subscriptionId}/ngung", null, 200);
            var stoppedAgain = await Send(adminClient, HttpMethod.Get, $"DangKyDichVu?maHopDong={contractId}", null, 200);
            Check(stoppedAgain.EnumerateArray().Single(s => Int(s, "maDangKy") == subscriptionId)
                .GetProperty("ngayKetThuc").GetString() == stopDate, "repeated service stop keeps the original end date");
        }

        var notification = await Send(adminClient, HttpMethod.Post, "ThongBao",
            new { tieuDe = "Thông báo kiểm thử " + suffix, noiDung = "Bản sao dữ liệu demo", danhSachMaTaiKhoan = new[] { fixture.TenantAccountId } }, 200);
        int notificationId = Int(notification, "maThongBao");
        var inbox = await Send(tenantClient, HttpMethod.Get, $"ThongBao/customer/{fixture.TenantAccountId}", null, 200);
        Check(inbox.EnumerateArray().Any(n => Int(n, "maThongBao") == notificationId),
            "customer receives the intended notification");
    }

    static HttpClient Client() => new(new HttpClientHandler { CookieContainer = new CookieContainer(), AllowAutoRedirect = false }) { BaseAddress = new Uri(ApiBaseUrl.TrimEnd('/') + "/api/"), Timeout = TimeSpan.FromMinutes(12) };
    static async Task Login(HttpClient client, string username, string password) => await Send(client, HttpMethod.Post, "Auth/login", new { username, password }, 200);
    static int Int(JsonElement value, string field) => value.GetProperty(field).GetInt32();
    static async Task<JsonElement> Send(HttpClient client, HttpMethod method, string path, object? body, int status)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body != null) request.Content = JsonContent.Create(body, options: Json);
        using var response = await client.SendAsync(request); string text = await response.Content.ReadAsStringAsync();
        if ((int)response.StatusCode != status) throw new InvalidOperationException($"{method} {path}: expected {status}, got {(int)response.StatusCode}. {text}");
        Check(true, $"{method} {path} => {status}");
        return JsonSerializer.Deserialize<JsonElement>(text, Json);
    }
    record Fixture(string Database, string Tag, string Admin, string Tenant, string Other, string Password, int AdminAccountId, int TenantAccountId, int CustomerId, int OtherCustomerId);
}
