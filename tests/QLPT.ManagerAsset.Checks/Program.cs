using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using QLPT.API.Controllers;
using QLPT.API.Models;
using QLPT.API.Services;

// Chỉ chạy trên bản sao được đặt tên tường minh; giao dịch được rollback khi xong.
const string clone = "QLPT_DemoPrep_20261019";
var connection = $"Server=.\\SQLEXPRESS;Database={clone};Trusted_Connection=True;TrustServerCertificate=True;";
await using var db = new Klcn052QuanLyPhongTroContext(
    new DbContextOptionsBuilder<Klcn052QuanLyPhongTroContext>().UseSqlServer(connection).Options);
if (db.Database.GetDbConnection().Database != clone) throw new Exception("Sai database kiểm thử.");
int groups = await db.TaiSanPhongs.CountAsync();
int quantity = await db.TaiSanPhongs.SumAsync(x => x.SoLuong ?? 0);
int units = await db.TaiSanCaThes.CountAsync();
Check(groups > 0 && units == quantity && await db.TaiSanCaThes.Select(x => x.MaDinhDanh).Distinct().CountAsync() == units,
    "mỗi hiện vật có mã riêng và số lượng khớp bản ghi cũ");
var sites = await db.CoSos.OrderBy(x => x.MaCoSo).Select(x => x.MaCoSo).Take(2).ToListAsync();
if (sites.Count < 2) throw new Exception("Bản sao cần hai cơ sở để thử cách ly quyền.");
int managerId = await db.TaiKhoans.Select(x => x.MaTaiKhoan).FirstAsync();
await using var transaction = await db.Database.BeginTransactionAsync();
db.QuanLyCoSos.Add(new QuanLyCoSo { MaTaiKhoan = managerId, MaCoSo = sites[0] });
await db.SaveChangesAsync();
var manager = new ClaimsPrincipal(new ClaimsIdentity([
    new Claim(ClaimTypes.Role, "Manager"), new Claim(ClaimTypes.NameIdentifier, managerId.ToString())], "TestCookie"));
var filter = new ApiAccessFilter(db);

async Task<bool> Allowed(string controller, string action, string verb,
    Dictionary<string, object?>? arguments = null, ClaimsPrincipal? asUser = null)
{
    var http = new DefaultHttpContext { User = asUser ?? manager };
    http.Request.Method = verb;
    var descriptor = new ControllerActionDescriptor { ControllerName = controller, ActionName = action };
    var context = new ActionContext(http, new RouteData(), descriptor, new ModelStateDictionary());
    var executing = new ActionExecutingContext(context, [], arguments ?? [], new object());
    bool invoked = false;
    await filter.OnActionExecutionAsync(executing, () =>
    {
        invoked = true;
        return Task.FromResult(new ActionExecutedContext(context, [], new object()));
    });
    return invoked && executing.Result == null;
}

Check(await Allowed("Phong", "GetPhongs", "GET"), "quản lý được xem phòng");
Check(await Allowed("OcrChiSoDien", "Read", "POST"), "quản lý được đọc ảnh chỉ số điện qua API");
Check(!await Allowed("SaoLuu", "GetAll", "GET"), "quản lý không được xem sao lưu");
Check(!await Allowed("TaiKhoan", "GetAll", "GET"), "quản lý không được xem tài khoản");
Check(await Allowed("ThongBao", "GetByAccount", "GET", new() { ["maTaiKhoan"] = managerId }) &&
    await Allowed("ThongBao", "MarkRead", "PUT", new() { ["maTaiKhoan"] = managerId }) &&
    !await Allowed("ThongBao", "GetByAccount", "GET", new() { ["maTaiKhoan"] = managerId + 1 }) &&
    !await Allowed("ThongBao", "Create", "POST"),
    "quản lý chỉ đọc và đánh dấu thông báo của chính mình");
Check(await Allowed("KhachThue", "GetAll", "GET") && await Allowed("ChiSoDienNuoc", "GetAll", "GET") &&
    await Allowed("DanhMucMoRong", "GetCuTru", "GET") && await Allowed("HopDong", "SapHetHan", "GET"),
    "quản lý xem khách, chỉ số, cư trú và hợp đồng sắp hết hạn thuộc cơ sở");
Check(!await Allowed("HoaDon", "GetAll", "GET") && !await Allowed("ThuChi", "GetAll", "GET"),
    "quản lý không mở toàn bộ dữ liệu tài chính");
var backupOperator = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, "BackupOperator")], "TestCookie"));
Check(await Allowed("SaoLuu", "GetAll", "GET", asUser: backupOperator) &&
    await Allowed("SaoLuu", "Tao", "POST", asUser: backupOperator),
    "nhân sự sao lưu chỉ được xem và tạo bản sao lưu");
Check(!await Allowed("SaoLuu", "Delete", "DELETE", asUser: backupOperator) &&
    !await Allowed("Phong", "GetPhongs", "GET", asUser: backupOperator) &&
    !await Allowed("TaiKhoan", "GetAll", "GET", asUser: backupOperator),
    "nhân sự sao lưu không được xóa, xem phòng hay xem tài khoản");
int ownRoom = await db.Phongs.Where(x => x.MaCoSo == sites[0]).Select(x => x.MaPhong).FirstAsync();
int otherRoom = await db.Phongs.Where(x => x.MaCoSo == sites[1]).Select(x => x.MaPhong).FirstAsync();
int ownCustomer = await db.HopDongs.Where(x => x.MaPhongNavigation.MaCoSo == sites[0] &&
    x.TrangThai == NghiepVu.HopDong.HieuLuc).Select(x => x.MaKhachDaiDien).FirstAsync();
Check(await Allowed("KhachThue", "Create", "POST", new() { ["model"] = new KhachThue { MaCoSoTiepNhan = sites[0] } }) &&
    !await Allowed("KhachThue", "Create", "POST", new() { ["model"] = new KhachThue { MaCoSoTiepNhan = sites[1] } }) &&
    await Allowed("KhachThue", "TiepNhan", "POST", new() { ["dto"] = new TiepNhanKhachDto { MaCoSo = sites[0] } }) &&
    !await Allowed("KhachThue", "TiepNhan", "POST", new() { ["dto"] = new TiepNhanKhachDto { MaCoSo = sites[1] } }),
    "quản lý chỉ tạo hoặc tiếp nhận khách tại cơ sở được giao");
Check(await Allowed("HopDong", "Create", "POST", new() { ["model"] = new HopDong { MaPhong = ownRoom,
        MaKhachDaiDien = ownCustomer } }) &&
    !await Allowed("HopDong", "Create", "POST", new() { ["model"] = new HopDong { MaPhong = otherRoom,
        MaKhachDaiDien = ownCustomer } }) &&
    await Allowed("DatPhong", "Create", "POST", new() { ["model"] = new DatPhong { MaPhong = ownRoom,
        MaKhach = ownCustomer } }) &&
    !await Allowed("DatPhong", "Create", "POST", new() { ["model"] = new DatPhong { MaPhong = otherRoom,
        MaKhach = ownCustomer } }),
    "quản lý được tạo hợp đồng và đặt phòng tại cơ sở của mình, bị chặn ở cơ sở khác");
Check(await Allowed("ChiSoDienNuoc", "GetPrevious", "GET", new() { ["maPhong"] = ownRoom }) &&
    !await Allowed("ChiSoDienNuoc", "GetPrevious", "GET", new() { ["maPhong"] = otherRoom }) &&
    await Allowed("ChiSoDienNuoc", "Create", "POST", new() { ["model"] = new ChiSoDienNuoc { MaPhong = ownRoom } }) &&
    !await Allowed("ChiSoDienNuoc", "Create", "POST", new() { ["model"] = new ChiSoDienNuoc { MaPhong = otherRoom } }),
    "ghi chỉ số và đọc kỳ trước chỉ trong cơ sở được giao");
Check(await Allowed("Phong", "GetPhong", "GET", new() { ["id"] = ownRoom }), "quản lý xem phòng được giao");
Check(!await Allowed("Phong", "GetPhong", "GET", new() { ["id"] = otherRoom }), "quản lý không xem phòng cơ sở khác");
Check(!await Allowed("Phong", "PutPhong", "PUT", new() { ["id"] = ownRoom,
    ["model"] = new PhongUpdateDto { MaCoSo = sites[1] } }), "quản lý không chuyển phòng sang cơ sở khác");
Check(!await Allowed("TaiSan", "GetCaTheTheoPhong", "GET", new() { ["maPhong"] = otherRoom }),
    "quản lý không xem hiện vật phòng cơ sở khác");
Check(!await Allowed("TaiSan", "NhapKho", "POST") &&
    !await Allowed("TaiSan", "XuatKho", "POST") &&
    !await Allowed("TaiSan", "GetTonKho", "GET"),
    "quản lý không thay chủ trọ nhập, xuất hoặc xem tổng kho chung");
int ownAssignment = await db.TaiSanPhongs.Where(x => x.MaPhongNavigation.MaCoSo == sites[0])
    .Select(x => x.MaTaiSanPhong).FirstAsync();
int ownQuantity = (await db.TaiSanPhongs.FindAsync(ownAssignment))?.SoLuong ?? 0;
Check(!await Allowed("TaiSan", "Create", "POST") &&
    !await Allowed("TaiSan", "GanVaoPhong", "POST", new() { ["model"] = new TaiSanPhong { MaPhong = ownRoom } }) &&
    !await Allowed("TaiSan", "GoKhoiPhong", "DELETE", new() { ["id"] = ownAssignment }) &&
    !await Allowed("TaiSan", "UpdateTaiSanPhong", "PUT", new() { ["id"] = ownAssignment,
        ["dto"] = new TaiSanPhongUpdateDto { SoLuong = ownQuantity + 1 } }) &&
    await Allowed("TaiSan", "UpdateTaiSanPhong", "PUT", new() { ["id"] = ownAssignment,
        ["dto"] = new TaiSanPhongUpdateDto { SoLuong = ownQuantity } }),
    "quản lý không tạo mẫu hay thay số lượng hiện vật để vòng qua kho chung");
int ownUnit = await db.TaiSanCaThes.Where(x => x.MaPhong == ownRoom).Select(x => x.MaTaiSanCaThe).FirstAsync();
Check(await Allowed("TaiSan", "AddLichSu", "POST", new() { ["id"] = ownUnit,
    ["dto"] = new LichSuTaiSanDto { LoaiSuKien = "Sửa chữa" } }),
    "quản lý ghi sửa chữa hiện vật tại cơ sở được giao");
int otherUnit = await db.TaiSanCaThes.Where(x => x.MaPhongNavigation!.MaCoSo == sites[1])
    .Select(x => x.MaTaiSanCaThe).FirstAsync();
Check(!await Allowed("TaiSan", "AddLichSu", "POST", new() { ["id"] = otherUnit }),
    "quản lý không ghi lịch sử hiện vật cơ sở khác");
Check(!await Allowed("TaiSan", "AddLichSu", "POST", new() { ["id"] = ownUnit,
    ["dto"] = new LichSuTaiSanDto { LoaiSuKien = "Thanh lý" } }),
    "quản lý không thanh lý hiện vật để vòng qua quyền chủ trọ");

ControllerContext ControllerContext() => new() { HttpContext = new DefaultHttpContext { User = manager } };
var phong = new PhongController(db) { ControllerContext = ControllerContext() };
var roomResult = (OkObjectResult)await phong.GetPhongs(null);
var roomJson = JsonSerializer.SerializeToElement(roomResult.Value);
Check(roomJson.EnumerateArray().All(x => x.GetProperty("MaCoSo").GetInt32() == sites[0]), "danh sách phòng lọc theo cơ sở");
int roomWithHistory = await db.Phongs.Where(x => x.MaCoSo == sites[0] &&
    (db.HopDongs.Any(h => h.MaPhong == x.MaPhong) || db.DatPhongs.Any(b => b.MaPhong == x.MaPhong)))
    .Select(x => x.MaPhong).FirstAsync();
int originalType = await db.Phongs.Where(x => x.MaPhong == roomWithHistory).Select(x => x.MaLoaiPhong).SingleAsync();
int alternateType = await db.LoaiPhongs.Where(x => x.MaLoaiPhong != originalType).Select(x => x.MaLoaiPhong).FirstAsync();
Check(await phong.PutPhong(roomWithHistory, new PhongUpdateDto { MaLoaiPhong = alternateType }) is BadRequestObjectResult,
    "phòng có lịch sử không thể đổi loại phòng");
var coSo = new CoSoController(db) { ControllerContext = ControllerContext() };
var siteResult = (OkObjectResult)await coSo.GetAll();
var siteJson = JsonSerializer.SerializeToElement(siteResult.Value);
Check(siteJson.GetArrayLength() == 1 && siteJson[0].GetProperty("MaCoSo").GetInt32() == sites[0], "danh sách cơ sở lọc theo phân công");
var tenants = new KhachThueController(db) { ControllerContext = ControllerContext() };
var tenantList = JsonSerializer.SerializeToElement(((OkObjectResult)await tenants.GetAll()).Value);
var expectedTenants = await db.KhachThues.Where(k => k.MaCoSoTiepNhan == sites[0] ||
    db.DatPhongs.Any(d => d.MaKhach == k.MaKhach && d.MaPhongNavigation.MaCoSo == sites[0]) ||
    db.HopDongs.Any(h => h.MaKhachDaiDien == k.MaKhach &&
    h.MaPhongNavigation.MaCoSo == sites[0]) || db.CuTrus.Any(c => c.MaKhach == k.MaKhach &&
    c.MaHopDongNavigation.MaPhongNavigation.MaCoSo == sites[0])).Select(k => k.MaKhach).ToListAsync();
Check(tenantList.EnumerateArray().Select(x => x.GetProperty("MaKhach").GetInt32()).Order().SequenceEqual(expectedTenants.Order()),
    "danh sách khách quản lý không lộ khách cơ sở khác");
int ownTenant = expectedTenants.First();
int otherTenant = await db.HopDongs.Where(h => h.MaPhongNavigation.MaCoSo == sites[1] &&
    !expectedTenants.Contains(h.MaKhachDaiDien)).Select(h => h.MaKhachDaiDien).FirstAsync();
Check(await Allowed("KhachThue", "GetById", "GET", new() { ["id"] = ownTenant }) &&
    !await Allowed("KhachThue", "GetById", "GET", new() { ["id"] = otherTenant }),
    "quản lý không xem chi tiết khách chỉ thuộc cơ sở khác");
var meter = new ChiSoDienNuocController(db) { ControllerContext = ControllerContext() };
var meterList = JsonSerializer.SerializeToElement(((OkObjectResult)await meter.GetAll(null, null, null)).Value);
var siteRoomIds = await db.Phongs.Where(x => x.MaCoSo == sites[0]).Select(x => x.MaPhong).ToListAsync();
Check(meterList.EnumerateArray().All(x => siteRoomIds.Contains(x.GetProperty("MaPhong").GetInt32())),
    "danh sách chỉ số điện nước chỉ thuộc cơ sở được giao");
var residence = new DanhMucMoRongController(db) { ControllerContext = ControllerContext() };
var residenceList = JsonSerializer.SerializeToElement(((OkObjectResult)await residence.GetCuTru(null)).Value);
var allowedContracts = await db.HopDongs.Where(h => h.MaPhongNavigation.MaCoSo == sites[0]).Select(h => h.MaHopDong).ToListAsync();
int otherContractId = await db.HopDongs.Where(h => h.MaPhongNavigation.MaCoSo == sites[1])
    .Select(h => h.MaHopDong).FirstAsync();
Check(await Allowed("BanGiao", "GetByContract", "GET", new() { ["maHopDong"] = allowedContracts[0] }) &&
    !await Allowed("BanGiao", "GetByContract", "GET", new() { ["maHopDong"] = otherContractId }),
    "quản lý chỉ được xuất biên bản hợp đồng thuộc cơ sở được giao");
Check(residenceList.EnumerateArray().All(x => allowedContracts.Contains(x.GetProperty("MaHopDong").GetInt32())),
    "danh sách cư trú chỉ thuộc hợp đồng cơ sở được giao");
var resident = await db.CuTrus.AsNoTracking().Select(c => new { c.MaCuTru, c.MaKhach }).FirstAsync();
var mobileCustomer = new ClaimsPrincipal(new ClaimsIdentity([
    new Claim(ClaimTypes.Role, "Customer"), new Claim(ClaimTypes.NameIdentifier, managerId.ToString()),
    new Claim("MaKhach", resident.MaKhach.ToString())], "TestCookie"));
Check(await Allowed("DanhMucMoRong", "GetCuTruByCustomer", "GET",
        new() { ["maKhach"] = resident.MaKhach }, mobileCustomer) &&
    !await Allowed("DanhMucMoRong", "GetCuTruByCustomer", "GET",
        new() { ["maKhach"] = resident.MaKhach + 100000 }, mobileCustomer) &&
    !await Allowed("DanhMucMoRong", "GetCuTru", "GET", asUser: mobileCustomer) &&
    await Allowed("DanhMucMoRong", "DeNghiKhaiBao", "POST",
        new() { ["id"] = resident.MaCuTru }, mobileCustomer),
    "khách chỉ xem và gửi đề nghị cho lượt cư trú của chính mình");
var ownResidence = JsonSerializer.SerializeToElement(((OkObjectResult)await residence.GetCuTruByCustomer(resident.MaKhach)).Value);
var ownResidenceIds = await db.CuTrus.Where(c => c.MaKhach == resident.MaKhach).Select(c => c.MaCuTru).ToListAsync();
Check(ownResidence.EnumerateArray().All(x => ownResidenceIds.Contains(x.GetProperty("MaCuTru").GetInt32())),
    "API mobile chỉ trả về lượt cư trú của khách đăng nhập");
var activeResidence = await db.CuTrus.Where(c => c.MaHopDongNavigation.TrangThai == NghiepVu.HopDong.HieuLuc &&
        c.TrangThaiCuTru != NghiepVu.CuTru.DaRoiDi && c.TrangThaiKhaiBao != "Chờ duyệt")
    .Select(c => new { c.MaCuTru, c.MaKhach }).FirstAsync();
var declaringCustomer = new ClaimsPrincipal(new ClaimsIdentity([
    new Claim(ClaimTypes.Role, "Customer"), new Claim("MaKhach", activeResidence.MaKhach.ToString()),
    new Claim(ClaimTypes.NameIdentifier, managerId.ToString())], "TestCookie"));
var customerDeclaration = new DanhMucMoRongController(db)
    { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = declaringCustomer } } };
var declaration = new GuiKhaiBaoCuTruDto { DiaChiThuongTru = "12 Nguyễn Trãi, Quận 1, TP. Hồ Chí Minh",
    ThongTinBoSung = "Tôi đang ở đúng phòng ghi trong hợp đồng." };
Check(await customerDeclaration.DeNghiKhaiBao(activeResidence.MaCuTru, declaration) is OkObjectResult &&
    await db.KhaiBaoCuTrus.AnyAsync(k => k.MaCuTru == activeResidence.MaCuTru && k.TrangThai == "Chờ duyệt"),
    "khách gửi nội dung cư trú và lưu một hồ sơ chờ duyệt");
Check(await customerDeclaration.DeNghiKhaiBao(activeResidence.MaCuTru, declaration) is BadRequestObjectResult,
    "không gửi trùng hồ sơ cư trú đang chờ duyệt");
Check(await residence.UpdateCuTru(activeResidence.MaCuTru, new CuTru { TrangThaiKhaiBao = "Đã khai báo" }) is BadRequestObjectResult,
    "không duyệt tắt bằng thay đổi trạng thái cư trú");
Check(await residence.XuLyKhaiBao(activeResidence.MaCuTru,
    new XuLyKhaiBaoCuTruDto { ChapNhan = false, LyDoTraLai = "Bổ sung địa chỉ thường trú đầy đủ." }) is OkObjectResult &&
    await db.KhaiBaoCuTrus.AnyAsync(k => k.MaCuTru == activeResidence.MaCuTru && k.TrangThai == "Cần bổ sung" &&
        k.LyDoTraLai != null) &&
    await db.CuTrus.AnyAsync(c => c.MaCuTru == activeResidence.MaCuTru && c.TrangThaiKhaiBao == "Chưa khai báo"),
    "quản lý trả hồ sơ với lý do, khách có thể gửi lại");
Check(await customerDeclaration.DeNghiKhaiBao(activeResidence.MaCuTru, declaration) is OkObjectResult &&
    await residence.XuLyKhaiBao(activeResidence.MaCuTru, new XuLyKhaiBaoCuTruDto { ChapNhan = true }) is OkObjectResult &&
    await db.KhaiBaoCuTrus.CountAsync(k => k.MaCuTru == activeResidence.MaCuTru) == 2 &&
    await db.CuTrus.AnyAsync(c => c.MaCuTru == activeResidence.MaCuTru && c.TrangThaiKhaiBao == "Đã khai báo"),
    "gửi lại và xác nhận giữ được lịch sử hai lần nộp");
Check(await customerDeclaration.DeNghiKhaiBao(activeResidence.MaCuTru, declaration) is OkObjectResult,
    "khách còn ở được gửi thông tin cập nhật sau lần duyệt trước");
var residenceAtCheckout = await db.CuTrus.FindAsync(activeResidence.MaCuTru);
string? oldResidenceStatus = residenceAtCheckout!.TrangThaiCuTru;
residenceAtCheckout.TrangThaiCuTru = NghiepVu.CuTru.DaRoiDi;
await db.SaveChangesAsync();
Check(await residence.XuLyKhaiBao(activeResidence.MaCuTru,
    new XuLyKhaiBaoCuTruDto { ChapNhan = true }) is BadRequestObjectResult &&
    await residence.XuLyKhaiBao(activeResidence.MaCuTru,
    new XuLyKhaiBaoCuTruDto { ChapNhan = false, LyDoTraLai = "Hợp đồng đã kết thúc." }) is OkObjectResult,
    "khách đã rời không được duyệt muộn nhưng có thể trả hồ sơ");
residenceAtCheckout.TrangThaiCuTru = oldResidenceStatus;
await db.SaveChangesAsync();
var hopDong = new HopDongController(db) { ControllerContext = ControllerContext() };
var contractResult = (OkObjectResult)await hopDong.GetAll();
var contractJson = JsonSerializer.SerializeToElement(contractResult.Value);
var allowedRooms = await db.Phongs.Where(x => x.MaCoSo == sites[0]).Select(x => x.MaPhong).ToListAsync();
Check(contractJson.EnumerateArray().All(x => allowedRooms.Contains(x.GetProperty("maPhong").GetInt32())),
    "danh sách hợp đồng lọc theo cơ sở");
var taiSan = new TaiSanController(db) { ControllerContext = ControllerContext() };
var assetResult = (OkObjectResult)await taiSan.GetCaTheTheoPhong(ownRoom);
var assetJson = JsonSerializer.SerializeToElement(assetResult.Value);
Check(assetJson.EnumerateArray().Any(x => x.GetProperty("MaTaiSanCaThe").GetInt32() == ownUnit),
    "API đọc từng hiện vật và số lần sửa");
var logResult = await taiSan.AddLichSu(ownUnit, new LichSuTaiSanDto
{ LoaiSuKien = "Sửa chữa", NgaySuKien = DateTime.Today, ChiPhi = 125000, NoiDung = "Thay bản lề kiểm thử" });
Check(logResult is OkObjectResult && await db.LichSuTaiSans.CountAsync(x => x.MaTaiSanCaThe == ownUnit && x.LoaiSuKien == "Sửa chữa") == 1,
    "ghi sửa chữa gắn đúng mã hiện vật");
Check(await taiSan.AddLichSu(ownUnit, new LichSuTaiSanDto
    { LoaiSuKien = "Sửa chữa", NgaySuKien = DateTime.Today, ChiPhi = 1.234m, NoiDung = "Chi phí lẻ sai" }) is BadRequestObjectResult,
    "chi phí sửa chữa không bị làm tròn âm thầm khi vượt hai số lẻ");
var inspected = await taiSan.UpdateTinhTrangCaThe(ownUnit, new TinhTrangCaTheDto
{ TinhTrang = "Đã bảo dưỡng", NgayMua = DateTime.Today.AddYears(-2), GiaMua = 500000 });
Check(inspected is ObjectResult { StatusCode: 403 }, "quản lý không sửa được ngày và giá mua");
Check(await taiSan.UpdateTinhTrangCaThe(ownUnit, new TinhTrangCaTheDto
    { TinhTrang = "Đã bảo dưỡng" }) is OkObjectResult &&
    await db.TaiSanCaThes.AnyAsync(x => x.MaTaiSanCaThe == ownUnit && x.TinhTrang == "Đã bảo dưỡng"),
    "quản lý vẫn kiểm kê được tình trạng hiện vật");
var ownerForPurchase = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, "Admin")], "TestCookie"));
var ownerAssets = new TaiSanController(db)
    { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = ownerForPurchase } } };
Check(await ownerAssets.UpdateTinhTrangCaThe(ownUnit, new TinhTrangCaTheDto
    { TinhTrang = "Đã bảo dưỡng", GiaMua = 1.234m }) is BadRequestObjectResult,
    "giá mua không bị làm tròn âm thầm khi vượt hai số lẻ");
Check(await ownerAssets.UpdateTinhTrangCaThe(ownUnit, new TinhTrangCaTheDto
    { TinhTrang = "Đã bảo dưỡng", NgayMua = DateTime.Today.AddYears(-2), GiaMua = 500000 }) is OkObjectResult &&
    await db.TaiSanCaThes.AnyAsync(x => x.MaTaiSanCaThe == ownUnit && x.GiaMua == 500000) &&
    await db.LichSuTaiSans.AnyAsync(x => x.MaTaiSanCaThe == ownUnit && x.NoiDung.Contains("Giá mua:")),
    "chủ trọ sửa giá mua và lịch sử giữ dấu thay đổi");
var repairForAsset = new YeuCauSuaChua { MaPhong = ownRoom, MaKhach = ownTenant,
    NoiDung = "Sửa bản lề giường trong phòng", NgayBao = DateTime.Today,
    TrangThai = NghiepVu.SuaChua.ChoTiepNhan, ChiPhi = 0 };
db.YeuCauSuaChuas.Add(repairForAsset);
await db.SaveChangesAsync();
var repairs = new SuaChuaController(db) { ControllerContext = ControllerContext() };
Check(await repairs.Update(repairForAsset.MaYeuCau, new SuaChuaUpdateDto
    { TrangThai = NghiepVu.SuaChua.DaHoanThanh, ChiPhi = 0, NgayHoanThanh = DateTime.Today,
      MaTaiSanCaThe = otherUnit }) is BadRequestObjectResult,
    "không gắn sửa chữa vào hiện vật của cơ sở/phòng khác");
Check(await repairs.Update(repairForAsset.MaYeuCau, new SuaChuaUpdateDto
    { TrangThai = NghiepVu.SuaChua.DaHoanThanh, ChiPhi = 0, NgayHoanThanh = DateTime.Today,
      MaTaiSanCaThe = ownUnit }) is OkObjectResult &&
    await db.LichSuTaiSans.CountAsync(x => x.MaYeuCau == repairForAsset.MaYeuCau && x.MaTaiSanCaThe == ownUnit) == 1,
    "hoàn thành sửa chữa ghi đúng một sự kiện vào mã hiện vật");
Check(await repairs.Update(repairForAsset.MaYeuCau, new SuaChuaUpdateDto
    { TrangThai = NghiepVu.SuaChua.DaHoanThanh, ChiPhi = 55000 }) is OkObjectResult &&
    await db.LichSuTaiSans.CountAsync(x => x.MaYeuCau == repairForAsset.MaYeuCau && x.ChiPhi == 55000) == 1,
    "bổ sung chi phí cập nhật lịch sử hiện vật mà không nhân đôi sự kiện");
Check(await repairs.Update(repairForAsset.MaYeuCau, new SuaChuaUpdateDto
    { TrangThai = NghiepVu.SuaChua.DaHoanThanh, ChiPhi = 55000 }) is OkObjectResult &&
    await db.PhieuThuChis.CountAsync(x => x.LoaiPhieu == NghiepVu.PhieuChi &&
        x.NoiDung != null && x.NoiDung.Contains($"(yêu cầu sửa chữa số {repairForAsset.MaYeuCau})")) == 1,
    "lưu lại yêu cầu hoàn thành không hạch toán chi phí hai lần");
var repairList = JsonSerializer.SerializeToElement(((OkObjectResult)await repairs.GetAll()).Value);
Check(repairList.EnumerateArray().Any(x => x.GetProperty("MaYeuCau").GetInt32() == repairForAsset.MaYeuCau &&
    x.GetProperty("MaDinhDanhHienVat").GetString() is { Length: > 0 }),
    "danh sách sửa chữa hiển thị được mã hiện vật đã liên kết");
var pair = await (from room in db.Phongs.Where(x => x.MaCoSo == sites[0])
                  from category in db.TaiSans
                  where (room.TrangThai == NghiepVu.Phong.Trong || room.TrangThai == NghiepVu.Phong.BaoTri) &&
                        !db.HopDongs.Any(h => h.MaPhong == room.MaPhong && h.TrangThai == NghiepVu.HopDong.HieuLuc) &&
                        !db.TaiSanPhongs.Any(x => x.MaPhong == room.MaPhong && x.MaTaiSan == category.MaTaiSan)
                  select new { room.MaPhong, category.MaTaiSan }).FirstAsync();
var assigned = await taiSan.GanVaoPhong(new TaiSanPhong { MaPhong = pair.MaPhong, MaTaiSan = pair.MaTaiSan,
    SoLuong = 2, TinhTrang = "Mới" });
Check(assigned is OkObjectResult && await db.TaiSanCaThes.CountAsync(x => x.MaPhong == pair.MaPhong && x.MaTaiSan == pair.MaTaiSan) == 2,
    "gán hai chiếc cùng mẫu tạo hai mã hiện vật khác nhau");
int newGroupId = await db.TaiSanPhongs.Where(x => x.MaPhong == pair.MaPhong && x.MaTaiSan == pair.MaTaiSan)
    .Select(x => x.MaTaiSanPhong).SingleAsync();
Check(await db.LichSuTaiSans.CountAsync(x => x.MaTaiSanCaTheNavigation.MaTaiSanPhong == newGroupId &&
    x.LoaiSuKien == "Ghi nhận") == 2,
    "mỗi hiện vật mới có mốc ghi nhận riêng trong lịch sử");
Check(await taiSan.Delete(pair.MaTaiSan) is BadRequestObjectResult,
    "không xóa mẫu tài sản đã gán vào phòng");
int removedUnitId = await db.TaiSanCaThes.Where(x => x.MaTaiSanPhong == newGroupId)
    .Select(x => x.MaTaiSanCaThe).FirstAsync();
Check(await taiSan.UpdateTaiSanPhong(newGroupId, new TaiSanPhongUpdateDto { SoLuong = 1 }) is BadRequestObjectResult,
    "giảm số lượng cần chọn đúng mã hiện vật");
Check(await taiSan.UpdateTaiSanPhong(newGroupId, new TaiSanPhongUpdateDto
    { SoLuong = 1, MaTaiSanCaTheGoiKho = [removedUnitId] }) is OkObjectResult &&
    await db.TaiSanCaThes.AnyAsync(x => x.MaTaiSanCaThe == removedUnitId && x.TrangThai == "Trong kho" && x.MaPhong == null) &&
    await db.TaiSanPhongs.AnyAsync(x => x.MaTaiSanPhong == newGroupId && x.SoLuong == 1),
    "gỡ đúng hiện vật đã chọn và giữ mã trong kho");
var managerInventory = (OkObjectResult)await taiSan.GetCaTheAll();
var managerUnits = JsonSerializer.SerializeToElement(managerInventory.Value);
Check(managerUnits.EnumerateArray().All(x => x.GetProperty("MaPhong").ValueKind == JsonValueKind.Number &&
    allowedRooms.Contains(x.GetProperty("MaPhong").GetInt32())) &&
    !managerUnits.EnumerateArray().Any(x => x.GetProperty("MaTaiSanCaThe").GetInt32() == removedUnitId),
    "quản lý chỉ thấy hiện vật của cơ sở được giao, không thấy kho chung");
var admin = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, "Admin")], "TestCookie"));
var adminInventory = new TaiSanController(db)
    { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = admin } } };
var allInventory = (OkObjectResult)await adminInventory.GetCaTheAll();
var allUnits = JsonSerializer.SerializeToElement(allInventory.Value);
Check(allUnits.EnumerateArray().Any(x => x.GetProperty("MaTaiSanCaThe").GetInt32() == removedUnitId &&
    x.GetProperty("TrangThai").GetString() == "Trong kho"),
    "chủ trọ còn thấy mã và lịch sử của hiện vật trong kho");
var vacantRoom = await db.Phongs.Where(p =>
    (p.TrangThai == NghiepVu.Phong.Trong || p.TrangThai == NghiepVu.Phong.BaoTri) &&
    !db.HopDongs.Any(h => h.MaPhong == p.MaPhong && h.TrangThai == NghiepVu.HopDong.HieuLuc))
    .Select(p => p.MaPhong).FirstAsync();
var batchCategory = new TaiSan { TenTaiSan = "Mẫu kiểm thử nhập lô " + Guid.NewGuid().ToString("N")[..8],
    DonViTinh = "cái" };
db.TaiSans.Add(batchCategory);
await db.SaveChangesAsync();
Check(await adminInventory.NhapKho(new NhapKhoTaiSanDto { MaTaiSan = batchCategory.MaTaiSan,
    SoLuong = 50, NgayMua = DateTime.Today, GiaMua = 500000m, TinhTrang = "Mới" }) is OkObjectResult &&
    await db.TaiSanCaThes.CountAsync(x => x.MaTaiSan == batchCategory.MaTaiSan && x.TrangThai == "Trong kho") == 50 &&
    await db.TaiSanCaThes.Where(x => x.MaTaiSan == batchCategory.MaTaiSan)
        .Select(x => x.MaDinhDanh).Distinct().CountAsync() == 50,
    "nhập số lượng 50 một lần tạo 50 mã hiện vật riêng trong kho");
Check(await adminInventory.XuatKho(new XuatKhoTaiSanDto { MaTaiSan = batchCategory.MaTaiSan,
    MaPhong = vacantRoom, SoLuong = 20 }) is OkObjectResult &&
    await db.TaiSanCaThes.CountAsync(x => x.MaTaiSan == batchCategory.MaTaiSan && x.TrangThai == "Trong kho") == 30 &&
    await db.TaiSanCaThes.CountAsync(x => x.MaTaiSan == batchCategory.MaTaiSan && x.MaPhong == vacantRoom) == 20 &&
    await db.LichSuTaiSans.CountAsync(x => x.MaTaiSanCaTheNavigation.MaTaiSan == batchCategory.MaTaiSan &&
        x.LoaiSuKien == "Điều chuyển") == 20,
    "chuyển 20 chiếc vào phòng còn 30 chiếc trong kho và lưu lịch sử từng chiếc");
var stockSummary = JsonSerializer.SerializeToElement(((OkObjectResult)await adminInventory.GetTonKho()).Value);
Check(stockSummary.EnumerateArray().Any(x => x.GetProperty("MaTaiSan").GetInt32() == batchCategory.MaTaiSan &&
    x.GetProperty("TrangThai").GetString() == "Trong kho" && x.GetProperty("SoLuong").GetInt32() == 30) &&
    stockSummary.EnumerateArray().Any(x => x.GetProperty("MaTaiSan").GetInt32() == batchCategory.MaTaiSan &&
    x.GetProperty("TrangThai").GetString() == "Đang sử dụng" && x.GetProperty("SoLuong").GetInt32() == 20),
    "API tổng tồn trả đúng 30 trong kho và 20 trong phòng để WPF hiển thị");
Check(await adminInventory.XuatKho(new XuatKhoTaiSanDto { MaTaiSan = batchCategory.MaTaiSan,
    MaPhong = vacantRoom, SoLuong = 31 }) is BadRequestObjectResult &&
    await db.TaiSanCaThes.CountAsync(x => x.MaTaiSan == batchCategory.MaTaiSan && x.TrangThai == "Trong kho") == 30,
    "không xuất quá số tồn kho");
var originalCode = await db.TaiSanCaThes.Where(x => x.MaTaiSanCaThe == removedUnitId)
    .Select(x => x.MaDinhDanh).SingleAsync();
Check(await adminInventory.GanCaTheVaoPhong(removedUnitId, new GanCaTheVaoPhongDto { MaPhong = vacantRoom }) is OkObjectResult &&
    await db.TaiSanCaThes.AnyAsync(x => x.MaTaiSanCaThe == removedUnitId && x.MaDinhDanh == originalCode &&
        x.MaPhong == vacantRoom && x.TrangThai == "Đang sử dụng") &&
    await db.LichSuTaiSans.CountAsync(x => x.MaTaiSanCaThe == removedUnitId && x.LoaiSuKien == "Điều chuyển") == 1,
    "đưa hiện vật từ kho vào phòng giữ mã và thêm lịch sử điều chuyển");
Check(await adminInventory.GoCaTheVeKho(removedUnitId) is OkObjectResult &&
    await db.TaiSanCaThes.AnyAsync(x => x.MaTaiSanCaThe == removedUnitId && x.MaDinhDanh == originalCode &&
        x.MaPhong == null && x.TrangThai == "Trong kho") &&
    await db.LichSuTaiSans.CountAsync(x => x.MaTaiSanCaThe == removedUnitId && x.LoaiSuKien == "Điều chuyển") == 2,
    "gỡ đúng hiện vật về kho giữ mã và bổ sung lịch sử nguồn phòng");
var accounts = new TaiKhoanController(db) { ControllerContext = ControllerContext() };
string login = "test_manager_" + Guid.NewGuid().ToString("N")[..8];
var createdAccount = await accounts.Create(new TaoTaiKhoanDto
{ TenDangNhap = login, MatKhau = "ManagerTest@2026", VaiTro = "Manager", MaCoSos = [sites[0]] });
Check(createdAccount is OkObjectResult && await db.TaiKhoans.AnyAsync(x => x.TenDangNhap == login && x.VaiTro == "Manager"),
    "chủ trọ cấp tài khoản Manager trên cơ sở được chọn");
int createdId = await db.TaiKhoans.Where(x => x.TenDangNhap == login).Select(x => x.MaTaiKhoan).SingleAsync();
Check(await db.QuanLyCoSos.AnyAsync(x => x.MaTaiKhoan == createdId && x.MaCoSo == sites[0]),
    "phân công cơ sở được lưu cùng tài khoản");
var accountList = await accounts.GetAll();
Check(accountList is OkObjectResult, "danh sách tài khoản kèm cơ sở được giao tải được");
var updatedAccount = await accounts.Update(createdId, new SuaTaiKhoanDto { VaiTro = "Manager", MaCoSos = [sites[1]] });
Check(updatedAccount is OkObjectResult && await db.QuanLyCoSos.AnyAsync(x => x.MaTaiKhoan == createdId && x.MaCoSo == sites[1]) &&
    !await db.QuanLyCoSos.AnyAsync(x => x.MaTaiKhoan == createdId && x.MaCoSo == sites[0]),
    "đổi cơ sở phân công không giữ quyền cũ");
int unlinkedTenant = await db.KhachThues.Where(k => !db.TaiKhoans.Any(t => t.MaKhach == k.MaKhach))
    .Select(k => k.MaKhach).FirstAsync();
string customerLogin = "test_mobile_" + Guid.NewGuid().ToString("N")[..8];
var issuedCustomer = await accounts.Create(new TaoTaiKhoanDto
{ TenDangNhap = customerLogin, MatKhau = "MobileTest@2026", VaiTro = "Customer", MaKhach = unlinkedTenant });
Check(issuedCustomer is OkObjectResult && await db.TaiKhoans.AnyAsync(x => x.TenDangNhap == customerLogin &&
    x.VaiTro == "Customer" && x.MaKhach == unlinkedTenant && x.TrangThai == "Active"),
    "chủ trọ cấp Customer gắn hồ sơ khách cho ứng dụng mobile");
string backupLogin = "test_backup_" + Guid.NewGuid().ToString("N")[..8];
var issuedBackup = await accounts.Create(new TaoTaiKhoanDto
{ TenDangNhap = backupLogin, MatKhau = "BackupTest@2026", VaiTro = "BackupOperator" });
Check(issuedBackup is OkObjectResult && await db.TaiKhoans.AnyAsync(x => x.TenDangNhap == backupLogin &&
    x.VaiTro == "BackupOperator" && x.MaKhach == null),
    "chủ trọ cấp tài khoản sao lưu không gắn khách hay cơ sở");
NghiepVu.GhiPhieu(db, "Thu", "Thu tiền hóa đơn tháng 10/2026 - A101", 100000m, "Khách kiểm thử");
await db.SaveChangesAsync();
int autoReceipt = await db.PhieuThuChis.Where(x => x.GhiChu != null && x.GhiChu.StartsWith("[QLPT:auto]"))
    .OrderByDescending(x => x.MaPhieu).Select(x => x.MaPhieu).FirstAsync();
var ledger = new ThuChiController(db) { ControllerContext = ControllerContext() };
Check(await ledger.Update(autoReceipt, new PhieuThuChi { SoTien = 1m }) is BadRequestObjectResult &&
    await ledger.Delete(autoReceipt) is BadRequestObjectResult,
    "phiếu thu tự sinh từ hóa đơn không thể sửa hoặc xóa trong sổ");
Check(NghiepVu.LaPhieuTuDong(new PhieuThuChi { NoiDung = "Hoàn tiền đặt cọc A101 (thanh lý HĐ #1)" }),
    "phiếu tự sinh cũ được nhận diện theo diễn giải nghiệp vụ");
var manualReceipt = await ledger.Create(new PhieuThuChi
    { LoaiPhieu = "Chi", NoiDung = "Mua vật tư văn phòng", SoTien = 50000m });
int manualId = await db.PhieuThuChis.Where(x => x.NoiDung == "Mua vật tư văn phòng")
    .OrderByDescending(x => x.MaPhieu).Select(x => x.MaPhieu).FirstAsync();
Check(manualReceipt is OkObjectResult && await ledger.Update(manualId, new PhieuThuChi { SoTien = 55000m }) is OkObjectResult &&
    await ledger.Delete(manualId) is OkObjectResult,
    "phiếu nhập thủ công vẫn sửa và xóa được khi nhập nhầm");
int activeSite = await db.HopDongs.Where(h => h.TrangThai == NghiepVu.HopDong.HieuLuc &&
    h.MaPhongNavigation.MaCoSo.HasValue).Select(h => h.MaPhongNavigation.MaCoSo!.Value).FirstAsync();
var liveSite = await db.CoSos.FindAsync(activeSite) ?? throw new Exception("Thiếu cơ sở đang cho thuê");
var siteEditor = new CoSoController(db) { ControllerContext = ControllerContext() };
Check(await siteEditor.Update(activeSite, new CoSo { TenCoSo = liveSite.TenCoSo,
    DiaChi = liveSite.DiaChi + " kiểm thử", SoDienThoai = liveSite.SoDienThoai }) is BadRequestObjectResult,
    "không đổi địa chỉ cơ sở khi còn hợp đồng hiệu lực");
var linkedContract = await db.HopDongs.Where(h => h.TrangThai == NghiepVu.HopDong.HieuLuc).FirstAsync();
var violation = new ViPham { MaPhong = linkedContract.MaPhong, MaKhach = linkedContract.MaKhachDaiDien,
    NoiDung = "Thử ràng buộc", TrangThai = NghiepVu.ViPham.DaXuLy, NgayViPham = DateTime.Now };
db.ViPhams.Add(violation);
var repair = new YeuCauSuaChua { MaPhong = linkedContract.MaPhong, MaKhach = linkedContract.MaKhachDaiDien,
    NoiDung = "Thử ràng buộc", TrangThai = NghiepVu.SuaChua.DangSua, NgayBao = DateTime.Now };
db.YeuCauSuaChuas.Add(repair);
var notice = new ThongBao { TieuDe = "Thử ràng buộc", NoiDung = "Giữ lịch sử đã gửi", NgayTao = DateTime.Now };
notice.ThongBaoNguoiNhans.Add(new ThongBaoNguoiNhan { MaTaiKhoan = managerId });
db.ThongBaos.Add(notice);
await db.SaveChangesAsync();
var violationEditor = new ViPhamController(db);
Check(await violationEditor.Update(violation.MaViPham, new ViPham { NoiDung = "Đổi sau xử lý" }) is BadRequestObjectResult &&
    await violationEditor.Delete(violation.MaViPham) is BadRequestObjectResult,
    "vi phạm đã xử lý giữ nguyên nội dung và lịch sử");
var repairEditor = new SuaChuaController(db) { ControllerContext = ControllerContext() };
Check(await repairEditor.Delete(repair.MaYeuCau) is BadRequestObjectResult,
    "yêu cầu đang sửa không thể xóa");
var noticeEditor = new ThongBaoController(db) { ControllerContext = ControllerContext() };
Check(await noticeEditor.Delete(notice.MaThongBao) is BadRequestObjectResult,
    "thông báo đã gửi không thể xóa khỏi lịch sử người nhận");
var customerEditor = new KhachThueController(db) { ControllerContext = ControllerContext() };
var activeCustomer = await db.KhachThues.FindAsync(linkedContract.MaKhachDaiDien)
    ?? throw new Exception("Thiếu khách đang thuê để kiểm tra sửa hồ sơ.");
var anotherCustomer = await db.KhachThues.FirstAsync(k => k.MaKhach != activeCustomer.MaKhach && k.SoDienThoai != null);
string oldPhone = activeCustomer.SoDienThoai ?? "";
var duplicatePhone = await customerEditor.Update(activeCustomer.MaKhach,
    new KhachThueUpdateDto { SoDienThoai = anotherCustomer.SoDienThoai });
Check(duplicatePhone is BadRequestObjectResult &&
    JsonSerializer.SerializeToElement(((BadRequestObjectResult)duplicatePhone).Value).GetProperty("message").GetString()!.Contains("Số điện thoại") &&
    activeCustomer.SoDienThoai == oldPhone,
    "SĐT đã thuộc khách khác bị từ chối rõ lý do, không ghi đè hồ sơ");
string newPhone = Enumerable.Range(0, 1000).Select(i => $"099900{i:0000}")
    .First(phone => !db.KhachThues.Any(k => k.SoDienThoai == phone));
Check(await customerEditor.Update(activeCustomer.MaKhach,
        new KhachThueUpdateDto { SoDienThoai = newPhone }) is OkObjectResult &&
    await db.KhachThues.AnyAsync(k => k.MaKhach == activeCustomer.MaKhach && k.SoDienThoai == newPhone),
    "khách đang thuê đổi được SĐT hợp lệ");
int settledCustomerId = await db.HopDongs.Where(h => h.TrangThai == "Đã thanh lý" &&
    h.MaKhachDaiDien != activeCustomer.MaKhach).Select(h => h.MaKhachDaiDien).FirstAsync();
Check(await customerEditor.Update(settledCustomerId,
        new KhachThueUpdateDto { DiaChi = "12 Nguyễn Văn Trỗi, Phú Nhuận, TP.HCM" }) is OkObjectResult,
    "khách đã thanh lý vẫn sửa được địa chỉ liên hệ");
Check(await customerEditor.Update(activeCustomer.MaKhach,
        new KhachThueUpdateDto { Cccd = anotherCustomer.Cccd }) is BadRequestObjectResult,
    "CCCD thuộc khách khác bị từ chối trước khi ghi SQL");
string testEmail = "kiemthu.khach.thue.20261021@example.com";
Check(await customerEditor.Update(anotherCustomer.MaKhach,
        new KhachThueUpdateDto { Email = testEmail }) is OkObjectResult &&
    await customerEditor.Update(activeCustomer.MaKhach,
        new KhachThueUpdateDto { Email = testEmail }) is BadRequestObjectResult,
    "email thuộc khách khác bị từ chối rõ lý do");
string newCccd = Enumerable.Range(100000, 900000).Select(x => $"079026{x:000000}")
    .First(x => !db.KhachThues.Any(k => k.Cccd == x));
string intakePhone = Enumerable.Range(0, 1000).Select(i => $"098800{i:0000}")
    .First(x => !db.KhachThues.Any(k => k.SoDienThoai == x));
Check(await customerEditor.Create(new KhachThue { HoTen = "Nguyễn Thị Lan",
        Cccd = newCccd, SoDienThoai = intakePhone, DiaChi = "Thành phố Hồ Chí Minh",
        MaCoSoTiepNhan = sites[0] }) is OkObjectResult &&
    await db.KhachThues.AnyAsync(k => k.Cccd == newCccd && k.MaCoSoTiepNhan == sites[0]),
    "quản lý tạo khách mới tại đúng cơ sở và lưu một hồ sơ dùng chung");
// Chuyển phạm vi tài khoản thử sang cơ sở thứ hai để kiểm tra khách đang thuê ở cơ sở đầu.
db.QuanLyCoSos.RemoveRange(db.QuanLyCoSos.Where(x => x.MaTaiKhoan == managerId));
db.QuanLyCoSos.Add(new QuanLyCoSo { MaTaiKhoan = managerId, MaCoSo = sites[1] });
await db.SaveChangesAsync();
string transferCccd = (await db.KhachThues.FindAsync(ownCustomer))!.Cccd!;
Check(await customerEditor.TiepNhan(new TiepNhanKhachDto { Cccd = transferCccd,
        MaCoSo = sites[1] }) is OkObjectResult &&
    await db.KhachThues.AnyAsync(k => k.MaKhach == ownCustomer && k.MaCoSoTiepNhan == sites[1]),
    "quản lý cơ sở mới tiếp nhận đúng hồ sơ cũ bằng CCCD, không tạo trùng khách");
int nextRoom = await db.Phongs.Where(p => p.MaCoSo == sites[1] && p.TrangThai == NghiepVu.Phong.Trong &&
    !db.DatPhongs.Any(d => d.MaPhong == p.MaPhong && d.TrangThai == NghiepVu.DatPhong.DaDatCoc))
    .Select(p => p.MaPhong).FirstAsync();
var movingReservation = new DatPhong { MaPhong = nextRoom, MaKhach = ownCustomer,
    NgayDat = DateOnly.FromDateTime(DateTime.Today),
    NgayDuKienNhan = DateOnly.FromDateTime(DateTime.Today.AddDays(7)),
    NgayHenKyHopDong = DateOnly.FromDateTime(DateTime.Today.AddDays(7)), TienCoc = 0 };
Check(await Allowed("DatPhong", "Create", "POST", new() { ["model"] = movingReservation }) &&
    await new DatPhongController(db) { ControllerContext = ControllerContext() }.Create(movingReservation) is OkObjectResult &&
    await db.HopDongs.AnyAsync(h => h.MaKhachDaiDien == ownCustomer && h.TrangThai == NghiepVu.HopDong.HieuLuc) &&
    await db.DatPhongs.AnyAsync(d => d.MaKhach == ownCustomer && d.MaPhong == nextRoom &&
        d.TrangThai == NghiepVu.DatPhong.DaDatCoc),
    "khách đang thuê cơ sở cũ vẫn giữ chỗ được ở cơ sở mới, hợp đồng cũ còn nguyên");
await transaction.RollbackAsync();
Console.WriteLine("PASS: các kiểm tra quản lý cơ sở và mã hiện vật trên bản sao SQL.");

static void Check(bool pass, string message)
{
    if (!pass) throw new InvalidOperationException("FAIL: " + message);
    Console.WriteLine("PASS: " + message);
}
