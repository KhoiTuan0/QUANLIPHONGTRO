using System;
using System.Collections.Generic;

namespace QLPT.API.Models;

public partial class TaiSanPhong
{
    public int MaTaiSanPhong { get; set; }

    public int MaPhong { get; set; }

    public int MaTaiSan { get; set; }

    public int? SoLuong { get; set; }

    public string? TinhTrang { get; set; }

    public string? GhiChu { get; set; }

    public virtual Phong MaPhongNavigation { get; set; } = null!;

    public virtual TaiSan MaTaiSanNavigation { get; set; } = null!;
}
