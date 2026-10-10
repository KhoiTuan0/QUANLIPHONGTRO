using System;
using System.Collections.Generic;

namespace QLPT.API.Models;

public partial class HoaDon
{
    public int MaHoaDon { get; set; }

    public int MaHopDong { get; set; }

    public int Thang { get; set; }

    public int Nam { get; set; }

    public DateOnly? NgayLap { get; set; }

    public DateOnly? HanThanhToan { get; set; }

    public decimal? TongTien { get; set; }

    public decimal? DaThanhToan { get; set; }

    public decimal? ConLai { get; set; }

    public string? TrangThai { get; set; }

    public virtual ICollection<ChiTietHoaDon> ChiTietHoaDons { get; set; } = new List<ChiTietHoaDon>();

    public virtual HopDong MaHopDongNavigation { get; set; } = null!;

    public virtual ICollection<ThanhToan> ThanhToans { get; set; } = new List<ThanhToan>();
}
