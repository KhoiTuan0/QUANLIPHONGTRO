using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using QLPT.API.Controllers;
using QLPT.API.Models;
using QLPT.API.Services;

public static class ApiValidationChecks
{
    public static async Task<int> RunAsync()
    {
        // A missing context proves invalid input is rejected before any database access.
        var contracts = new HopDongController(null!);
        var bookings = new DatPhongController(null!);
        var rooms = new PhongController(null!);
        var assets = new TaiSanController(null!);
        var customers = new KhachThueController(null!);
        var checkouts = new BaoTraPhongController(null!);
        var repairRequests = new YeuCauSuaChuaController(null!);
        var repairs = new SuaChuaController(null!);
        var residents = new DanhMucMoRongController(null!);
        var handovers = new BanGiaoController(null!);
        var today = DateOnly.FromDateTime(DateTime.Today);

        HopDong Contract(Action<HopDong>? change = null)
        {
            var contract = new HopDong
            {
                MaPhong = 1, MaKhachDaiDien = 2, NgayBatDau = today,
                NgayKetThuc = today.AddYears(1), GiaThueThang = 2_000_000, TienCoc = 1_000_000
            };
            change?.Invoke(contract);
            return contract;
        }

        DatPhong Booking(Action<DatPhong>? change = null)
        {
            var booking = new DatPhong { MaPhong = 1, MaKhach = 2, NgayDat = today, TienCoc = 1_000_000 };
            change?.Invoke(booking);
            return booking;
        }

        BanGiaoCreateDto Handover(Action<BanGiaoCreateDto>? change = null)
        {
            var handover = new BanGiaoCreateDto
            {
                MaHopDong = 1, MaPhong = 2, LoaiBanGiao = BanGiaoController.TraPhong,
                NgayBanGiao = today, ChiSoDien = 100, ChiSoNuoc = 50,
                ChiTietBanGiaos = [new ChiTietBanGiaoCreateDto { MaTaiSan = 3, SoLuong = 1, TinhTrang = "Tốt", ChiPhiPhat = 0 }]
            };
            change?.Invoke(handover);
            return handover;
        }

        var cases = new List<(string Name, Func<Task<IActionResult>> Run)>
        {
            ("contract missing room", () => contracts.Create(Contract(h => h.MaPhong = 0))),
            ("contract missing customer", () => contracts.Create(Contract(h => h.MaKhachDaiDien = 0))),
            ("contract missing start date", () => contracts.Create(Contract(h => h.NgayBatDau = default))),
            ("contract missing end date", () => contracts.Create(Contract(h => h.NgayKetThuc = default))),
            ("contract end equals start", () => contracts.Create(Contract(h => h.NgayKetThuc = today))),
            ("contract end before start", () => contracts.Create(Contract(h => h.NgayKetThuc = today.AddDays(-1)))),
            ("contract zero rent", () => contracts.Create(Contract(h => h.GiaThueThang = 0))),
            ("contract negative rent", () => contracts.Create(Contract(h => h.GiaThueThang = -1))),
            ("contract negative deposit", () => contracts.Create(Contract(h => h.TienCoc = -1))),
            ("contract invalid signing date", () => contracts.Create(Contract(h => h.NgayKy = DateOnly.MinValue))),
            ("expiry negative window", () => contracts.SapHetHan(-1)),
            ("expiry excessive window", () => contracts.SapHetHan(int.MaxValue)),
            ("booking missing room", () => bookings.Create(Booking(b => b.MaPhong = 0))),
            ("booking missing customer", () => bookings.Create(Booking(b => b.MaKhach = 0))),
            ("booking negative deposit", () => bookings.Create(Booking(b => b.TienCoc = -1))),
            ("booking arrival before booking date", () => bookings.Create(Booking(b => b.NgayDuKienNhan = today.AddDays(-1)))),
            ("booking appointment before booking date", () => bookings.Create(Booking(b => b.NgayHenKyHopDong = today.AddDays(-1)))),
            ("room blank name", () => rooms.PostPhong(new Phong { TenPhong = "  " })),
            ("room missing room type", () => rooms.PostPhong(new Phong { TenPhong = "A101" })),
            ("room oversized name", () => rooms.PostPhong(new Phong { TenPhong = new string('a', 51) })),
            ("room negative price", () => rooms.PostPhong(new Phong { TenPhong = "A101", GiaThue = -1 })),
            ("room cannot directly become occupied", () => rooms.PostPhong(new Phong { TenPhong = "A101", TrangThai = NghiepVu.Phong.DangThue })),
            ("room cannot directly become reserved", () => rooms.PostPhong(new Phong { TenPhong = "A101", TrangThai = NghiepVu.Phong.DaDatCoc })),
            ("room unknown status", () => rooms.PostPhong(new Phong { TenPhong = "A101", TrangThai = "test" })),
            ("asset blank name", () => assets.Create(new TaiSan { TenTaiSan = " " })),
            ("asset oversized name", () => assets.Create(new TaiSan { TenTaiSan = new string('a', 101) })),
            ("asset negative value", () => assets.Create(new TaiSan { TenTaiSan = "Giường", GiaTriThamKhao = -1 })),
            ("asset assignment missing room", () => assets.GanVaoPhong(new TaiSanPhong { MaTaiSan = 1, SoLuong = 1 })),
            ("asset assignment missing asset", () => assets.GanVaoPhong(new TaiSanPhong { MaPhong = 1, SoLuong = 1 })),
            ("asset assignment zero quantity", () => assets.GanVaoPhong(new TaiSanPhong { MaPhong = 1, MaTaiSan = 1, SoLuong = 0 })),
            ("asset assignment negative quantity", () => assets.GanVaoPhong(new TaiSanPhong { MaPhong = 1, MaTaiSan = 1, SoLuong = -1 })),
            ("customer blank name", () => customers.Create(new KhachThue { HoTen = " " })),
            ("customer oversized name", () => customers.Create(new KhachThue { HoTen = new string('a', 101) })),
            ("customer oversized identity number", () => customers.Create(new KhachThue { HoTen = "Khách", Cccd = new string('1', 21) })),
            ("customer oversized phone", () => customers.Create(new KhachThue { HoTen = "Khách", SoDienThoai = new string('1', 21) })),
            ("customer invalid email", () => customers.Create(new KhachThue { HoTen = "Khách", Email = "email không hợp lệ" })),
            ("customer future birth date", () => customers.Create(new KhachThue { HoTen = "Khách", NgaySinh = today.AddDays(1) })),
            ("customer invalid birth date", () => customers.Create(new KhachThue { HoTen = "Khách", NgaySinh = DateOnly.MinValue })),
            ("customer update blank name", () => customers.Update(1, new KhachThueUpdateDto { HoTen = " " })),
            ("customer update future birth date", () => customers.Update(1, new KhachThueUpdateDto { NgaySinh = today.AddDays(1) })),
            ("checkout missing references", () => checkouts.Create(new BaoTraPhong())),
            ("checkout past expected date", () => checkouts.Create(new BaoTraPhong { MaHopDong = 1, MaKhach = 2, NgayDuKienTra = today.AddDays(-1) })),
            ("repair request missing references", () => repairRequests.Create(new YeuCauSuaChua { NoiDung = "Cửa hỏng" })),
            ("repair request empty content", () => repairRequests.Create(new YeuCauSuaChua { MaPhong = 1, MaKhach = 2, NoiDung = " " })),
            ("repair missing references", () => repairs.Create(new TaoSuaChuaDto { NoiDung = "Cửa hỏng" })),
            ("repair invalid contract reference", () => repairs.Create(new TaoSuaChuaDto { MaHopDong = 0, NoiDung = "Cửa hỏng" })),
            ("repair future request date", () => repairs.Create(new TaoSuaChuaDto { MaPhong = 1, MaKhach = 2, NoiDung = "Cửa hỏng", NgayYeuCau = DateTime.Today.AddDays(1) })),
            ("repair invalid request date", () => repairs.Create(new TaoSuaChuaDto { MaPhong = 1, MaKhach = 2, NoiDung = "Cửa hỏng", NgayYeuCau = DateTime.MinValue })),
            ("repair oversized content", () => repairs.Create(new TaoSuaChuaDto { MaPhong = 1, MaKhach = 2, NoiDung = new string('a', 501) })),
            ("repair update negative cost", () => repairs.Update(1, new SuaChuaUpdateDto { ChiPhi = -1 })),
            ("repair update unknown status", () => repairs.Update(1, new SuaChuaUpdateDto { TrangThai = "test" })),
            ("residency missing references", () => residents.AddCuTru(new CuTru())),
            ("new resident cannot start as departed", () => residents.AddCuTru(new CuTru { MaHopDong = 1, MaKhach = 2, TrangThaiCuTru = NghiepVu.CuTru.DaRoiDi })),
            ("new resident cannot include departure date", () => residents.AddCuTru(new CuTru { MaHopDong = 1, MaKhach = 2, NgayRoi = today })),
            ("residence declaration needs an address", () => residents.DeNghiKhaiBao(1, new GuiKhaiBaoCuTruDto())),
            ("residence declaration address too long", () => residents.DeNghiKhaiBao(1,
                new GuiKhaiBaoCuTruDto { DiaChiThuongTru = new string('a', 301) })),
            ("rejected residence declaration needs a reason", () => residents.XuLyKhaiBao(1,
                new XuLyKhaiBaoCuTruDto { ChapNhan = false })),
            ("handover missing references", () => handovers.Create(Handover(h => h.MaHopDong = 0))),
            ("handover unknown kind", () => handovers.Create(Handover(h => h.LoaiBanGiao = "test"))),
            ("handover missing date", () => handovers.Create(Handover(h => h.NgayBanGiao = default))),
            ("handover future date", () => handovers.Create(Handover(h => h.NgayBanGiao = today.AddDays(1)))),
            ("handover negative meter", () => handovers.Create(Handover(h => h.ChiSoDien = -1))),
            ("handover meter precision", () => handovers.Create(Handover(h => h.ChiSoNuoc = 1.123m))),
            ("handover meter overflow", () => handovers.Create(Handover(h => h.ChiSoDien = decimal.MaxValue))),
            ("handover duplicate asset", () => handovers.Create(Handover(h => h.ChiTietBanGiaos!.Add(h.ChiTietBanGiaos[0])))),
            ("handover invalid asset reference", () => handovers.Create(Handover(h => h.ChiTietBanGiaos![0].MaTaiSan = 0))),
            ("handover negative asset quantity", () => handovers.Create(Handover(h => h.ChiTietBanGiaos![0].SoLuong = -1))),
            ("handover receipt zero quantity", () => handovers.Create(Handover(h => { h.LoaiBanGiao = BanGiaoController.NhanPhong; h.ChiTietBanGiaos![0].SoLuong = 0; }))),
            ("handover negative fine", () => handovers.Create(Handover(h => h.ChiTietBanGiaos![0].ChiPhiPhat = -1))),
            ("handover fine precision", () => handovers.Create(Handover(h => h.ChiTietBanGiaos![0].ChiPhiPhat = 1.123m))),
            ("handover receipt cannot include fine", () => handovers.Create(Handover(h => { h.LoaiBanGiao = BanGiaoController.NhanPhong; h.ChiTietBanGiaos![0].ChiPhiPhat = 100; }))),
            ("handover blank asset condition", () => handovers.Create(Handover(h => h.ChiTietBanGiaos![0].TinhTrang = " "))),
            ("handover oversized notes", () => handovers.Create(Handover(h => h.GhiChu = new string('a', 301))))
        };

        foreach (var (name, run) in cases)
        {
            var result = await run();
            if (result is not BadRequestObjectResult { StatusCode: 400 } badRequest)
                throw new InvalidOperationException($"API validation failed: {name}; received {result.GetType().Name}. The input must be rejected without database access.");
            var message = badRequest.Value?.GetType().GetProperty("message")?.GetValue(badRequest.Value)?.ToString();
            if (string.IsNullOrWhiteSpace(message))
                throw new InvalidOperationException($"API validation failed: {name}; missing a readable error message.");
        }

        var updateRoutes = typeof(SuaChuaController).GetMethod(nameof(SuaChuaController.Update))!
            .GetCustomAttributes(typeof(HttpPutAttribute), false).Cast<HttpPutAttribute>();
        if (!updateRoutes.Any(r => r.Template == "/api/YeuCauSuaChua/{id}"))
            throw new InvalidOperationException("The repair request PUT route must share the repair accounting action.");
        var customerRoutes = typeof(YeuCauSuaChuaController).GetMethod(nameof(YeuCauSuaChuaController.GetAll))!
            .GetCustomAttributes(typeof(HttpGetAttribute), false).Cast<HttpGetAttribute>();
        if (!customerRoutes.Any(r => r.Template == "customer/{maKhach}"))
            throw new InvalidOperationException("The mobile customer repair request route must be available.");
        var handoverRoutes = typeof(BanGiaoController).GetMethod(nameof(BanGiaoController.Create))!
            .GetCustomAttributes(typeof(HttpPostAttribute), false).Cast<HttpPostAttribute>();
        if (!handoverRoutes.Any(r => r.Template == "/api/DanhMucMoRong/ban-giao"))
            throw new InvalidOperationException("The existing handover client POST route must be available.");
        if (BanGiaoController.Validate(Handover()) != null)
            throw new InvalidOperationException("Valid handover input must pass structural validation.");
        int imageChecks = await RunImageChecksAsync();
        Console.WriteLine($"PASS {cases.Count} API validation scenarios without database access, 3 routes and {imageChecks} image checks.");
        return cases.Count + 4 + imageChecks;
    }

    private static async Task<int> RunImageChecksAsync()
    {
        string temporaryRoot = Path.Combine(Path.GetTempPath(), "qlpt-image-check-" + Guid.NewGuid().ToString("N"));
        var environment = new ImageCheckEnvironment { ContentRootPath = temporaryRoot };
        var images = new AnhController(environment) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aFQAAAABJRU5ErkJggg==");
        var streams = new List<MemoryStream>();
        FormFile Image(byte[] data, string filename = "meter.png", string mime = "image/png", long? length = null)
        {
            var stream = new MemoryStream(data);
            streams.Add(stream);
            return new FormFile(stream, 0, length ?? data.Length, "image", filename)
            {
                Headers = new HeaderDictionary(), ContentType = mime
            };
        }
        try
        {
            var invalid = new List<Task<IActionResult>>
            {
                images.Upload(null),
                images.Upload(Image([])),
                images.Upload(Image([], length: AnhController.MaxUploadBytes + 1)),
                images.Upload(Image("<svg/>"u8.ToArray(), "image.svg", "image/svg+xml")),
                images.Upload(Image(png, "meter.jpg", "image/jpeg")),
                images.Upload(Image(png, mime: "image/jpeg")),
                images.Upload(Image("not an image"u8.ToArray())),
                images.Upload(Image(png), "../../outside"),
                images.Get(Guid.Empty)
            };
            foreach (var pending in invalid)
                if (await pending is not BadRequestObjectResult { StatusCode: 400 })
                    throw new InvalidOperationException("An invalid image upload or image ID must be rejected.");
            if (Directory.Exists(temporaryRoot))
                throw new InvalidOperationException("Rejected image input must not create storage directories or files.");
            if (await images.Get(Guid.NewGuid()) is not NotFoundObjectResult)
                throw new InvalidOperationException("An unknown image must return 404.");

            // The client omits a Content-Type header; the signature supplies the format safely.
            var saved = await images.Upload(Image(png, "../../tenant.PNG", "application/octet-stream"));
            if (saved is not OkObjectResult ok)
                throw new InvalidOperationException("A valid PNG upload must succeed.");
            string? path = ok.Value?.GetType().GetProperty("duongDanAnh")?.GetValue(ok.Value)?.ToString();
            if (path == null || !path.StartsWith("/api/Anh/") || !Guid.TryParse(path[9..], out var id))
                throw new InvalidOperationException("Image upload must return the client-compatible relative GUID URL.");
            string storage = Path.Combine(temporaryRoot, "App_Data", "Anh");
            var storedFiles = Directory.GetFiles(storage);
            if (storedFiles.Length != 1 || Path.GetFileName(storedFiles[0]) != id.ToString("N") + ".png")
                throw new InvalidOperationException("Image storage must ignore the caller's filename and use the server GUID.");
            var loaded = await images.Get(id);
            if (loaded is not FileContentResult file || file.ContentType != "image/png" || !file.FileContents.SequenceEqual(png))
                throw new InvalidOperationException("Image read must return the original PNG bytes and matching MIME type.");
            if (images.Response.Headers.XContentTypeOptions != "nosniff")
                throw new InvalidOperationException("Image responses must disable MIME sniffing.");
            return invalid.Count + 5;
        }
        finally
        {
            foreach (var stream in streams) stream.Dispose();
            // Delete only the fixed directories and files created by this isolated fixture.
            string imageDirectory = Path.Combine(temporaryRoot, "App_Data", "Anh");
            if (Directory.Exists(imageDirectory))
            {
                foreach (var file in Directory.GetFiles(imageDirectory)) File.Delete(file);
                Directory.Delete(imageDirectory);
            }
            string dataDirectory = Path.Combine(temporaryRoot, "App_Data");
            if (Directory.Exists(dataDirectory)) Directory.Delete(dataDirectory);
            if (Directory.Exists(temporaryRoot)) Directory.Delete(temporaryRoot);
        }
    }

    private sealed class ImageCheckEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "QLPT.ImageChecks";
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = "";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
