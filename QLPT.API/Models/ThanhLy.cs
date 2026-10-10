using System;
using System.Collections.Generic;

namespace QLPT.API.Models;

public partial class ThanhLy
{
    public int MaThanhLy { get; set; }

    public int MaHopDong { get; set; }

    public DateOnly? NgayThanhLy { get; set; }

    public decimal? TienNo { get; set; }

    public decimal? TienPhat { get; set; }

    public decimal? TienHoanCoc { get; set; }

    public decimal? TongQuyetToan { get; set; }

    public string? GhiChu { get; set; }

    public virtual HopDong MaHopDongNavigation { get; set; } = null!;
}
