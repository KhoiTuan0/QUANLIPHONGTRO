using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using QLPT.API.Controllers;
using QLPT.API.Models;
using QLPT.API.Services;

public static class ApiAccessChecks
{
    public static async Task<int> RunAsync()
    {
        int checks = 0;
        var filter = new ApiAccessFilter(null!);
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());
        ClaimsPrincipal Principal(string role, string customer = "7", string account = "12") => new(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, role), new Claim("MaKhach", customer), new Claim(ClaimTypes.NameIdentifier, account)], "TestCookie"));
        var customer = Principal("Customer");
        var admin = Principal("Admin");
        var backupOperator = Principal("BackupOperator");

        async Task Verify(string controller, string action, string method, ClaimsPrincipal user, int expectedStatus = 0,
            Dictionary<string, object?>? arguments = null)
        {
            var http = new DefaultHttpContext { User = user };
            http.Request.Method = method;
            var descriptor = new ControllerActionDescriptor { ControllerName = controller, ActionName = action };
            var actionContext = new ActionContext(http, new RouteData(), descriptor, new ModelStateDictionary());
            var metadata = new List<IFilterMetadata>();
            var target = new object();
            var executing = new ActionExecutingContext(actionContext, metadata, arguments ?? [], target);
            bool ran = false;
            await filter.OnActionExecutionAsync(executing, () =>
            {
                ran = true;
                return Task.FromResult(new ActionExecutedContext(actionContext, metadata, target));
            });
            if (expectedStatus == 0)
            {
                if (!ran || executing.Result != null)
                    throw new InvalidOperationException($"Access to {method} {controller}.{action} should have been permitted.");
            }
            else
            {
                if (ran || executing.Result is not ObjectResult denied || denied.StatusCode != expectedStatus)
                    throw new InvalidOperationException($"Access to {method} {controller}.{action} must stop with HTTP {expectedStatus}.");
                if (string.IsNullOrWhiteSpace(denied.Value?.GetType().GetProperty("message")?.GetValue(denied.Value)?.ToString()))
                    throw new InvalidOperationException("Access rejection must include a readable error message.");
            }
            checks++;
        }

        foreach (string action in new[] { "Login", "Logout" })
            await Verify("Auth", action, "POST", anonymous);
        await Verify("Auth", "Register", "POST", anonymous, 401);
        await Verify("Phong", "GetPhongs", "GET", anonymous, 401);
        await Verify("KhachThue", "GetAll", "GET", anonymous, 401);
        await Verify("Anh", "Get", "GET", anonymous, 401);
        await Verify("OcrChiSoDien", "Read", "POST", anonymous, 401);
        await Verify("Unknown", "FutureAction", "POST", anonymous, 401);
        await Verify("SaoLuu", "GetAll", "GET", backupOperator);
        await Verify("SaoLuu", "Tao", "POST", backupOperator);
        await Verify("SaoLuu", "KhoiPhucBanSao", "POST", backupOperator);
        await Verify("SaoLuu", "KhoiPhucBanSao", "GET", backupOperator, 403);
        await Verify("HopDong", "GetAll", "GET", backupOperator, 403);
        await Verify("OcrChiSoDien", "Read", "POST", backupOperator, 403);
        await Verify("OcrChiSoDien", "Read", "POST", customer, 403);
        await Verify("OcrChiSoDien", "Read", "POST", admin);

        foreach (var (controller, action, method) in new[]
        {
            ("Phong", "PostPhong", "POST"), ("Phong", "PutPhong", "PUT"),
            ("KhachThue", "GetAll", "GET"), ("KhachThue", "Update", "PUT"),
            ("HopDong", "Create", "POST"), ("HopDong", "GiaHan", "POST"), ("HopDong", "ThanhLyHopDong", "POST"),
            ("DatPhong", "Create", "POST"), ("TaiSan", "GanVaoPhong", "POST"),
            ("TaiSan", "GanCaTheVaoPhong", "POST"), ("TaiSan", "GoCaTheVeKho", "POST"),
            ("BanGiao", "GetByContract", "GET"), ("BanGiao", "Create", "POST"),
            ("Anh", "Upload", "POST"), ("HoaDon", "GetAll", "GET"),
            ("DanhMucMoRong", "Dashboard", "GET"), ("DanhMucMoRong", "XuLyKhaiBao", "POST"),
            ("SaoLuu", "Tao", "POST"), ("SaoLuu", "KhoiPhucBanSao", "POST"),
            ("SuaChua", "Update", "PUT"), ("Unknown", "FutureAction", "GET")
        })
            await Verify(controller, action, method, customer, 403);

        foreach (var (controller, action, parameter) in new[]
        {
            ("KhachThue", "GetById", "id"), ("KhachThue", "GetHistory", "id"),
            ("HopDong", "GetByCustomer", "maKhach"), ("HoaDon", "GetByCustomer", "maKhach"),
            ("YeuCauSuaChua", "GetAll", "maKhach")
        })
        {
            await Verify(controller, action, "GET", customer, arguments: new() { [parameter] = 7 });
            await Verify(controller, action, "GET", customer, 403, new() { [parameter] = 8 });
            await Verify(controller, action, "GET", customer, 403);
        }
        foreach (var (controller, action) in new[]
        {
            ("Phong", "GetPhongs"), ("Phong", "GetPhong"), ("CoSo", "GetAll"), ("LoaiPhong", "GetAll"), ("DichVu", "GetAll")
        })
            await Verify(controller, action, "GET", customer);
        foreach (string controller in new[] { "Phong", "CoSo", "LoaiPhong", "DichVu", "Anh" })
            await Verify(controller, "FutureSensitiveAction", "GET", customer, 403);
        await Verify("Anh", "Get", "GET", customer, arguments: new() { ["id"] = Guid.NewGuid() });
        await Verify("YeuCauSuaChua", "Create", "POST", customer, arguments: new() { ["model"] = new YeuCauSuaChua { MaKhach = 7, MaPhong = 5 } });
        await Verify("YeuCauSuaChua", "Create", "POST", customer, 403, new() { ["model"] = new YeuCauSuaChua { MaKhach = 8, MaPhong = 5 } });
        await Verify("BaoTraPhong", "Create", "POST", customer, arguments: new() { ["model"] = new BaoTraPhong { MaKhach = 7, MaHopDong = 5 } });
        await Verify("BaoTraPhong", "Create", "POST", customer, 403, new() { ["model"] = new BaoTraPhong { MaKhach = 8, MaHopDong = 5 } });
        await Verify("BaoTraPhong", "GetAll", "GET", customer, 403);
        await Verify("YeuCauSuaChua", "GetAll", "GET", customer, 403);

        await Verify("ThongBao", "GetByAccount", "GET", customer, arguments: new() { ["maTaiKhoan"] = 12 });
        await Verify("ThongBao", "GetByAccount", "GET", customer, 403, new() { ["maTaiKhoan"] = 13 });
        await Verify("ThongBao", "MarkRead", "PUT", customer, arguments: new() { ["maTaiKhoan"] = 12 });
        await Verify("ThongBao", "MarkRead", "PUT", customer, 403, new() { ["maTaiKhoan"] = 13 });
        var reply = new PhanHoiDto { MaTaiKhoan = 0, NoiDung = "Phản hồi của khách thuê" };
        await Verify("ThongBao", "PhanHoi", "POST", customer, arguments: new() { ["dto"] = reply });
        if (reply.MaTaiKhoan != 12) throw new InvalidOperationException("A reply without an account ID must bind to the signed-in account.");
        checks++;
        await Verify("ThongBao", "PhanHoi", "POST", customer, 403, new() { ["dto"] = new PhanHoiDto { MaTaiKhoan = 13 } });

        foreach (var invalidPrincipal in new[]
        {
            Principal("Customer", customer: ""), Principal("Customer", customer: "abc"), Principal("Customer", customer: "0"),
            Principal("Customer", account: ""), Principal("Customer", account: "abc"), Principal("Customer", account: "-1"),
            Principal("Unknown")
        })
            await Verify("Phong", "GetAll", "GET", invalidPrincipal, 403);

        foreach (var (controller, action, method) in new[]
        {
            ("Phong", "PostPhong", "POST"), ("KhachThue", "GetAll", "GET"),
            ("HopDong", "ThanhLyHopDong", "POST"), ("SaoLuu", "Tao", "POST"),
            ("SaoLuu", "KhoiPhucBanSao", "POST"),
            ("BanGiao", "Create", "POST"), ("Anh", "Upload", "POST"),
            ("TaiSan", "GanCaTheVaoPhong", "POST"), ("TaiSan", "GoCaTheVeKho", "POST"),
            ("DanhMucMoRong", "XuLyKhaiBao", "POST"),
            ("Unknown", "FutureAction", "GET")
        })
            await Verify(controller, action, method, admin);

        Console.WriteLine($"PASS {checks} API access checks. Database ownership lookups are intentionally not simulated.");
        return checks;
    }
}
