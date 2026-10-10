using System;
using System.Collections.Generic;

namespace QLPT.API.Models;

public partial class LoaiPhong
{
    public int MaLoaiPhong { get; set; }

    public string TenLoaiPhong { get; set; } = null!;

    public decimal? DienTich { get; set; }

    public int? SoNguoiToiDa { get; set; }

    public decimal? GiaMacDinh { get; set; }

    public string? MoTa { get; set; }

    public virtual ICollection<Phong> Phongs { get; set; } = new List<Phong>();
}
