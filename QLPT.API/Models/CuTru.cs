using System;
using System.Collections.Generic;

namespace QLPT.API.Models;

public partial class CuTru
{
    public int MaCuTru { get; set; }

    public int MaHopDong { get; set; }

    public int MaKhach { get; set; }

    public DateOnly? NgayVao { get; set; }

    public DateOnly? NgayRoi { get; set; }

    public string? TrangThaiCuTru { get; set; }

    public string? TrangThaiKhaiBao { get; set; }

    public DateOnly? NgayKhaiBao { get; set; }

    public string? GhiChu { get; set; }

    public virtual HopDong MaHopDongNavigation { get; set; } = null!;

    public virtual KhachThue MaKhachNavigation { get; set; } = null!;
}
