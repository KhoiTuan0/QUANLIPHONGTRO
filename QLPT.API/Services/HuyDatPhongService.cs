using Microsoft.EntityFrameworkCore;
using QLPT.API.Models;

namespace QLPT.API.Services;

public static class HuyDatPhongService
{
    // Caller owns the transaction. Lock the booking before validating or issuing a refund.
    public static async Task<DatPhong?> LockAsync(Klcn052QuanLyPhongTroContext db, int id)
    {
        var roomId = await db.DatPhongs.AsNoTracking().Where(d => d.MaDatPhong == id).Select(d => (int?)d.MaPhong).FirstOrDefaultAsync();
        if (roomId == null) return null;
        // Same room -> booking lock order as receiving a room / creating a contract.
        await db.Phongs.FromSqlInterpolated($"SELECT * FROM dbo.Phong WITH (UPDLOCK,HOLDLOCK) WHERE MaPhong={roomId.Value}").FirstOrDefaultAsync();
        return await db.DatPhongs.FromSqlInterpolated($"SELECT * FROM dbo.DatPhong WITH (UPDLOCK,HOLDLOCK) WHERE MaDatPhong={id}").FirstOrDefaultAsync();
    }

    public static async Task<string?> CancelAsync(Klcn052QuanLyPhongTroContext db, DatPhong booking,
        decimal refund, int? requestId = null)
    {
        if (booking.TrangThai != NghiepVu.DatPhong.DaDatCoc) return "Phiếu đặt không còn giữ chỗ; không thể hủy hoặc hoàn cọc lần nữa.";
        if (refund < 0 || refund > (booking.TienCoc ?? 0) || decimal.Round(refund, 2) != refund)
            return "Tiền hoàn phải từ 0 đến tiền cọc, tối đa 2 chữ số thập phân.";
        if (await db.YeuCauHuyDatPhongs.AnyAsync(x => x.MaDatPhong == booking.MaDatPhong && x.TrangThai == "Chờ duyệt" && x.MaYeuCau != requestId))
            return "Phiếu đặt có yêu cầu hủy chờ duyệt. Hãy xử lý yêu cầu đó trước.";
        if (await db.HopDongs.AnyAsync(h => h.MaPhong == booking.MaPhong && h.TrangThai == NghiepVu.HopDong.HieuLuc))
            return "Phòng đã có hợp đồng hiệu lực; không thể hủy giữ chỗ.";
        var room = await db.Phongs.FindAsync(booking.MaPhong);
        var tenant = await db.KhachThues.FindAsync(booking.MaKhach);
        booking.TrangThai = NghiepVu.DatPhong.DaHuy;
        if (room?.TrangThai == NghiepVu.Phong.DaDatCoc && !await db.DatPhongs.AnyAsync(d =>
                d.MaPhong == booking.MaPhong && d.MaDatPhong != booking.MaDatPhong && d.TrangThai == NghiepVu.DatPhong.DaDatCoc))
            room.TrangThai = NghiepVu.Phong.Trong;
        NghiepVu.GhiPhieu(db, NghiepVu.PhieuChi,
            $"Hoàn tiền đặt cọc {NghiepVu.ChuThuongDau(room?.TenPhong ?? $"phòng #{booking.MaPhong}")} (khách hủy)", refund, tenant?.HoTen);
        return null;
    }
}
