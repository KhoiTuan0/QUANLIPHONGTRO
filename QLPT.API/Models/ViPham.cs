using System;
using System.Collections.Generic;

namespace QLPT.API.Models;

public partial class ViPham
{
    public int MaViPham { get; set; }

    public int MaPhong { get; set; }

    public int MaKhach { get; set; }

    public DateTime? NgayViPham { get; set; }

    public string? NoiDung { get; set; }

    public string? HinhThucXuLy { get; set; }

    public decimal? TienPhat { get; set; }

    public string? TrangThai { get; set; }

    public string? DuongDanAnh { get; set; }

    public virtual KhachThue MaKhachNavigation { get; set; } = null!;

    public virtual Phong MaPhongNavigation { get; set; } = null!;
}
