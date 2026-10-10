using System;
using System.Collections.Generic;

namespace QLPT.API.Models;

public partial class BanGiaoTaiSan
{
    public int MaBanGiao { get; set; }

    public int MaHopDong { get; set; }

    public int MaPhong { get; set; }

    public string? LoaiBanGiao { get; set; }

    public DateOnly? NgayBanGiao { get; set; }

    public decimal? ChiSoDien { get; set; }

    public decimal? ChiSoNuoc { get; set; }

    public string? GhiChu { get; set; }

    public virtual ICollection<ChiTietBanGiao> ChiTietBanGiaos { get; set; } = new List<ChiTietBanGiao>();

    public virtual HopDong MaHopDongNavigation { get; set; } = null!;

    public virtual Phong MaPhongNavigation { get; set; } = null!;
}
