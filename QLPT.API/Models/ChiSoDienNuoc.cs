using System;
using System.Collections.Generic;

namespace QLPT.API.Models;

public partial class ChiSoDienNuoc
{
    public int MaChiSo { get; set; }

    public int MaPhong { get; set; }

    public string LoaiChiSo { get; set; } = null!;

    public int Thang { get; set; }

    public int Nam { get; set; }

    public decimal? ChiSoCu { get; set; }

    public decimal? ChiSoMoi { get; set; }

    public DateOnly? NgayGhi { get; set; }

    public string? DuongDanAnh { get; set; }

    public decimal? ChiSoOcr { get; set; }

    public decimal? ChiSoXacNhan { get; set; }

    public virtual Phong MaPhongNavigation { get; set; } = null!;
}
