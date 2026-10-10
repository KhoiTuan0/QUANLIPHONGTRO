using System;
using System.Collections.Generic;

namespace QLPT.API.Models;

public partial class TaiSan
{
    public int MaTaiSan { get; set; }

    public string TenTaiSan { get; set; } = null!;

    public string? DonViTinh { get; set; }

    public decimal? GiaTriThamKhao { get; set; }

    public string? MoTa { get; set; }

    public virtual ICollection<ChiTietBanGiao> ChiTietBanGiaos { get; set; } = new List<ChiTietBanGiao>();

    public virtual ICollection<TaiSanPhong> TaiSanPhongs { get; set; } = new List<TaiSanPhong>();
}
