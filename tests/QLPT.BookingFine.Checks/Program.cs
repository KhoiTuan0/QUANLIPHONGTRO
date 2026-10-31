using System.Security.Claims;
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

const string clone = "QLPT_DemoPrep_20261019";
await using var db = new Klcn052QuanLyPhongTroContext(new DbContextOptionsBuilder<Klcn052QuanLyPhongTroContext>()
    .UseSqlServer($"Server=.\\SQLEXPRESS;Database={clone};Trusted_Connection=True;TrustServerCertificate=True").Options);
if (db.Database.GetDbConnection().Database != clone) throw new Exception("Wrong test database");
await using var tx = await db.Database.BeginTransactionAsync();
int checks = 0;
void Check(bool ok, string text) { if (!ok) throw new Exception(text); checks++; Console.WriteLine($"PASS {text}"); }
ClaimsPrincipal User(string role, int account, int customer = 0) => new(new ClaimsIdentity([
    new Claim(ClaimTypes.Role, role), new Claim(ClaimTypes.NameIdentifier, account.ToString()), new Claim("MaKhach", customer.ToString())], "Test"));
T As<T>(T controller, ClaimsPrincipal user) where T : ControllerBase { controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = user } }; return controller; }
async Task<bool> Allowed(string controller, string action, ClaimsPrincipal user)
{
    var http = new DefaultHttpContext { User = user }; http.Request.Method = "POST";
    var ac = new ActionContext(http, new RouteData(), new ControllerActionDescriptor { ControllerName = controller, ActionName = action }, new ModelStateDictionary());
    var exec = new ActionExecutingContext(ac, [], new Dictionary<string, object?>(), new object()); bool called = false;
    await new ApiAccessFilter(db).OnActionExecutionAsync(exec, () => { called = true; return Task.FromResult(new ActionExecutedContext(ac, [], new object())); }); return called;
}
var sites = await db.CoSos.OrderBy(s => s.MaCoSo).Select(s => s.MaCoSo).Take(2).ToListAsync();
int account = await db.TaiKhoans.Select(a => a.MaTaiKhoan).FirstAsync();
// Account used only as a claim fixture; no credentials or role data modified.
var assignments = await db.QuanLyCoSos.Where(x => x.MaTaiKhoan == account).ToListAsync();
db.QuanLyCoSos.RemoveRange(assignments); await db.SaveChangesAsync();
db.QuanLyCoSos.Add(new QuanLyCoSo { MaTaiKhoan = account, MaCoSo = sites[0] });
var tenant = new KhachThue { HoTen = "Kiểm thử rollback", Cccd = "099999999981", SoDienThoai = "0909999981", DiaChi = "Dữ liệu kiểm thử" };
var otherTenant = new KhachThue { HoTen = "Kiểm thử khác", Cccd = "099999999982", SoDienThoai = "0909999982" };
db.KhachThues.AddRange(tenant, otherTenant);
var type = await db.LoaiPhongs.Select(x => x.MaLoaiPhong).FirstAsync();
var room = new Phong { TenPhong = "CHECK-HUY-29", MaLoaiPhong = type, MaCoSo = sites[0], GiaThue = 2000000, TrangThai = NghiepVu.Phong.DaDatCoc };
var outside = new Phong { TenPhong = "CHECK-NGOAI-29", MaLoaiPhong = type, MaCoSo = sites[1], GiaThue = 2000000, TrangThai = NghiepVu.Phong.DaDatCoc };
db.Phongs.AddRange(room, outside); await db.SaveChangesAsync();
DatPhong Booking(Phong r) => new() { MaPhong = r.MaPhong, MaKhach = tenant.MaKhach, NgayDat = new(2026,10,1), NgayDuKienNhan = new(2026,11,1), TienCoc = 1000000, TrangThai = NghiepVu.DatPhong.DaDatCoc };
var booking = Booking(room); var foreign = Booking(outside); db.DatPhongs.AddRange(booking, foreign); await db.SaveChangesAsync();
var owner = User("Admin", account); var manager = User("Manager", account); var customer = User("Customer", account, tenant.MaKhach);
var customerApi = As(new YeuCauHuyDatPhongController(db), customer);
var managerApi = As(new YeuCauHuyDatPhongController(db), manager);
Check(await customerApi.Create(new(booking.MaDatPhong, "Xin hủy giữ chỗ")) is OkObjectResult, "customer requests own booking");
int requestId = await db.YeuCauHuyDatPhongs.Where(r => r.MaDatPhong == booking.MaDatPhong).Select(r => r.MaYeuCau).SingleAsync();
Check(booking.TrangThai == NghiepVu.DatPhong.DaDatCoc, "request does not release room");
Check(await customerApi.Create(new(booking.MaDatPhong, "duplicate")) is ConflictObjectResult, "duplicate pending blocked");
Check(await As(new YeuCauHuyDatPhongController(db), User("Customer", account, otherTenant.MaKhach)).Create(new(booking.MaDatPhong, "wrong owner")) is ForbidResult, "customer cannot request another tenant booking");
Check(await managerApi.Create(new(foreign.MaDatPhong, "wrong site")) is ForbidResult, "manager cannot request outside site");
Check(!await Allowed("YeuCauHuyDatPhong", "XuLy", customer), "customer approval denied by access filter");
Check(!await Allowed("ViPhamHoaDon", "AddToInvoice", manager), "manager financial amendment denied");
Check(await managerApi.XuLy(requestId, new(true, 1000001, "Too much")) is BadRequestObjectResult, "refund above deposit blocked");
Check(await As(new DatPhongController(db), manager).Huy(booking.MaDatPhong, new()) is BadRequestObjectResult, "direct cancel cannot bypass pending request");
int payments = await db.PhieuThuChis.CountAsync();
Check(await managerApi.XuLy(requestId, new(false, 0, "Khách thống nhất tiếp tục giữ chỗ")) is OkObjectResult, "reject keeps booking");
Check(booking.TrangThai == NghiepVu.DatPhong.DaDatCoc && await db.PhieuThuChis.CountAsync() == payments, "rejection no refund or room change");
Check(await customerApi.Create(new(booking.MaDatPhong, "Gửi lại sau trao đổi")) is OkObjectResult, "new request permitted after rejection");
requestId = await db.YeuCauHuyDatPhongs.Where(r => r.MaDatPhong == booking.MaDatPhong && r.TrangThai == "Chờ duyệt").Select(r => r.MaYeuCau).SingleAsync();
Check(await managerApi.XuLy(requestId, new(true, 600000, "Thỏa thuận hoàn một phần")) is OkObjectResult, "manager approves partial refund");
Check(booking.TrangThai == NghiepVu.DatPhong.DaHuy && room.TrangThai == NghiepVu.Phong.Trong && await db.PhieuThuChis.CountAsync() == payments + 1, "approval cancels and issues one refund");
Check(await managerApi.XuLy(requestId, new(true, 600000, "replay")) is ConflictObjectResult && await db.PhieuThuChis.CountAsync() == payments + 1, "repeated approval never refunds twice");

// Separate room / contract to isolate invoice rules from booking cancellation.
var invoiceRoom = new Phong { TenPhong = "CHECK-PHAT-29", MaLoaiPhong = type, MaCoSo = sites[0], GiaThue = 2000000, TrangThai = NghiepVu.Phong.DangThue };
db.Phongs.Add(invoiceRoom); await db.SaveChangesAsync();
var contract = new HopDong { MaPhong = invoiceRoom.MaPhong, MaKhachDaiDien = tenant.MaKhach, NgayKy = new(2026,1,1), NgayBatDau = new(2026,1,1), NgayKetThuc = new(2026,12,31), GiaThueThang = 2000000, TienCoc = 0, TrangThai = NghiepVu.HopDong.HieuLuc };
db.HopDongs.Add(contract); await db.SaveChangesAsync();
var invoice = HoaDonBuilder.TaoHoaDon(db, contract.MaHopDong, 10, 2026, [new ChiTietHoaDon { NoiDung = "Tiền phòng", SoLuong = 1, DonGia = 2000000, ThanhTien = 2000000 }]);
var violation = new ViPham { MaPhong = invoiceRoom.MaPhong, MaKhach = tenant.MaKhach, NgayViPham = new(2026,10,15), NoiDung = "Kiểm thử khoản phạt", TrangThai = NghiepVu.ViPham.DaXuLy, TienPhat = 200000 };
db.ViPhams.Add(violation); await db.SaveChangesAsync();
var fines = As(new ViPhamHoaDonController(db), owner);
Check(await fines.AddToInvoice(violation.MaViPham, new(invoice.MaHoaDon)) is OkObjectResult, "owner links processed penalty");
Check(invoice.TongTien == 2200000 && invoice.ConLai == 2200000 && (invoice.DaThanhToan ?? 0) == 0 && await db.PhieuThuChis.CountAsync() == payments + 1, "penalty raises debt without fake income");
Check(await fines.AddToInvoice(violation.MaViPham, new(invoice.MaHoaDon)) is ConflictObjectResult, "duplicate penalty rejected");
Check(await db.ChiTietHoaDons.CountAsync(x => x.MaViPham == violation.MaViPham) == 1 && violation.DuocTinhHoaDon, "unique link and marker saved together");
var second = new ViPham { MaPhong = invoiceRoom.MaPhong, MaKhach = tenant.MaKhach, NgayViPham = new(2026,11,1), NoiDung = "future penalty", TrangThai = NghiepVu.ViPham.DaXuLy, TienPhat = 100000 };
db.ViPhams.Add(second); await db.SaveChangesAsync();
Check(await fines.AddToInvoice(second.MaViPham, new(invoice.MaHoaDon)) is BadRequestObjectResult, "future penalty cannot enter older invoice");
second.NgayViPham = new(2026,10,16); second.MaKhach = otherTenant.MaKhach; await db.SaveChangesAsync();
Check(await fines.AddToInvoice(second.MaViPham, new(invoice.MaHoaDon)) is BadRequestObjectResult, "wrong tenant invoice rejected");
second.MaKhach = tenant.MaKhach; invoice.DaThanhToan = 100000; invoice.ConLai -= 100000; await db.SaveChangesAsync();
Check(await fines.AddToInvoice(second.MaViPham, new(invoice.MaHoaDon)) is BadRequestObjectResult, "partially paid invoice cannot be amended");

// A signer may book another vacant room for a relative; only the actual occupant
// is added to residency, and one occupant cannot reside in two active rooms.
db.CuTrus.Add(new CuTru { MaHopDong = contract.MaHopDong, MaKhach = tenant.MaKhach,
    NgayVao = new(2026, 1, 1), TrangThaiCuTru = NghiepVu.CuTru.DangO,
    TrangThaiKhaiBao = NghiepVu.CuTru.ChuaKhaiBao });
var familyRoom = new Phong { TenPhong = "CHECK-THUE-HO", MaLoaiPhong = type,
    MaCoSo = sites[0], GiaThue = 2000000, TrangThai = NghiepVu.Phong.Trong };
db.Phongs.Add(familyRoom); await db.SaveChangesAsync();
var familyBooking = new DatPhong { MaPhong = familyRoom.MaPhong, MaKhach = tenant.MaKhach,
    NgayDat = new(2026, 10, 31), NgayDuKienNhan = new(2026, 11, 1),
    NgayHenKyHopDong = new(2026, 11, 1), TienCoc = 0 };
Check(await As(new DatPhongController(db), owner).Create(familyBooking) is OkObjectResult,
    "active tenant may book another vacant room at same site");
HopDong FamilyContract(int occupant) => new() { MaPhong = familyRoom.MaPhong,
    MaKhachDaiDien = tenant.MaKhach, MaKhachCuTru = occupant,
    NgayKy = new(2026, 10, 31), NgayBatDau = new(2026, 11, 1),
    NgayKetThuc = new(2027, 10, 31), GiaThueThang = 2000000, TienCoc = 0 };
Check(await As(new HopDongController(db), owner).Create(FamilyContract(tenant.MaKhach)) is BadRequestObjectResult,
    "same resident cannot occupy two active rooms");
Check(await As(new HopDongController(db), owner).Create(FamilyContract(otherTenant.MaKhach)) is OkObjectResult,
    "signer may rent second room for a different occupant");
Check(await db.CuTrus.AnyAsync(c => c.MaKhach == otherTenant.MaKhach && c.MaHopDongNavigation.MaPhong == familyRoom.MaPhong),
    "new contract records actual occupant, not signer");
Check(await As(new KhachThueController(db), owner).Update(tenant.MaKhach,
    new KhachThueUpdateDto { SoDienThoai = "0909999971" }) is OkObjectResult,
    "tenant contact change accepted");
Check(await db.LichSuKhachThues.AnyAsync(x => x.MaKhach == tenant.MaKhach && x.Truong == "Số điện thoại"
    && x.GiaTriCu == "0909999981" && x.GiaTriMoi == "0909999971"),
    "tenant identity change retains old and new value");
await tx.RollbackAsync();
Console.WriteLine($"PASS {checks} checks; all fixture changes rolled back on {clone}.");
