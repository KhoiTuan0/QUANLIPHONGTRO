using System;
using System.Collections.Generic;

namespace QLPT.API.Models;

public partial class ThanhToan
{
    public int MaThanhToan { get; set; }

    public int MaHoaDon { get; set; }

    public DateTime? NgayThanhToan { get; set; }

    public decimal? SoTien { get; set; }

    public string? PhuongThuc { get; set; }

    public string? GhiChu { get; set; }

    public virtual HoaDon MaHoaDonNavigation { get; set; } = null!;
}
