using System;
using System.Collections.Generic;

namespace QLPT.API.Models;

public partial class KhachThue
{
    public int MaKhach { get; set; }

    public string HoTen { get; set; } = null!;

    public string? Cccd { get; set; }

    public DateOnly? NgaySinh { get; set; }

    public string? GioiTinh { get; set; }

    public string? SoDienThoai { get; set; }

    public string? Email { get; set; }

    public string? DiaChi { get; set; }

    public virtual ICollection<BaoTraPhong> BaoTraPhongs { get; set; } = new List<BaoTraPhong>();

    public virtual ICollection<CuTru> CuTrus { get; set; } = new List<CuTru>();

    public virtual ICollection<DatPhong> DatPhongs { get; set; } = new List<DatPhong>();

    public virtual ICollection<HopDong> HopDongs { get; set; } = new List<HopDong>();

    public virtual ICollection<PhuongTien> PhuongTiens { get; set; } = new List<PhuongTien>();

    public virtual TaiKhoan? TaiKhoan { get; set; }

    public virtual ICollection<ViPham> ViPhams { get; set; } = new List<ViPham>();

    public virtual ICollection<YeuCauSuaChua> YeuCauSuaChuas { get; set; } = new List<YeuCauSuaChua>();
}
