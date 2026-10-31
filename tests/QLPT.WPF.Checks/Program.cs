using System.Net;
using System.Net.Http;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using QLPT.WPF.Controls;
using QLPT.WPF.Services;
using QLPT.WPF.Views.Dialogs;
using QLPT.WPF.Views;
using QLPT.WPF.ViewModels;

class Program
{
    [STAThread]
    static void Main()
    {
        var app = new QLPT.WPF.App();
        app.InitializeComponent();
        // This is a headless behavior check: do not open the production login window
        // when a DispatcherFrame is pumped later in the checks.
        var startup = typeof(QLPT.WPF.App).GetMethod("App_Startup", BindingFlags.Instance | BindingFlags.NonPublic)!;
        app.Startup -= (StartupEventHandler)Delegate.CreateDelegate(typeof(StartupEventHandler), app, startup);
        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var scrollProbe = new ScrollViewer { Width = 180, Height = 100,
            VerticalScrollBarVisibility = ScrollBarVisibility.Visible,
            Content = new Border { Height = 400 } };
        scrollProbe.Measure(new Size(180, 100));
        scrollProbe.Arrange(new Rect(0, 0, 180, 100));
        scrollProbe.UpdateLayout();
        var scrollBar = scrollProbe.Template.FindName("PART_VerticalScrollBar", scrollProbe) as ScrollBar;
        Check(scrollBar != null && scrollBar.ActualWidth <= 10,
            $"vertical scrollbars reserve no more than 10 pixels (actual {scrollBar?.ActualWidth}, width {scrollBar?.Width}, min {scrollBar?.MinWidth}, margin {scrollBar?.Margin}, source {DependencyPropertyHelper.GetValueSource(scrollBar!, FrameworkElement.WidthProperty).BaseValueSource})");
        Session.MaTaiKhoan = 1;
        Session.VaiTro = "Admin";
        var printContract = JsonSerializer.SerializeToElement(new {
            maHopDong = 17, ngayKy = "2026-10-20", ngayBatDau = "2026-10-21", ngayKetThuc = "2027-10-20",
            giaThueThang = 2400000m, tienCoc = 2400000m, ghiChu = "Kiểm tra <phụ lục>"
        });
        var printRoom = JsonSerializer.SerializeToElement(new { tenPhong = "A101 <script>alert(1)</script>", tenCoSo = "Nhà trọ An Hòa" });
        var printCustomer = JsonSerializer.SerializeToElement(new { hoTen = "Nguyễn <Minh> Anh", cccd = "079026000101",
            soDienThoai = "0904500101", diaChi = "Thủ Đức" });
        string contractHtml = BieuMauHtml.HopDong(printContract, printRoom, printCustomer);
        Check(contractHtml.Contains("2.400.000") && contractHtml.Contains("21/10/2026") &&
            contractHtml.Contains("Nguyễn &lt;Minh&gt; Anh") && !contractHtml.Contains("<script>alert(1)</script>"),
            "contract form uses saved values and HTML-encodes tenant and room text");
        var printHandover = JsonSerializer.SerializeToElement(new { maBanGiao = 5, maHopDong = 17,
            loaiBanGiao = "Nhận phòng", ngayBanGiao = "2026-10-21", tenPhong = "A101", hoTenKhach = "Nguyễn Minh Anh",
            chiSoDien = 110, chiSoNuoc = 12, ghiChu = "Đã đối chiếu", chiTietBanGiaos = new[] {
                new { tenTaiSan = "Giường <gỗ>", soLuong = 1, tinhTrang = "Tốt", chiPhiPhat = (decimal?)null, ghiChu = "" }
            } });
        string handoverHtml = BieuMauHtml.BanGiao(printHandover);
        Check(handoverHtml.Contains("Giường &lt;gỗ&gt;") && handoverHtml.Contains("0 đ") &&
            handoverHtml.Contains("21/10/2026") && WebUtility.HtmlDecode(handoverHtml).Contains("Đã đối chiếu"),
            "handover form prints stored asset details and handles an empty penalty");
        var fake = new FixtureHandler();
        typeof(ApiClient).GetField("_http", BindingFlags.NonPublic | BindingFlags.Static)!
            .SetValue(null, new HttpClient(fake) { BaseAddress = new Uri("http://localhost/") });

        fake.Overrides["/api/KhachThue/5"] = () => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
            { Content = new StringContent("{\"message\":\"Số điện thoại đã thuộc về khách thuê khác.\"}") });
        try
        {
            ApiClient.PutAsync("/api/KhachThue/5", new { soDienThoai = "0904500103" }).GetAwaiter().GetResult();
            Check(false, "duplicate customer phone must be rejected");
        }
        catch (ApiResponseException ex)
        {
            Check(ex.Message.Contains("Số điện thoại đã thuộc về khách thuê khác."),
                "WPF shows the business reason for a duplicate phone");
        }
        fake.Overrides.Remove("/api/KhachThue/5");

        var picker = new EntityPicker { Source = LookupKind.KhachThue };
        picker.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        var combo = (ComboBox)picker.FindName("CbOptions");
        var search = (TextBox)picker.FindName("TxtSearch");
        Check(picker.IsReady && combo.Items.Count == 100, "100 existing tenants loaded");
        Check(combo.IsEditable, "picker combines typing and selection in one field");
        picker.SelectedId = "100";
        Check(picker.TryGetSelectedId(out var selected) && selected == 100, "existing tenant selected");
        search.Text = "dang van khoi";
        Check(combo.Items.Count == 1 && picker.TryGetSelectedId(out selected) && selected == 100, "accent insensitive name search");
        search.Text = "000000000100";
        Check(combo.Items.Count == 1, "identity search");
        search.Text = "0900000100";
        Check(combo.Items.Count == 1, "phone search");
        search.Text = "unknown arbitrary tenant";
        Check(combo.Items.Count == 0 && !picker.TryGetSelectedId(out _) && picker.SelectedId == "", "no-result search cannot save a hidden or invented tenant");
        search.Text = "";
        picker.SelectedId = "99999";
        Check(!picker.TryGetSelectedId(out _), "nonexistent selected id rejected");

        var rooms = LookupData.LoadAsync(LookupKind.PhongTrong).GetAwaiter().GetResult();
        Check(rooms.Count == 1 && rooms[0].Id == "1" && rooms[0].Label.Contains("Cơ sở A"), "available rooms exclude maintenance, occupied and stale reserved states; display facility");
        var booked = LookupData.LoadAsync(LookupKind.PhongDaDat).GetAwaiter().GetResult();
        Check(booked.Count == 1 && booked[0].Id == "5", "only valid reserved rooms selectable for receiving");
        var contracts = LookupData.LoadAsync(LookupKind.HopDongHieuLuc).GetAwaiter().GetResult();
        Check(contracts.Count == 1 && contracts[0].CustomerId == "1", "active contracts identify representative");

        var receive = new HopDongDialog("5", "100", 1500000);
        foreach (var name in new[] { "PickMaPhong", "PickMaKhachDaiDien" })
            ((EntityPicker)receive.FindName(name)).RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        var roomPicker = (EntityPicker)receive.FindName("PickMaPhong");
        var tenantPicker = (EntityPicker)receive.FindName("PickMaKhachDaiDien");
        Check(roomPicker.ReadOnly && tenantPicker.ReadOnly && roomPicker.TryGetSelectedId(out selected) && selected == 5 && tenantPicker.TryGetSelectedId(out selected) && selected == 100, "receiving locks booked room and tenant");
        Check(VnMoney.Parse(((TextBox)receive.FindName("TxtTienCoc")).Text) == 1500000 && VnMoney.Parse(((TextBox)receive.FindName("TxtGiaThueThang")).Text) == 2000000, "receiving inherits deposit and room rent");

        var notice = new ThongBaoDialog();
        notice.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        var recipients = (ListBox)notice.FindName("ListRecipients");
        Check(recipients.Items.Count == 2, "notice excludes inactive and admin accounts");
        ((RecipientOption)recipients.Items[0]).IsSelected = true;
        ((TextBox)notice.FindName("TxtSearchRecipient")).Text = "dang van khoi";
        Check(recipients.Items.Count == 1 && ((TextBlock)notice.FindName("TxtRecipientStatus")).Text.Contains("Đã chọn 1"), "recipient selection survives search filtering");
        Check(((CheckBox)notice.FindName("CbAll")).IsChecked != true, "empty selection does not default to broadcast");

        fake.EmptyTenants = true;
        picker.ReloadAsync().GetAwaiter().GetResult();
        Check(picker.IsReady && combo.Items.Count == 0 && !picker.TryGetSelectedId(out _), "empty list blocks saving");
        fake.EmptyTenants = false;
        fake.FailTenants = true;
        picker.ReloadAsync().GetAwaiter().GetResult();
        Check(!picker.IsReady && !picker.TryGetSelectedId(out _) && ((TextBlock)picker.FindName("TxtStatus")).Text.Contains("Không tải"), "load failure blocks saving and shows retry state");
        fake.FailTenants = false;
        picker.ReloadAsync().GetAwaiter().GetResult();
        Check(picker.IsReady && combo.Items.Count == 100, "reload recovers from server failure");

        var residence = new CuTruViewModel { SelectedItem = new CuTruItem { MaCuTru = "9", TrangThaiCuTru = "Đang ở" } };
        residence.TamVangCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        Check(JsonDocument.Parse(fake.LastBody).RootElement.GetProperty("trangThaiCuTru").GetString() == "Tạm vắng" && !fake.LastBody.Contains("trangThaiKhaiBao"), "temporary absence changes residence rather than declaration status");
        residence.TroLaiCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        Check(JsonDocument.Parse(fake.LastBody).RootElement.GetProperty("trangThaiCuTru").GetString() == "Đang ở", "returning restores active residence status");

        var backups = new SaoLuuViewModel();
        Check(backups.InfoMessage.Contains("Backups:SqlServerDirectory") && !backups.InfoMessage.Contains("chưa được hỗ trợ"), "backup 503 explains configuration instead of claiming unsupported API");
        fake.MissingBackupConfig = false;
        backups.TaoSaoLuuCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        Check(fake.BackupPosted && backups.Items.Count == 1 && backups.InfoMessage.Contains("checksum") && backups.InfoMessage.Contains("C:/SQLBackups/fixture.bak"), "backup creation uses real endpoint and displays verified result path");

        Window[] dialogs = { new DatPhongDialog(), new BaoTraDialog(), new CuTruDialog(null), new ChiSoDialog(),
            new HoaDonDialog(), new DangKyDichVuDialog(""), new SuaChuaDialog(), new ViPhamDialog(), new PhongDialog(null),
            new TaiKhoanDialog(null), new ThuChiDialog(),
            new KhoTaiSanDialog(new TaiSanItem { MaTaiSan = "1", TenTaiSan = "Giường", SoLuongTonKho = 50 }),
            new KhoTaiSanDialog(new TaiSanItem { MaTaiSan = "1", TenTaiSan = "Giường", SoLuongTonKho = 50 }, 1),
            new KhaiBaoCuTruDialog(new CuTruItem
                { MaCuTru = "1", HoTen = "Khách thử", TenPhong = "A101" }), new TaiSanLichSuDialog(new TaiSanCaTheItem
                { MaTaiSanCaThe = "1", MaDinhDanh = "TS00000001", TenTaiSan = "Giường" }) };
        foreach (var dialog in dialogs) Check(dialog.Content != null, dialog.GetType().Name + " XAML loads");

        var meterDialog = new ChiSoDialog();
        typeof(ChiSoDialog).GetField("_ocrSuggestion", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(meterDialog, 8867m);
        typeof(ChiSoDialog).GetMethod("UseSuggestion_Click", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(meterDialog, new object?[] { null, new RoutedEventArgs() });
        Check(((TextBox)meterDialog.FindName("TxtChiSoMoi")).Text == "08867",
            "meter OCR suggestion preserves the leading zero shown on a five-digit meter");

        SessionAndInputChecks.Run(Check, fake);

        Session.MaTaiKhoan = 1;
        Session.MaKhach = null;
        Session.VaiTro = "Admin";
        var shellCookies = new CookieContainer();
        shellCookies.Add(new Uri(AppConfig.ApiBaseUrl), new Cookie("QLPT.Session", "fixture", "/"));
        typeof(ApiClient).GetField("_cookies", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, shellCookies);
        typeof(ApiClient).GetField("_http", BindingFlags.NonPublic | BindingFlags.Static)!
            .SetValue(null, new HttpClient(fake, false) { BaseAddress = new Uri(AppConfig.ApiBaseUrl) });
        fake.Overrides["/api/OcrChiSoDien"] = () => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent("{\"suggestion\":\"0008867\",\"source\":\"Gemini\"}") });
        string blankMeter = Path.Combine(Path.GetTempPath(), $"qlpt-ocr-ui-{Guid.NewGuid():N}.png");
        try
        {
            var visual = new DrawingVisual();
            using (var drawing = visual.RenderOpen())
                drawing.DrawRectangle(Brushes.White, null, new Rect(0, 0, 300, 90));
            var bitmap = new RenderTargetBitmap(300, 90, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var file = File.Create(blankMeter)) encoder.Save(file);
            var ocrUi = new ChiSoDialog();
            typeof(ChiSoDialog).GetField("_imagePath", BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(ocrUi, blankMeter);
            typeof(ChiSoDialog).GetField("_numberRegion", BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(ocrUi, new Int32Rect(35, 15, 35, 50));
            typeof(ChiSoDialog).GetField("_digitCount", BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(ocrUi, 7);
            typeof(ChiSoDialog).GetMethod("ReadOcr_Click", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(ocrUi, new object?[] { null, new RoutedEventArgs() });
            try
            {
                PumpUntil(() => fake.Requests.Contains("POST /api/OcrChiSoDien") &&
                    ((Button)ocrUi.FindName("BtnReadOcr")).IsEnabled);
            }
            catch (TimeoutException ex)
            {
                throw new InvalidOperationException($"OCR UI status: {((TextBlock)ocrUi.FindName("TxtOcrStatus")).Text}; " +
                    $"button enabled: {((Button)ocrUi.FindName("BtnReadOcr")).IsEnabled}; " +
                    $"requests: {string.Join(",", fake.Requests.TakeLast(4))}", ex);
            }
            Check(((TextBlock)ocrUi.FindName("TxtOcrStatus")).Text.Contains("0008867") &&
                ((Button)ocrUi.FindName("BtnUseSuggestion")).IsEnabled,
                "meter dialog falls back to API when local OCR is uncertain and preserves leading zeros");
        }
        finally { File.Delete(blankMeter); }
        fake.Overrides.Remove("/api/OcrChiSoDien");
        var shell = new MainWindow();
        var shellVm = (MainViewModel)shell.DataContext;
        Check(shell.Content != null && shellVm.CurrentPage is FrameworkElement, "MainWindow and initial dashboard load");
        var dashboard = (QLPT.WPF.Views.Pages.DashboardPage)shellVm.CurrentPage;
        typeof(QLPT.WPF.Views.Pages.DashboardPage)
            .GetMethod("PhongChart_HoveredPointsChanged", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(dashboard, new object?[] { null, null, Array.Empty<LiveChartsCore.Kernel.ChartPoint>() });
        Check(((DashboardViewModel)dashboard.DataContext).PhongDangRe.Contains("Rê chuột"),
            "donut hover tolerates a null point list when pointer leaves the chart");
        foreach (string page in new[] { "Phòng trọ", "Khách thuê", "Đặt phòng", "Hợp đồng", "Chỉ số điện nước",
            "Hóa đơn", "Vi phạm", "Sửa chữa", "Báo cáo", "Sao lưu" })
        {
            shellVm.NavigateToCommand.Execute(page);
            Check(shellVm.CurrentPage is FrameworkElement && shellVm.ErrorMessage.Length == 0, page + " page constructs");
        }
        shell.Close();
        Session.VaiTro = "Manager";
        var managerShell = new MainWindow();
        var managerVm = (MainViewModel)managerShell.DataContext;
        Check(managerVm.CurrentPageTitle == "Phòng trọ", "manager starts on assigned-room workflow");
        foreach (string page in new[] { "Khách thuê", "Đặt phòng", "Hợp đồng", "Cư trú", "Chỉ số điện nước", "Sửa chữa", "Thông báo" })
        {
            managerVm.NavigateToCommand.Execute(page);
            Check(managerVm.CurrentPageTitle == page && managerVm.ErrorMessage.Length == 0,
                "manager opens assigned-site " + page + " page");
        }
        managerVm.NavigateToCommand.Execute("Tài sản");
        Check(managerVm.CurrentPageTitle == "Tài sản" && managerVm.ErrorMessage.Length == 0,
            "manager opens physical-asset page");
        ApiClient.GetRawAsync("/api/DatPhong").GetAwaiter().GetResult();
        Check(fake.Requests.Contains("GET /api/DatPhong"), "manager can request assigned-site bookings");
        managerVm.NavigateToCommand.Execute("Hóa đơn");
        Check(managerVm.CurrentPageTitle == "Tài sản" && managerVm.ErrorMessage.Length > 0,
            "manager cannot open owner finance page");
        managerShell.Close();
        Session.VaiTro = "BackupOperator";
        var backupShell = new MainWindow();
        var backupVm = (MainViewModel)backupShell.DataContext;
        Check(backupVm.CurrentPageTitle == "Sao lưu", "backup operator starts on backup page");
        backupVm.NavigateToCommand.Execute("Phòng trọ");
        Check(backupVm.CurrentPageTitle == "Sao lưu" && backupVm.ErrorMessage.Length > 0,
            "backup operator cannot open room management");
        backupShell.Close();
        Console.WriteLine("PASS: " + _count + " WPF behavior checks; no visible windows or real API writes.");
    }
    static int _count;
    static void Check(bool ok, string name)
    {
        if (!ok) throw new InvalidOperationException("FAIL: " + name);
        _count++;
        Console.WriteLine("PASS: " + name);
    }
    static void PumpUntil(Func<bool> done)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (!done())
        {
            if (DateTime.UtcNow >= deadline) throw new TimeoutException("Meter dialog OCR did not finish.");
            var frame = new DispatcherFrame();
            var timer = new DispatcherTimer(TimeSpan.FromMilliseconds(40), DispatcherPriority.Background,
                (_, _) => frame.Continue = false, Dispatcher.CurrentDispatcher);
            Dispatcher.PushFrame(frame);
            timer.Stop();
        }
    }
}

class FixtureHandler : HttpMessageHandler
{
    public bool EmptyTenants;
    public bool FailTenants;
    public bool MissingBackupConfig = true;
    public bool BackupPosted;
    public string LastBody = "";
    public List<string> Requests { get; } = new();
    public Dictionary<string, Func<Task<HttpResponseMessage>>> Overrides { get; } = new();
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        var path = request.RequestUri!.AbsolutePath;
        Requests.Add(request.Method + " " + path);
        if (Overrides.TryGetValue(path, out var custom)) return custom();
        if (request.Content != null) LastBody = request.Content.ReadAsStringAsync(token).GetAwaiter().GetResult();
        if (path == "/api/SaoLuu" && MissingBackupConfig)
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = new StringContent("{\"message\":\"missing config fixture\"}") });
        if (path == "/api/SaoLuu/tao")
        {
            BackupPosted = true;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { success = true, message = "SQL Server đã kiểm tra checksum.", duongDan = "C:/SQLBackups/fixture.bak" })) });
        }
        if (path == "/api/KhachThue" && FailTenants)
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("{\"message\":\"fixture error\"}") });
        object data = path switch
        {
            "/api/KhachThue" => EmptyTenants ? Array.Empty<object>() : Enumerable.Range(1, 100).Select(i => new
            { maKhach = i, hoTen = i == 100 ? "Đặng Văn Khôi" : "Nguyễn Văn " + i, cccd = i.ToString("D12"), soDienThoai = "090" + i.ToString("D7") }).ToArray(),
            "/api/Phong" => new[] { Room(1,"Trống"), Room(2,"Bảo trì"), Room(3,"Đang thuê"), Room(4,"Trống"), Room(5,"Đã đặt cọc"), Room(6,"Trống") },
            "/api/CoSo" => new[] { new { maCoSo = 1, tenCoSo = "Cơ sở A" } },
            "/api/LoaiPhong" => new[] { new { maLoaiPhong = 1, tenLoaiPhong = "Phòng đôi" } },
            "/api/DichVu" => new[] { new { maDichVu = 1, tenDichVu = "Internet", trangThai = "Đang áp dụng" } },
            "/api/HopDong" => new[] { new { maHopDong = 1, maPhong = 4, tenPhong = "Phòng 4", maKhachDaiDien = 1, hoTenKhach = "Nguyễn Văn 1", trangThai = "Đang hiệu lực" }, new { maHopDong = 2, maPhong = 3, tenPhong = "Phòng 3", maKhachDaiDien = 2, hoTenKhach = "Nguyễn Văn 2", trangThai = "Đã thanh lý" } },
            "/api/DatPhong" => new[] { new { maPhong = 5, maKhach = 100, trangThai = "Đã đặt cọc" }, new { maPhong = 6, maKhach = 99, trangThai = "Đã đặt cọc" } },
            "/api/TaiKhoan" => new[] { Account(1,1,"Customer","Active"), Account(2,100,"Customer","Active"), Account(3,2,"Customer","Locked"), Account(4,3,"Admin","Active") },
            "/api/SaoLuu" => new[] { new { id = 1, tenFile = "fixture.bak", ngayTao = "2026-10-13T00:00:00", kichThuoc = "1024 byte" } },
            _ => Array.Empty<object>()
        };
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(data), Encoding.UTF8, "application/json") });
    }
    static object Room(int id, string status) => new { maPhong = id, tenPhong = "Phòng " + id, maCoSo = 1, tenCoSo = "Cơ sở A", giaThue = 2000000, trangThai = status };
    static object Account(int id, int tenant, string role, string status) => new { maTaiKhoan = id, maKhach = tenant, tenDangNhap = "khach" + id, vaiTro = role, trangThai = status };
}
