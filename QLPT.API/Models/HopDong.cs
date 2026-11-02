using System;
using System.Collections.Generic;

namespace QLPT.API.Models;

public partial class HopDong
{
    public int MaHopDong { get; set; }

    public int MaPhong { get; set; }

    public int MaKhachDaiDien { get; set; }

    public DateOnly? NgayKy { get; set; }

    public DateOnly NgayBatDau { get; set; }

    public DateOnly NgayKetThuc { get; set; }

    public decimal? GiaThueThang { get; set; }

    public decimal? TienCoc { get; set; }

    public string? TrangThai { get; set; }

    public string? GhiChu { get; set; }

    public virtual ICollection<BanGiaoTaiSan> BanGiaoTaiSans { get; set; } = new List<BanGiaoTaiSan>();

    public virtual ICollection<BaoTraPhong> BaoTraPhongs { get; set; } = new List<BaoTraPhong>();

    public virtual ICollection<CuTru> CuTrus { get; set; } = new List<CuTru>();

    public virtual ICollection<DangKyDichVu> DangKyDichVus { get; set; } = new List<DangKyDichVu>();

    public virtual ICollection<GiaHanHopDong> GiaHanHopDongs { get; set; } = new List<GiaHanHopDong>();

    public virtual ICollection<HoaDon> HoaDons { get; set; } = new List<HoaDon>();

    public virtual KhachThue MaKhachDaiDienNavigation { get; set; } = null!;

    public virtual Phong MaPhongNavigation { get; set; } = null!;

    public virtual ThanhLy? ThanhLy { get; set; }
}
