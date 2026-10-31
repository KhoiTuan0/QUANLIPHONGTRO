using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLPT.API.Models;
using QLPT.API.Services;

namespace QLPT.API.Controllers;

// Financial amendment: Admin only (ApiAccessFilter denies unlisted roles/actions).
[ApiController]
[Route("api/ViPham")]
public class ViPhamHoaDonController(Klcn052QuanLyPhongTroContext db) : ControllerBase
{
    private static bool Matches(ViPham violation, HoaDon invoice, HopDong contract) =>
        violation.NgayViPham is DateTime day && invoice.Thang is >= 1 and <= 12 && invoice.Nam is >= 2000 and <= 9999 &&
        contract.MaPhong == violation.MaPhong && contract.MaKhachDaiDien == violation.MaKhach &&
        DateOnly.FromDateTime(day) >= contract.NgayBatDau && DateOnly.FromDateTime(day) <= contract.NgayKetThuc &&
        day.Year * 12 + day.Month <= invoice.Nam * 12 + invoice.Thang &&
        HoaDonBuilder.KiemTraKy(contract, invoice.Thang, invoice.Nam) == null && contract.TrangThai == NghiepVu.HopDong.HieuLuc;

    [HttpGet("{id:int}/hoa-don-phu-hop")]
    public async Task<IActionResult> GetEligible(int id)
    {
        var violation = await db.ViPhams.AsNoTracking().FirstOrDefaultAsync(v => v.MaViPham == id);
        if (violation == null) return NotFound();
        if (violation.TrangThai != NghiepVu.ViPham.DaXuLy || violation.TienPhat is not > 0 || violation.DuocTinhHoaDon ||
            await db.ChiTietHoaDons.AnyAsync(c => c.MaViPham == id)) return Ok(Array.Empty<object>());
        var invoices = await db.HoaDons.AsNoTracking().Include(h => h.MaHopDongNavigation)
            .Where(h => h.MaHopDongNavigation.MaPhong == violation.MaPhong && h.MaHopDongNavigation.MaKhachDaiDien == violation.MaKhach &&
                (h.DaThanhToan ?? 0) == 0 && h.TrangThai != NghiepVu.HoaDon.DaThanhToan && !h.ThanhToans.Any())
            .OrderByDescending(h => h.Nam).ThenByDescending(h => h.Thang).ToListAsync();
        return Ok(invoices.Where(h => Matches(violation, h, h.MaHopDongNavigation)).Select(h => new {
            h.MaHoaDon, h.MaHopDong, h.Thang, h.Nam, h.TongTien, TienPhat = violation.TienPhat,
            TongSauKhiThem = (h.TongTien ?? 0) + violation.TienPhat
        }));
    }

    [HttpPost("{id:int}/hoa-don")]
    public async Task<IActionResult> AddToInvoice(int id, GanPhatHoaDonDto dto)
    {
        if (id <= 0 || dto.MaHoaDon <= 0) return BadRequest(new { message = "Chọn vi phạm và hóa đơn hợp lệ." });
        await using var tx = db.Database.CurrentTransaction == null ? await db.Database.BeginTransactionAsync() : null;
        var contractId = await db.HoaDons.AsNoTracking().Where(h => h.MaHoaDon == dto.MaHoaDon).Select(h => (int?)h.MaHopDong).FirstOrDefaultAsync();
        if (contractId == null) return NotFound(new { message = "Không tìm thấy hóa đơn." });
        // Match invoice-generation/contract-settlement lock order.
        var contract = await db.HopDongs.FromSqlInterpolated($"SELECT * FROM dbo.HopDong WITH (UPDLOCK,HOLDLOCK) WHERE MaHopDong={contractId.Value}").SingleAsync();
        var invoice = await db.HoaDons.FromSqlInterpolated($"SELECT * FROM dbo.HoaDon WITH (UPDLOCK,HOLDLOCK) WHERE MaHoaDon={dto.MaHoaDon}").FirstOrDefaultAsync();
        var violation = await db.ViPhams.FromSqlInterpolated($"SELECT * FROM dbo.ViPham WITH (UPDLOCK,HOLDLOCK) WHERE MaViPham={id}").FirstOrDefaultAsync();
        if (invoice == null || violation == null) return NotFound(new { message = "Không tìm thấy hóa đơn hoặc vi phạm." });
        if (violation.DuocTinhHoaDon || await db.ChiTietHoaDons.AnyAsync(c => c.MaViPham == id))
            return Conflict(new { message = "Khoản phạt đã được đưa vào hóa đơn; không tính lần nữa." });
        if (violation.TrangThai != NghiepVu.ViPham.DaXuLy || violation.TienPhat is not > 0 ||
            decimal.Round(violation.TienPhat.Value, 2) != violation.TienPhat)
            return BadRequest(new { message = "Cần xử lý vi phạm và chốt khoản phạt dương, tối đa 2 chữ số thập phân." });
        if ((invoice.DaThanhToan ?? 0) != 0 || invoice.TrangThai == NghiepVu.HoaDon.DaThanhToan ||
            await db.ThanhToans.AnyAsync(t => t.MaHoaDon == invoice.MaHoaDon))
            return BadRequest(new { message = "Hóa đơn đã có thanh toán, không được bổ sung tiền phạt." });
        if (!Matches(violation, invoice, contract))
            return BadRequest(new { message = "Hóa đơn phải thuộc đúng phòng, khách đại diện, hợp đồng hiệu lực và kỳ không trước ngày vi phạm." });
        decimal total = invoice.TongTien ?? 0;
        decimal sum = await db.ChiTietHoaDons.Where(c => c.MaHoaDon == invoice.MaHoaDon).SumAsync(c => c.ThanhTien ?? 0);
        if (total < 0 || total != sum || (invoice.ConLai ?? total) != total)
            return BadRequest(new { message = "Tổng tiền, chi tiết hoặc công nợ chưa khớp; cần đối chiếu trước khi thêm phạt." });
        decimal updated = total + violation.TienPhat.Value;
        if (updated > 9999999999999999.99m) return BadRequest(new { message = "Tổng tiền vượt giới hạn lưu trữ." });
        string content = $"Phạt vi phạm #{id}: {violation.NoiDung}";
        db.ChiTietHoaDons.Add(new ChiTietHoaDon { MaHoaDon = invoice.MaHoaDon, MaViPham = id,
            NoiDung = content.Length > 200 ? content[..200] : content, SoLuong = 1,
            DonGia = violation.TienPhat, ThanhTien = violation.TienPhat });
        violation.DuocTinhHoaDon = true;
        invoice.TongTien = updated; invoice.ConLai = updated;
        if (await NghiepVu.TaiKhoanCuaKhachAsync(db, contract.MaKhachDaiDien) is int account)
            NghiepVu.TaoThongBao(db, "Bổ sung tiền phạt vào hóa đơn",
                $"Hóa đơn #{invoice.MaHoaDon} bổ sung vi phạm #{id}: {NghiepVu.Tien(violation.TienPhat)}. Tổng cần thanh toán: {NghiepVu.Tien(updated)}.",
                NghiepVu.ThongBao.LoaiCaNhan, new[] { account });
        await db.SaveChangesAsync();
        if (tx != null) await tx.CommitAsync();
        return Ok(new { success = true, invoice.MaHoaDon, invoice.TongTien, invoice.ConLai,
            message = "Đã bổ sung khoản phạt vào hóa đơn. Chưa ghi nhận thu tiền." });
    }
}

public record GanPhatHoaDonDto(int MaHoaDon);
