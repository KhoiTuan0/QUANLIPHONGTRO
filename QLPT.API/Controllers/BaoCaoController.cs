using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLPT.API.Models;

namespace QLPT.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class BaoCaoController(Klcn052QuanLyPhongTroContext context) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string loai, [FromQuery] DateOnly tuNgay, [FromQuery] DateOnly denNgay, [FromQuery] int? maCoSo)
    {
        if (loai is not ("doanh-thu" or "phong" or "khach" or "hop-dong")) return BadRequest(new { message = "Loại báo cáo không hợp lệ." });
        if (tuNgay.Year < 1900 || denNgay.Year > 2100 || denNgay < tuNgay || maCoSo is <= 0)
            return BadRequest(new { message = "Khoảng ngày hoặc cơ sở không hợp lệ." });
        if (maCoSo.HasValue && !await context.CoSos.AnyAsync(c => c.MaCoSo == maCoSo)) return NotFound(new { message = "Không tìm thấy cơ sở." });

        object Column(string key, string name, string type = "chu") => new { truong = key, ten = name, kieu = type };
        object Summary(string name, decimal value, string type = "so") => new { chiTieu = name, giaTri = value, kieu = type };
        IActionResult Report(object[] columns, object rows, object[] summary, string note) => Ok(new { loai, tuNgay, denNgay, maCoSo, cot = columns, dong = rows, tomTat = summary, ghiChu = note });
        if (loai == "doanh-thu")
        {
            var start = tuNgay.ToDateTime(TimeOnly.MinValue); var end = denNgay.AddDays(1).ToDateTime(TimeOnly.MinValue);
            var rows = await context.ThanhToans.AsNoTracking().Where(t => t.NgayThanhToan >= start && t.NgayThanhToan < end &&
                    t.PhuongThuc != "Cấn trừ tiền cọc" && (!maCoSo.HasValue || t.MaHoaDonNavigation.MaHopDongNavigation.MaPhongNavigation.MaCoSo == maCoSo))
                .OrderByDescending(t => t.NgayThanhToan)
                .Select(t => new { t.MaThanhToan, t.NgayThanhToan, t.MaHoaDon, t.SoTien, t.PhuongThuc,
                    TenPhong = t.MaHoaDonNavigation.MaHopDongNavigation.MaPhongNavigation.TenPhong,
                    HoTen = t.MaHoaDonNavigation.MaHopDongNavigation.MaKhachDaiDienNavigation.HoTen }).ToListAsync();
            return Report([Column("maThanhToan", "Mã thanh toán"), Column("ngayThanhToan", "Ngày thu", "ngay"), Column("tenPhong", "Phòng"),
                Column("hoTen", "Khách thuê"), Column("maHoaDon", "Mã hóa đơn"), Column("soTien", "Số tiền", "tien"), Column("phuongThuc", "Phương thức")],
                rows, [Summary("Số lần thu", rows.Count), Summary("Tiền hóa đơn thực thu", rows.Sum(t => t.SoTien ?? 0), "tien")],
                "Tiền hóa đơn đã thu trong khoảng ngày; không gồm tiền đặt cọc, cấn trừ cọc, phiếu thu ngoài hóa đơn hoặc tiền hoàn trả.");
        }
        if (loai == "phong")
        {
            var rows = await context.Phongs.AsNoTracking().Where(p => !maCoSo.HasValue || p.MaCoSo == maCoSo)
                .OrderBy(p => p.TenPhong).Select(p => new { p.MaPhong, p.TenPhong, p.TrangThai, p.GiaThue, p.Tang,
                    TenCoSo = p.MaCoSoNavigation == null ? "Chưa gán" : p.MaCoSoNavigation.TenCoSo,
                    TenLoaiPhong = p.MaLoaiPhongNavigation == null ? "Chưa gán" : p.MaLoaiPhongNavigation.TenLoaiPhong }).ToListAsync();
            return Report([Column("maPhong", "Mã phòng"), Column("tenPhong", "Phòng"), Column("tenCoSo", "Cơ sở"), Column("tenLoaiPhong", "Loại phòng"),
                Column("trangThai", "Trạng thái"), Column("giaThue", "Giá thuê", "tien"), Column("tang", "Tầng")], rows,
                [Summary("Tổng phòng", rows.Count), Summary("Phòng trống", rows.Count(p => p.TrangThai == "Trống")), Summary("Đang thuê", rows.Count(p => p.TrangThai == "Đang thuê"))],
                "Danh mục và trạng thái phòng hiện tại; khoảng ngày không áp dụng cho báo cáo phòng.");
        }
        if (loai == "khach")
        {
            var rows = await context.KhachThues.AsNoTracking().Where(k => !maCoSo.HasValue ||
                    k.HopDongs.Any(h => h.MaPhongNavigation.MaCoSo == maCoSo) || k.DatPhongs.Any(d => d.MaPhongNavigation.MaCoSo == maCoSo))
                .OrderBy(k => k.HoTen).ThenBy(k => k.MaKhach).Select(k => new { k.MaKhach, k.HoTen, k.Cccd, k.SoDienThoai, k.Email, k.DiaChi }).ToListAsync();
            return Report([Column("maKhach", "Mã khách"), Column("hoTen", "Họ tên"), Column("cccd", "CCCD"), Column("soDienThoai", "Điện thoại"),
                Column("email", "Email"), Column("diaChi", "Địa chỉ")], rows, [Summary("Hồ sơ khách thuê", rows.Count)],
                "Hồ sơ hiện có, gồm khách mới và khách cũ. Lọc cơ sở theo hợp đồng hoặc phiếu đặt; khoảng ngày không áp dụng cho hồ sơ.");
        }
        var contracts = await context.HopDongs.AsNoTracking().Where(h => h.NgayBatDau <= denNgay && h.NgayKetThuc >= tuNgay &&
                (!maCoSo.HasValue || h.MaPhongNavigation.MaCoSo == maCoSo))
            .OrderByDescending(h => h.NgayBatDau).Select(h => new { h.MaHopDong, h.NgayBatDau, h.NgayKetThuc, h.GiaThueThang, h.TienCoc, h.TrangThai,
                TenPhong = h.MaPhongNavigation.TenPhong, HoTen = h.MaKhachDaiDienNavigation.HoTen }).ToListAsync();
        return Report([Column("maHopDong", "Mã hợp đồng"), Column("tenPhong", "Phòng"), Column("hoTen", "Khách đại diện"),
            Column("ngayBatDau", "Bắt đầu", "ngay"), Column("ngayKetThuc", "Kết thúc", "ngay"), Column("giaThueThang", "Giá/tháng", "tien"),
            Column("tienCoc", "Tiền cọc", "tien"), Column("trangThai", "Trạng thái")], contracts,
            [Summary("Hợp đồng trong khoảng ngày", contracts.Count), Summary("Đang hiệu lực hiện tại", contracts.Count(h => h.TrangThai == "Đang hiệu lực"))],
            "Hợp đồng có thời hạn giao với khoảng ngày đã chọn; trạng thái là trạng thái hiện tại.");
    }
}
