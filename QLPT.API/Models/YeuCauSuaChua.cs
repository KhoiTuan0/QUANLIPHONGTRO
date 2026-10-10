using System;
using System.Collections.Generic;

namespace QLPT.API.Models;

public partial class YeuCauSuaChua
{
    public int MaYeuCau { get; set; }

    public int MaPhong { get; set; }

    public int MaKhach { get; set; }

    public DateTime? NgayBao { get; set; }

    public string? NoiDung { get; set; }

    public string? TrangThai { get; set; }

    public decimal? ChiPhi { get; set; }

    public DateTime? NgayHoanThanh { get; set; }

    public string? DuongDanAnh { get; set; }

    public bool? KhachXacNhan { get; set; }

    public virtual KhachThue MaKhachNavigation { get; set; } = null!;

    public virtual Phong MaPhongNavigation { get; set; } = null!;
}
