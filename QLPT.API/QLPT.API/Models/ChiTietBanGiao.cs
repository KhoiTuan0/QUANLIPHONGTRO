using System;
using System.Collections.Generic;

namespace QLPT.API.Models;

public partial class ChiTietBanGiao
{
    public int MaChiTietBanGiao { get; set; }

    public int MaBanGiao { get; set; }

    public int MaTaiSan { get; set; }

    public int? SoLuong { get; set; }

    public string? TinhTrang { get; set; }

    public decimal? ChiPhiPhat { get; set; }

    public string? GhiChu { get; set; }

    public virtual BanGiaoTaiSan MaBanGiaoNavigation { get; set; } = null!;

    public virtual TaiSan MaTaiSanNavigation { get; set; } = null!;
}
