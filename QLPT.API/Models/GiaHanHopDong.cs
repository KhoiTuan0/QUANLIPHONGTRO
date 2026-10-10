using System;
using System.Collections.Generic;

namespace QLPT.API.Models;

public partial class GiaHanHopDong
{
    public int MaGiaHan { get; set; }

    public int MaHopDong { get; set; }

    public DateOnly? NgayGiaHan { get; set; }

    public DateOnly? NgayKetThucCu { get; set; }

    public DateOnly? NgayKetThucMoi { get; set; }

    public decimal? GiaThueMoi { get; set; }

    public string? GhiChu { get; set; }

    public virtual HopDong MaHopDongNavigation { get; set; } = null!;
}
