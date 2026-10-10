using System;
using System.Collections.Generic;

namespace QLPT.API.Models;

public partial class ChiTietHoaDon
{
    public int MaChiTiet { get; set; }

    public int MaHoaDon { get; set; }

    public string? NoiDung { get; set; }

    public decimal? DonGia { get; set; }

    public decimal? SoLuong { get; set; }

    public decimal? ThanhTien { get; set; }

    public virtual HoaDon MaHoaDonNavigation { get; set; } = null!;
}
