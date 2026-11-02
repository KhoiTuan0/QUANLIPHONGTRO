using System;
using System.Collections.Generic;

namespace QLPT.API.Models;

public partial class DatPhong
{
    public int MaDatPhong { get; set; }

    public int MaPhong { get; set; }

    public int MaKhach { get; set; }

    public DateOnly NgayDat { get; set; }

    public DateOnly? NgayDuKienNhan { get; set; }

    public decimal? TienCoc { get; set; }

    public string? TrangThai { get; set; }

    public string? GhiChu { get; set; }

    public DateOnly? NgayHenKyHopDong { get; set; }

    public virtual KhachThue MaKhachNavigation { get; set; } = null!;

    public virtual Phong MaPhongNavigation { get; set; } = null!;
}
