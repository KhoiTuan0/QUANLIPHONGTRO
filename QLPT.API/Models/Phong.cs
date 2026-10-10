using System;
using System.Collections.Generic;

namespace QLPT.API.Models;

public partial class Phong
{
    public int MaPhong { get; set; }

    public int MaLoaiPhong { get; set; }

    public string TenPhong { get; set; } = null!;

    public int? Tang { get; set; }

    public decimal? GiaThue { get; set; }

    public string? TrangThai { get; set; }

    public string? MoTa { get; set; }

    public int? MaCoSo { get; set; }

    public string? HinhAnh { get; set; }

    public virtual ICollection<BanGiaoTaiSan> BanGiaoTaiSans { get; set; } = new List<BanGiaoTaiSan>();

    public virtual ICollection<ChiSoDienNuoc> ChiSoDienNuocs { get; set; } = new List<ChiSoDienNuoc>();

    public virtual ICollection<DatPhong> DatPhongs { get; set; } = new List<DatPhong>();

    public virtual HopDong? HopDong { get; set; }

    public virtual CoSo? MaCoSoNavigation { get; set; }

    public virtual LoaiPhong MaLoaiPhongNavigation { get; set; } = null!;

    public virtual ICollection<TaiSanPhong> TaiSanPhongs { get; set; } = new List<TaiSanPhong>();

    public virtual ICollection<ViPham> ViPhams { get; set; } = new List<ViPham>();

    public virtual ICollection<YeuCauSuaChua> YeuCauSuaChuas { get; set; } = new List<YeuCauSuaChua>();
}
