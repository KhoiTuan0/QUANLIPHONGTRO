using System;
using System.Collections.Generic;

namespace QLPT.API.Models;

public partial class PhuongTien
{
    public int MaPhuongTien { get; set; }

    public int MaKhach { get; set; }

    public string? LoaiXe { get; set; }

    public string? BienSoXe { get; set; }

    public string? MoTa { get; set; }

    public string? TrangThai { get; set; }

    public virtual KhachThue MaKhachNavigation { get; set; } = null!;
}
