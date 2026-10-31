using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using QLPT.API.Controllers;
using QLPT.API.Models;

namespace QLPT.API.Services;

// Authorization is applied to every controller action, including future actions.
// Customer access is an explicit list; unlisted actions require an administrator.
public sealed class ApiAccessFilter(Klcn052QuanLyPhongTroContext database) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var action = (ControllerActionDescriptor)context.ActionDescriptor;
        if (action.ControllerName == "Auth" && action.ActionName is "Login" or "Logout")
        { await next(); return; }
        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated != true)
        { context.Result = new UnauthorizedObjectResult(new { message = "Vui lòng đăng nhập để sử dụng API." }); return; }
        if (user.IsInRole("Admin")) { await next(); return; }
        if (user.IsInRole("BackupOperator"))
        {
            if (action.ControllerName == "SaoLuu" &&
                (action.ActionName == "GetAll" && HttpMethods.IsGet(context.HttpContext.Request.Method) ||
                 (action.ActionName is "Tao" or "KhoiPhucBanSao") && HttpMethods.IsPost(context.HttpContext.Request.Method)))
            { await next(); return; }
            Deny(context); return;
        }
        if (user.IsInRole("Manager"))
        {
            if (!int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out int managerId))
            { Deny(context); return; }
            var sites = await database.QuanLyCoSos.AsNoTracking().Where(x => x.MaTaiKhoan == managerId)
                .Select(x => x.MaCoSo).ToListAsync();
            if (sites.Count == 0) { Deny(context); return; }
            int RouteId(string name) => context.ActionArguments.TryGetValue(name, out var value) && value is int id ? id : 0;
            Task<bool> OwnRoom(int id) => database.Phongs.AsNoTracking().AnyAsync(x => x.MaPhong == id && x.MaCoSo.HasValue && sites.Contains(x.MaCoSo.Value));
            Task<bool> OwnAssignment(int id) => database.TaiSanPhongs.AsNoTracking().AnyAsync(x => x.MaTaiSanPhong == id && x.MaPhongNavigation.MaCoSo.HasValue && sites.Contains(x.MaPhongNavigation.MaCoSo.Value));
            Task<bool> OwnUnit(int id) => database.TaiSanCaThes.AsNoTracking().AnyAsync(x => x.MaTaiSanCaThe == id && x.MaPhongNavigation != null && x.MaPhongNavigation.MaCoSo.HasValue && sites.Contains(x.MaPhongNavigation.MaCoSo.Value));
            Task<bool> OwnRepair(int id) => database.YeuCauSuaChuas.AsNoTracking().AnyAsync(x => x.MaYeuCau == id && x.MaPhongNavigation.MaCoSo.HasValue && sites.Contains(x.MaPhongNavigation.MaCoSo.Value));
            Task<bool> ManagerOwnContract(int id) => database.HopDongs.AsNoTracking().AnyAsync(x => x.MaHopDong == id && x.MaPhongNavigation.MaCoSo.HasValue && sites.Contains(x.MaPhongNavigation.MaCoSo.Value));
            Task<bool> OwnBooking(int id) => database.DatPhongs.AsNoTracking().AnyAsync(x => x.MaDatPhong == id &&
                x.MaPhongNavigation.MaCoSo.HasValue && sites.Contains(x.MaPhongNavigation.MaCoSo.Value));
            async Task<bool> OwnTenant(int id) => await database.HopDongs.AsNoTracking().AnyAsync(h => h.MaKhachDaiDien == id &&
                h.MaPhongNavigation.MaCoSo.HasValue && sites.Contains(h.MaPhongNavigation.MaCoSo.Value)) ||
                await database.CuTrus.AsNoTracking().AnyAsync(c => c.MaKhach == id && c.MaHopDongNavigation.MaPhongNavigation.MaCoSo.HasValue &&
                    sites.Contains(c.MaHopDongNavigation.MaPhongNavigation.MaCoSo.Value)) ||
                await database.DatPhongs.AsNoTracking().AnyAsync(d => d.MaKhach == id && d.MaPhongNavigation.MaCoSo.HasValue &&
                    sites.Contains(d.MaPhongNavigation.MaCoSo.Value)) ||
                await database.KhachThues.AsNoTracking().AnyAsync(k => k.MaKhach == id && k.MaCoSoTiepNhan.HasValue && sites.Contains(k.MaCoSoTiepNhan.Value));
            async Task<bool> OwnTenantCurrent(int id) => await database.KhachThues.AsNoTracking().AnyAsync(k => k.MaKhach == id &&
                    k.MaCoSoTiepNhan.HasValue && sites.Contains(k.MaCoSoTiepNhan.Value)) ||
                await database.DatPhongs.AsNoTracking().AnyAsync(d => d.MaKhach == id && d.TrangThai == NghiepVu.DatPhong.DaDatCoc &&
                    d.MaPhongNavigation.MaCoSo.HasValue && sites.Contains(d.MaPhongNavigation.MaCoSo.Value)) ||
                await database.HopDongs.AsNoTracking().AnyAsync(h => h.MaKhachDaiDien == id && h.TrangThai == NghiepVu.HopDong.HieuLuc &&
                    h.MaPhongNavigation.MaCoSo.HasValue && sites.Contains(h.MaPhongNavigation.MaCoSo.Value));
            Task<bool> OwnMeter(int id) => database.ChiSoDienNuocs.AsNoTracking().AnyAsync(x => x.MaChiSo == id &&
                x.MaPhongNavigation.MaCoSo.HasValue && sites.Contains(x.MaPhongNavigation.MaCoSo.Value));
            Task<bool> OwnResidence(int id) => database.CuTrus.AsNoTracking().AnyAsync(x => x.MaCuTru == id &&
                x.MaHopDongNavigation.MaPhongNavigation.MaCoSo.HasValue && sites.Contains(x.MaHopDongNavigation.MaPhongNavigation.MaCoSo.Value));
            bool managerAllowed = false;
            switch (action.ControllerName)
            {
                case "YeuCauHuyDatPhong":
                    // Controller scopes every read/write to assigned sites, including request IDs.
                    managerAllowed = action.ActionName is "GetAll" or "Create" or "XuLy";
                    break;
                case "OcrChiSoDien": managerAllowed = action.ActionName == "Read"; break;
                case "CoSo": managerAllowed = action.ActionName == "GetAll"; break;
                case "LoaiPhong": managerAllowed = action.ActionName == "GetAll"; break;
                case "HopDong":
                    if (action.ActionName is "GetAll" or "SapHetHan") managerAllowed = true;
                    else if (action.ActionName == "Create")
                        managerAllowed = context.ActionArguments.Values.OfType<HopDong>().FirstOrDefault() is { } contract &&
                            await OwnRoom(contract.MaPhong) && await OwnTenantCurrent(contract.MaKhachDaiDien) &&
                            await OwnTenantCurrent(contract.MaKhachCuTru ?? contract.MaKhachDaiDien);
                    else if (action.ActionName is "GetById" or "GiaHan" or "ThanhLyHopDong" or "Update")
                        managerAllowed = await ManagerOwnContract(RouteId("id"));
                    break;
                case "KhachThue":
                    if (action.ActionName == "GetAll") managerAllowed = true;
                    else if (action.ActionName is "GetById" or "GetEditHistory") managerAllowed = await OwnTenant(RouteId("id"));
                    else if (action.ActionName == "Update") managerAllowed = await OwnTenantCurrent(RouteId("id"));
                    else if (action.ActionName == "Create")
                        managerAllowed = context.ActionArguments.Values.OfType<KhachThue>().FirstOrDefault() is { MaCoSoTiepNhan: int site } && sites.Contains(site);
                    else if (action.ActionName == "TiepNhan")
                        managerAllowed = context.ActionArguments.Values.OfType<TiepNhanKhachDto>().FirstOrDefault() is { MaCoSo: int site } && sites.Contains(site);
                    break;
                case "DatPhong":
                    if (action.ActionName == "GetAll") managerAllowed = true;
                    else if (action.ActionName == "Create")
                        managerAllowed = context.ActionArguments.Values.OfType<DatPhong>().FirstOrDefault() is { } booking &&
                            await OwnRoom(booking.MaPhong) && await OwnTenantCurrent(booking.MaKhach);
                    else if (action.ActionName == "Huy") managerAllowed = await OwnBooking(RouteId("id"));
                    break;
                case "HoaDon":
                    if (action.ActionName == "GetByHopDong") managerAllowed = await ManagerOwnContract(RouteId("maHopDong"));
                    else if (action.ActionName == "GetById")
                    {
                        int invoiceId = RouteId("id");
                        managerAllowed = await database.HoaDons.AsNoTracking().AnyAsync(h =>
                            h.MaHoaDon == invoiceId && h.MaHopDongNavigation.MaPhongNavigation.MaCoSo.HasValue &&
                            sites.Contains(h.MaHopDongNavigation.MaPhongNavigation.MaCoSo.Value));
                    }
                    break;
                case "ChiSoDienNuoc":
                    if (action.ActionName == "GetAll") managerAllowed = true;
                    else if (action.ActionName == "GetPrevious") managerAllowed = await OwnRoom(RouteId("maPhong"));
                    else if (action.ActionName == "Create")
                        managerAllowed = context.ActionArguments.Values.OfType<ChiSoDienNuoc>().FirstOrDefault() is { } reading && await OwnRoom(reading.MaPhong);
                    else if (action.ActionName is "Update" or "Delete") managerAllowed = await OwnMeter(RouteId("id"));
                    break;
                case "DanhMucMoRong":
                    if (action.ActionName == "GetCuTru") managerAllowed = true;
                    else if (action.ActionName is "UpdateCuTru" or "XuLyKhaiBao") managerAllowed = await OwnResidence(RouteId("id"));
                    break;
                case "BanGiao":
                    if (action.ActionName == "GetByContract") managerAllowed = await ManagerOwnContract(RouteId("maHopDong"));
                    break;
                case "ThongBao":
                    managerAllowed = (action.ActionName is "GetByAccount" or "MarkRead") &&
                        RouteId("maTaiKhoan") == managerId;
                    break;
                case "Phong":
                    if (action.ActionName == "GetPhongs") managerAllowed = true;
                    else if (action.ActionName == "PostPhong")
                        managerAllowed = context.ActionArguments.Values.OfType<Phong>().Any(x => x.MaCoSo.HasValue && sites.Contains(x.MaCoSo.Value));
                    else if (action.ActionName == "PutPhong")
                    {
                        int roomId = RouteId("id");
                        var update = context.ActionArguments.Values.OfType<PhongUpdateDto>().FirstOrDefault();
                        managerAllowed = await OwnRoom(roomId) && update != null &&
                            (!update.MaCoSoProvided || await database.Phongs.AsNoTracking().AnyAsync(x => x.MaPhong == roomId && x.MaCoSo == update.MaCoSo));
                    }
                    else if (action.ActionName is "GetPhong" or "DeletePhong") managerAllowed = await OwnRoom(RouteId("id"));
                    break;
                case "TaiSan":
                    if (action.ActionName is "GetAll" or "GetCaTheAll") managerAllowed = true;
                    else if (action.ActionName is "GetByPhong" or "GetCaTheTheoPhong") managerAllowed = await OwnRoom(RouteId("maPhong"));
                    else if (action.ActionName == "UpdateTaiSanPhong")
                    {
                        int assignmentId = RouteId("id");
                        var update = context.ActionArguments.Values.OfType<TaiSanPhongUpdateDto>().FirstOrDefault();
                        managerAllowed = update != null && await OwnAssignment(assignmentId) &&
                            (!update.SoLuong.HasValue || await database.TaiSanPhongs.AsNoTracking()
                                .AnyAsync(x => x.MaTaiSanPhong == assignmentId && x.SoLuong == update.SoLuong));
                    }
                    else if (action.ActionName == "GetLichSu" || action.ActionName == "UpdateTinhTrangCaThe")
                        managerAllowed = await OwnUnit(RouteId("id"));
                    else if (action.ActionName == "AddLichSu")
                        managerAllowed = context.ActionArguments.Values.OfType<LichSuTaiSanDto>().FirstOrDefault() is { } history &&
                            history.LoaiSuKien != "Thanh lý" && await OwnUnit(RouteId("id"));
                    break;
                case "SuaChua":
                    if (action.ActionName == "GetAll") managerAllowed = true;
                    else if (action.ActionName == "Create")
                        managerAllowed = context.ActionArguments.Values.OfType<TaoSuaChuaDto>().FirstOrDefault() is { MaHopDong: int contractId } && await ManagerOwnContract(contractId);
                    else if (action.ActionName is "Update" or "Delete") managerAllowed = await OwnRepair(RouteId("id"));
                    break;
            }
            if (!managerAllowed) { Deny(context); return; }
            await next(); return;
        }
        if (!user.IsInRole("Customer") || !int.TryParse(user.FindFirstValue("MaKhach"), out int customerId) || customerId <= 0 ||
            !int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out int accountId) || accountId <= 0)
        { Deny(context); return; }

        int Id(string name) => context.ActionArguments.TryGetValue(name, out var value) && value is int id ? id : 0;
        Task<bool> OwnContract(int id) => database.HopDongs.AsNoTracking().AnyAsync(h => h.MaHopDong == id && h.MaKhachDaiDien == customerId);
        bool read = HttpMethods.IsGet(context.HttpContext.Request.Method);
        bool allowed = false;
        switch (action.ControllerName)
        {
            case "YeuCauHuyDatPhong":
                // Controller derives ownership from session MaKhach; customer cannot approve.
                allowed = action.ActionName is "GetAll" or "Create";
                break;
            case "DatPhong":
                if (read && action.ActionName == "GetAll") allowed = true;
                else if (action.ActionName == "Create" &&
                    context.ActionArguments.Values.OfType<DatPhong>().FirstOrDefault() is { } reservation)
                    allowed = reservation.MaKhach == customerId && reservation.TienCoc is null or 0;
                break;
            case "Phong": allowed = read && action.ActionName is "GetPhongs" or "GetPhong"; break;
            case "CoSo" or "LoaiPhong" or "DichVu": allowed = read && action.ActionName == "GetAll"; break;
            case "Anh": allowed = read && action.ActionName == "Get"; break;
            case "KhachThue":
                allowed = read && action.ActionName is "GetById" or "GetHistory" && Id("id") == customerId;
                break;
            case "HopDong":
                if (read && action.ActionName == "GetByCustomer") allowed = Id("maKhach") == customerId;
                else if (read && action.ActionName == "GetById") allowed = await OwnContract(Id("id"));
                break;
            case "HoaDon":
                if (read && action.ActionName == "GetByCustomer") allowed = Id("maKhach") == customerId;
                else if (read && action.ActionName == "GetByHopDong") allowed = await OwnContract(Id("maHopDong"));
                else if (read && action.ActionName == "GetById")
                { int id = Id("id"); allowed = await database.HoaDons.AsNoTracking().AnyAsync(h => h.MaHoaDon == id && h.MaHopDongNavigation.MaKhachDaiDien == customerId); }
                break;
            case "YeuCauSuaChua":
                if (read && action.ActionName == "GetAll") allowed = Id("maKhach") == customerId;
                else if (action.ActionName == "Create") allowed = context.ActionArguments.Values.OfType<YeuCauSuaChua>().FirstOrDefault()?.MaKhach == customerId;
                else if (action.ActionName == "ConfirmRepair")
                { int id = Id("id"); allowed = await database.YeuCauSuaChuas.AsNoTracking().AnyAsync(r => r.MaYeuCau == id && r.MaKhach == customerId); }
                break;
            case "BaoTraPhong":
                allowed = action.ActionName == "Create" && context.ActionArguments.Values.OfType<BaoTraPhong>().FirstOrDefault()?.MaKhach == customerId;
                break;
            case "DanhMucMoRong":
                if (read && action.ActionName == "GetCuTruByCustomer") allowed = Id("maKhach") == customerId;
                else if (action.ActionName == "DeNghiKhaiBao")
                {
                    int residenceId = Id("id");
                    allowed = await database.CuTrus.AsNoTracking().AnyAsync(c => c.MaCuTru == residenceId && c.MaKhach == customerId);
                }
                break;
            case "ThongBao":
                if (action.ActionName is "GetByAccount" or "MarkRead") allowed = Id("maTaiKhoan") == accountId;
                else if (action.ActionName == "PhanHoi" && context.ActionArguments.Values.OfType<PhanHoiDto>().FirstOrDefault() is { } reply)
                { allowed = reply.MaTaiKhoan == accountId || reply.MaTaiKhoan == 0; if (allowed) reply.MaTaiKhoan = accountId; }
                break;
        }
        if (!allowed) { Deny(context); return; }
        await next();
    }

    static void Deny(ActionExecutingContext context) => context.Result = new ObjectResult(new { message = "Tài khoản không có quyền truy cập dữ liệu hoặc thao tác này." }) { StatusCode = 403 };
}
