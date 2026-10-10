using System;
using System.Collections.Generic;

namespace QLPT.API.Models;

public partial class BaoTraPhong
{
    public int MaBaoTra { get; set; }

    public int MaHopDong { get; set; }

    public int MaKhach { get; set; }

    public DateOnly? NgayBao { get; set; }

    public DateOnly? NgayDuKienTra { get; set; }

    public string? LyDo { get; set; }

    public string? TrangThai { get; set; }

    public string? GhiChu { get; set; }

    public virtual HopDong MaHopDongNavigation { get; set; } = null!;

    public virtual KhachThue MaKhachNavigation { get; set; } = null!;
}
